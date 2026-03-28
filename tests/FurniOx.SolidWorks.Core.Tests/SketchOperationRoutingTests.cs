using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class SketchOperationRoutingTests : RoutingTestBase
{
    public static IEnumerable<object[]> SketchGeometryOperations => ToOperationData(
        "Sketch.CreateSketch",
        "Sketch.ExitSketch",
        "Sketch.EditSketch",
        "Sketch.SketchCircle",
        "Sketch.SketchLine",
        "Sketch.SketchCenterLine",
        "Sketch.SketchArc",
        "Sketch.Sketch3PointArc",
        "Sketch.SketchTangentArc",
        "Sketch.SketchCornerRectangle",
        "Sketch.SketchPoint",
        "Sketch.SketchEllipse",
        "Sketch.SketchSpline",
        "Sketch.SketchPolygon");

    public static IEnumerable<object[]> SketchInspectionOperations => ToOperationData(
        "Sketch.ListSketchSegments",
        "Sketch.GetSketchSegmentInfo",
        "Sketch.AnalyzeSketch");

    public static IEnumerable<object[]> SketchParametricOperations => ToOperationData(
        "Sketch.AddConstraint",
        "Sketch.AddDimension");

    public static IEnumerable<object[]> SketchProductivityOperations => ToOperationData(
        "Sketch.LinearPattern",
        "Sketch.CircularPattern",
        "Sketch.MirrorSketch",
        "Sketch.OffsetEntities",
        "Sketch.RotateSketch",
        "Sketch.ScaleSketch",
        "Sketch.TrimEntity",
        "Sketch.ExtendEntity",
        "Sketch.ConvertEntities",
        "Sketch.SplitEntity");

    public static IEnumerable<object[]> SketchAdvancedOperations => ToOperationData(
        "Sketch.SketchFillet",
        "Sketch.SketchChamfer",
        "Sketch.SketchSlot",
        "Sketch.Create3DSketch");

    public static IEnumerable<object[]> SketchSpecializedOperations => ToOperationData(
        "Sketch.InsertBlock",
        "Sketch.MakeBlock",
        "Sketch.ListConstraints",
        "Sketch.DisplayConstraints",
        "Sketch.DeleteConstraint",
        "Sketch.SketchText",
        "Sketch.ExplodeBlock");

    [Theory]
    [MemberData(nameof(SketchGeometryOperations))]
    [MemberData(nameof(SketchInspectionOperations))]
    [MemberData(nameof(SketchParametricOperations))]
    [MemberData(nameof(SketchProductivityOperations))]
    [MemberData(nameof(SketchAdvancedOperations))]
    [MemberData(nameof(SketchSpecializedOperations))]
    public async Task SketchOperations_RouteSuccessfully(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success, $"Expected routing to succeed for '{operation}'.");
        Assert.Equal(operation, adapter.LastOperation);
    }

    [Fact]
    public async Task UnknownSketchOperation_ReturnsFailure()
    {
        var router = CreateRouter(new RejectingAdapter());

        var result = await router.RouteAsync("Sketch.UnknownOperation", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    private static IEnumerable<object[]> ToOperationData(params string[] operations)
    {
        foreach (var operation in operations)
        {
            yield return new object[] { operation };
        }
    }
}
