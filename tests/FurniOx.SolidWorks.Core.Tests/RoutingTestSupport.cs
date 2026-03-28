using System.Collections.Generic;
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

public abstract class RoutingTestBase : IDisposable
{
    private readonly CircuitBreaker _circuitBreaker;
    private readonly PerformanceMonitor _monitor;
    private readonly StaTaskRunner _staTaskRunner;

    protected RoutingTestBase()
    {
        var settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = 5,
                ResetTimeoutSeconds = 30
            }
        };

        _circuitBreaker = new CircuitBreaker(settings, NullLogger<CircuitBreaker>.Instance);
        _monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        _staTaskRunner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
    }

    public void Dispose()
    {
        _staTaskRunner.Dispose();
    }

    protected SmartRouter CreateRouter(ISolidWorksAdapter adapter)
    {
        return new SmartRouter(
            _circuitBreaker,
            adapter,
            _staTaskRunner,
            _monitor,
            NullLogger<SmartRouter>.Instance);
    }

    protected sealed class OperationRecordingAdapter : ISolidWorksAdapter
    {
        public string? LastOperation { get; private set; }
        public IDictionary<string, object?>? LastParameters { get; private set; }

        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            LastOperation = operation;
            LastParameters = parameters;

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                MockResponse = true,
                Operation = operation,
                ParameterCount = parameters.Count
            }));
        }
    }

    protected sealed class RejectingAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExecutionResult.Failure($"Unknown sketch operation: {operation}"));
        }
    }
}
