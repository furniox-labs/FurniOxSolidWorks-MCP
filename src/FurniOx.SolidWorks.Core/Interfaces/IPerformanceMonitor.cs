using System;
using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Interfaces;

public interface IPerformanceMonitor
{
    void RecordExecution(string operation, TimeSpan duration, bool success);
    IReadOnlyCollection<ExecutionMetric> Snapshot();
}

public sealed class ExecutionMetric
{
    public string Operation { get; init; } = string.Empty;
    public int Invocations { get; init; }
    public int Successes { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public double SuccessRate => Invocations == 0 ? 0 : (double)Successes / Invocations;
}
