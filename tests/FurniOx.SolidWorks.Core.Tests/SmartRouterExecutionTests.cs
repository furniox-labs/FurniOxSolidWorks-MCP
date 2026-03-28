using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Shared.Configuration;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class SmartRouterExecutionTests : SmartRouterTestBase
{
    [Fact]
    public async Task RouteAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        var router = CreateRouter(new SuccessAdapter());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.RouteAsync("Sketch.SketchCircle", new Dictionary<string, object?>(), cts.Token));
    }

    [Fact]
    public async Task RouteAsync_WhenAdapterThrows_ReturnsFailure()
    {
        var router = CreateRouter(new ThrowingComAdapter());

        var result = await router.RouteAsync("Document.SaveModel", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task RouteAsync_PropagatesOperationCanceledException()
    {
        var router = CreateRouter(new CancellingAdapter());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.RouteAsync("Sketch.SketchLine", new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task RouteAsync_WithSuccessAdapter_ReturnsData()
    {
        var router = CreateRouter(new SuccessAdapter());

        var result = await router.RouteAsync("Sketch.SketchCircle", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task RouteAsync_ExecutionResult_ContainsCorrectMessage_OnFailure()
    {
        var router = CreateRouter(new FailureAdapter());

        var result = await router.RouteAsync("Feature.CreateExtrusion", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Equal("Simulated failure", result.Message);
    }

    [Fact]
    public async Task RouteAsync_ForwardsParameters_ToAdapter()
    {
        IDictionary<string, object?>? capturedParams = null;
        var router = CreateRouter(new CapturingAdapter(parameters => capturedParams = parameters));
        var inputParams = new Dictionary<string, object?>
        {
            ["depth"] = 10.0,
            ["direction"] = "positive"
        };

        await router.RouteAsync("Feature.CreateExtrusion", inputParams);

        Assert.NotNull(capturedParams);
        Assert.Equal(inputParams["depth"], capturedParams["depth"]);
        Assert.Equal(inputParams["direction"], capturedParams["direction"]);
    }

    [Fact]
    public async Task RouteAsync_WhenParameterLimitExceeded_ReturnsFailureWithoutCallingAdapter()
    {
        var settings = new SolidWorksSettings
        {
            ComParameterLimit = 1,
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = 10,
                ResetTimeoutSeconds = 60
            }
        };
        var adapter = new CapturingAdapter(_ => throw new InvalidOperationException("Adapter should not be called."));
        var router = CreateRouter(adapter, StaTaskRunner, settings);

        var result = await router.RouteAsync(
            "Feature.CreateExtrusion",
            new Dictionary<string, object?>
            {
                ["Depth"] = 10.0,
                ["ReverseDirection"] = false
            });

        Assert.False(result.Success);
        Assert.Contains("parameter limit", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Data);
    }
}
