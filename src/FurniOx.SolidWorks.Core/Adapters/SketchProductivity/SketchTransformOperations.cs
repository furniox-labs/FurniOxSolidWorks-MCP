using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.SketchProductivity;

internal sealed class SketchTransformOperations : OperationHandlerBase
{
    public SketchTransformOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchTransformOperations> logger)
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
            "Sketch.MirrorSketch" => MirrorSketchAsync(parameters),
            "Sketch.RotateSketch" => RotateSketchAsync(parameters),
            "Sketch.OffsetEntities" => OffsetEntitiesAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch transform operation: {operation}"))
        };
    }

    private Task<ExecutionResult> MirrorSketchAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSegmentSelectionSupport.TryGetActiveSketch(_connection, out var model, out _, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        if (!parameters.TryGetValue("EntityIds", out var entityIdsValue) || entityIdsValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("EntityIds parameter is required"));
        }

        var mirrorLineId = GetIntParam(parameters, "MirrorLineId", -1);
        if (mirrorLineId < 0)
        {
            return Task.FromResult(ExecutionResult.Failure("MirrorLineId parameter is required"));
        }

        var entityIds = SketchSegmentSelectionSupport.ParseEntityIds(entityIdsValue);

        model!.ClearSelection2(true);
        if (!SketchSegmentSelectionSupport.TryGetSegments(activeSketch!, out var segments, out errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to read sketch segments"));
        }

        var mirrorLine = SketchSegmentSelectionSupport.FindSegment(segments!, mirrorLineId);
        if (mirrorLine == null)
        {
            model.ClearSelection2(true);
            return Task.FromResult(ExecutionResult.Failure($"Mirror line ID {mirrorLineId} not found"));
        }

        if (!mirrorLine.Select4(false, null))
        {
            model.ClearSelection2(true);
            return Task.FromResult(ExecutionResult.Failure("Failed to select mirror line"));
        }

        model.Extension.SelectByID2(
            mirrorLine.GetName(),
            "SKETCHSEGMENT",
            0,
            0,
            0,
            false,
            2,
            null,
            0);

        var selectedCount = 0;
        var segmentSet = segments!;
        foreach (var entityId in entityIds)
        {
            var targetSegment = SketchSegmentSelectionSupport.FindSegment(segmentSet, entityId);
            if (targetSegment == null)
            {
                model.ClearSelection2(true);
                return Task.FromResult(ExecutionResult.Failure($"Entity ID {entityId} not found"));
            }

            model.Extension.SelectByID2(
                targetSegment.GetName(),
                "SKETCHSEGMENT",
                0,
                0,
                0,
                true,
                1,
                null,
                0);

            selectedCount++;
        }

        model.SketchMirror();
        model.ClearSelection2(true);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            EntitiesMirrored = selectedCount,
            MirrorLineId = mirrorLineId
        }));
    }

    private Task<ExecutionResult> RotateSketchAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSegmentSelectionSupport.TryGetActiveSketch(_connection, out var model, out _, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        if (!parameters.TryGetValue("EntityIds", out var entityIdsValue) || entityIdsValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("EntityIds parameter is required"));
        }

        var entityIds = SketchSegmentSelectionSupport.ParseEntityIds(entityIdsValue);
        var copy = GetBoolParam(parameters, "Copy", false);
        var numCopies = GetIntParam(parameters, "NumCopies", 1);
        var keepRelations = GetBoolParam(parameters, "KeepRelations", true);
        var baseX = MmToMeters(GetDoubleParam(parameters, "BaseX", 0.0));
        var baseY = MmToMeters(GetDoubleParam(parameters, "BaseY", 0.0));
        var baseZ = MmToMeters(GetDoubleParam(parameters, "BaseZ", 0.0));
        var destX = GetDoubleParam(parameters, "DestX", 0.0);
        var destY = GetDoubleParam(parameters, "DestY", 0.0);
        var destZ = GetDoubleParam(parameters, "DestZ", 1.0);
        var angle = DegreesToRadians(GetDoubleParam(parameters, "Angle", 45.0));

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

        model.Extension.RotateOrCopy(
            copy,
            numCopies,
            keepRelations,
            baseX,
            baseY,
            baseZ,
            destX,
            destY,
            destZ,
            angle);

        model.ClearSelection2(true);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            EntitiesRotated = selectedCount,
            Copy = copy,
            NumCopies = numCopies,
            Angle = RadiansToDegrees(angle),
            BaseX = MetersToMm(baseX),
            BaseY = MetersToMm(baseY),
            BaseZ = MetersToMm(baseZ)
        }));
    }

    private Task<ExecutionResult> OffsetEntitiesAsync(IDictionary<string, object?> parameters)
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
        var offset = MmToMeters(GetDoubleParam(parameters, "Offset", 5.0));
        var bothDirections = GetBoolParam(parameters, "BothDirections", false);
        var chain = GetBoolParam(parameters, "Chain", true);
        var capEnds = GetIntParam(parameters, "CapEnds", 0);
        var makeConstruction = GetIntParam(parameters, "MakeConstruction", 0);
        var addDimensions = GetBoolParam(parameters, "AddDimensions", false);

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

        var success = sketchManager!.SketchOffset2(
            offset,
            bothDirections,
            chain,
            capEnds,
            makeConstruction,
            addDimensions);

        model.ClearSelection2(true);

        if (!success)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create offset"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            EntitiesOffset = selectedCount,
            Offset = MetersToMm(offset),
            BothDirections = bothDirections,
            Chain = chain,
            CapEnds = capEnds
        }));
    }
}
