using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class FeatureExtrusionToolsTests
{
    private static Mock<ISmartRouter> CreateRouter(string operation, Func<IDictionary<string, object?>, bool> predicate)
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                operation,
                It.Is<IDictionary<string, object?>>(p => predicate(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Ok = true }));
        return router;
    }

    [Fact]
    public async Task CreateExtrusion_DefaultParams_PassesDepthAndSingleDirectionToRouter()
    {
        var router = CreateRouter("Feature.CreateExtrusion", p =>
            (double)p["Depth"]! == 10.0 &&
            (bool)p["SingleDirection"]! == true &&
            (bool)p["ReverseDirection"]! == false &&
            (bool)p["MergeResult"]! == true);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateExtrusion();

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateExtrusion_CustomDepth_PassesDepthToRouter()
    {
        var router = CreateRouter("Feature.CreateExtrusion", p =>
            (double)p["Depth"]! == 25.0 &&
            (bool)p["SingleDirection"]! == false &&
            (double)p["Depth2"]! == 15.0);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateExtrusion(depth: 25.0, singleDirection: false, depth2: 15.0);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateExtrusion_RouterReturnsFailure_ThrowsMcpException()
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                "Feature.CreateExtrusion",
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.Failure("No sketch selected"));

        var tools = new FeatureExtrusionTools(router.Object);
        await Assert.ThrowsAsync<McpException>(() => tools.CreateExtrusion());
    }

    [Fact]
    public async Task CreateExtrusion_WithDraftParams_PassesDraftParamsToRouter()
    {
        var router = CreateRouter("Feature.CreateExtrusion", p =>
            (bool)p["UseDraft1"]! == true &&
            (double)p["DraftAngle1"]! == 5.0 &&
            (bool)p["DraftOutward1"]! == false);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateExtrusion(useDraft1: true, draftAngle1: 5.0, draftOutward1: false);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateCutExtrusion_DefaultParams_PassesDepthAndEndConditionToRouter()
    {
        var router = CreateRouter("Feature.CreateCutExtrusion", p =>
            (double)p["Depth"]! == 10.0 &&
            (int)p["EndCondition1"]! == 1 &&
            (bool)p["FlipSideToCut"]! == false);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateCutExtrusion();

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateCutExtrusion_CustomParams_PassesAllKeysToRouter()
    {
        var router = CreateRouter("Feature.CreateCutExtrusion", p =>
            (double)p["Depth"]! == 30.0 &&
            (bool)p["FlipSideToCut"]! == true &&
            (bool)p["NormalCut"]! == true);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateCutExtrusion(depth: 30.0, flipSideToCut: true, normalCut: true);

        router.VerifyAll();
    }
}
