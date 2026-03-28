using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Sorting;

public sealed class SortingFeatureReorderOperations : OperationHandlerBase
{
    public SortingFeatureReorderOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SortingFeatureReorderOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ReorderFeaturesByPositions(parameters));
    }

    private ExecutionResult ReorderFeaturesByPositions(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return ExecutionResult.Failure("Not connected to SolidWorks");
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return ExecutionResult.Failure("No active document");
        }

        var dryRun = GetBoolParam(parameters, "DryRun", false);
        var featureType = GetStringParam(parameters, "FeatureType", string.Empty).ToLowerInvariant();
        var preserveFolders = GetBoolParam(parameters, "PreserveFolders", true);

        parameters.TryGetValue("Positions", out var positionsParam);
        if (positionsParam == null)
        {
            return ExecutionResult.Failure("Positions parameter is required. Format: [{\"name\": \"FeatureName\", \"position\": 1}, ...]");
        }

        List<SortingPositionEntry> positionEntries;
        try
        {
            positionEntries = SortingParameterParser.ParsePositionEntries(positionsParam);
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failure($"Failed to parse Positions: {ex.Message}");
        }

        if (positionEntries.Count == 0)
        {
            return ExecutionResult.Failure("Positions array is empty");
        }

        var duplicatePositions = positionEntries.GroupBy(entry => entry.Position).Where(group => group.Count() > 1).ToList();
        if (duplicatePositions.Any())
        {
            return ExecutionResult.Failure($"Duplicate positions found: {string.Join(", ", duplicatePositions.Select(group => group.Key))}");
        }

        if (positionEntries.Any(entry => entry.Position < 1))
        {
            return ExecutionResult.Failure("Positions must be >= 1 (1-based indexing)");
        }

        try
        {
            var allFeaturesInTree = SortingFeatureSupport.GetReorderableFeaturesInTreeOrder(model, featureType);
            var allFeatures = allFeaturesInTree.Select(entry => entry.Feature).ToList();

            if (allFeatures.Count == 0)
            {
                var typeMessage = string.IsNullOrEmpty(featureType) ? string.Empty : $" of type '{featureType}'";
                return ExecutionResult.Failure($"No features{typeMessage} found in document");
            }

            var featureByName = new Dictionary<string, IFeature>(StringComparer.OrdinalIgnoreCase);
            foreach (var feature in allFeatures)
            {
                var name = feature.Name;
                if (!string.IsNullOrEmpty(name) && !featureByName.ContainsKey(name))
                {
                    featureByName[name] = feature;
                }
            }

            var notFound = positionEntries.Where(entry => !featureByName.ContainsKey(entry.Name)).ToList();
            if (notFound.Any())
            {
                return ExecutionResult.Failure($"Features not found: {string.Join(", ", notFound.Select(entry => entry.Name))}");
            }

            var sortedByPosition = positionEntries.OrderBy(entry => entry.Position).ToList();
            var explicitlyPositioned = new HashSet<string>(sortedByPosition.Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);
            var remainingFeatures = allFeatures.Where(feature => !explicitlyPositioned.Contains(feature.Name ?? string.Empty)).ToList();

            var targetOrder = new List<IFeature>();
            var maxPosition = sortedByPosition.Max(entry => entry.Position);
            var remainingIndex = 0;

            for (var position = 1; position <= Math.Max(maxPosition, allFeatures.Count); position++)
            {
                var entry = sortedByPosition.FirstOrDefault(item => item.Position == position);
                if (entry != null)
                {
                    targetOrder.Add(featureByName[entry.Name]);
                }
                else if (remainingIndex < remainingFeatures.Count)
                {
                    targetOrder.Add(remainingFeatures[remainingIndex]);
                    remainingIndex++;
                }
            }

            while (remainingIndex < remainingFeatures.Count)
            {
                targetOrder.Add(remainingFeatures[remainingIndex]);
                remainingIndex++;
            }

            List<IFeature> effectiveTargetOrder;
            SortingFeatureFolderGroupState? folderState = null;
            if (preserveFolders)
            {
                folderState = SortingFeatureSupport.ComputeFeatureFolderGroupState(allFeaturesInTree, targetOrder);
                effectiveTargetOrder = SortingFeatureSupport.BuildEffectiveFeatureOrderFromState(folderState);
            }
            else
            {
                effectiveTargetOrder = targetOrder;
            }

            var changes = new List<SortingChange>();
            for (var i = 0; i < effectiveTargetOrder.Count; i++)
            {
                var feature = effectiveTargetOrder[i];
                var originalIndex = allFeatures.IndexOf(feature);
                if (originalIndex != i)
                {
                    changes.Add(new SortingChange
                    {
                        ComponentName = feature.Name ?? "Unknown",
                        OriginalPosition = originalIndex,
                        NewPosition = i,
                        SortKey = (i + 1).ToString()
                    });
                }
            }

            if (dryRun)
            {
                var dryWarnings = new List<string> { "Dry run - no changes applied" };
                if (preserveFolders)
                {
                    dryWarnings.Add("PreserveFolders is enabled: only reorders within existing FeatureManager folders; features will not move into/out of folders.");
                }

                return ExecutionResult.SuccessResult(new SortingResult
                {
                    Applied = false,
                    TotalComponentCount = allFeatures.Count,
                    ReorderedCount = changes.Count,
                    SortBy = "custom_position",
                    SortOrder = "ascending",
                    Scope = string.IsNullOrEmpty(featureType) ? "all_features" : featureType,
                    Changes = changes,
                    Warnings = dryWarnings
                }, $"Dry run: {changes.Count} features would be reordered");
            }

            var warnings = new List<string>();
            if (preserveFolders)
            {
                warnings.Add("PreserveFolders is enabled: only reorders within existing FeatureManager folders; features will not move into/out of folders.");
            }

            var successCount = preserveFolders && folderState != null
                ? ApplyFeatureReorderingPreservingFolders(model, folderState, warnings)
                : ApplyFeatureReordering(model, effectiveTargetOrder, warnings);

            return ExecutionResult.SuccessResult(new SortingResult
            {
                Applied = true,
                TotalComponentCount = allFeatures.Count,
                ReorderedCount = successCount,
                SkippedCount = changes.Count - successCount,
                SortBy = "custom_position",
                SortOrder = "ascending",
                Scope = string.IsNullOrEmpty(featureType) ? "all_features" : featureType,
                Changes = changes,
                Warnings = warnings
            }, $"Reordered {successCount} features by custom positions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder features by positions");
            return ExecutionResult.Failure($"Failed to reorder features: {ex.Message}");
        }
    }

    private int ApplyFeatureReordering(
        ModelDoc2 model,
        List<IFeature> targetOrder,
        List<string> warnings)
    {
        var app = _connection.Application;
        if (app == null || targetOrder.Count < 2)
        {
            return 0;
        }

        var successCount = 0;
        var view = model.ActiveView as IModelView;
        var undoRecordingStarted = false;

        try
        {
            app.CommandInProgress = true;
            if (view != null)
            {
                view.EnableGraphicsUpdate = false;
            }

            model.Extension.StartRecordingUndoObject();
            undoRecordingStarted = true;

            for (var i = targetOrder.Count - 2; i >= 0; i--)
            {
                var featureToMove = targetOrder[i];
                var followingFeature = targetOrder[i + 1];

                try
                {
                    var moveName = featureToMove.Name ?? string.Empty;
                    var targetName = followingFeature.Name ?? string.Empty;

                    if (string.IsNullOrEmpty(moveName) || string.IsNullOrEmpty(targetName))
                    {
                        warnings.Add("Encountered a feature with an empty name; skipping reorder step.");
                        continue;
                    }

                    var result = model.Extension.ReorderFeature(moveName, targetName, (int)swMoveLocation_e.swMoveBefore);
                    if (result)
                    {
                        successCount++;
                    }
                    else
                    {
                        warnings.Add($"Failed to move '{moveName}' before '{targetName}'");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Error moving '{featureToMove.Name}': {ex.Message}");
                }
            }
        }
        finally
        {
            if (undoRecordingStarted)
            {
                try { model.Extension.FinishRecordingUndoObject("Reorder Features"); } catch (Exception ex) { warnings.Add($"Failed to end undo recording: {ex.Message}"); }
            }

            if (view != null)
            {
                try { view.EnableGraphicsUpdate = true; } catch { }
            }

            try { model.GraphicsRedraw2(); } catch { }
            app.CommandInProgress = false;
        }

        return successCount;
    }

    private int ApplyFeatureReorderingPreservingFolders(
        ModelDoc2 model,
        SortingFeatureFolderGroupState state,
        List<string> warnings)
    {
        var successCount = 0;

        foreach (var group in state.Groups)
        {
            if (group.Features.Count < 2)
            {
                continue;
            }

            var desiredGroupOrder = group.Features
                .OrderBy(feature =>
                {
                    var name = feature.Name ?? string.Empty;
                    return state.DesiredIndexByName.TryGetValue(name, out var index) ? index : int.MaxValue;
                })
                .ToList();

            var anyDifference = false;
            for (var i = 0; i < desiredGroupOrder.Count; i++)
            {
                if (!ReferenceEquals(desiredGroupOrder[i], group.Features[i]))
                {
                    anyDifference = true;
                    break;
                }
            }

            if (!anyDifference)
            {
                continue;
            }

            successCount += ApplyFeatureReordering(model, desiredGroupOrder, warnings);
        }

        return successCount;
    }
}
