using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class SketchInspectionToolsTests
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
    public async Task ListSketchSegments_RoutesToCorrectOperation()
    {
        var router = CreateRouterAny("Sketch.ListSketchSegments");

        var tools = new SketchInspectionTools(router.Object);
        await tools.ListSketchSegments();

        router.VerifyAll();
    }

    [Fact]
    public async Task GetSketchSegmentInfo_PassesSegmentIdToRouter()
    {
        var router = CreateRouter("Sketch.GetSketchSegmentInfo", p =>
            (int)p["SegmentId"]! == 42);

        var tools = new SketchInspectionTools(router.Object);
        await tools.GetSketchSegmentInfo(42);

        router.VerifyAll();
    }

    [Fact]
    public async Task AnalyzeSketch_DefaultParams_PassesStandardFieldsAndDefaultFlagsToRouter()
    {
        var router = CreateRouter("Sketch.AnalyzeSketch", p =>
            (string)p["Fields"]! == "standard" &&
            (bool)p["IncludePoints"]! == true &&
            (bool)p["IncludeSegments"]! == true &&
            (bool)p["IncludeRelations"]! == true &&
            (bool)p["IncludeDimensions"]! == true &&
            (bool)p["CalculateStatistics"]! == false &&
            p["OutputPath"] == null);

        var tools = new SketchInspectionTools(router.Object);
        await tools.AnalyzeSketch();

        router.VerifyAll();
    }

    [Fact]
    public async Task AnalyzeSketch_FullFields_PassesConnectivityAndOutputPathToRouter()
    {
        var router = CreateRouter("Sketch.AnalyzeSketch", p =>
            (string)p["Fields"]! == "full" &&
            (bool)p["CalculateStatistics"]! == true &&
            (bool)p["IncludeConnectivity"]! == true &&
            (double)p["GapToleranceMm"]! == 0.05 &&
            (string)p["OutputPath"]! == @"C:\out\sketch.json");

        var tools = new SketchInspectionTools(router.Object);
        await tools.AnalyzeSketch(
            fields: "full",
            calculateStatistics: true,
            includeConnectivity: true,
            gapToleranceMm: 0.05,
            outputPath: @"C:\out\sketch.json");

        router.VerifyAll();
    }
}
