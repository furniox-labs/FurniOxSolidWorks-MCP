using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class SortingToolsTests
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
    public async Task ReorderByPositions_DefaultParams_PassesPositionsDryRunAndPreserveFoldersToRouter()
    {
        var positions = "[{\"name\":\"Part1-1\",\"position\":1}]";

        var router = CreateRouter("Sorting.ReorderByPositions", p =>
            (string)p["Positions"]! == positions &&
            (bool)p["DryRun"]! == false &&
            (bool)p["PreserveFolders"]! == true);

        var tools = new SortingTools(router.Object);
        await tools.ReorderByPositions(positions);

        router.VerifyAll();
    }

    [Fact]
    public async Task ReorderByPositions_DryRun_PassesDryRunTrueToRouter()
    {
        var router = CreateRouter("Sorting.ReorderByPositions", p =>
            (bool)p["DryRun"]! == true);

        var tools = new SortingTools(router.Object);
        await tools.ReorderByPositions("[]", dryRun: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task ReorderFeaturesByPositions_PassesPositionsAndFeatureTypeToRouter()
    {
        var positions = "[{\"name\":\"Boss-Extrude1\",\"position\":2}]";

        var router = CreateRouter("Sorting.ReorderFeaturesByPositions", p =>
            (string)p["Positions"]! == positions &&
            (string)p["FeatureType"]! == "MacroFeature" &&
            (bool)p["PreserveFolders"]! == true &&
            (bool)p["DryRun"]! == false);

        var tools = new SortingTools(router.Object);
        await tools.ReorderFeaturesByPositions(positions, featureType: "MacroFeature");

        router.VerifyAll();
    }

    [Fact]
    public async Task ListComponentFolders_RoutesToCorrectOperation()
    {
        var router = CreateRouterAny("Sorting.ListComponentFolders");
        var tools = new SortingTools(router.Object);

        await tools.ListComponentFolders();

        router.VerifyAll();
    }
}
