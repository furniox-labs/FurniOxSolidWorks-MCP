using System;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace FurniOx.SolidWorks.Core.Intelligence;

public sealed class CircuitBreaker : ICircuitBreaker
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<CircuitBreaker> _logger;

    public CircuitBreaker(SolidWorksSettings settings, ILogger<CircuitBreaker> logger)
    {
        _logger = logger;
        var breakerSettings = settings.CircuitBreaker ?? new CircuitBreakerSettings();

        // Configure for consecutive failures: FailureRatio=1.0 means 100% of recent calls must fail
        // Short SamplingDuration effectively tracks consecutive failures within that window
        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0, // Require 100% failure rate (consecutive failures)
                SamplingDuration = TimeSpan.FromSeconds(5), // Short window for consecutive tracking
                MinimumThroughput = breakerSettings.FailureThreshold, // N consecutive failures
                BreakDuration = TimeSpan.FromSeconds(breakerSettings.ResetTimeoutSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception, "Circuit breaker opened for {Delay} after {Threshold} consecutive failures.",
                        args.BreakDuration, breakerSettings.FailureThreshold);
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker closed and reset.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker half-open, testing if system recovered.");
                    return default;
                }
            })
            .Build();
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
        await _pipeline.ExecuteAsync(async ct => await operation(ct), cancellationToken);
}
