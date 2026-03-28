using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Intelligence;

public sealed class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, Metric> _metrics = new();

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
    }

    public void RecordExecution(string operation, TimeSpan duration, bool success)
    {
        // Use immutable replacement pattern for thread safety
        _metrics.AddOrUpdate(operation,
            _ => new Metric(operation, 1, success ? 1 : 0, duration),
            (_, existing) =>
            {
                // Create new immutable Metric instead of mutating existing
                return new Metric(
                    existing.Operation,
                    existing.Invocations + 1,
                    existing.Successes + (success ? 1 : 0),
                    existing.TotalDuration + duration);
            });

        _logger.LogDebug("Recorded {Operation}: duration={Duration}, success={Success}", operation, duration, success);
    }

    public IReadOnlyCollection<ExecutionMetric> Snapshot()
        => _metrics.Values.Select(m => new ExecutionMetric
        {
            Operation = m.Operation,
            Invocations = m.Invocations,
            Successes = m.Successes,
            TotalDuration = m.TotalDuration
        }).ToList();

    private sealed class Metric
    {
        public Metric(string operation, int invocations, int successes, TimeSpan totalDuration)
        {
            Operation = operation;
            Invocations = invocations;
            Successes = successes;
            TotalDuration = totalDuration;
        }

        public string Operation { get; init; }
        public int Invocations { get; init; }  // Changed to init for immutability
        public int Successes { get; init; }    // Changed to init for immutability
        public TimeSpan TotalDuration { get; init; }  // Changed to init for immutability
    }
}
