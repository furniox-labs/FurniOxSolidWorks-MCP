using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.SketchProductivity;

internal sealed class SketchEditingOperations : OperationHandlerBase
{
    public SketchEditingOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchEditingOperations> logger)
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
            "Sketch.TrimEntity" => TrimEntityAsync(parameters),
            "Sketch.ExtendEntity" => ExtendEntityAsync(parameters),
            "Sketch.ConvertEntities" => ConvertEntitiesAsync(parameters),
            "Sketch.SplitEntity" => SplitEntityAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch editing operation: {operation}"))
        };
    }

    private Task<ExecutionResult> TrimEntityAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSegmentSelectionSupport.TryGetActiveSketch(_connection, out var model, out var sketchManager, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        if (!parameters.TryGetValue("EntityIds", out var entityIdsValue) || entityIdsValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("EntityIds parameter is required"));
        }

        var trimOption = GetIntParam(parameters, "TrimOption", 0);
        var x = MmToMeters(GetDoubleParam(parameters, "X", 0.0));
        var y = MmToMeters(GetDoubleParam(parameters, "Y", 0.0));
        var z = MmToMeters(GetDoubleParam(parameters, "Z", 0.0));
        var entityIds = SketchSegmentSelectionSupport.ParseEntityIds(entityIdsValue);

        model!.ClearSelection2(true);
        if (!SketchSegmentSelectionSupport.TryGetSegments(activeSketch!, out var segments, out errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to read sketch segments"));
        }

        if (!SketchSegmentSelectionSupport.TrySelectSegments(model, segments!, entityIds, out var selectedCount, out errorMessage))
        {
            model.ClearSelection2(true);
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to select sketch entities"));
        }

        sketchManager!.SketchTrim(trimOption, x, y, z);
        model.ClearSelection2(true);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            EntitiesTrimmed = selectedCount,
            TrimOption = trimOption,
            X = MetersToMm(x),
            Y = MetersToMm(y),
            Z = MetersToMm(z)
        }));
    }

    private Task<ExecutionResult> ExtendEntityAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSegmentSelectionSupport.TryGetActiveSketch(_connection, out var model, out var sketchManager, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        if (!parameters.TryGetValue("EntityIds", out var entityIdsValue) || entityIdsValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("EntityIds parameter is required"));
        }

        var entityIds = SketchSegmentSelectionSupport.ParseEntityIds(entityIdsValue);

        model!.ClearSelection2(true);
        if (!SketchSegmentSelectionSupport.TryGetSegments(activeSketch!, out var segments, out errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to read sketch segments"));
        }

        if (!SketchSegmentSelectionSupport.TrySelectSegments(model, segments!, entityIds, out var selectedCount, out errorMessage))
        {
            model.ClearSelection2(true);
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to select sketch entities"));
        }

        var success = sketchManager!.SketchExtend(0.0, 0.0, 0.0);
        model.ClearSelection2(true);

        if (!success)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to extend entity"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            EntitiesExtended = selectedCount
        }));
    }

    private Task<ExecutionResult> ConvertEntitiesAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSegmentSelectionSupport.TryGetActiveSketch(_connection, out var model, out var sketchManager, out _, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var useEqualCurvature = GetBoolParam(parameters, "UseEqualCurvature", false);
        var useSilhouetteEdges = GetBoolParam(parameters, "UseSilhouetteEdges", false);
        var success = sketchManager!.SketchUseEdge3(useEqualCurvature, useSilhouetteEdges);
        model!.ClearSelection2(true);

        if (!success)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to convert entities. Ensure model edges are selected."));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            UseEqualCurvature = useEqualCurvature,
            UseSilhouetteEdges = useSilhouetteEdges
        }));
    }

    private Task<ExecutionResult> SplitEntityAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSegmentSelectionSupport.TryGetActiveSketch(_connection, out _, out _, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        if (!parameters.TryGetValue("EntityId", out var entityIdValue) || entityIdValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("EntityId parameter is required"));
        }

        var entityId = Convert.ToInt32(entityIdValue);
        var x = MmToMeters(GetDoubleParam(parameters, "X", 0.0));
        var y = MmToMeters(GetDoubleParam(parameters, "Y", 0.0));
        var z = MmToMeters(GetDoubleParam(parameters, "Z", 0.0));
        var closedX = MmToMeters(GetDoubleParam(parameters, "ClosedX", 0.0));
        var closedY = MmToMeters(GetDoubleParam(parameters, "ClosedY", 0.0));
        var closedZ = MmToMeters(GetDoubleParam(parameters, "ClosedZ", 0.0));

        if (!SketchSegmentSelectionSupport.TryGetSegments(activeSketch!, out var segments, out errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to read sketch segments"));
        }

        var targetSegment = SketchSegmentSelectionSupport.FindSegment(segments!, entityId);
        if (targetSegment == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Entity ID {entityId} not found"));
        }

        targetSegment.SplitEntity(x, y, z, closedX, closedY, closedZ);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            OriginalEntityId = entityId,
            SplitX = MetersToMm(x),
            SplitY = MetersToMm(y),
            SplitZ = MetersToMm(z),
            Note = "Entity split completed (method returns void, cannot verify new segment ID)"
        }));
    }
}
