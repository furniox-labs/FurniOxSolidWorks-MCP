using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.SketchAdvanced;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Dispatches advanced sketch operations to focused handlers.
/// </summary>
public class SketchAdvancedOperations : OperationHandlerBase
{
    private readonly SketchCornerOperations _cornerOperations;
    private readonly SketchSlotOperations _slotOperations;
    private readonly SketchThreeDimensionalOperations _threeDimensionalOperations;
    private readonly SketchCurveOperations _curveOperations;

    internal SketchAdvancedOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchAdvancedOperations> logger,
        SketchCornerOperations cornerOperations,
        SketchSlotOperations slotOperations,
        SketchThreeDimensionalOperations threeDimensionalOperations,
        SketchCurveOperations curveOperations)
        : base(connection, settings, logger)
    {
        _cornerOperations = cornerOperations;
        _slotOperations = slotOperations;
        _threeDimensionalOperations = threeDimensionalOperations;
        _curveOperations = curveOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.SketchFillet" or "Sketch.SketchChamfer"
                => _cornerOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.SketchSlot" or "Sketch.SketchSlot_Straight" or "Sketch.SketchSlot_Arc"
                => _slotOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.Create3DSketch" or "Sketch.Sketch3DLine" or "Sketch.Sketch3DSpline"
                => _threeDimensionalOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.SketchParabola" or "Sketch.SketchConic" or "Sketch.SketchHexagon" or "Sketch.SketchBezier"
                => _curveOperations.ExecuteAsync(operation, parameters, cancellationToken),

            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch advanced operation: {operation}"))
        };
    }
}
