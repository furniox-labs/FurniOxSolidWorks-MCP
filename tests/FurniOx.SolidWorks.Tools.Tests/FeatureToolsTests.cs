using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

/// <summary>
/// Parameter-mapping tests for FeatureTools.cs (class: FeatureExtrusionTools).
/// Covers scenarios not present in FeatureExtrusionToolsTests.cs.
/// </summary>
public sealed class FeatureToolsTests
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
    public async Task CreateExtrusion_ReverseDirection_PassesReverseDirectionTrueToRouter()
    {
        var router = CreateRouter("Feature.CreateExtrusion", p =>
            (bool)p["ReverseDirection"]! == true &&
            (double)p["Depth"]! == 10.0);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateExtrusion(reverseDirection: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateExtrusion_AutoSelect_DefaultsToTrue()
    {
        var router = CreateRouter("Feature.CreateExtrusion", p =>
            (bool)p["AutoSelect"]! == true);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateExtrusion();

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateCutExtrusion_NormalCutAndOptimize_PassesBothFlagsToRouter()
    {
        var router = CreateRouter("Feature.CreateCutExtrusion", p =>
            (bool)p["NormalCut"]! == true &&
            (bool)p["OptimizeGeometry"]! == true);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateCutExtrusion(normalCut: true, optimizeGeometry: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateCutExtrusion_PropagateToPartsFalse_IsDefaultBehavior()
    {
        var router = CreateRouter("Feature.CreateCutExtrusion", p =>
            (bool)p["PropagateFeatureToParts"]! == false);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateCutExtrusion();

        router.VerifyAll();
    }
}
