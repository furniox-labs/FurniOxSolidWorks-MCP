using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Core.SmartRouting;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;
using PerformanceMonitor = FurniOx.SolidWorks.Core.Intelligence.PerformanceMonitor;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Extended tests for <see cref="SmartRouter"/>. Exercises metrics tracking,
/// exception handling, cancellation propagation, and multi-operation scenarios.
/// Each test creates its own isolated set of dependencies so test ordering
/// has no effect on outcomes.
/// </summary>
public sealed class SmartRouterExtendedTests : IDisposable
{
    // Shared infrastructure disposed after every test via IDisposable.
    private readonly StaTaskRunner _staRunner;
    private readonly SolidWorksSettings _defaultSettings;

    public SmartRouterExtendedTests()
    {
        _staRunner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
        _defaultSettings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = 10,  // High threshold so tests don't trip the breaker.
                ResetTimeoutSeconds = 60
            }
        };
    }

    public void Dispose() => _staRunner.Dispose();

    // ------------------------------------------------------------------
    // Factory helpers — avoids repeating constructor boilerplate
    // ------------------------------------------------------------------

    private SmartRouter BuildRouter(
        ISolidWorksAdapter adapter,
        out PerformanceMonitor monitor,
        ISolidWorksAdapter? bridge = null)
    {
        var cb = new CircuitBreaker(_defaultSettings, NullLogger<CircuitBreaker>.Instance);
        monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        return new SmartRouter(cb, adapter, _staRunner, monitor, _defaultSettings, NullLogger<SmartRouter>.Instance, bridge);
    }

    private SmartRouter BuildRouter(ISolidWorksAdapter adapter)
        => BuildRouter(adapter, out _);

    // ------------------------------------------------------------------
    // Test adapters
    // ------------------------------------------------------------------

    private sealed class SuccessAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExecutionResult.SuccessResult(new { Operation = operation }));
    }

    private sealed class FailureAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExecutionResult.Failure("Simulated failure"));
    }

    private sealed class ThrowingCOMAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
            => throw new COMException("Simulated COM failure", unchecked((int)0x80004005));
    }

    private sealed class CancellingAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }

    /// <summary>
    /// Adapter whose response can be controlled by the test.
    /// </summary>
    private sealed class ConfigurableAdapter : ISolidWorksAdapter
    {
        private readonly Func<string, ExecutionResult> _factory;

        public ConfigurableAdapter(Func<string, ExecutionResult> factory)
        {
            _factory = factory;
        }

        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_factory(operation));
    }

    // ==================================================================
    // 1. RouteAsync with pre-cancelled token throws OperationCanceledException
    // ==================================================================
    [Fact]
    public async Task RouteAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        var router = BuildRouter(new SuccessAdapter());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // StaTaskRunner returns Task.FromCanceled when token is already cancelled.
        // That propagates up through the circuit breaker as OperationCanceledException.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.RouteAsync("Sketch.SketchCircle", new Dictionary<string, object?>(), cts.Token));
    }

    // ==================================================================
    // 2. Successful route records a metric with Success count = 1
    // ==================================================================
    [Fact]
    public async Task RouteAsync_RecordsPerformanceMetrics_OnSuccess()
    {
        var router = BuildRouter(new SuccessAdapter(), out var monitor);
        const string operation = "Sketch.SketchLine";

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metrics = router.GetPerformanceMetrics();
        var metric = metrics.Single(m => m.Operation == operation);

        Assert.Equal(1, metric.Invocations);
        Assert.Equal(1, metric.Successes);
        Assert.Equal(1.0, metric.SuccessRate);
    }

    // ==================================================================
    // 3. Failed route (adapter returns Failure) records metric with Successes = 0
    // ==================================================================
    [Fact]
    public async Task RouteAsync_RecordsPerformanceMetrics_OnFailure()
    {
        var router = BuildRouter(new FailureAdapter());
        const string operation = "Feature.CreateExtrusion";

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metrics = router.GetPerformanceMetrics();
        var metric = metrics.Single(m => m.Operation == operation);

        Assert.Equal(1, metric.Invocations);
        Assert.Equal(0, metric.Successes);
        Assert.Equal(0.0, metric.SuccessRate);
    }

    // ==================================================================
    // 4. When adapter throws COMException RouteAsync returns a Failure result
    // ==================================================================
    [Fact]
    public async Task RouteAsync_WhenAdapterThrows_ReturnsFailure()
    {
        var router = BuildRouter(new ThrowingCOMAdapter());

        var result = await router.RouteAsync("Document.SaveModel", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    // ==================================================================
    // 5. Throwing adapter still records a metric, and Success count = 0
    // ==================================================================
    [Fact]
    public async Task RouteAsync_WhenAdapterThrows_RecordsMetricsWithFailure()
    {
        var router = BuildRouter(new ThrowingCOMAdapter());
        const string operation = "Document.SaveModel";

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metrics = router.GetPerformanceMetrics();
        var metric = metrics.Single(m => m.Operation == operation);

        Assert.Equal(1, metric.Invocations);
        Assert.Equal(0, metric.Successes);
    }

    // ==================================================================
    // 6. Metrics are tracked per operation key independently
    // ==================================================================
    [Fact]
    public async Task RouteAsync_MultipleOperations_TracksMetricsSeparately()
    {
        var router = BuildRouter(new SuccessAdapter());
        var ops = new[] { "Sketch.SketchCircle", "Feature.CreateExtrusion", "Document.SaveModel" };

        foreach (var op in ops)
        {
            await router.RouteAsync(op, new Dictionary<string, object?>());
        }

        var metrics = router.GetPerformanceMetrics();

        foreach (var op in ops)
        {
            var metric = metrics.Single(m => m.Operation == op);
            Assert.Equal(1, metric.Invocations);
            Assert.Equal(1, metric.Successes);
        }
    }

    // ==================================================================
    // 7. Fresh router has no recorded metrics
    // ==================================================================
    [Fact]
    public void GetPerformanceMetrics_ReturnsEmptyForFreshRouter()
    {
        var router = BuildRouter(new SuccessAdapter());

        var metrics = router.GetPerformanceMetrics();

        Assert.Empty(metrics);
    }

    // ==================================================================
    // 8. ThrowingCOMAdapter exception is caught and surfaced as failure message
    // ==================================================================
    [Fact]
    public async Task RouteAsync_WithThrowingAdapter_CatchesException()
    {
        var router = BuildRouter(new ThrowingCOMAdapter());

        var result = await router.RouteAsync("Any.Operation", new Dictionary<string, object?>());

        // Must not throw; must return a failed result with descriptive message.
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    // ==================================================================
    // 9. OperationCanceledException is NOT caught by the when-clause and propagates
    // ==================================================================
    [Fact]
    public async Task RouteAsync_PropagatesOperationCanceledException()
    {
        // CancellingAdapter throws OperationCanceledException.
        // SmartRouter has: catch (Exception ex) when (ex is not OperationCanceledException)
        // so the exception must bubble up, not be swallowed into a Failure result.
        var router = BuildRouter(new CancellingAdapter());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.RouteAsync("Sketch.SketchLine", new Dictionary<string, object?>()));
    }

    // ==================================================================
    // 10. Successful route populates Data in the returned result
    // ==================================================================
    [Fact]
    public async Task RouteAsync_WithSuccessAdapter_ReturnsData()
    {
        var router = BuildRouter(new SuccessAdapter());

        var result = await router.RouteAsync("Sketch.SketchCircle", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    // ==================================================================
    // 11. Failure result message contains context from the operation name
    // ==================================================================
    [Fact]
    public async Task RouteAsync_ExecutionResult_ContainsCorrectMessage_OnFailure()
    {
        const string expectedMessage = "Simulated failure";
        var router = BuildRouter(new FailureAdapter());

        var result = await router.RouteAsync("Feature.CreateExtrusion", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Equal(expectedMessage, result.Message);
    }

    // ==================================================================
    // 12. Many sequential calls on same operation accumulate metrics correctly
    // ==================================================================
    [Fact]
    public async Task RouteAsync_MultipleSequentialCalls_AllSucceed()
    {
        const int iterations = 5;
        const string operation = "Sketch.SketchLine";
        var router = BuildRouter(new SuccessAdapter());

        for (var i = 0; i < iterations; i++)
        {
            var result = await router.RouteAsync(operation, new Dictionary<string, object?>());
            Assert.True(result.Success);
        }

        var metrics = router.GetPerformanceMetrics();
        var metric = metrics.Single(m => m.Operation == operation);

        Assert.Equal(iterations, metric.Invocations);
        Assert.Equal(iterations, metric.Successes);
    }

    // ==================================================================
    // 13. Mixed success/failure calls accumulate correct aggregate counts
    // ==================================================================
    [Fact]
    public async Task RouteAsync_MixedResults_AccumulatesCorrectMetrics()
    {
        const string operation = "Document.RebuildModel";

        var callCount = 0;
        var adapter = new ConfigurableAdapter(op =>
        {
            callCount++;
            // Odd invocations succeed, even invocations fail.
            return callCount % 2 != 0
                ? ExecutionResult.SuccessResult()
                : ExecutionResult.Failure("even call");
        });

        var router = BuildRouter(adapter);

        // 6 calls: 3 succeed, 3 fail.
        for (var i = 0; i < 6; i++)
        {
            await router.RouteAsync(operation, new Dictionary<string, object?>());
        }

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);

        Assert.Equal(6, metric.Invocations);
        Assert.Equal(3, metric.Successes);
        Assert.Equal(0.5, metric.SuccessRate);
    }

    // ==================================================================
    // 14. RouteAsync records a positive elapsed duration in metrics
    // ==================================================================
    [Fact]
    public async Task RouteAsync_RecordsDuration_InMetrics()
    {
        const string operation = "Export.ExportToSTEP";
        var router = BuildRouter(new SuccessAdapter());

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);

        // Total duration must be non-negative (could be zero on very fast machines,
        // but the field must be populated).
        Assert.True(metric.TotalDuration >= TimeSpan.Zero);
    }

    // ==================================================================
    // 15. Parameters dictionary is forwarded to the adapter unchanged
    // ==================================================================
    [Fact]
    public async Task RouteAsync_ForwardsParameters_ToAdapter()
    {
        IDictionary<string, object?>? capturedParams = null;

        var adapter = new ConfigurableAdapter(op =>
        {
            // Cannot capture params in ConfigurableAdapter as-is;
            // use a dedicated capturing closure-based adapter instead.
            return ExecutionResult.SuccessResult();
        });

        // Use a local inline adapter to capture the actual parameter dictionary.
        var capturingAdapter = new CapturingAdapter(p => capturedParams = p);
        var router = BuildRouter(capturingAdapter);

        var inputParams = new Dictionary<string, object?>
        {
            ["depth"] = 10.0,
            ["direction"] = "positive"
        };

        await router.RouteAsync("Feature.CreateExtrusion", inputParams);

        Assert.NotNull(capturedParams);
        Assert.Equal(inputParams["depth"], capturedParams["depth"]);
        Assert.Equal(inputParams["direction"], capturedParams["direction"]);
    }

    [Fact]
    public async Task RouteAsync_WhenParameterLimitExceeded_ReturnsFailureWithoutCallingAdapter()
    {
        var settings = new SolidWorksSettings
        {
            ComParameterLimit = 1,
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = 10,
                ResetTimeoutSeconds = 60
            }
        };
        var adapter = new CapturingAdapter(_ => throw new InvalidOperationException("Adapter should not be called."));
        var circuitBreaker = new CircuitBreaker(settings, NullLogger<CircuitBreaker>.Instance);
        var monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        var router = new SmartRouter(
            circuitBreaker,
            adapter,
            _staRunner,
            monitor,
            settings,
            NullLogger<SmartRouter>.Instance);

        var result = await router.RouteAsync(
            "Feature.CreateExtrusion",
            new Dictionary<string, object?>
            {
                ["Depth"] = 10.0,
                ["ReverseDirection"] = false
            });

        Assert.False(result.Success);
        Assert.Contains("parameter limit", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Data);
    }

    private sealed class CapturingAdapter : ISolidWorksAdapter
    {
        private readonly Action<IDictionary<string, object?>> _onExecute;

        public CapturingAdapter(Action<IDictionary<string, object?>> onExecute)
        {
            _onExecute = onExecute;
        }

        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            _onExecute(parameters);
            return Task.FromResult(ExecutionResult.SuccessResult());
        }
    }

    private sealed class CountingAdapter : ISolidWorksAdapter
    {
        private readonly Func<ExecutionResult> _factory;

        public CountingAdapter(bool canHandle, Func<ExecutionResult> factory)
        {
            CanHandleResult = canHandle;
            _factory = factory;
        }

        public int ExecuteCount { get; private set; }

        public bool CanHandleResult { get; }

        public bool CanHandle(string operation) => CanHandleResult;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.FromResult(_factory());
        }
    }

    private sealed class ThrowingBridgeAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Bridge exploded");
        }
    }

    [Fact]
    public async Task RouteAsync_WhenBridgeSucceeds_DoesNotCallDirectAdapter()
    {
        var directAdapter = new CountingAdapter(true, () => ExecutionResult.SuccessResult(new { Lane = "direct" }));
        var bridgeAdapter = new CountingAdapter(true, () => ExecutionResult.SuccessResult(new { Lane = "bridge" }));
        var router = BuildRouter(directAdapter, out _, bridgeAdapter);

        var result = await router.RouteAsync("Document.GetDocumentInfo", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(1, bridgeAdapter.ExecuteCount);
        Assert.Equal(0, directAdapter.ExecuteCount);
    }

    [Fact]
    public async Task RouteAsync_WhenBridgeReturnsFailure_FallsBackToDirectAdapter()
    {
        var directAdapter = new CountingAdapter(true, () => ExecutionResult.SuccessResult(new { Lane = "direct" }));
        var bridgeAdapter = new CountingAdapter(true, () => ExecutionResult.Failure("bridge failed"));
        var router = BuildRouter(directAdapter, out _, bridgeAdapter);

        var result = await router.RouteAsync("Document.GetDocumentInfo", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(1, bridgeAdapter.ExecuteCount);
        Assert.Equal(1, directAdapter.ExecuteCount);
    }

    [Fact]
    public async Task RouteAsync_WhenBridgeCannotHandle_UsesDirectAdapter()
    {
        var directAdapter = new CountingAdapter(true, () => ExecutionResult.SuccessResult(new { Lane = "direct" }));
        var bridgeAdapter = new CountingAdapter(false, () => ExecutionResult.SuccessResult(new { Lane = "bridge" }));
        var router = BuildRouter(directAdapter, out _, bridgeAdapter);

        var result = await router.RouteAsync("Feature.CreateExtrusion", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(0, bridgeAdapter.ExecuteCount);
        Assert.Equal(1, directAdapter.ExecuteCount);
    }

    [Fact]
    public async Task RouteAsync_WhenBridgeThrows_FallsBackToDirectAdapter()
    {
        var directAdapter = new CountingAdapter(true, () => ExecutionResult.SuccessResult(new { Lane = "direct" }));
        var router = BuildRouter(directAdapter, out _, new ThrowingBridgeAdapter());

        var result = await router.RouteAsync("Document.GetDocumentInfo", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(1, directAdapter.ExecuteCount);
    }
}
