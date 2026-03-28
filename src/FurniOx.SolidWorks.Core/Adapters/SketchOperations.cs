using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Main coordinator for sketch operations (49 operations total)
/// Routes operations to specialized handlers:
/// - SketchGeometryOperations: Basic geometry creation (13 ops) - Phase 0
/// - SketchInspectionOperations: Entity discovery (2 ops) - Phase 1
/// - SketchParametricOperations: Constraints and dimensions (2 ops) - Phase 1
/// - SketchProductivityOperations: Patterns, transformations, trim/extend (10 ops) - Phase 2
/// - SketchAdvancedOperations: Advanced curves, slots, 3D sketch support (12 ops) - Phase 3
/// - SketchSpecializedOperations: Text, blocks, constraint management (10 ops) - Phase 4
/// </summary>
public class SketchOperations : OperationHandlerBase
{
    private readonly SketchGeometryOperations _geometryOps;
    private readonly SketchInspectionOperations _inspectionOps;
    private readonly SketchParametricOperations _parametricOps;
    private readonly SketchProductivityOperations _productivityOps;
    private readonly SketchAdvancedOperations _advancedOps;
    private readonly SketchSpecializedOperations _specializedOps;

    public SketchOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchOperations> logger,
        SketchGeometryOperations geometryOps,
        SketchInspectionOperations inspectionOps,
        SketchParametricOperations parametricOps,
        SketchProductivityOperations productivityOps,
        SketchAdvancedOperations advancedOps,
        SketchSpecializedOperations specializedOps)
        : base(connection, settings, logger)
    {
        _geometryOps = geometryOps;
        _inspectionOps = inspectionOps;
        _parametricOps = parametricOps;
        _productivityOps = productivityOps;
        _advancedOps = advancedOps;
        _specializedOps = specializedOps;
    }

    public override async Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        // Route to appropriate handler based on operation name
        return operation switch
        {
            // Geometry operations (14)
            "Sketch.CreateSketch" or
            "Sketch.ExitSketch" or
            "Sketch.EditSketch" or
            "Sketch.SketchCircle" or
            "Sketch.SketchLine" or
            "Sketch.SketchCenterLine" or
            "Sketch.SketchArc" or
            "Sketch.Sketch3PointArc" or
            "Sketch.SketchTangentArc" or
            "Sketch.SketchCornerRectangle" or
            "Sketch.SketchPoint" or
            "Sketch.SketchEllipse" or
            "Sketch.SketchSpline" or
            "Sketch.SketchPolygon"
                => await _geometryOps.ExecuteAsync(operation, parameters, cancellationToken),

            // Inspection operations (3)
            "Sketch.ListSketchSegments" or
            "Sketch.GetSketchSegmentInfo" or
            "Sketch.AnalyzeSketch"
                => await _inspectionOps.ExecuteAsync(operation, parameters, cancellationToken),

            // Parametric operations (2)
            "Sketch.AddConstraint" or
            "Sketch.AddDimension"
                => await _parametricOps.ExecuteAsync(operation, parameters, cancellationToken),

            // Productivity operations (10) - Phase 2
            "Sketch.LinearPattern" or "Sketch.CircularPattern" or
            "Sketch.MirrorSketch" or "Sketch.OffsetEntities" or
            "Sketch.RotateSketch" or "Sketch.ScaleSketch" or
            "Sketch.TrimEntity" or "Sketch.ExtendEntity" or
            "Sketch.ConvertEntities" or "Sketch.SplitEntity"
                => await _productivityOps.ExecuteAsync(operation, parameters, cancellationToken),

            // Advanced operations (12) - Phase 3
            "Sketch.SketchFillet" or "Sketch.SketchChamfer" or
            "Sketch.SketchSlot" or "Sketch.Create3DSketch" or
            "Sketch.SketchParabola" or "Sketch.SketchConic" or
            "Sketch.Sketch3DLine" or "Sketch.Sketch3DSpline" or
            "Sketch.SketchSlot_Straight" or "Sketch.SketchSlot_Arc" or
            "Sketch.SketchHexagon" or "Sketch.SketchBezier"
                => await _advancedOps.ExecuteAsync(operation, parameters, cancellationToken),

            // Specialized operations (10) - Phase 4
            "Sketch.InsertBlock" or "Sketch.MakeBlock" or
            "Sketch.ListConstraints" or "Sketch.DisplayConstraints" or
            "Sketch.DeleteConstraint" or "Sketch.SketchText" or
            "Sketch.ExplodeBlock" or "Sketch.SaveBlock" or
            "Sketch.SketchTextOnPath" or "Sketch.SketchSymbol"
                => await _specializedOps.ExecuteAsync(operation, parameters, cancellationToken),

            // Unknown operation
            _ => ExecutionResult.Failure($"Unknown sketch operation: {operation}")
        };
    }
}
