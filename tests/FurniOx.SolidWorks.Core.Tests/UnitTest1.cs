using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.SmartRouting;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Xunit;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;
using PerformanceMonitor = FurniOx.SolidWorks.Core.Intelligence.PerformanceMonitor;

namespace FurniOx.SolidWorks.Core.Tests;

public class RouterAndCircuitBreakerTests
{
    [Fact]
    public async Task RouteAsync_ExecutesAdapter_Successfully()
    {
        var settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 2, ResetTimeoutSeconds = 10 }
        };

        var adapter = new SuccessAdapter();
        var circuitBreaker = new CircuitBreaker(settings, NullLogger<CircuitBreaker>.Instance);
        var monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        var staRunner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
        var router = new SmartRouter(
            circuitBreaker,
            adapter,
            staRunner,
            monitor,
            NullLogger<SmartRouter>.Instance);

        var result = await router.RouteAsync("Sketch.SketchCircle", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CircuitBreaker_TripsAfterThreshold()
    {
        var settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 2, ResetTimeoutSeconds = 5 }
        };

        var circuitBreaker = new CircuitBreaker(settings, NullLogger<CircuitBreaker>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync<int>(_ => throwInvalidOperation()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => circuitBreaker.ExecuteAsync<int>(_ => throwInvalidOperation()));
        await Assert.ThrowsAsync<BrokenCircuitException>(() => circuitBreaker.ExecuteAsync<int>(_ => throwInvalidOperation()));

        static Task<int> throwInvalidOperation() => Task.FromException<int>(new InvalidOperationException());
    }

    private sealed class SuccessAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(string operation, IDictionary<string, object?> parameters, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(ExecutionResult.SuccessResult(new { Operation = operation }));
    }
}
