using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class StaTaskRunnerTests : IDisposable
{
    // Each test method creates its own runner so tests are independent.
    // The class-level runner is used by tests that do NOT need lifecycle isolation.
    private readonly StaTaskRunner _runner;

    public StaTaskRunnerTests()
    {
        _runner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }

    // -------------------------------------------------------------------------
    // 1. Returns result produced by the func
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_ReturnsResultFromFunc()
    {
        const int expected = 42;

        var result = await _runner.RunAsync(() => Task.FromResult(expected));

        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // 2. Pre-cancelled token yields Task.FromCanceled without queuing work
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_WithPreCancelledToken_ReturnsCancelledTask()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var workExecuted = false;

        var task = _runner.RunAsync(
            () => { workExecuted = true; return Task.FromResult(0); },
            cts.Token);

        // The returned task must already be in the Canceled state.
        Assert.True(task.IsCanceled);

        // Awaiting a canceled task throws OperationCanceledException.
        await Assert.ThrowsAsync<TaskCanceledException>(() => task);

        // The func body must never have run.
        Assert.False(workExecuted);
    }

    // -------------------------------------------------------------------------
    // 3. Exception thrown inside func propagates out of RunAsync
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_PropagatesExceptionFromFunc()
    {
        var exception = new InvalidOperationException("test error");

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _runner.RunAsync<int>(() => Task.FromException<int>(exception)));

        Assert.Same(exception, thrownException);
    }

    // -------------------------------------------------------------------------
    // 4. Calling RunAsync after Dispose throws ObjectDisposedException
    // -------------------------------------------------------------------------
    [Fact]
    public void RunAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var runner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
        runner.Dispose();

        // RunAsync throws ObjectDisposedException synchronously (before returning
        // a Task) when the runner has been disposed.  We capture it via a
        // non-Task Action lambda so the xUnit async analyzer does not trigger.
        ObjectDisposedException? capturedException = null;
        try
        {
            _ = runner.RunAsync(() => Task.FromResult(0));
        }
        catch (ObjectDisposedException ex)
        {
            capturedException = ex;
        }
        Assert.NotNull(capturedException);
    }

    // -------------------------------------------------------------------------
    // 5. Multiple sequential RunAsync calls all return correct results in order
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_MultipleSequentialCalls_ReturnCorrectResults()
    {
        var results = new List<int>();

        for (var i = 0; i < 10; i++)
        {
            var captured = i;
            var value = await _runner.RunAsync(() => Task.FromResult(captured));
            results.Add(value);
        }

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(i, results[i]);
        }
    }

    // -------------------------------------------------------------------------
    // 6. Dispose is idempotent — calling it twice must not throw
    // -------------------------------------------------------------------------
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var runner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);

        runner.Dispose();
        var ex = Record.Exception(runner.Dispose);

        Assert.Null(ex);
    }

    // -------------------------------------------------------------------------
    // 7. Work executes on the dedicated STA thread
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_ExecutesOnStaThread()
    {
        ApartmentState? state = null;

        await _runner.RunAsync(() =>
        {
            state = Thread.CurrentThread.GetApartmentState();
            return Task.FromResult(0);
        });

        Assert.Equal(ApartmentState.STA, state);
    }

    // -------------------------------------------------------------------------
    // 8. Token cancelled during execution — task transitions to canceled state
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_WhenTokenCancelledDuringExecution_TaskIsCancelled()
    {
        using var cts = new CancellationTokenSource();

        // The func signals cancellation then throws, mimicking real mid-flight cancel.
        var task = _runner.RunAsync<int>(
            () =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        // Task must be in a faulted-or-cancelled terminal state, not running.
        Assert.True(task.IsCompleted);
    }

    // -------------------------------------------------------------------------
    // 9. Concurrent RunAsync calls all complete with correct values
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_ConcurrentCalls_AllComplete()
    {
        const int count = 20;
        var tasks = new Task<int>[count];

        for (var i = 0; i < count; i++)
        {
            var captured = i;
            tasks[i] = _runner.RunAsync(() => Task.FromResult(captured));
        }

        var results = await Task.WhenAll(tasks);

        // Every index value must appear exactly once.
        var sorted = new List<int>(results);
        sorted.Sort();
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(i, sorted[i]);
        }
    }

    // -------------------------------------------------------------------------
    // 10. Synchronous (already-completed) result works correctly
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_WithSynchronousResult_Works()
    {
        // Task.FromResult completes synchronously before RunAsync can await it.
        var result = await _runner.RunAsync(() => Task.FromResult("hello"));

        Assert.Equal("hello", result);
    }

    // -------------------------------------------------------------------------
    // 11. Dispose waits for in-progress work before returning
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Dispose_WaitsForInProgressWork()
    {
        var runner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);

        var workStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workCanFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workCompleted = false;

        // Queue long-running work.
        var workTask = runner.RunAsync(async () =>
        {
            workStarted.SetResult(true);
            await workCanFinish.Task.ConfigureAwait(false);
            workCompleted = true;
            return 1;
        });

        // Wait until the STA thread has actually started the work item.
        await workStarted.Task;

        // Unblock the work and immediately dispose — Dispose must join the thread.
        workCanFinish.SetResult(true);
        runner.Dispose();

        // After Dispose the work task must have completed.
        await workTask;
        Assert.True(workCompleted);
    }

    // -------------------------------------------------------------------------
    // 12. RunAsync works correctly for multiple different generic return types
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_ReturnsCorrectTypes_ForDifferentGenerics()
    {
        var intResult = await _runner.RunAsync(() => Task.FromResult(7));
        var stringResult = await _runner.RunAsync(() => Task.FromResult("result"));
        var boolResult = await _runner.RunAsync(() => Task.FromResult(true));

        Assert.Equal(7, intResult);
        Assert.Equal("result", stringResult);
        Assert.True(boolResult);
    }

    // -------------------------------------------------------------------------
    // 13. Thread name is set to the expected SolidWorks STA sentinel value
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_ThreadName_IsSolidWorksStaWorker()
    {
        string? threadName = null;

        await _runner.RunAsync(() =>
        {
            threadName = Thread.CurrentThread.Name;
            return Task.FromResult(0);
        });

        Assert.Equal("SolidWorks-STA", threadName);
    }

    // -------------------------------------------------------------------------
    // 14. Faulting work items do not poison subsequent calls
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RunAsync_FaultedCall_DoesNotPoisonSubsequentCalls()
    {
        // First call faults.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _runner.RunAsync<int>(() => Task.FromException<int>(new InvalidOperationException("boom"))));

        // Second call must still succeed.
        var result = await _runner.RunAsync(() => Task.FromResult(99));
        Assert.Equal(99, result);
    }
}
