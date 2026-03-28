using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.SketchProductivity;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Dispatches sketch productivity operations to focused handlers.
/// </summary>
public class SketchProductivityOperations : OperationHandlerBase
{
    private readonly SketchPatternOperations _patternOperations;
    private readonly SketchTransformOperations _transformOperations;
    private readonly SketchEditingOperations _editingOperations;

    internal SketchProductivityOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchProductivityOperations> logger,
        SketchPatternOperations patternOperations,
        SketchTransformOperations transformOperations,
        SketchEditingOperations editingOperations)
        : base(connection, settings, logger)
    {
        _patternOperations = patternOperations;
        _transformOperations = transformOperations;
        _editingOperations = editingOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.LinearPattern" or "Sketch.CircularPattern"
                => _patternOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.MirrorSketch" or "Sketch.RotateSketch" or "Sketch.OffsetEntities"
                => _transformOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.TrimEntity" or "Sketch.ExtendEntity" or "Sketch.ConvertEntities" or "Sketch.SplitEntity"
                => _editingOperations.ExecuteAsync(operation, parameters, cancellationToken),

            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch productivity operation: {operation}"))
        };
    }
}
