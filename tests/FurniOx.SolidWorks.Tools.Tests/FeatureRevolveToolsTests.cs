using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class FeatureRevolveToolsTests
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
    public async Task CreateRevolve_DefaultParams_PassesAngleAndSolidFlagsToRouter()
    {
        var router = CreateRouter("Feature.CreateRevolve", p =>
            (double)p["Angle1"]! == 360.0 &&
            (bool)p["IsSolid"]! == true &&
            (bool)p["IsThin"]! == false &&
            (bool)p["IsCut"]! == false &&
            (bool)p["SingleDirection"]! == true &&
            (bool)p["MergeResult"]! == true);

        var tools = new FeatureRevolveTools(router.Object);
        await tools.CreateRevolve();

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateRevolve_WithAxisEntity_PassesAxisEntityAndTypeToRouter()
    {
        var router = CreateRouter("Feature.CreateRevolve", p =>
            (string)p["AxisEntity"]! == "Line1@Sketch1" &&
            (string)p["AxisEntityType"]! == "LINE");

        var tools = new FeatureRevolveTools(router.Object);
        await tools.CreateRevolve(axisEntity: "Line1@Sketch1", axisEntityType: "LINE");

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateRevolve_CutRevolve_PassesIsCutTrueToRouter()
    {
        var router = CreateRouter("Feature.CreateRevolve", p =>
            (bool)p["IsCut"]! == true &&
            (double)p["Angle1"]! == 180.0);

        var tools = new FeatureRevolveTools(router.Object);
        await tools.CreateRevolve(isCut: true, angle1: 180.0);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateRevolve_ThinRevolve_PassesThinParamsToRouter()
    {
        var router = CreateRouter("Feature.CreateRevolve", p =>
            (bool)p["IsThin"]! == true &&
            (int)p["ThinType"]! == 0 &&
            (double)p["ThinThickness1"]! == 3.0);

        var tools = new FeatureRevolveTools(router.Object);
        await tools.CreateRevolve(isThin: true, thinType: 0, thinThickness1: 3.0);

        router.VerifyAll();
    }
}
