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

public sealed class FeatureCutExtrusionOperations : OperationHandlerBase
{
    public FeatureCutExtrusionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<FeatureCutExtrusionOperations> logger)
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

        var depth = GetDoubleParam(parameters, "Depth", 10.0);
        var reverseDirection = GetBoolParam(parameters, "ReverseDirection", false);
        var singleDirection = GetBoolParam(parameters, "SingleDirection", true);
        var depth2 = GetDoubleParam(parameters, "Depth2", 10.0);
        var flipSideToCut = GetBoolParam(parameters, "FlipSideToCut", false);
        var endCondition1 = GetIntParam(parameters, "EndCondition1", 0);
        var endCondition2 = GetIntParam(parameters, "EndCondition2", 0);
        var upToEntity1 = GetStringParam(parameters, "UpToEntity1", "");
        var upToEntity2 = GetStringParam(parameters, "UpToEntity2", "");
        var offsetDistance1 = GetDoubleParam(parameters, "OffsetDistance1", 0.0);
        var offsetDistance2 = GetDoubleParam(parameters, "OffsetDistance2", 0.0);
        var offsetReverse1 = GetBoolParam(parameters, "OffsetReverse1", false);
        var offsetReverse2 = GetBoolParam(parameters, "OffsetReverse2", false);
        var translateSurface1 = GetBoolParam(parameters, "TranslateSurface1", false);
        var translateSurface2 = GetBoolParam(parameters, "TranslateSurface2", false);
        var startCondition = GetIntParam(parameters, "StartCondition", 0);
        var startOffset = GetDoubleParam(parameters, "StartOffset", 0.0);
        var startEntity = GetStringParam(parameters, "StartEntity", "");
        var flipStartOffset = GetBoolParam(parameters, "FlipStartOffset", false);
        var useDraft1 = GetBoolParam(parameters, "UseDraft1", false);
        var useDraft2 = GetBoolParam(parameters, "UseDraft2", false);
        var draftAngle1 = GetDoubleParam(parameters, "DraftAngle1", 0.0);
        var draftAngle2 = GetDoubleParam(parameters, "DraftAngle2", 0.0);
        var draftInward1 = GetBoolParam(parameters, "DraftInward1", false);
        var draftInward2 = GetBoolParam(parameters, "DraftInward2", false);
        var normalCut = GetBoolParam(parameters, "NormalCut", false);
        var optimizeGeometry = GetBoolParam(parameters, "OptimizeGeometry", false);
        var useFeatureScope = GetBoolParam(parameters, "UseFeatureScope", true);
        var useAutoSelect = GetBoolParam(parameters, "UseAutoSelect", true);
        var assemblyFeatureScope = GetBoolParam(parameters, "AssemblyFeatureScope", true);
        var autoSelectComponents = GetBoolParam(parameters, "AutoSelectComponents", true);
        var propagateFeatureToParts = GetBoolParam(parameters, "PropagateFeatureToParts", false);

        if (endCondition1 == (int)swEndConditions_e.swEndCondBlind && depth <= 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Depth must be greater than 0 for Blind end condition"));
        }

        if (!singleDirection && endCondition2 == (int)swEndConditions_e.swEndCondBlind && depth2 <= 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Depth2 must be greater than 0 for Blind end condition in direction 2"));
        }

        if (useDraft1 && (draftAngle1 < 0 || draftAngle1 > 30))
        {
            return Task.FromResult(ExecutionResult.Failure("Draft angle 1 must be between 0° and 30°"));
        }

        if (useDraft2 && (draftAngle2 < 0 || draftAngle2 > 30))
        {
            return Task.FromResult(ExecutionResult.Failure("Draft angle 2 must be between 0° and 30°"));
        }

        if ((endCondition1 == (int)swEndConditions_e.swEndCondUpToSurface ||
             endCondition1 == (int)swEndConditions_e.swEndCondUpToVertex ||
             endCondition1 == (int)swEndConditions_e.swEndCondUpToBody ||
             endCondition1 == (int)swEndConditions_e.swEndCondOffsetFromSurface) && string.IsNullOrEmpty(upToEntity1))
        {
            return Task.FromResult(ExecutionResult.Failure("UpToEntity1 required for specified end condition (UpToSurface/UpToVertex/UpToBody/OffsetFromSurface)"));
        }

        if (!singleDirection && (endCondition2 == (int)swEndConditions_e.swEndCondUpToSurface ||
             endCondition2 == (int)swEndConditions_e.swEndCondUpToVertex ||
             endCondition2 == (int)swEndConditions_e.swEndCondUpToBody ||
             endCondition2 == (int)swEndConditions_e.swEndCondOffsetFromSurface) && string.IsNullOrEmpty(upToEntity2))
        {
            return Task.FromResult(ExecutionResult.Failure("UpToEntity2 required for specified end condition in direction 2"));
        }

        if ((startCondition == (int)swStartConditions_e.swStartSurface ||
             startCondition == (int)swStartConditions_e.swStartVertex) && string.IsNullOrEmpty(startEntity))
        {
            return Task.FromResult(ExecutionResult.Failure("StartEntity required for Surface or Vertex start condition"));
        }

        depth = MmToMeters(depth);
        depth2 = MmToMeters(depth2);
        startOffset = MmToMeters(startOffset);
        offsetDistance1 = MmToMeters(offsetDistance1);
        offsetDistance2 = MmToMeters(offsetDistance2);
        var draftAngle1Rad = DegreesToRadians(draftAngle1);
        var draftAngle2Rad = DegreesToRadians(draftAngle2);

        try
        {
            model.ClearSelection2(true);

            if (!string.IsNullOrEmpty(startEntity))
            {
                var startSelected = modelExt.SelectByID2(startEntity, FeatureSupport.GetEntityType(startCondition), 0, 0, 0, false, 0, null, 0);
                if (!startSelected)
                {
                    return Task.FromResult(ExecutionResult.Failure($"Failed to select start entity: {startEntity}"));
                }
            }

            if (!string.IsNullOrEmpty(upToEntity1))
            {
                var entity1Selected = modelExt.SelectByID2(upToEntity1, FeatureSupport.GetEntityType(endCondition1), 0, 0, 0, true, 32, null, 0);
                if (!entity1Selected)
                {
                    return Task.FromResult(ExecutionResult.Failure($"Failed to select end entity 1: {upToEntity1}"));
                }
            }

            if (!singleDirection && !string.IsNullOrEmpty(upToEntity2))
            {
                var entity2Selected = modelExt.SelectByID2(upToEntity2, FeatureSupport.GetEntityType(endCondition2), 0, 0, 0, true, 32, null, 0);
                if (!entity2Selected)
                {
                    return Task.FromResult(ExecutionResult.Failure($"Failed to select end entity 2: {upToEntity2}"));
                }
            }

            var feature = model.FeatureManager.FeatureCut4(
                singleDirection,
                flipSideToCut,
                reverseDirection,
                endCondition1,
                endCondition2,
                depth,
                depth2,
                useDraft1,
                useDraft2,
                draftInward1,
                draftInward2,
                draftAngle1Rad,
                draftAngle2Rad,
                offsetReverse1,
                offsetReverse2,
                translateSurface1,
                translateSurface2,
                normalCut,
                useFeatureScope,
                useAutoSelect,
                assemblyFeatureScope,
                autoSelectComponents,
                propagateFeatureToParts,
                startCondition,
                startOffset,
                flipStartOffset,
                optimizeGeometry);

            if (feature == null)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "Failed to create cut extrusion feature. Check that sketch intersects existing geometry, parameters are valid, and there is solid material to remove."));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                FeatureName = feature.Name,
                FeatureType = "Cut-Extrude",
                Parameters = new
                {
                    Depth = MetersToMm(depth),
                    ReverseDirection = reverseDirection,
                    SingleDirection = singleDirection,
                    Depth2 = singleDirection ? (double?)null : MetersToMm(depth2),
                    EndCondition1 = ((swEndConditions_e)endCondition1).ToString(),
                    EndCondition2 = singleDirection ? null : ((swEndConditions_e)endCondition2).ToString(),
                    StartCondition = ((swStartConditions_e)startCondition).ToString(),
                    UseDraft1 = useDraft1,
                    DraftAngle1 = useDraft1 ? draftAngle1 : (double?)null,
                    UseDraft2 = useDraft2 && !singleDirection,
                    DraftAngle2 = (useDraft2 && !singleDirection) ? draftAngle2 : (double?)null,
                    UseFeatureScope = useFeatureScope,
                    UseAutoSelect = useAutoSelect
                }
            }));
        }
        finally
        {
            model.ClearSelection2(true);
        }
    }
}

