using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class ToolRoutingContractTests
{
    [Fact]
    public async Task AssemblyBrowserTools_ListAssemblyComponents_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("AssemblyBrowser.ListAssemblyComponents", parameters =>
            (bool)parameters["TopLevelOnly"]! == true &&
            (bool)parameters["IncludePaths"]! == false);

        var tools = new AssemblyBrowserTools(router.Object);
        await tools.ListAssemblyComponents(topLevelOnly: true, includePaths: false);

        router.VerifyAll();
    }

    [Fact]
    public async Task FeatureExtrusionTools_CreateExtrusion_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("Feature.CreateExtrusion", parameters =>
            (double)parameters["Depth"]! == 12 &&
            (double)parameters["Depth2"]! == 6 &&
            (bool)parameters["SingleDirection"]! == false);

        var tools = new FeatureExtrusionTools(router.Object);
        await tools.CreateExtrusion(depth: 12, singleDirection: false, depth2: 6);

        router.VerifyAll();
    }

    [Fact]
    public async Task FeatureRevolveTools_CreateRevolve_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("Feature.CreateRevolve", parameters =>
            (string?)parameters["AxisEntity"] == "Axis1" &&
            (double)parameters["Angle1"]! == 180);

        var tools = new FeatureRevolveTools(router.Object);
        await tools.CreateRevolve(axisEntity: "Axis1", angle1: 180);

        router.VerifyAll();
    }

    [Fact]
    public async Task FeatureFilletTools_CreateFillet_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("Feature.CreateFillet", parameters =>
            (double)parameters["Radius"]! == 4.5);

        var tools = new FeatureFilletTools(router.Object);
        await tools.CreateFillet(radius: 4.5, edgeNames: ["Edge1@Part1"]);

        router.VerifyAll();
    }

    [Fact]
    public async Task FeatureShellTools_CreateShell_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("Feature.CreateShell", parameters =>
            (double)parameters["Thickness"]! == 2.5 &&
            (int)parameters["Direction"]! == 1);

        var tools = new FeatureShellTools(router.Object);
        await tools.CreateShell(thickness: 2.5, direction: 1, faceNames: ["Face1@Part1"]);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchGeometryTools_EditSketch_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("Sketch.EditSketch", parameters =>
            (string)parameters["SketchName"]! == "Sketch7" &&
            (bool)parameters["UseSelected"]! == false);

        var tools = new SketchGeometryTools(router.Object);
        await tools.EditSketch("Sketch7", false);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchInspectionTools_AnalyzeSketch_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("Sketch.AnalyzeSketch", parameters =>
            (string)parameters["Fields"]! == "full" &&
            (bool)parameters["IncludeConnectivity"]! == true);

        var tools = new SketchInspectionTools(router.Object);
        await tools.AnalyzeSketch(fields: "full", includeConnectivity: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchParametricTools_AddDimension_PreservesOptionalValueKey()
    {
        var router = CreateRouter("Sketch.AddDimension", parameters =>
            (string)parameters["DimensionType"]! == "distance" &&
            (double)parameters["Value"]! == 25);

        var tools = new SketchParametricTools(router.Object);
        await tools.AddDimension("distance", [1, 2], 10, 20, value: 25);

        router.VerifyAll();
    }

    [Fact]
    public async Task SketchSpecializedTools_InsertBlock_RoutesToSameOperationAndKeys()
    {
        var router = CreateRouter("Sketch.InsertBlock", parameters =>
            (string)parameters["FilePath"]! == "C:/blocks/a.sldblk" &&
            (double)parameters["Scale"]! == 2);

        var tools = new SketchSpecializedTools(router.Object);
        await tools.InsertBlock("C:/blocks/a.sldblk", 1, 2, scale: 2);

        router.VerifyAll();
    }

    [Fact]
    public void AnalyzeSketch_HasMcpServerToolAttribute()
    {
        Assert.NotNull(GetToolAttribute<SketchInspectionTools>(nameof(SketchInspectionTools.AnalyzeSketch)));
    }

    [Fact]
    public void ListAssemblyComponents_HasMcpServerToolAttribute()
    {
        Assert.NotNull(GetToolAttribute<AssemblyBrowserTools>(nameof(AssemblyBrowserTools.ListAssemblyComponents)));
    }

    private static Mock<ISmartRouter> CreateRouter(string operation, Func<IDictionary<string, object?>, bool> predicate)
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                operation,
                It.Is<IDictionary<string, object?>>(parameters => predicate(parameters)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Ok = true }));
        return router;
    }

    private static McpServerToolAttribute GetToolAttribute<TTool>(string methodName)
    {
        var method = typeof(TTool).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        return method!.GetCustomAttributes(typeof(McpServerToolAttribute), false)
            .Cast<McpServerToolAttribute>()
            .Single();
    }
}
