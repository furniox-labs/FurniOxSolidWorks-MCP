using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchAdvanced;

internal sealed class SketchSlotOperations : OperationHandlerBase
{
    public SketchSlotOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchSlotOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.SketchSlot" => SketchSlotAsync(parameters),
            "Sketch.SketchSlot_Straight" => SketchSlotStraightAsync(parameters),
            "Sketch.SketchSlot_Arc" => SketchSlotArcAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch slot operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SketchSlotAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var slotCreationType = GetIntParam(parameters, "SlotCreationType", 0);
        var slotLengthType = GetIntParam(parameters, "SlotLengthType", 0);
        var width = MmToMeters(GetDoubleParam(parameters, "Width", 10.0));
        var x1 = MmToMeters(GetDoubleParam(parameters, "X1", 0.0));
        var y1 = MmToMeters(GetDoubleParam(parameters, "Y1", 0.0));
        var z1 = MmToMeters(GetDoubleParam(parameters, "Z1", 0.0));
        var x2 = MmToMeters(GetDoubleParam(parameters, "X2", 50.0));
        var y2 = MmToMeters(GetDoubleParam(parameters, "Y2", 0.0));
        var z2 = MmToMeters(GetDoubleParam(parameters, "Z2", 0.0));
        var x3 = MmToMeters(GetDoubleParam(parameters, "X3", 50.0));
        var y3 = MmToMeters(GetDoubleParam(parameters, "Y3", 50.0));
        var z3 = MmToMeters(GetDoubleParam(parameters, "Z3", 0.0));
        var centerArcDirection = GetIntParam(parameters, "CenterArcDirection", 1);
        var addDimension = GetBoolParam(parameters, "AddDimension", false);

        var slot = sketchManager!.CreateSketchSlot(
            slotCreationType,
            slotLengthType,
            width,
            x1,
            y1,
            z1,
            x2,
            y2,
            z2,
            x3,
            y3,
            z3,
            centerArcDirection,
            addDimension) as SketchSlot;

        if (slot == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create slot - check parameters"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Slot created successfully",
            ["slotType"] = slotCreationType,
            ["width_mm"] = width * 1000,
            ["lengthType"] = slotLengthType
        }));
    }

    private Task<ExecutionResult> SketchSlotStraightAsync(IDictionary<string, object?> parameters)
    {
        var modifiedParameters = new Dictionary<string, object?>(parameters)
        {
            ["SlotCreationType"] = 0
        };

        return SketchSlotAsync(modifiedParameters);
    }

    private Task<ExecutionResult> SketchSlotArcAsync(IDictionary<string, object?> parameters)
    {
        var arcType = GetIntParam(parameters, "ArcType", 2);
        if (arcType != 2 && arcType != 4)
        {
            return Task.FromResult(ExecutionResult.Failure("ArcType must be 2 (centerpoint arc) or 4 (3-point arc)"));
        }

        var modifiedParameters = new Dictionary<string, object?>(parameters)
        {
            ["SlotCreationType"] = arcType
        };

        return SketchSlotAsync(modifiedParameters);
    }

    private bool TryGetActiveSketch(out SketchManager? sketchManager, out string? errorMessage)
    {
        errorMessage = null;
        sketchManager = null;

        var app = _connection.Application;
        if (app == null)
        {
            errorMessage = "Not connected to SolidWorks";
            return false;
        }

        var model = app.ActiveDoc as ModelDoc2;
        if (model == null)
        {
            errorMessage = "No active document";
            return false;
        }

        sketchManager = model.SketchManager;
        if (sketchManager.ActiveSketch is not Sketch)
        {
            errorMessage = "No active sketch";
            return false;
        }

        return true;
    }
}
