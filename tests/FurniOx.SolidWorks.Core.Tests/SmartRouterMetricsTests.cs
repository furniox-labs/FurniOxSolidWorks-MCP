using System;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class SmartRouterMetricsTests : SmartRouterTestBase
{
    [Fact]
    public async Task RouteAsync_RecordsPerformanceMetrics_OnSuccess()
    {
        var router = CreateRouter(new SuccessAdapter());
        const string operation = "Sketch.SketchLine";

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);
        Assert.Equal(1, metric.Invocations);
        Assert.Equal(1, metric.Successes);
        Assert.Equal(1.0, metric.SuccessRate);
    }

    [Fact]
    public async Task RouteAsync_RecordsPerformanceMetrics_OnFailure()
    {
        var router = CreateRouter(new FailureAdapter());
        const string operation = "Feature.CreateExtrusion";

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);
        Assert.Equal(1, metric.Invocations);
        Assert.Equal(0, metric.Successes);
        Assert.Equal(0.0, metric.SuccessRate);
    }

    [Fact]
    public async Task RouteAsync_WhenAdapterThrows_RecordsMetricsWithFailure()
    {
        var router = CreateRouter(new ThrowingComAdapter());
        const string operation = "Document.SaveModel";

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);
        Assert.Equal(1, metric.Invocations);
        Assert.Equal(0, metric.Successes);
    }

    [Fact]
    public async Task RouteAsync_MultipleOperations_TracksMetricsSeparately()
    {
        var router = CreateRouter(new SuccessAdapter());
        var operations = new[] { "Sketch.SketchCircle", "Feature.CreateExtrusion", "Document.SaveModel" };

        foreach (var operation in operations)
        {
            await router.RouteAsync(operation, new Dictionary<string, object?>());
        }

        var metrics = router.GetPerformanceMetrics();
        foreach (var operation in operations)
        {
            var metric = metrics.Single(m => m.Operation == operation);
            Assert.Equal(1, metric.Invocations);
            Assert.Equal(1, metric.Successes);
        }
    }

    [Fact]
    public void GetPerformanceMetrics_ReturnsEmptyForFreshRouter()
    {
        var router = CreateRouter(new SuccessAdapter());
        Assert.Empty(router.GetPerformanceMetrics());
    }

    [Fact]
    public async Task RouteAsync_MultipleSequentialCalls_AllSucceed()
    {
        const int iterations = 5;
        const string operation = "Sketch.SketchLine";
        var router = CreateRouter(new SuccessAdapter());

        for (var i = 0; i < iterations; i++)
        {
            var result = await router.RouteAsync(operation, new Dictionary<string, object?>());
            Assert.True(result.Success);
        }

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);
        Assert.Equal(iterations, metric.Invocations);
        Assert.Equal(iterations, metric.Successes);
    }

    [Fact]
    public async Task RouteAsync_MixedResults_AccumulatesCorrectMetrics()
    {
        const string operation = "Document.RebuildModel";
        var callCount = 0;
        var router = CreateRouter(new ConfigurableAdapter(_ =>
        {
            callCount++;
            return callCount % 2 != 0
                ? ExecutionResult.SuccessResult()
                : ExecutionResult.Failure("even call");
        }));

        for (var i = 0; i < 6; i++)
        {
            await router.RouteAsync(operation, new Dictionary<string, object?>());
        }

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);
        Assert.Equal(6, metric.Invocations);
        Assert.Equal(3, metric.Successes);
        Assert.Equal(0.5, metric.SuccessRate);
    }

    [Fact]
    public async Task RouteAsync_RecordsDuration_InMetrics()
    {
        const string operation = "Export.ExportToSTEP";
        var router = CreateRouter(new SuccessAdapter());

        await router.RouteAsync(operation, new Dictionary<string, object?>());

        var metric = router.GetPerformanceMetrics().Single(m => m.Operation == operation);
        Assert.True(metric.TotalDuration >= TimeSpan.Zero);
    }
}
