using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class SketchSpecializedToolsTests
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
    public async Task DeleteConstraint_PassesConstraintIndexToRouter()
    {
        var router = CreateRouter("Sketch.DeleteConstraint", p =>
            (int)p["ConstraintIndex"]! == 3);

        var tools = new SketchSpecializedTools(router.Object);
        await tools.DeleteConstraint(3);

        router.VerifyAll();
    }

    [Fact]
    public async Task DisplayConstraints_Show_PassesTrueToRouter()
    {
        var router = CreateRouter("Sketch.DisplayConstraints", p =>
            (bool)p["Show"]! == true);

        var tools = new SketchSpecializedTools(router.Object);
        await tools.DisplayConstraints(true);

        router.VerifyAll();
    }

    [Fact]
    public async Task InsertBlock_PassesAllParamsToRouter()
    {
        var router = CreateRouter("Sketch.InsertBlock", p =>
            (string)p["FilePath"]! == @"C:\blocks\table.sldblk" &&
            (double)p["X"]! == 5 &&
            (double)p["Y"]! == 10 &&
            (double)p["Scale"]! == 2.0 &&
            (double)p["Angle"]! == 45);

        var tools = new SketchSpecializedTools(router.Object);
        await tools.InsertBlock(@"C:\blocks\table.sldblk", 5, 10, scale: 2.0, angle: 45);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchText_PassesAllTextParamsToRouter()
    {
        var router = CreateRouter("Sketch.SketchText", p =>
            (string)p["Text"]! == "Hello" &&
            (double)p["CharHeight"]! == 8 &&
            (double)p["CharWidth"]! == 6 &&
            (double)p["Angle"]! == 0 &&
            (string)p["FontName"]! == "Arial" &&
            (bool)p["FlipX"]! == false &&
            (bool)p["FlipY"]! == false &&
            (double)p["ObliqAngle"]! == 0);

        var tools = new SketchSpecializedTools(router.Object);
        await tools.SketchText("Hello", 8, 6, 0, "Arial");

        router.VerifyAll();
    }
}
