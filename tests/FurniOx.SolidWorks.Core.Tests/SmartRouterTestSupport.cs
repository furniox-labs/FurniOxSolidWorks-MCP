using System;
using System.Collections.Generic;
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

public abstract class SmartRouterTestBase : IDisposable
{
    private readonly SolidWorksSettings _defaultSettings;
    protected readonly StaTaskRunner StaTaskRunner;

    protected SmartRouterTestBase()
    {
        StaTaskRunner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
        _defaultSettings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = 10,
                ResetTimeoutSeconds = 60
            }
        };
    }

    public void Dispose()
    {
        StaTaskRunner.Dispose();
    }

    protected SmartRouter CreateRouter(ISolidWorksAdapter adapter)
    {
        var circuitBreaker = new CircuitBreaker(_defaultSettings, NullLogger<CircuitBreaker>.Instance);
        var monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        return new SmartRouter(
            circuitBreaker,
            adapter,
            StaTaskRunner,
            monitor,
            _defaultSettings,
            NullLogger<SmartRouter>.Instance);
    }

    protected static SmartRouter CreateRouter(
        ISolidWorksAdapter adapter,
        StaTaskRunner staTaskRunner,
        SolidWorksSettings settings)
    {
        var circuitBreaker = new CircuitBreaker(settings, NullLogger<CircuitBreaker>.Instance);
        var monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        return new SmartRouter(
            circuitBreaker,
            adapter,
            staTaskRunner,
            monitor,
            settings,
            NullLogger<SmartRouter>.Instance);
    }

    protected sealed class SuccessAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new { Operation = operation }));
        }
    }

    protected sealed class FailureAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExecutionResult.Failure("Simulated failure"));
        }
    }

    protected sealed class ThrowingComAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            throw new COMException("Simulated COM failure", unchecked((int)0x80004005));
        }
    }

    protected sealed class CancellingAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    protected sealed class ConfigurableAdapter : ISolidWorksAdapter
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
        {
            return Task.FromResult(_factory(operation));
        }
    }

    protected sealed class CapturingAdapter : ISolidWorksAdapter
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
}
