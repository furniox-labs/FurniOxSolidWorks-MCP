using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class SketchGeometryToolsTests
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

    private static Mock<ISmartRouter> CreateRouterAny(string operation)
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                operation,
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Ok = true }));
        return router;
    }

    [Fact]
    public async Task CreateSketch_PassesPlaneToRouter()
    {
        var router = CreateRouter("Sketch.CreateSketch", p =>
            (string)p["Plane"]! == "Top");

        var tools = new SketchGeometryTools(router.Object);
        await tools.CreateSketch("Top");

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateSketch_DefaultPlane_PassesFrontToRouter()
    {
        var router = CreateRouter("Sketch.CreateSketch", p =>
            (string)p["Plane"]! == "Front");

        var tools = new SketchGeometryTools(router.Object);
        await tools.CreateSketch();

        router.VerifyAll();
    }

    [Fact]
    public async Task ExitSketch_RoutesToCorrectOperation()
    {
        var router = CreateRouterAny("Sketch.ExitSketch");

        var tools = new SketchGeometryTools(router.Object);
        await tools.ExitSketch();

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchLine_PassesAllCoordinatesToRouter()
    {
        var router = CreateRouter("Sketch.SketchLine", p =>
            (double)p["X1"]! == 0 &&
            (double)p["Y1"]! == 0 &&
            (double)p["X2"]! == 50 &&
            (double)p["Y2"]! == 30);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchLine(0, 0, 50, 30);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchLine_RouterReturnsFailure_ThrowsMcpException()
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                "Sketch.SketchLine",
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.Failure("No active sketch"));

        var tools = new SketchGeometryTools(router.Object);
        await Assert.ThrowsAsync<McpException>(() => tools.SketchLine(0, 0, 10, 10));
    }

    [Fact]
    public async Task SketchCircle_PassesCenterAndRadiusToRouter()
    {
        var router = CreateRouter("Sketch.SketchCircle", p =>
            (double)p["CenterX"]! == 10 &&
            (double)p["CenterY"]! == 20 &&
            (double)p["Radius"]! == 15);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchCircle(10, 20, 15);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchCornerRectangle_PassesAllCornersToRouter()
    {
        var router = CreateRouter("Sketch.SketchCornerRectangle", p =>
            (double)p["X1"]! == 0 &&
            (double)p["Y1"]! == 0 &&
            (double)p["X2"]! == 100 &&
            (double)p["Y2"]! == 50);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchCornerRectangle(0, 0, 100, 50);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchArc_PassesAllParamsToRouter()
    {
        var router = CreateRouter("Sketch.SketchArc", p =>
            (double)p["CenterX"]! == 5 &&
            (double)p["CenterY"]! == 5 &&
            (double)p["Radius"]! == 10 &&
            (double)p["StartAngle"]! == 0 &&
            (double)p["EndAngle"]! == 90 &&
            (bool)p["Clockwise"]! == false);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchArc(5, 5, 10);

        router.VerifyAll();
    }

    [Fact]
    public async Task Sketch3PointArc_PassesAllPointsToRouter()
    {
        var router = CreateRouter("Sketch.Sketch3PointArc", p =>
            (double)p["X1"]! == 0 &&
            (double)p["Y1"]! == 0 &&
            (double)p["X2"]! == 5 &&
            (double)p["Y2"]! == 5 &&
            (double)p["X3"]! == 10 &&
            (double)p["Y3"]! == 0);

        var tools = new SketchGeometryTools(router.Object);
        await tools.Sketch3PointArc(0, 0, 5, 5, 10, 0);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchCenterLine_PassesCoordinatesToRouter()
    {
        var router = CreateRouter("Sketch.SketchCenterLine", p =>
            (double)p["X1"]! == -50 &&
            (double)p["Y1"]! == 0 &&
            (double)p["X2"]! == 50 &&
            (double)p["Y2"]! == 0);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchCenterLine(-50, 0, 50, 0);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchPoint_PassesXYZToRouter()
    {
        var router = CreateRouter("Sketch.SketchPoint", p =>
            (double)p["X"]! == 5 &&
            (double)p["Y"]! == 10 &&
            (double)p["Z"]! == 0);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchPoint(5, 10);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchPolygon_PassesSidesAndInscribedToRouter()
    {
        var router = CreateRouter("Sketch.SketchPolygon", p =>
            (int)p["Sides"]! == 6 &&
            (bool)p["Inscribed"]! == true);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchPolygon(0, 0, 10, 0);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchTangentArc_PassesArcTypeToRouter()
    {
        var router = CreateRouter("Sketch.SketchTangentArc", p =>
            (int)p["ArcType"]! == 2);

        var tools = new SketchGeometryTools(router.Object);
        await tools.SketchTangentArc(0, 0, 10, 10, arcType: 2);

        router.VerifyAll();
    }
}
