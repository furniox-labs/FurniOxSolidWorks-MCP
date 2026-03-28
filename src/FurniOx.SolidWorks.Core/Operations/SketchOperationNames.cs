using System.Collections.Generic;
using System.Linq;

namespace FurniOx.SolidWorks.Core.Operations;

public static class SketchOperationNames
{
    public const string CreateSketch = "Sketch.CreateSketch";
    public const string ExitSketch = "Sketch.ExitSketch";
    public const string EditSketch = "Sketch.EditSketch";
    public const string SketchCircle = "Sketch.SketchCircle";
    public const string SketchLine = "Sketch.SketchLine";
    public const string SketchCenterLine = "Sketch.SketchCenterLine";
    public const string SketchArc = "Sketch.SketchArc";
    public const string Sketch3PointArc = "Sketch.Sketch3PointArc";
    public const string SketchTangentArc = "Sketch.SketchTangentArc";
    public const string SketchCornerRectangle = "Sketch.SketchCornerRectangle";
    public const string SketchPoint = "Sketch.SketchPoint";
    public const string SketchEllipse = "Sketch.SketchEllipse";
    public const string SketchSpline = "Sketch.SketchSpline";
    public const string SketchPolygon = "Sketch.SketchPolygon";
    public const string ListSketchSegments = "Sketch.ListSketchSegments";
    public const string GetSketchSegmentInfo = "Sketch.GetSketchSegmentInfo";
    public const string AnalyzeSketch = "Sketch.AnalyzeSketch";
    public const string AddConstraint = "Sketch.AddConstraint";
    public const string AddDimension = "Sketch.AddDimension";
    public const string LinearPattern = "Sketch.LinearPattern";
    public const string CircularPattern = "Sketch.CircularPattern";
    public const string MirrorSketch = "Sketch.MirrorSketch";
    public const string OffsetEntities = "Sketch.OffsetEntities";
    public const string RotateSketch = "Sketch.RotateSketch";
    public const string ScaleSketch = "Sketch.ScaleSketch";
    public const string TrimEntity = "Sketch.TrimEntity";
    public const string ExtendEntity = "Sketch.ExtendEntity";
    public const string ConvertEntities = "Sketch.ConvertEntities";
    public const string SplitEntity = "Sketch.SplitEntity";
    public const string SketchFillet = "Sketch.SketchFillet";
    public const string SketchChamfer = "Sketch.SketchChamfer";
    public const string SketchSlot = "Sketch.SketchSlot";
    public const string Create3DSketch = "Sketch.Create3DSketch";
    public const string SketchParabola = "Sketch.SketchParabola";
    public const string SketchConic = "Sketch.SketchConic";
    public const string Sketch3DLine = "Sketch.Sketch3DLine";
    public const string Sketch3DSpline = "Sketch.Sketch3DSpline";
    public const string SketchSlotStraight = "Sketch.SketchSlot_Straight";
    public const string SketchSlotArc = "Sketch.SketchSlot_Arc";
    public const string SketchHexagon = "Sketch.SketchHexagon";
    public const string SketchBezier = "Sketch.SketchBezier";
    public const string InsertBlock = "Sketch.InsertBlock";
    public const string MakeBlock = "Sketch.MakeBlock";
    public const string ListConstraints = "Sketch.ListConstraints";
    public const string DisplayConstraints = "Sketch.DisplayConstraints";
    public const string DeleteConstraint = "Sketch.DeleteConstraint";
    public const string SketchText = "Sketch.SketchText";
    public const string ExplodeBlock = "Sketch.ExplodeBlock";
    public const string SaveBlock = "Sketch.SaveBlock";
    public const string SketchTextOnPath = "Sketch.SketchTextOnPath";
    public const string SketchSymbol = "Sketch.SketchSymbol";

    public static readonly IReadOnlyList<string> Geometry =
    [
        CreateSketch,
        ExitSketch,
        EditSketch,
        SketchCircle,
        SketchLine,
        SketchCenterLine,
        SketchArc,
        Sketch3PointArc,
        SketchTangentArc,
        SketchCornerRectangle,
        SketchPoint,
        SketchEllipse,
        SketchSpline,
        SketchPolygon
    ];

    public static readonly IReadOnlyList<string> Inspection =
    [
        ListSketchSegments,
        GetSketchSegmentInfo,
        AnalyzeSketch
    ];

    public static readonly IReadOnlyList<string> Parametric =
    [
        AddConstraint,
        AddDimension
    ];

    public static readonly IReadOnlyList<string> Productivity =
    [
        LinearPattern,
        CircularPattern,
        MirrorSketch,
        OffsetEntities,
        RotateSketch,
        ScaleSketch,
        TrimEntity,
        ExtendEntity,
        ConvertEntities,
        SplitEntity
    ];

    public static readonly IReadOnlyList<string> Advanced =
    [
        SketchFillet,
        SketchChamfer,
        SketchSlot,
        Create3DSketch,
        SketchParabola,
        SketchConic,
        Sketch3DLine,
        Sketch3DSpline,
        SketchSlotStraight,
        SketchSlotArc,
        SketchHexagon,
        SketchBezier
    ];

    public static readonly IReadOnlyList<string> Specialized =
    [
        InsertBlock,
        MakeBlock,
        ListConstraints,
        DisplayConstraints,
        DeleteConstraint,
        SketchText,
        ExplodeBlock,
        SaveBlock,
        SketchTextOnPath,
        SketchSymbol
    ];

    public static readonly IReadOnlyList<string> All =
        Geometry.Concat(Inspection).Concat(Parametric).Concat(Productivity).Concat(Advanced).Concat(Specialized).ToArray();
}
