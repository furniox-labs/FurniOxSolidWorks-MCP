using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Core.SmartRouting;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;
using PerformanceMonitor = FurniOx.SolidWorks.Core.Intelligence.PerformanceMonitor;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Fills gaps in SmartRouter coverage not addressed by SmartRouterExecutionTests or
/// SmartRouterMetricsTests: circuit-breaker wrapping, performance-monitor delegation,
/// parameter-limit boundary, and always-fires finally-block recording.
/// </summary>
public sealed class SmartRouterAdditionalTests : SmartRouterTestBase
{
    // ── Circuit-breaker wrapping ──────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_WhenCircuitBreakerOpen_ReturnsFailureResult()
    {
        // Polly requires MinimumThroughput >= 2; use threshold 2 to trip the circuit.
        var settings = new SolidWorksSettings
        {
            ComParameterLimit = 0,
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 2, ResetTimeoutSeconds = 60 }
        };
        var throwingAdapter = new ThrowingComAdapter();
        var router = CreateRouter(throwingAdapter, StaTaskRunner, settings);

        // Two calls reach the failure threshold and open the circuit.
        await router.RouteAsync("Document.OpenModel", new Dictionary<string, object?>());
        await router.RouteAsync("Document.OpenModel", new Dictionary<string, object?>());

        // Third call hits the open circuit — router must wrap the BrokenCircuitException as failure.
        var result = await router.RouteAsync("Document.OpenModel", new Dictionary<string, object?>());

        Assert.False(result.Success);
    }

    // ── Parameter limit boundary ──────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_WhenParameterLimitIsZero_DoesNotEnforceLimit()
    {
        var settings = new SolidWorksSettings
        {
            ComParameterLimit = 0, // 0 means disabled
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 10, ResetTimeoutSeconds = 60 }
        };
        var router = CreateRouter(new SuccessAdapter(), StaTaskRunner, settings);
        var manyParams = new Dictionary<string, object?>();
        for (var i = 0; i < 100; i++) manyParams[$"p{i}"] = i;

        var result = await router.RouteAsync("Feature.CreateExtrusion", manyParams);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RouteAsync_WhenParamCountEqualsLimit_Succeeds()
    {
        var settings = new SolidWorksSettings
        {
            ComParameterLimit = 2,
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 10, ResetTimeoutSeconds = 60 }
        };
        var router = CreateRouter(new SuccessAdapter(), StaTaskRunner, settings);

        // Exactly at the limit — should pass.
        var result = await router.RouteAsync("Sketch.SketchCircle", new Dictionary<string, object?>
        {
            ["x"] = 0.0,
            ["y"] = 0.0
        });

        Assert.True(result.Success);
    }

    // ── Performance monitor always fires (finally block) ─────────────────────

    [Fact]
    public async Task RouteAsync_OnAdapterFailureResult_RecordsFailureInMonitor()
    {
        var mockMonitor = new Mock<IPerformanceMonitor>();
        mockMonitor.Setup(m => m.Snapshot()).Returns(Array.Empty<ExecutionMetric>());

        var cbSettings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 10, ResetTimeoutSeconds = 60 }
        };
        var circuitBreaker = new CircuitBreaker(cbSettings, NullLogger<CircuitBreaker>.Instance);

        var router = new SmartRouter(
            circuitBreaker,
            new FailureAdapter(),
            StaTaskRunner,
            mockMonitor.Object,
            cbSettings,
            NullLogger<SmartRouter>.Instance);

        await router.RouteAsync("Feature.CreateExtrusion", new Dictionary<string, object?>());

        mockMonitor.Verify(
            m => m.RecordExecution(
                "Feature.CreateExtrusion",
                It.IsAny<TimeSpan>(),
                false),
            Times.Once);
    }

    [Fact]
    public async Task RouteAsync_OnAdapterSuccess_RecordsSuccessInMonitor()
    {
        var mockMonitor = new Mock<IPerformanceMonitor>();
        mockMonitor.Setup(m => m.Snapshot()).Returns(Array.Empty<ExecutionMetric>());

        var cbSettings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 10, ResetTimeoutSeconds = 60 }
        };
        var circuitBreaker = new CircuitBreaker(cbSettings, NullLogger<CircuitBreaker>.Instance);

        var router = new SmartRouter(
            circuitBreaker,
            new SuccessAdapter(),
            StaTaskRunner,
            mockMonitor.Object,
            cbSettings,
            NullLogger<SmartRouter>.Instance);

        await router.RouteAsync("Sketch.SketchLine", new Dictionary<string, object?>());

        mockMonitor.Verify(
            m => m.RecordExecution(
                "Sketch.SketchLine",
                It.IsAny<TimeSpan>(),
                true),
            Times.Once);
    }

    // ── GetPerformanceMetrics delegates to monitor ────────────────────────────

    [Fact]
    public void GetPerformanceMetrics_DelegatesToPerformanceMonitorSnapshot()
    {
        var expected = new[]
        {
            new ExecutionMetric { Operation = "Sketch.SketchCircle", Invocations = 3, Successes = 2 }
        };
        var mockMonitor = new Mock<IPerformanceMonitor>();
        mockMonitor.Setup(m => m.Snapshot()).Returns(expected);

        var cbSettings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 10, ResetTimeoutSeconds = 60 }
        };
        var circuitBreaker = new CircuitBreaker(cbSettings, NullLogger<CircuitBreaker>.Instance);

        var router = new SmartRouter(
            circuitBreaker,
            new SuccessAdapter(),
            StaTaskRunner,
            mockMonitor.Object,
            cbSettings,
            NullLogger<SmartRouter>.Instance);

        var metrics = router.GetPerformanceMetrics();

        mockMonitor.Verify(m => m.Snapshot(), Times.Once);
        Assert.Single(metrics);
    }

    // ── Adapter throwing records failure via finally ──────────────────────────

    [Fact]
    public async Task RouteAsync_WhenAdapterThrows_RecordsFailureInMonitor()
    {
        var mockMonitor = new Mock<IPerformanceMonitor>();
        mockMonitor.Setup(m => m.Snapshot()).Returns(Array.Empty<ExecutionMetric>());

        var cbSettings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 10, ResetTimeoutSeconds = 60 }
        };
        var circuitBreaker = new CircuitBreaker(cbSettings, NullLogger<CircuitBreaker>.Instance);

        var router = new SmartRouter(
            circuitBreaker,
            new ThrowingComAdapter(),
            StaTaskRunner,
            mockMonitor.Object,
            cbSettings,
            NullLogger<SmartRouter>.Instance);

        await router.RouteAsync("Document.SaveModel", new Dictionary<string, object?>());

        mockMonitor.Verify(
            m => m.RecordExecution(
                "Document.SaveModel",
                It.IsAny<TimeSpan>(),
                false),
            Times.Once);
    }
}
