using System;
using System.Threading;
using System.Threading.Tasks;

namespace FurniOx.SolidWorks.Core.Interfaces;

public interface ICircuitBreaker
{
    /// <summary>
    /// Executes an operation with circuit breaker protection.
    /// The operation receives the cancellation token to support proper shutdown.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
