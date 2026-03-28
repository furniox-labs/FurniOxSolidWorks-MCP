using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.SketchProductivity;

internal sealed class SketchPatternOperations : OperationHandlerBase
{
    public SketchPatternOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchPatternOperations> logger)
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
            "Sketch.LinearPattern" => LinearPatternAsync(parameters),
            "Sketch.CircularPattern" => CircularPatternAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch pattern operation: {operation}"))
        };
    }

    private Task<ExecutionResult> LinearPatternAsync(IDictionary<string, object?> parameters)
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
        var numX = GetIntParam(parameters, "NumX", 2);
        var numY = GetIntParam(parameters, "NumY", 1);
        var spaceX = MmToMeters(GetDoubleParam(parameters, "SpaceX", 10.0));
        var spaceY = MmToMeters(GetDoubleParam(parameters, "SpaceY", 10.0));
        var angleX = DegreesToRadians(GetDoubleParam(parameters, "AngleX", 0.0));
        var angleY = DegreesToRadians(GetDoubleParam(parameters, "AngleY", 90.0));
        var patternGeometry = GetBoolParam(parameters, "PatternGeometry", true);
        var patternConstraints = GetBoolParam(parameters, "PatternConstraints", true);
        var angleDim = GetBoolParam(parameters, "AngleDim", false);
        var dimX = GetBoolParam(parameters, "DimX", false);
        var dimY = GetBoolParam(parameters, "DimY", false);

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

        var success = sketchManager!.CreateLinearSketchStepAndRepeat(
            numX,
            numY,
            spaceX,
            spaceY,
            angleX,
            angleY,
            string.Empty,
            patternGeometry,
            patternConstraints,
            angleDim,
            dimX,
            dimY);

        model.ClearSelection2(true);

        if (!success)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create linear pattern"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            EntitiesSelected = selectedCount,
            NumX = numX,
            NumY = numY,
            SpaceX = MetersToMm(spaceX),
            SpaceY = MetersToMm(spaceY),
            AngleX = RadiansToDegrees(angleX),
            AngleY = RadiansToDegrees(angleY)
        }));
    }

    private Task<ExecutionResult> CircularPatternAsync(IDictionary<string, object?> parameters)
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
        var arcRadius = MmToMeters(GetDoubleParam(parameters, "ArcRadius", 50.0));
        var arcAngle = DegreesToRadians(GetDoubleParam(parameters, "ArcAngle", 0.0));
        var patternNum = GetIntParam(parameters, "PatternNum", 6);
        var patternSpacing = DegreesToRadians(GetDoubleParam(parameters, "PatternSpacing", 60.0));
        var patternRotate = GetBoolParam(parameters, "PatternRotate", true);
        var deleteInstances = GetStringParam(parameters, "DeleteInstances", string.Empty);
        var radiusDim = GetBoolParam(parameters, "RadiusDim", false);
        var angleDim = GetBoolParam(parameters, "AngleDim", false);
        var createNumOfInstancesDim = GetBoolParam(parameters, "CreateNumOfInstancesDim", false);

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

        var success = sketchManager!.CreateCircularSketchStepAndRepeat(
            arcRadius,
            arcAngle,
            patternNum,
            patternSpacing,
            patternRotate,
            deleteInstances,
            radiusDim,
            angleDim,
            createNumOfInstancesDim);

        model.ClearSelection2(true);

        if (!success)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create circular pattern"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Success = true,
            EntitiesSelected = selectedCount,
            PatternNum = patternNum,
            ArcRadius = MetersToMm(arcRadius),
            ArcAngle = RadiansToDegrees(arcAngle),
            PatternSpacing = RadiansToDegrees(patternSpacing),
            PatternRotate = patternRotate
        }));
    }
}
