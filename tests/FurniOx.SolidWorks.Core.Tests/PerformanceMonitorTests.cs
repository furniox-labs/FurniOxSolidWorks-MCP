using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Intelligence;
using FurniOx.SolidWorks.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Unit tests for PerformanceMonitor.
///
/// PerformanceMonitor uses ConcurrentDictionary with an immutable-replacement
/// update pattern: each RecordExecution creates a new Metric record rather than
/// mutating the existing one. Tests verify both the functional contract and the
/// thread-safety guarantee that arises from this design.
/// </summary>
public class PerformanceMonitorTests
{
    private static PerformanceMonitor CreateMonitor()
        => new(NullLogger<PerformanceMonitor>.Instance);

    // =========================================================================
    // RecordExecution — first call
    // =========================================================================

    [Fact]
    public void RecordExecution_FirstCall_CreatesNewMetric()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("Sketch.CreateLine", TimeSpan.FromMilliseconds(10), success: true);

        IReadOnlyCollection<ExecutionMetric> metrics = monitor.Snapshot();
        Assert.Single(metrics);
        Assert.Equal("Sketch.CreateLine", metrics.First().Operation);
    }

    [Fact]
    public void RecordExecution_FirstCall_InvocationCountIsOne()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("Feature.Extrude", TimeSpan.FromMilliseconds(50), success: true);

        ExecutionMetric metric = monitor.Snapshot().First();
        Assert.Equal(1, metric.Invocations);
    }

    // =========================================================================
    // RecordExecution — invocation counting
    // =========================================================================

    [Fact]
    public void RecordExecution_MultipleCalls_IncrementsInvocationCount()
    {
        var monitor = CreateMonitor();
        const string operation = "Document.Open";

        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(10), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(15), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(12), success: false);

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(3, metric.Invocations);
    }

    // =========================================================================
    // RecordExecution — success counting
    // =========================================================================

    [Fact]
    public void RecordExecution_SuccessCalls_IncrementsSuccessCount()
    {
        var monitor = CreateMonitor();
        const string operation = "Export.SaveStep";

        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(100), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(110), success: true);

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(2, metric.Successes);
    }

    [Fact]
    public void RecordExecution_FailureCalls_DoNotIncrementSuccessCount()
    {
        var monitor = CreateMonitor();
        const string operation = "Feature.CreateFillet";

        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(20), success: false);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(25), success: false);

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(0, metric.Successes);
    }

    [Fact]
    public void RecordExecution_MixedSuccessAndFailure_CountsCorrectly()
    {
        var monitor = CreateMonitor();
        const string operation = "Sketch.AddRelation";

        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(5), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(5), success: false);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(5), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(5), success: false);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(5), success: true);

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(5, metric.Invocations);
        Assert.Equal(3, metric.Successes);
    }

    // =========================================================================
    // RecordExecution — duration accumulation
    // =========================================================================

    [Fact]
    public void RecordExecution_MultipleCalls_AccumulatesTotalDuration()
    {
        var monitor = CreateMonitor();
        const string operation = "Configuration.Activate";

        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(10), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(20), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(30), success: false);

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(TimeSpan.FromMilliseconds(60), metric.TotalDuration);
    }

    [Fact]
    public void RecordExecution_FirstCall_SetsTotalDurationExactly()
    {
        var monitor = CreateMonitor();
        var duration = TimeSpan.FromMilliseconds(123);

        monitor.RecordExecution("Sketch.CreateArc", duration, success: true);

        ExecutionMetric metric = monitor.Snapshot().First();
        Assert.Equal(duration, metric.TotalDuration);
    }

    // =========================================================================
    // RecordExecution — multiple distinct operations
    // =========================================================================

    [Fact]
    public void RecordExecution_DifferentOperations_TrackedSeparately()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("Sketch.CreateLine", TimeSpan.FromMilliseconds(10), success: true);
        monitor.RecordExecution("Sketch.CreateLine", TimeSpan.FromMilliseconds(10), success: true);
        monitor.RecordExecution("Feature.Extrude", TimeSpan.FromMilliseconds(50), success: true);

        IReadOnlyCollection<ExecutionMetric> metrics = monitor.Snapshot();

        Assert.Equal(2, metrics.Count);

        ExecutionMetric lineMetric = metrics.First(m => m.Operation == "Sketch.CreateLine");
        ExecutionMetric extrudeMetric = metrics.First(m => m.Operation == "Feature.Extrude");

        Assert.Equal(2, lineMetric.Invocations);
        Assert.Equal(1, extrudeMetric.Invocations);
    }

    [Fact]
    public void RecordExecution_ThreeDistinctOperations_AllAppearInSnapshot()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("Op.A", TimeSpan.FromMilliseconds(1), success: true);
        monitor.RecordExecution("Op.B", TimeSpan.FromMilliseconds(2), success: true);
        monitor.RecordExecution("Op.C", TimeSpan.FromMilliseconds(3), success: false);

        IReadOnlyCollection<ExecutionMetric> metrics = monitor.Snapshot();

        Assert.Equal(3, metrics.Count);
        Assert.Contains(metrics, m => m.Operation == "Op.A");
        Assert.Contains(metrics, m => m.Operation == "Op.B");
        Assert.Contains(metrics, m => m.Operation == "Op.C");
    }

    // =========================================================================
    // Snapshot — basic contract
    // =========================================================================

    [Fact]
    public void Snapshot_EmptyMonitor_ReturnsEmptyCollection()
    {
        var monitor = CreateMonitor();

        IReadOnlyCollection<ExecutionMetric> metrics = monitor.Snapshot();

        Assert.NotNull(metrics);
        Assert.Empty(metrics);
    }

    [Fact]
    public void Snapshot_AfterSingleRecord_ContainsCorrectData()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("Selection.Select", TimeSpan.FromMilliseconds(8), success: true);

        ExecutionMetric metric = monitor.Snapshot().First();

        Assert.Equal("Selection.Select", metric.Operation);
        Assert.Equal(1, metric.Invocations);
        Assert.Equal(1, metric.Successes);
        Assert.Equal(TimeSpan.FromMilliseconds(8), metric.TotalDuration);
    }

    // =========================================================================
    // Snapshot — immutability (snapshot isolation)
    // =========================================================================

    [Fact]
    public void Snapshot_SubsequentRecordingDoesNotMutateExistingSnapshot()
    {
        var monitor = CreateMonitor();
        monitor.RecordExecution("Sketch.Trim", TimeSpan.FromMilliseconds(5), success: true);

        // Capture a snapshot before adding more data
        IReadOnlyCollection<ExecutionMetric> snapshot = monitor.Snapshot();
        int invocationsBefore = snapshot.First().Invocations;

        // Add more recordings after the snapshot was taken
        monitor.RecordExecution("Sketch.Trim", TimeSpan.FromMilliseconds(5), success: true);
        monitor.RecordExecution("Sketch.Trim", TimeSpan.FromMilliseconds(5), success: true);

        // The already-captured snapshot must not reflect the new data
        Assert.Equal(invocationsBefore, snapshot.First().Invocations);
        Assert.Equal(1, invocationsBefore);
    }

    [Fact]
    public void Snapshot_CalledTwice_EachReflectsCurrentState()
    {
        var monitor = CreateMonitor();
        monitor.RecordExecution("Feature.Shell", TimeSpan.FromMilliseconds(40), success: true);

        IReadOnlyCollection<ExecutionMetric> firstSnapshot = monitor.Snapshot();

        monitor.RecordExecution("Feature.Shell", TimeSpan.FromMilliseconds(40), success: false);

        IReadOnlyCollection<ExecutionMetric> secondSnapshot = monitor.Snapshot();

        Assert.Equal(1, firstSnapshot.First().Invocations);
        Assert.Equal(2, secondSnapshot.First().Invocations);
    }

    // =========================================================================
    // SuccessRate derived property
    // =========================================================================

    [Fact]
    public void Snapshot_AllSuccesses_SuccessRateIsOne()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("Sketch.CreateRect", TimeSpan.FromMilliseconds(5), success: true);
        monitor.RecordExecution("Sketch.CreateRect", TimeSpan.FromMilliseconds(5), success: true);

        ExecutionMetric metric = monitor.Snapshot().First();
        Assert.Equal(1.0, metric.SuccessRate);
    }

    [Fact]
    public void Snapshot_NoSuccesses_SuccessRateIsZero()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("Feature.Revolve", TimeSpan.FromMilliseconds(30), success: false);
        monitor.RecordExecution("Feature.Revolve", TimeSpan.FromMilliseconds(30), success: false);

        ExecutionMetric metric = monitor.Snapshot().First();
        Assert.Equal(0.0, metric.SuccessRate);
    }

    [Fact]
    public void Snapshot_HalfSuccesses_SuccessRateIsPointFive()
    {
        var monitor = CreateMonitor();

        monitor.RecordExecution("CustomProperty.Set", TimeSpan.FromMilliseconds(5), success: true);
        monitor.RecordExecution("CustomProperty.Set", TimeSpan.FromMilliseconds(5), success: false);

        ExecutionMetric metric = monitor.Snapshot().First();
        Assert.Equal(0.5, metric.SuccessRate, precision: 10);
    }

    // =========================================================================
    // Thread safety
    // =========================================================================

    [Fact]
    public void RecordExecution_ConcurrentCalls_ProduceConsistentInvocationCount()
    {
        var monitor = CreateMonitor();
        const string operation = "Sketch.CreateCircle";
        const int threadCount = 100;

        // Fire 100 concurrent RecordExecution calls on the same operation key
        Parallel.For(0, threadCount, _ =>
            monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(1), success: true));

        ExecutionMetric metric = monitor.Snapshot().First(m => m.Operation == operation);

        // ConcurrentDictionary + immutable-replacement pattern must produce exactly 100
        Assert.Equal(threadCount, metric.Invocations);
    }

    [Fact]
    public async Task RecordExecution_ConcurrentCallsMixedSuccess_SuccessCountIsConsistent()
    {
        var monitor = CreateMonitor();
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
        var monitor = CreateMonitor();
        var operations = new[] { "Op.Alpha", "Op.Beta", "Op.Gamma" };
        const int callsPerOperation = 50;

        Parallel.ForEach(operations, op =>
        {
            for (int i = 0; i < callsPerOperation; i++)
            {
                monitor.RecordExecution(op, TimeSpan.FromMilliseconds(1), success: true);
            }
        });

        IReadOnlyCollection<ExecutionMetric> metrics = monitor.Snapshot();

        Assert.Equal(3, metrics.Count);
        foreach (string op in operations)
        {
            ExecutionMetric metric = metrics.First(m => m.Operation == op);
            Assert.Equal(callsPerOperation, metric.Invocations);
        }
    }
}
