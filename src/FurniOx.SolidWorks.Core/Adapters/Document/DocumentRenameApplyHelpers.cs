#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Apply-time rename helpers shared across the public single-component rename
/// (DocumentRenameAnywhereOperations) and the private batch rename
/// (DocumentRenameBatchOperations).
/// </summary>
internal static class DocumentRenameApplyHelpers
{
    internal static BatchContext ResolveActiveAssembly(SolidWorksConnection connection)
    {
        var app = connection.Application;
        if (app == null) return BatchContext.Fail(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return BatchContext.Fail(ExecutionResult.Failure("No active document"));
        if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY || model is not IAssemblyDoc assembly)
        {
            return BatchContext.Fail(ExecutionResult.Failure("Active document must be the root assembly."));
        }

        return new BatchContext(app, model, assembly, null);
    }

    internal static RenameAttempt RenameSelectedComponent(ModelDoc2 rootModel, IComponent2 component, string newPath)
    {
        var originalSuppressionState = component.ReadSuppressionState();
        var suppressionWasChanged = false;

        try
        {
            if (component.IsSuppressed())
            {
                return RenameAttempt.Fail("Component is suppressed.");
            }

            if (component.GetModelDoc2() == null)
            {
                var resolveStatus = component.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
                if (resolveStatus != (int)swComponentSuppressionState_e.swComponentFullyResolved)
                {
                    return RenameAttempt.Fail($"Component could not be resolved (status {resolveStatus}).");
                }

                suppressionWasChanged = true;
            }

            var expectedExtension = Path.GetExtension(component.GetPathName());
            var fileName = Path.GetFileName(newPath);
            if (!DocumentRenameSupport.TryNormalizeNewName(fileName, expectedExtension, "this component", out var baseNameForApi, out var nameError))
            {
                return RenameAttempt.Fail(nameError ?? "Invalid target name.");
            }

            rootModel.ClearSelection2(true);
            var selMgr = (ISelectionMgr?)rootModel.SelectionManager;
            var selectData = (SelectData?)selMgr?.CreateSelectData();
            if (selectData != null) selectData.Mark = 0;

            if (!component.Select4(false, selectData, false))
            {
                return RenameAttempt.Fail("Select4 failed.");
            }

            var errorCode = rootModel.Extension.RenameDocument(baseNameForApi);
            var renameError = (swRenameDocumentError_e)errorCode;
            if (renameError != swRenameDocumentError_e.swRenameDocumentError_None)
            {
                return RenameAttempt.Fail(DocumentRenameSupport.GetRenameErrorMessage(renameError), errorCode, renameError.ToString());
            }

            return RenameAttempt.Ok(component.GetPathName() ?? string.Empty);
        }
        catch (Exception ex)
        {
            return RenameAttempt.Fail(ex.Message);
        }
        finally
        {
            try { rootModel.ClearSelection2(true); } catch { }
            if (suppressionWasChanged)
            {
                try { component.SetSuppression2(originalSuppressionState); } catch { }
            }
        }
    }

    internal static object RollbackApplied(
        ModelDoc2 rootModel,
        IReadOnlyList<ComponentOccurrence> originalOccurrences,
        IReadOnlyList<AppliedRename> applied)
    {
        var rolledBack = new List<object>();
        var failed = new List<object>();

        foreach (var item in applied.Reverse())
        {
            var currentOccurrences = DocumentRenamePathHelpers.BuildComponentOccurrences((IAssemblyDoc)rootModel);
            var occurrence = currentOccurrences.FirstOrDefault(component =>
                    string.Equals(component.NormalizedPath, DocumentRenamePathHelpers.NormalizePath(item.NewPath), StringComparison.OrdinalIgnoreCase))
                ?? originalOccurrences.FirstOrDefault(component =>
                    string.Equals(component.InstancePath, item.InstancePath, StringComparison.OrdinalIgnoreCase));

            if (occurrence == null)
            {
                failed.Add(new { item.OldPath, item.NewPath, Error = "Component not found for rollback" });
                continue;
            }

            var attempt = RenameSelectedComponent(rootModel, occurrence.Component, item.OldPath);
            if (attempt.Success)
            {
                rolledBack.Add(new { item.OldPath, item.NewPath, attempt.FinalPath });
            }
            else
            {
                failed.Add(new { item.OldPath, item.NewPath, attempt.Error, attempt.ErrorCode, attempt.ErrorName });
            }
        }

        return new
        {
            Attempted = applied.Count,
            RolledBackCount = rolledBack.Count,
            FailedCount = failed.Count,
            RolledBack = rolledBack,
            Failed = failed
        };
    }
}
