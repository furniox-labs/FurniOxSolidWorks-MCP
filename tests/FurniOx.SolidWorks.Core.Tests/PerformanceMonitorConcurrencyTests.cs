using System;
using System.Linq;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class PerformanceMonitorConcurrencyTests
{
    [Fact]
    public void RecordExecution_ConcurrentCalls_ProduceConsistentInvocationCount()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();
        const string operation = "Sketch.CreateCircle";
        const int threadCount = 100;

        Parallel.For(0, threadCount, _ =>
            monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(1), success: true));

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(threadCount, metric.Invocations);
    }

    [Fact]
    public async Task RecordExecution_ConcurrentCallsMixedSuccess_SuccessCountIsConsistent()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();
        const string operation = "Document.Save";
        const int successCount = 60;
        const int failureCount = 40;

        var successTasks = Enumerable.Repeat(0, successCount)
            .Select(_ => Task.Run(() =>
                monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(1), success: true)));

        var failureTasks = Enumerable.Repeat(0, failureCount)
            .Select(_ => Task.Run(() =>
                monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(1), success: false)));

        await Task.WhenAll([.. successTasks, .. failureTasks]);

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(successCount + failureCount, metric.Invocations);
        Assert.Equal(successCount, metric.Successes);
    }

    [Fact]
    public void RecordExecution_ConcurrentCallsOnDifferentOperations_AllTrackedIndependently()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();
        var operations = new[] { "Op.Alpha", "Op.Beta", "Op.Gamma" };
        const int callsPerOperation = 50;

        Parallel.ForEach(operations, operation =>
        {
            for (var i = 0; i < callsPerOperation; i++)
            {
                monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(1), success: true);
            }
        });

        var metrics = monitor.Snapshot();
        Assert.Equal(3, metrics.Count);

        foreach (var operation in operations)
        {
            ExecutionMetric metric = metrics.First(m => m.Operation == operation);
            Assert.Equal(callsPerOperation, metric.Invocations);
        }
    }
}
