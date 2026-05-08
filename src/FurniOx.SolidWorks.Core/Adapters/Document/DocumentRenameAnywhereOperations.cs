#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Single-component rename anywhere in the active root assembly tree, addressed
/// by full root-relative instance path. Public surface — batch sibling
/// (RenameComponentFilesBatch) lives in DocumentRenameBatchOperations (private).
/// </summary>
public sealed class DocumentRenameAnywhereOperations : OperationHandlerBase
{
    public DocumentRenameAnywhereOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentRenameAnywhereOperations> logger)
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
            DocumentGovernanceOperationNames.RenameComponentAnywhere => RenameComponentAnywhereAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown rename anywhere operation: {operation}"))
        };
    }

    private Task<ExecutionResult> RenameComponentAnywhereAsync(IDictionary<string, object?> parameters)
    {
        var context = DocumentRenameApplyHelpers.ResolveActiveAssembly(_connection);
        if (context.Result != null) return Task.FromResult(context.Result!);

        var instancePath = GetStringParam(parameters, "InstancePath");
        if (string.IsNullOrWhiteSpace(instancePath))
        {
            instancePath = GetStringParam(parameters, "ComponentPath");
        }

        var newName = GetStringParam(parameters, "NewName");
        if (string.IsNullOrWhiteSpace(instancePath) || string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult(ExecutionResult.Failure("InstancePath and NewName are required."));
        }

        var occurrences = DocumentRenamePathHelpers.BuildComponentOccurrences(context.Assembly!);
        var occurrence = occurrences.FirstOrDefault(item =>
            string.Equals(item.InstancePath, instancePath, System.StringComparison.OrdinalIgnoreCase));
        if (occurrence == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Component instance path '{instancePath}' not found from active root assembly.", new
            {
                Requested = instancePath,
                Available = occurrences.Select(item => item.InstancePath).Take(40).ToArray()
            }));
        }

        var oldPath = occurrence.Path;
        var expectedExtension = Path.GetExtension(oldPath);
        if (!DocumentRenameSupport.TryNormalizeNewName(newName, expectedExtension, "this component", out var _, out var nameError))
        {
            return Task.FromResult(ExecutionResult.Failure(nameError!));
        }

        var newPath = Path.Combine(
            Path.GetDirectoryName(oldPath) ?? string.Empty,
            DocumentRenamePathHelpers.EnsureExtension(newName, expectedExtension));
        var requireFullyResolved = GetBoolParam(parameters, "RequireFullyResolved", true);
        var plan = DocumentRenamePathHelpers.BuildBatchPlan(new[] { new RenameFileItem(oldPath, newPath) }, occurrences, requireFullyResolved).Single();
        if (plan.Blockers.Count > 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Rename preflight failed.", DocumentRenamePathHelpers.ToPlanOutput(plan)));
        }

        var dryRun = GetBoolParam(parameters, "DryRun", false);
        if (dryRun)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(DocumentRenamePathHelpers.ToPlanOutput(plan), "Rename preflight passed."));
        }

        var app = context.App!;
        var model = context.Model!;
        var previousTitle = model.GetTitle();
        var autoSave = GetBoolParam(parameters, "AutoSave", true);
        var saveRenamedChildren = GetBoolParam(parameters, "SaveRenamedChildren", true);
        var hiddenInGui = GetBoolParam(parameters, "HiddenInGui", true);
        var closeOpenedDocs = GetBoolParam(parameters, "CloseOpenedDocs", true);
        var suppressSaveDialogs = GetBoolParam(parameters, "SuppressSaveDialogs", true);
        var openSnapshot = DocumentRenameSaveHelpers.SnapshotOpenDocuments(app);
        object? childSaveSummary = null;
        object? openedDocumentCleanup = null;

        using var suppressionScope = suppressSaveDialogs ? new DialogSuppressionScope(app, _logger) : null;
        using var visibilityScope = DocumentVisibilityScope.HideNewDocuments(app, hiddenInGui);
        try
        {
            app.CommandInProgress = true;
            var attempt = DocumentRenameApplyHelpers.RenameSelectedComponent(model, occurrence.Component, newPath);
            if (!attempt.Success)
            {
                return Task.FromResult(ExecutionResult.Failure($"Rename failed: {attempt.Error}", attempt));
            }

            bool saved = false;
            int saveErrors = 0;
            int saveWarnings = 0;
            if (autoSave)
            {
                var searchFolders = new[]
                {
                    Path.GetDirectoryName(oldPath) ?? string.Empty,
                    Path.GetDirectoryName(newPath) ?? string.Empty
                };

                if (saveRenamedChildren)
                {
                    var appliedSingle = new List<AppliedRename> { new(oldPath, newPath, occurrence.InstancePath) };
                    var childSaves = DocumentRenameSaveHelpers.SaveRenamedChildren(app, appliedSingle, searchFolders, suppressSaveDialogs, _logger);
                    childSaveSummary = childSaves.Summary;
                    if (!childSaves.Success)
                    {
                        DocumentRenameApplyHelpers.RollbackApplied(model, occurrences, appliedSingle);
                        return Task.FromResult(ExecutionResult.Failure("Rename succeeded in memory but renamed child save failed.", new
                        {
                            Attempt = attempt,
                            ChildSaves = childSaveSummary,
                            AutoDismissedDialogCount = suppressionScope?.ClosedDialogCount ?? 0
                        }));
                    }
                }

                saved = DocumentSaveHelper.SaveWithRenamedReferences(
                    model,
                    _logger,
                    out saveErrors,
                    out saveWarnings,
                    searchFolders,
                    suppressSaveDialogs ? app : null);

                if (!saved || saveErrors != 0)
                {
                    DocumentRenameApplyHelpers.RollbackApplied(model, occurrences, new List<AppliedRename> { new(oldPath, newPath, occurrence.InstancePath) });
                    return Task.FromResult(ExecutionResult.Failure($"Rename succeeded in memory but save failed: {DocumentSaveHelper.FormatSaveError(saveErrors)}", new
                    {
                        Attempt = attempt,
                        Saved = saved,
                        SaveErrors = saveErrors,
                        SaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors),
                        SaveWarnings = saveWarnings,
                        ChildSaves = childSaveSummary,
                        AutoDismissedDialogCount = suppressionScope?.ClosedDialogCount ?? 0
                    }));
                }
            }

            openedDocumentCleanup = DocumentRenameSaveHelpers.CleanupOpenedDocuments(
                app,
                openSnapshot,
                new List<AppliedRename> { new(oldPath, newPath, occurrence.InstancePath) },
                closeOpenedDocs,
                hiddenInGui);

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Renamed = true,
                InstancePath = occurrence.InstancePath,
                OldPath = oldPath,
                NewPath = newPath,
                FinalPath = attempt.FinalPath,
                AutoSave = autoSave,
                SaveRenamedChildren = saveRenamedChildren,
                HiddenInGui = hiddenInGui,
                CloseOpenedDocs = closeOpenedDocs,
                SuppressSaveDialogs = suppressSaveDialogs,
                RequireFullyResolved = requireFullyResolved,
                Saved = autoSave ? saved : (bool?)null,
                SaveErrors = autoSave ? saveErrors : (int?)null,
                SaveWarnings = autoSave ? saveWarnings : (int?)null,
                ChildSaves = childSaveSummary,
                OpenedDocumentCleanup = openedDocumentCleanup,
                AutoDismissedDialogCount = suppressionScope?.ClosedDialogCount ?? 0
            }));
        }
        finally
        {
            try { model.ClearSelection2(true); } catch { }
            try { app.CommandInProgress = false; } catch { }
            DocumentRenamePathHelpers.TryRestoreActiveDocument(app, previousTitle);
        }
    }
}
