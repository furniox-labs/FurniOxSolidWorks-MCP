using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.SketchGeometry;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Dispatches basic sketch geometry operations to focused handlers.
/// </summary>
public class SketchGeometryOperations : OperationHandlerBase
{
    private readonly SketchSessionGeometryOperations _sessionOperations;
    private readonly SketchPrimitiveGeometryOperations _primitiveOperations;
    private readonly SketchArcGeometryOperations _arcOperations;
    private readonly SketchShapeGeometryOperations _shapeOperations;

    internal SketchGeometryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchGeometryOperations> logger,
        SketchSessionGeometryOperations sessionOperations,
        SketchPrimitiveGeometryOperations primitiveOperations,
        SketchArcGeometryOperations arcOperations,
        SketchShapeGeometryOperations shapeOperations)
        : base(connection, settings, logger)
    {
        _sessionOperations = sessionOperations;
        _primitiveOperations = primitiveOperations;
        _arcOperations = arcOperations;
        _shapeOperations = shapeOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.CreateSketch" or "Sketch.EditSketch" or "Sketch.ExitSketch"
                => _sessionOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.SketchCircle" or "Sketch.SketchLine" or "Sketch.SketchCenterLine" or "Sketch.SketchPoint"
                => _primitiveOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.SketchArc" or "Sketch.Sketch3PointArc" or "Sketch.SketchTangentArc"
                => _arcOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.SketchCornerRectangle" or "Sketch.SketchEllipse" or "Sketch.SketchSpline" or "Sketch.SketchPolygon"
                => _shapeOperations.ExecuteAsync(operation, parameters, cancellationToken),

            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch geometry operation: {operation}"))
        };
    }
}
