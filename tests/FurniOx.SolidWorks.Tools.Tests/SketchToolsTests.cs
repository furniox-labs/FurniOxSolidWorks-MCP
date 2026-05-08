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
/// Parameter-mapping tests for SketchTools.cs (class: SketchGeometryTools).
/// These tests verify non-overlapping scenarios not covered by SketchGeometryToolsTests.cs.
/// </summary>
public sealed class SketchToolsTests
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
    public async Task EditSketch_PassesSketchNameAndUseSelectedToRouter()
    {
        var router = CreateRouter("Sketch.EditSketch", p =>
            (string)p["SketchName"]! == "Sketch1" &&
            (bool)p["UseSelected"]! == false);

        var tools = new SketchGeometryTools(router.Object);
        await tools.EditSketch("Sketch1");

        router.VerifyAll();
    }

    [Fact]
    public async Task EditSketch_UseSelected_PassesTrueToRouter()
    {
        var router = CreateRouter("Sketch.EditSketch", p =>
            (bool)p["UseSelected"]! == true);

        var tools = new SketchGeometryTools(router.Object);
        await tools.EditSketch(useSelected: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchEllipse_PassesAllSixCoordinatesToRouter()
    {
        var router = CreateRouter("Sketch.SketchEllipse", p =>
            (double)p["Xc"]! == 0 &&
            (double)p["Yc"]! == 0 &&
            (double)p["Xmaj"]! == 10 &&
            (double)p["Ymaj"]! == 0 &&
            (double)p["Xmin"]! == 0 &&
            (double)p["Ymin"]! == 5);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchEllipse(0, 0, 10, 0, 0, 5);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchSpline_PassesPointsArrayToRouter()
    {
        var points = new double[] { 0, 0, 0, 10, 5, 0, 20, 0, 0 };
        var router = CreateRouter("Sketch.SketchSpline", p =>
            ReferenceEquals(p["Points"], points));

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchSpline(points);

        router.VerifyAll();
    }
}
