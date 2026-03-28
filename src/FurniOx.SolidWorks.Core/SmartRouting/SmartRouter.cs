using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.SmartRouting;

public sealed class SmartRouter : ISmartRouter
{
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ISolidWorksAdapter _adapter;
    private readonly IStaTaskRunner _staRunner;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly SolidWorksSettings _settings;
    private readonly ILogger<SmartRouter> _logger;
    private readonly ISolidWorksAdapter? _bridge;

    public SmartRouter(
        ICircuitBreaker circuitBreaker,
        ISolidWorksAdapter adapter,
        IStaTaskRunner staRunner,
        IPerformanceMonitor performanceMonitor,
        ILogger<SmartRouter> logger)
        : this(
            circuitBreaker,
            adapter,
            staRunner,
            performanceMonitor,
            new SolidWorksSettings(),
            logger,
            bridge: null)
    {
    }

    public SmartRouter(
        ICircuitBreaker circuitBreaker,
        ISolidWorksAdapter adapter,
        IStaTaskRunner staRunner,
        IPerformanceMonitor performanceMonitor,
        SolidWorksSettings settings,
        ILogger<SmartRouter> logger,
        ISolidWorksAdapter? bridge = null)
    {
        _circuitBreaker = circuitBreaker;
        _adapter = adapter;
        _staRunner = staRunner;
        _performanceMonitor = performanceMonitor;
        _settings = settings;
        _logger = logger;
        _bridge = bridge;
    }

    public async Task<ExecutionResult> RouteAsync(string operation, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        ExecutionResult result = ExecutionResult.Failure("Operation not executed.");
        try
        {
            if (_settings.ComParameterLimit > 0 && parameters.Count > _settings.ComParameterLimit)
            {
                result = ExecutionResult.Failure(
                    $"Operation '{operation}' exceeded the configured parameter limit of {_settings.ComParameterLimit}.",
                    new
                    {
                        ParameterCount = parameters.Count,
                        ParameterLimit = _settings.ComParameterLimit
                    });
                return result;
            }

            if (_bridge != null && _bridge.CanHandle(operation))
            {
                try
                {
                    _logger.LogDebug("Routing '{Operation}' via bridge", operation);
                    result = await _bridge.ExecuteAsync(operation, parameters, cancellationToken);

                    if (result.Success)
                    {
                        return result;
                    }

                    _logger.LogWarning(
                        "Bridge failed for '{Operation}': {Message}. Falling back to direct COM.",
                        operation,
                        result.Message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex,
                        "Bridge threw for '{Operation}'. Falling back to direct COM.",
                        operation);
                }
            }

            result = await _circuitBreaker.ExecuteAsync(
                ct => _staRunner.RunAsync(() => _adapter.ExecuteAsync(operation, parameters, ct), ct),
                cancellationToken);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Routing failed for {Operation}", operation);
            result = ExecutionResult.Failure($"Unhandled exception in '{operation}': {ex.Message}");
            return result;
        }
        finally
        {
            stopwatch.Stop();
            _performanceMonitor.RecordExecution(operation, stopwatch.Elapsed, result.Success);
        }
    }

    public IReadOnlyCollection<ExecutionMetric> GetPerformanceMetrics()
    {
        return _performanceMonitor.Snapshot();
    }
}
