using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.SketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchAdvanced;

internal sealed class SketchCornerOperations : OperationHandlerBase
{
    public SketchCornerOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchCornerOperations> logger)
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
            "Sketch.SketchFillet" => SketchFilletAsync(parameters),
            "Sketch.SketchChamfer" => SketchChamferAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch corner operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SketchFilletAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var model, out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var radius = MmToMeters(GetDoubleParam(parameters, "Radius", 5.0));
        var constrainedCorners = GetIntParam(parameters, "ConstrainedCorners", 2);

        var selectionManager = model!.SelectionManager as SelectionMgr;
        if ((selectionManager?.GetSelectedObjectCount2(-1) ?? 0) == 0)
        {
            _logger.LogWarning("No corner point selected - fillet requires pre-selected corner");
            return Task.FromResult(ExecutionResult.Failure("Corner point must be pre-selected (use Selection operations)"));
        }

        var filletSegment = sketchManager!.CreateFillet(radius, constrainedCorners) as SwSketchSegment;
        model.ClearSelection2(true);

        if (filletSegment == null)
        {
            _logger.LogWarning("CreateFillet returned null - verify corner selection and radius");
            return Task.FromResult(ExecutionResult.Failure("Failed to create fillet - check corner selection and radius"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Fillet created successfully",
            ["radius_mm"] = radius * 1000,
            ["constrainedAction"] = constrainedCorners
        }));
    }

    private Task<ExecutionResult> SketchChamferAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var model, out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var type = GetIntParam(parameters, "Type", 2);
        var distance = MmToMeters(GetDoubleParam(parameters, "Distance", 5.0));
        var angleOrDistance = type switch
        {
            0 => DegreesToRadians(GetDoubleParam(parameters, "Angle", 45.0)),
            1 => MmToMeters(GetDoubleParam(parameters, "Distance2", 5.0)),
            _ => 0.0
        };

        var selectionManager = model!.SelectionManager as SelectionMgr;
        if ((selectionManager?.GetSelectedObjectCount2(-1) ?? 0) == 0)
        {
            _logger.LogWarning("No corner point selected - chamfer requires pre-selected corner");
            return Task.FromResult(ExecutionResult.Failure("Corner point must be pre-selected (use Selection operations)"));
        }

        var chamferSegment = sketchManager!.CreateChamfer(type, distance, angleOrDistance) as SwSketchSegment;
        model.ClearSelection2(true);

        if (chamferSegment == null)
        {
            _logger.LogWarning("CreateChamfer returned null - verify corner selection and parameters");
            return Task.FromResult(ExecutionResult.Failure("Failed to create chamfer - check corner selection and parameters"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Chamfer created successfully",
            ["type"] = type,
            ["distance_mm"] = distance * 1000,
            ["angleOrDistance2"] = type == 0 ? RadiansToDegrees(angleOrDistance) : angleOrDistance * 1000
        }));
    }

    private bool TryGetActiveSketch(out ModelDoc2? model, out SketchManager? sketchManager, out string? errorMessage)
    {
        errorMessage = null;
        model = null;
        sketchManager = null;

        var app = _connection.Application;
        if (app == null)
        {
            errorMessage = "Not connected to SolidWorks";
            return false;
        }

        model = app.ActiveDoc as ModelDoc2;
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
