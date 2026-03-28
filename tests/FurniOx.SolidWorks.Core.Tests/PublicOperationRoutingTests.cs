using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class PublicOperationRoutingTests : RoutingTestBase
{
    public static IEnumerable<object[]> DocumentOperations => ToOperationData(
        "Document.CreateDocument",
        "Document.OpenModel",
        "Document.SaveModel",
        "Document.CloseModel");

    public static IEnumerable<object[]> ExportOperations => ToOperationData(
        "Export.ExportToSTEP",
        "Export.ExportToSTL",
        "Export.ExportToPDF",
        "Export.ExportToDXF",
        "Export.ExportToIGES");

    public static IEnumerable<object[]> ConfigurationOperations => ToOperationData(
        "Configuration.GetConfigurationNames",
        "Configuration.ActivateConfiguration",
        "Configuration.AddConfiguration",
        "Configuration.DeleteConfiguration",
        "Configuration.CopyConfiguration",
        "Configuration.GetConfigurationCount",
        "Configuration.ShowConfiguration");

    public static IEnumerable<object[]> SelectionOperations => ToOperationData(
        "Selection.SelectByID2",
        "Selection.SelectComponent",
        "Selection.ClearSelection2",
        "Selection.DeleteSelection2");

    public static IEnumerable<object[]> FeatureOperations => ToOperationData(
        "Feature.CreateExtrusion",
        "Feature.CreateCutExtrusion",
        "Feature.CreateRevolve",
        "Feature.CreateFillet",
        "Feature.CreateShell");

    public static IEnumerable<object[]> SortingOperations => ToOperationData(
        "Sorting.ListComponentFolders",
        "Sorting.ReorderByPositions",
        "Sorting.ReorderFeaturesByPositions");

    public static IEnumerable<object[]> AssemblyBrowserOperations => ToOperationData(
        "AssemblyBrowser.ListAssemblyComponents");

    [Theory]
    [MemberData(nameof(DocumentOperations))]
    [MemberData(nameof(ExportOperations))]
    [MemberData(nameof(ConfigurationOperations))]
    [MemberData(nameof(SelectionOperations))]
    [MemberData(nameof(FeatureOperations))]
    [MemberData(nameof(SortingOperations))]
    [MemberData(nameof(AssemblyBrowserOperations))]
    public async Task PublicOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success, $"Expected routing to succeed for '{operation}'.");
        Assert.Equal(operation, adapter.LastOperation);
    }

    [Fact]
    public async Task RouteAsync_PassesThroughParameters_ToAdapter()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);
        var parameters = new Dictionary<string, object?>
        {
            ["PlaneId"] = "Front Plane",
            ["X"] = 10.0,
            ["Y"] = 20.0,
            ["Radius"] = 5.0
        };

        await router.RouteAsync("Sketch.SketchCircle", parameters);

        Assert.NotNull(adapter.LastParameters);
        Assert.Equal("Front Plane", adapter.LastParameters["PlaneId"]);
        Assert.Equal(10.0, adapter.LastParameters["X"]);
        Assert.Equal(20.0, adapter.LastParameters["Y"]);
        Assert.Equal(5.0, adapter.LastParameters["Radius"]);
    }

    [Fact]
    public async Task RouteAsync_EmptyParameters_RouteSucceeds()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync("Sketch.ExitSketch", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal("Sketch.ExitSketch", adapter.LastOperation);
    }

    private static IEnumerable<object[]> ToOperationData(params string[] operations)
    {
        foreach (var operation in operations)
        {
            yield return new object[] { operation };
        }
    }
}
