using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Interfaces;

public interface ISmartRouter
{
    Task<ExecutionResult> RouteAsync(string operation, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
    IReadOnlyCollection<ExecutionMetric> GetPerformanceMetrics();
}

/// <summary>
/// Executes SolidWorks-bound operations on a dedicated STA thread.
/// </summary>
public interface IStaTaskRunner : IDisposable
{
    Task<T> RunAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default);
}
