using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Features;

public sealed class FeatureRevolveOperations : OperationHandlerBase
{
    public FeatureRevolveOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<FeatureRevolveOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        var modelExt = model.Extension;
        if (modelExt == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get model extension"));
        }

        var axisEntity = GetStringParam(parameters, "AxisEntity", "");
        var axisEntityType = GetStringParam(parameters, "AxisEntityType", "AXIS");
        var singleDirection = GetBoolParam(parameters, "SingleDirection", true);
        var isSolid = GetBoolParam(parameters, "IsSolid", true);
        var isThin = GetBoolParam(parameters, "IsThin", false);
        var isCut = GetBoolParam(parameters, "IsCut", false);
        var reverseDirection = GetBoolParam(parameters, "ReverseDirection", false);
        var angle1 = GetDoubleParam(parameters, "Angle1", 360.0);
        var angle2 = GetDoubleParam(parameters, "Angle2", 360.0);
        var endCondition1 = GetIntParam(parameters, "EndCondition1", 0);
        var endCondition2 = GetIntParam(parameters, "EndCondition2", 0);
        var bothDirectionUpToSameEntity = GetBoolParam(parameters, "BothDirectionUpToSameEntity", false);
        var upToEntity1 = GetStringParam(parameters, "UpToEntity1", "");
        var upToEntity2 = GetStringParam(parameters, "UpToEntity2", "");
        var offsetDistance1 = GetDoubleParam(parameters, "OffsetDistance1", 0.0);
        var offsetDistance2 = GetDoubleParam(parameters, "OffsetDistance2", 0.0);
        var offsetReverse1 = GetBoolParam(parameters, "OffsetReverse1", false);
        var offsetReverse2 = GetBoolParam(parameters, "OffsetReverse2", false);
        var thinType = GetIntParam(parameters, "ThinType", 2);
        var thinThickness1 = GetDoubleParam(parameters, "ThinThickness1", 2.0);
        var thinThickness2 = GetDoubleParam(parameters, "ThinThickness2", 2.0);
        var mergeResult = GetBoolParam(parameters, "MergeResult", true);
        var useFeatureScope = GetBoolParam(parameters, "UseFeatureScope", false);
        var useAutoSelect = GetBoolParam(parameters, "UseAutoSelect", true);

        if (string.IsNullOrEmpty(axisEntity))
        {
            return Task.FromResult(ExecutionResult.Failure("AxisEntity is required for revolve feature. Specify a reference axis, sketch centerline, or model edge."));
        }

        if (angle1 < 0 || angle1 > 360)
        {
            return Task.FromResult(ExecutionResult.Failure("Angle1 must be between 0° and 360° (use ReverseDirection instead of negative angles)"));
        }

        if (!singleDirection && (angle2 < 0 || angle2 > 360))
        {
            return Task.FromResult(ExecutionResult.Failure("Angle2 must be between 0° and 360°"));
        }

        if (endCondition1 == (int)swEndConditions_e.swEndCondBlind && angle1 <= 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Angle1 must be greater than 0° for Blind end condition"));
        }

        if (!singleDirection && endCondition2 == (int)swEndConditions_e.swEndCondBlind && angle2 <= 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Angle2 must be greater than 0° for Blind end condition in direction 2"));
        }

        if ((endCondition1 == (int)swEndConditions_e.swEndCondUpToSurface ||
             endCondition1 == (int)swEndConditions_e.swEndCondUpToBody ||
             endCondition1 == (int)swEndConditions_e.swEndCondUpToSelection ||
             endCondition1 == (int)swEndConditions_e.swEndCondOffsetFromSurface) && string.IsNullOrEmpty(upToEntity1))
        {
            return Task.FromResult(ExecutionResult.Failure("UpToEntity1 required for specified end condition (UpToSurface/UpToBody/UpToSelection/OffsetFromSurface)"));
        }

        if (!singleDirection && !bothDirectionUpToSameEntity &&
            (endCondition2 == (int)swEndConditions_e.swEndCondUpToSurface ||
             endCondition2 == (int)swEndConditions_e.swEndCondUpToBody ||
             endCondition2 == (int)swEndConditions_e.swEndCondUpToSelection ||
             endCondition2 == (int)swEndConditions_e.swEndCondOffsetFromSurface) && string.IsNullOrEmpty(upToEntity2))
        {
            return Task.FromResult(ExecutionResult.Failure("UpToEntity2 required for specified end condition in direction 2 (when BothDirectionUpToSameEntity=false)"));
        }

        if (isThin)
        {
            if (thinThickness1 <= 0)
            {
                return Task.FromResult(ExecutionResult.Failure("ThinThickness1 must be greater than 0 when IsThin=true"));
            }

            if (thinType == 3 && thinThickness2 <= 0)
            {
                return Task.FromResult(ExecutionResult.Failure("ThinThickness2 must be greater than 0 when ThinType=TwoDirection"));
            }
        }

        if (!isSolid && isCut)
        {
            return Task.FromResult(ExecutionResult.Failure("Surface features cannot be cuts (IsSolid must be true when IsCut=true)"));
        }

        var angle1Rad = DegreesToRadians(angle1);
        var angle2Rad = DegreesToRadians(angle2);
        offsetDistance1 = MmToMeters(offsetDistance1);
        offsetDistance2 = MmToMeters(offsetDistance2);
        thinThickness1 = MmToMeters(thinThickness1);
        thinThickness2 = MmToMeters(thinThickness2);

        try
        {
            model.ClearSelection2(true);

            // The sketch MUST be pre-selected before FeatureRevolve2.
            var lastSketch = FindLastSketch(model);
            if (lastSketch == null)
            {
                return Task.FromResult(ExecutionResult.Failure("No sketch found to revolve. Create a sketch first."));
            }

            var sketchSelected = modelExt.SelectByID2(lastSketch.Name, "SKETCH", 0, 0, 0, false, 0, null, 0);
            if (!sketchSelected)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to select sketch '{lastSketch.Name}' for revolve."));
            }

            var axisSelected = modelExt.SelectByID2(axisEntity, axisEntityType, 0, 0, 0, true, 16, null, 0);
            if (!axisSelected)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to select axis entity: {axisEntity} (type: {axisEntityType}). Check entity name and type."));
            }

            if (!string.IsNullOrEmpty(upToEntity1))
            {
                var entity1Selected = modelExt.SelectByID2(upToEntity1, FeatureSupport.GetEntityType(endCondition1), 0, 0, 0, true, 32, null, 0);
                if (!entity1Selected)
                {
                    return Task.FromResult(ExecutionResult.Failure($"Failed to select end entity 1: {upToEntity1}"));
                }
            }

            if (!singleDirection && !bothDirectionUpToSameEntity && !string.IsNullOrEmpty(upToEntity2))
            {
                var entity2Selected = modelExt.SelectByID2(upToEntity2, FeatureSupport.GetEntityType(endCondition2), 0, 0, 0, true, 32, null, 0);
                if (!entity2Selected)
                {
                    return Task.FromResult(ExecutionResult.Failure($"Failed to select end entity 2: {upToEntity2}"));
                }
            }

            var feature = model.FeatureManager.FeatureRevolve2(
                singleDirection,
                isSolid,
                isThin,
                isCut,
                reverseDirection,
                bothDirectionUpToSameEntity,
                endCondition1,
                endCondition2,
                angle1Rad,
                angle2Rad,
                offsetReverse1,
                offsetReverse2,
                offsetDistance1,
                offsetDistance2,
                thinType,
                thinThickness1,
                thinThickness2,
                mergeResult,
                useFeatureScope,
                useAutoSelect);

            if (feature == null)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "Failed to create revolve feature. Common causes: open profile when IsThin=false, profile crosses the axis, self-intersection, or invalid axis selection."));
            }

            if (string.IsNullOrEmpty(feature.Name))
            {
                return Task.FromResult(ExecutionResult.Failure("Revolve feature was created but appears invalid (empty name). This may indicate geometry errors."));
            }

            var rebuildResult = model.ForceRebuild3(false);
            if (!rebuildResult)
            {
                _logger.LogWarning("Revolve feature created but rebuild returned warnings. Feature may have errors.");
            }

            var errorCode = feature.GetErrorCode();
            if (errorCode != 0)
            {
                return Task.FromResult(ExecutionResult.Failure($"Revolve feature created but has error code {errorCode}. The feature may have geometric issues."));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                FeatureName = feature.Name,
                FeatureType = isCut ? "Revolve-Cut" : "Revolve-Boss",
                FeatureSubtype = isSolid ? (isThin ? "Thin-Solid" : "Solid") : "Surface",
                Parameters = new
                {
                    Angle1 = angle1,
                    Angle2 = singleDirection ? (double?)null : angle2,
                    SingleDirection = singleDirection,
                    ReverseDirection = reverseDirection,
                    EndCondition1 = ((swEndConditions_e)endCondition1).ToString(),
                    EndCondition2 = singleDirection ? null : ((swEndConditions_e)endCondition2).ToString(),
                    IsSolid = isSolid,
                    IsThin = isThin,
                    IsCut = isCut,
                    ThinThickness1 = isThin ? MetersToMm(thinThickness1) : (double?)null,
                    ThinType = isThin ? ((ThinWallType)thinType).ToString() : null,
                    MergeResult = mergeResult,
                    AxisEntity = axisEntity,
                    AxisEntityType = axisEntityType
                }
            }));
        }
        finally
        {
            model.ClearSelection2(true);
        }
    }

    private static IFeature? FindLastSketch(ModelDoc2 model)
    {
        IFeature? lastSketch = null;
        var feat = (IFeature?)model.FirstFeature();
        while (feat != null)
        {
            if (feat.GetTypeName2() == "ProfileFeature")
            {
                lastSketch = feat;
            }
            feat = (IFeature?)feat.GetNextFeature();
        }
        return lastSketch;
    }
}

