using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Connection;

/// <summary>
/// Dedicated STA worker that runs all COM-bound operations on a single thread.
/// Ensures SolidWorks interop stays on STA and avoids thread-pool MTA issues.
/// </summary>
public sealed class StaTaskRunner : IStaTaskRunner
{
    private readonly BlockingCollection<WorkItem> _queue = new();
    private readonly Thread _thread;
    private readonly ILogger<StaTaskRunner> _logger;
    private bool _disposed;

    public StaTaskRunner(ILogger<StaTaskRunner> logger)
    {
        _logger = logger;
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "SolidWorks-STA",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> RunAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(StaTaskRunner));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(new WorkItem(async () =>
        {
            try
            {
                var result = await func().ConfigureAwait(false);
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(oce.CancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, cancellationToken));

        return tcs.Task;
    }

    private void RunLoop()
    {
        try
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                if (item.Cancellation.IsCancellationRequested)
                {
                    continue;
                }

                try
                {
                    item.Execute().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "STA task execution failed");
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Queue disposed during shutdown; safe to ignore.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _queue.CompleteAdding();
        try
        {
            if (!_thread.Join(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("STA thread did not terminate within timeout");
            }
        }
        catch (ThreadStateException)
        {
            // Thread already stopped.
        }
        _queue.Dispose();
    }

    private sealed record WorkItem(Func<Task> Execute, CancellationToken Cancellation);
}
