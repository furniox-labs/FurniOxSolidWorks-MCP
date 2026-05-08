using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class SelectionToolsTests
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
    public async Task SelectComponent_PassesNameAppendAndMarkToRouter()
    {
        var router = CreateRouter("Selection.SelectComponent", p =>
            (string)p["Name"]! == "Part1-1" &&
            (bool)p["Append"]! == false &&
            (int)p["Mark"]! == 0);

        var tools = new SelectionTools(router.Object);
        await tools.SelectComponent("Part1-1");

        router.VerifyAll();
    }

    [Fact]
    public async Task SelectByID2_PassesAllCoordinatesAndTypeToRouter()
    {
        var router = CreateRouter("Selection.SelectByID2", p =>
            (string)p["Name"]! == "Edge<1>" &&
            (string)p["Type"]! == "EDGE" &&
            (double)p["X"]! == 0.1 &&
            (double)p["Y"]! == 0.2 &&
            (double)p["Z"]! == 0.3 &&
            (bool)p["Append"]! == false &&
            (int)p["Mark"]! == 0 &&
            (int)p["SelectOption"]! == 0);

        var tools = new SelectionTools(router.Object);
        await tools.SelectByID2("Edge<1>", "EDGE", x: 0.1, y: 0.2, z: 0.3);

        router.VerifyAll();
    }

    [Fact]
    public async Task ClearSelection2_RoutesToCorrectOperation()
    {
        var router = CreateRouterAny("Selection.ClearSelection2");
        var tools = new SelectionTools(router.Object);

        await tools.ClearSelection2();

        router.VerifyAll();
    }
}
