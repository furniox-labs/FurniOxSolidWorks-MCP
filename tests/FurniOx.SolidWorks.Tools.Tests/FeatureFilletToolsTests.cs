using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class FeatureFilletToolsTests
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
    public async Task CreateFillet_DefaultParams_PassesRadiusTypeAndOptionsToRouter()
    {
        var router = CreateRouter("Feature.CreateFillet", p =>
            (double)p["Radius"]! == 2.0 &&
            (int)p["Type"]! == 0 &&
            (int)p["Options"]! == 195 &&
            (int)p["OverflowType"]! == 0 &&
            (int)p["ProfileType"]! == 0);

        var tools = new FeatureFilletTools(router.Object);
        await tools.CreateFillet();

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateFillet_CustomRadiusAndEdgeNames_PassesEdgeNamesToRouter()
    {
        var edges = new[] { "Edge1@Part1", "Edge2@Part1" };
        var router = CreateRouter("Feature.CreateFillet", p =>
            (double)p["Radius"]! == 5.0 &&
            ReferenceEquals(p["EdgeNames"], edges));

        var tools = new FeatureFilletTools(router.Object);
        await tools.CreateFillet(radius: 5.0, edgeNames: edges);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateFillet_FaceFillet_PassesFaceSet1AndFaceSet2ToRouter()
    {
        var faceSet1 = new[] { "Face1@Part1" };
        var faceSet2 = new[] { "Face2@Part1" };
        var router = CreateRouter("Feature.CreateFillet", p =>
            (int)p["Type"]! == 2 &&
            ReferenceEquals(p["FaceSet1Names"], faceSet1) &&
            ReferenceEquals(p["FaceSet2Names"], faceSet2));

        var tools = new FeatureFilletTools(router.Object);
        await tools.CreateFillet(type: 2, faceSet1Names: faceSet1, faceSet2Names: faceSet2);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateFillet_VariableRadius_PassesVariableRadiiToRouter()
    {
        var radii = new[] { 1.0, 2.0, 3.0 };
        var router = CreateRouter("Feature.CreateFillet", p =>
            (int)p["Type"]! == 1 &&
            ReferenceEquals(p["VariableRadii"], radii));

        var tools = new FeatureFilletTools(router.Object);
        await tools.CreateFillet(type: 1, variableRadii: radii);

        router.VerifyAll();
    }
}
