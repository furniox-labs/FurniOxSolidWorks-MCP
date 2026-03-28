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

public sealed class SortingComponentReorderOperations : OperationHandlerBase
{
    public SortingComponentReorderOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SortingComponentReorderOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ReorderByPositions(parameters));
    }

    private ExecutionResult ReorderByPositions(IDictionary<string, object?> parameters)
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

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return ExecutionResult.Failure("Active document must be an assembly");
        }

        var assembly = (IAssemblyDoc)model;
        var dryRun = GetBoolParam(parameters, "DryRun", false);
        var preserveFolders = GetBoolParam(parameters, "PreserveFolders", true);

        parameters.TryGetValue("Positions", out var positionsParam);
        if (positionsParam == null)
        {
            return ExecutionResult.Failure("Positions parameter is required. Format: [{\"name\": \"ComponentName-1\", \"position\": 1}, ...]");
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
            var currentComponentsInTree = SortingComponentSupport.GetTopLevelComponentsInFeatureTreeOrder(model, assembly)
                .Where(entry => !SortingComponentSupport.IsSuppressedSafe(entry.Component))
                .ToList();

            var currentChildren = currentComponentsInTree.Select(entry => entry.Component).ToList();
            if (currentChildren.Count == 0)
            {
                return ExecutionResult.Failure("No components found in assembly");
            }

            var componentByName = new Dictionary<string, IComponent2>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in currentChildren)
            {
                var name = child.Name2;
                if (!string.IsNullOrEmpty(name) && !componentByName.ContainsKey(name))
                {
                    componentByName[name] = child;

                    var alternateName = SortingComponentSupport.ConvertInstanceNameFormat(name);
                    if (!string.IsNullOrEmpty(alternateName) && !componentByName.ContainsKey(alternateName))
                    {
                        componentByName[alternateName] = child;
                    }
                }
            }

            var notFound = positionEntries.Where(entry => !componentByName.ContainsKey(entry.Name)).ToList();
            if (notFound.Any())
            {
                return ExecutionResult.Failure($"Components not found: {string.Join(", ", notFound.Select(entry => entry.Name))}");
            }

            var sortedByPosition = positionEntries.OrderBy(entry => entry.Position).ToList();
            var explicitlyPositioned = new HashSet<string>(sortedByPosition.Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);
            var remainingComponents = currentChildren.Where(component => !explicitlyPositioned.Contains(component.Name2 ?? string.Empty)).ToList();

            var changes = new List<SortingChange>();
            var targetOrder = new List<IComponent2>();
            var maxPosition = sortedByPosition.Max(entry => entry.Position);
            var remainingIndex = 0;

            for (var position = 1; position <= Math.Max(maxPosition, currentChildren.Count); position++)
            {
                var entry = sortedByPosition.FirstOrDefault(item => item.Position == position);
                if (entry != null)
                {
                    targetOrder.Add(componentByName[entry.Name]);
                }
                else if (remainingIndex < remainingComponents.Count)
                {
                    targetOrder.Add(remainingComponents[remainingIndex]);
                    remainingIndex++;
                }
            }

            while (remainingIndex < remainingComponents.Count)
            {
                targetOrder.Add(remainingComponents[remainingIndex]);
                remainingIndex++;
            }

            List<IComponent2> effectiveTargetOrder;
            SortingFolderGroupState? folderState = null;
            if (preserveFolders)
            {
                folderState = SortingComponentSupport.ComputeFolderGroupState(currentComponentsInTree, targetOrder);
                effectiveTargetOrder = SortingComponentSupport.BuildEffectiveOrderFromState(folderState);
            }
            else
            {
                effectiveTargetOrder = targetOrder;
            }

            for (var i = 0; i < effectiveTargetOrder.Count; i++)
            {
                var component = effectiveTargetOrder[i];
                var originalIndex = currentChildren.IndexOf(component);
                if (originalIndex != i)
                {
                    changes.Add(new SortingChange
                    {
                        ComponentName = component.Name2 ?? "Unknown",
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
                    dryWarnings.Add("PreserveFolders is enabled: only reorders within existing FeatureManager folders; components will not move into/out of folders (e.g., 'Hardware').");
                }

                return ExecutionResult.SuccessResult(new SortingResult
                {
                    Applied = false,
                    TotalComponentCount = currentChildren.Count,
                    ReorderedCount = changes.Count,
                    SortBy = "custom_position",
                    SortOrder = "ascending",
                    Scope = "top_level",
                    Changes = changes,
                    Warnings = dryWarnings
                }, $"Dry run: {changes.Count} components would be reordered");
            }

            var warnings = new List<string>();
            if (preserveFolders)
            {
                warnings.Add("PreserveFolders is enabled: only reorders within existing FeatureManager folders; components will not move into/out of folders (e.g., 'Hardware').");
            }

            var successCount = preserveFolders && folderState != null
                ? ApplyPositionalReorderingPreservingFolders(assembly, model, folderState, warnings)
                : ApplyPositionalReordering(assembly, model, effectiveTargetOrder, warnings);

            return ExecutionResult.SuccessResult(new SortingResult
            {
                Applied = true,
                TotalComponentCount = currentChildren.Count,
                ReorderedCount = successCount,
                SkippedCount = changes.Count - successCount,
                SortBy = "custom_position",
                SortOrder = "ascending",
                Scope = "top_level",
                Changes = changes,
                Warnings = warnings
            }, $"Reordered {successCount} components by custom positions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder components by positions");
            return ExecutionResult.Failure($"Failed to reorder: {ex.Message}");
        }
    }

    private int ApplyPositionalReordering(
        IAssemblyDoc assembly,
        ModelDoc2 model,
        List<IComponent2> targetOrder,
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
                var source = targetOrder[i];
                var target = targetOrder[i + 1];

                try
                {
                    var result = assembly.ReorderComponents(source, target, (int)swReorderComponentsWhere_e.swReorderComponents_Before);
                    if (result)
                    {
                        successCount++;
                    }
                    else
                    {
                        warnings.Add($"Failed to move '{source.Name2}' before '{target.Name2}'");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Error moving '{source.Name2}': {ex.Message}");
                }
            }
        }
        finally
        {
            if (undoRecordingStarted)
            {
                try { model.Extension.FinishRecordingUndoObject("Reorder Components"); } catch (Exception ex) { warnings.Add($"Failed to end undo recording: {ex.Message}"); }
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

    private int ApplyPositionalReorderingPreservingFolders(
        IAssemblyDoc assembly,
        ModelDoc2 model,
        SortingFolderGroupState state,
        List<string> warnings)
    {
        var successCount = 0;

        foreach (var group in state.Groups)
        {
            if (group.Components.Count < 2)
            {
                continue;
            }

            var desiredGroupOrder = group.Components
                .OrderBy(component =>
                {
                    var name = component.Name2 ?? string.Empty;
                    return state.DesiredIndexByName.TryGetValue(name, out var index) ? index : int.MaxValue;
                })
                .ToList();

            var anyDifference = false;
            for (var i = 0; i < desiredGroupOrder.Count; i++)
            {
                if (!ReferenceEquals(desiredGroupOrder[i], group.Components[i]))
                {
                    anyDifference = true;
                    break;
                }
            }

            if (!anyDifference)
            {
                continue;
            }

            successCount += ApplyPositionalReordering(assembly, model, desiredGroupOrder, warnings);
        }

        return successCount;
    }
}
