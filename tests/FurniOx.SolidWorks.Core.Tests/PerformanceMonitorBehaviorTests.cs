using System;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Interfaces;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class PerformanceMonitorBehaviorTests
{
    [Fact]
    public void RecordExecution_FirstCall_CreatesMetricWithSingleInvocation()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();

        monitor.RecordExecution("Sketch.CreateLine", TimeSpan.FromMilliseconds(10), success: true);

        IReadOnlyCollection<ExecutionMetric> metrics = monitor.Snapshot();
        var metric = Assert.Single(metrics);
        Assert.Equal("Sketch.CreateLine", metric.Operation);
        Assert.Equal(1, metric.Invocations);
    }

    [Fact]
    public void RecordExecution_MultipleCalls_TracksInvocationsSuccessesAndDuration()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();
        const string operation = "Sketch.AddRelation";

        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(10), success: true);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(20), success: false);
        monitor.RecordExecution(operation, TimeSpan.FromMilliseconds(30), success: true);

        var metric = monitor.Snapshot().First(m => m.Operation == operation);
        Assert.Equal(3, metric.Invocations);
        Assert.Equal(2, metric.Successes);
        Assert.Equal(TimeSpan.FromMilliseconds(60), metric.TotalDuration);
    }

    [Fact]
    public void RecordExecution_DifferentOperations_AppearSeparately()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();

        monitor.RecordExecution("Sketch.CreateLine", TimeSpan.FromMilliseconds(10), success: true);
        monitor.RecordExecution("Sketch.CreateLine", TimeSpan.FromMilliseconds(10), success: true);
        monitor.RecordExecution("Feature.Extrude", TimeSpan.FromMilliseconds(50), success: true);

        IReadOnlyCollection<ExecutionMetric> metrics = monitor.Snapshot();
        Assert.Equal(2, metrics.Count);
        Assert.Equal(2, metrics.First(m => m.Operation == "Sketch.CreateLine").Invocations);
        Assert.Equal(1, metrics.First(m => m.Operation == "Feature.Extrude").Invocations);
    }

    [Fact]
    public void Snapshot_EmptyMonitor_ReturnsEmptyCollection()
    {
        Assert.Empty(PerformanceMonitorTestSupport.CreateMonitor().Snapshot());
    }

    [Fact]
    public void Snapshot_AfterSingleRecord_ContainsExpectedData()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();
        monitor.RecordExecution("Selection.Select", TimeSpan.FromMilliseconds(8), success: true);

        var metric = monitor.Snapshot().First();
        Assert.Equal("Selection.Select", metric.Operation);
        Assert.Equal(1, metric.Invocations);
        Assert.Equal(1, metric.Successes);
        Assert.Equal(TimeSpan.FromMilliseconds(8), metric.TotalDuration);
    }

    [Fact]
    public void Snapshot_IsImmutableAcrossSubsequentUpdates()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();
        monitor.RecordExecution("Sketch.Trim", TimeSpan.FromMilliseconds(5), success: true);
        IReadOnlyCollection<ExecutionMetric> snapshot = monitor.Snapshot();

        monitor.RecordExecution("Sketch.Trim", TimeSpan.FromMilliseconds(5), success: true);
        monitor.RecordExecution("Sketch.Trim", TimeSpan.FromMilliseconds(5), success: true);

        Assert.Equal(1, snapshot.First().Invocations);
        Assert.Equal(3, monitor.Snapshot().First().Invocations);
    }

    [Fact]
    public void Snapshot_CalledTwice_ReflectsCurrentStateEachTime()
    {
        var monitor = PerformanceMonitorTestSupport.CreateMonitor();
        monitor.RecordExecution("Feature.Shell", TimeSpan.FromMilliseconds(40), success: true);
        IReadOnlyCollection<ExecutionMetric> firstSnapshot = monitor.Snapshot();

        monitor.RecordExecution("Feature.Shell", TimeSpan.FromMilliseconds(40), success: false);
        IReadOnlyCollection<ExecutionMetric> secondSnapshot = monitor.Snapshot();

        Assert.Equal(1, firstSnapshot.First().Invocations);
        Assert.Equal(2, secondSnapshot.First().Invocations);
    }

    [Fact]
    public void Snapshot_ComputesSuccessRateForAllCommonCases()
    {
        var allSuccessMonitor = PerformanceMonitorTestSupport.CreateMonitor();
        allSuccessMonitor.RecordExecution("Sketch.CreateRect", TimeSpan.FromMilliseconds(5), success: true);
        allSuccessMonitor.RecordExecution("Sketch.CreateRect", TimeSpan.FromMilliseconds(5), success: true);
        Assert.Equal(1.0, allSuccessMonitor.Snapshot().First().SuccessRate);

        var noSuccessMonitor = PerformanceMonitorTestSupport.CreateMonitor();
        noSuccessMonitor.RecordExecution("Feature.Revolve", TimeSpan.FromMilliseconds(30), success: false);
        noSuccessMonitor.RecordExecution("Feature.Revolve", TimeSpan.FromMilliseconds(30), success: false);
        Assert.Equal(0.0, noSuccessMonitor.Snapshot().First().SuccessRate);

        var mixedMonitor = PerformanceMonitorTestSupport.CreateMonitor();
        mixedMonitor.RecordExecution("CustomProperty.Set", TimeSpan.FromMilliseconds(5), success: true);
        mixedMonitor.RecordExecution("CustomProperty.Set", TimeSpan.FromMilliseconds(5), success: false);
        Assert.Equal(0.5, mixedMonitor.Snapshot().First().SuccessRate, precision: 10);
    }
}
