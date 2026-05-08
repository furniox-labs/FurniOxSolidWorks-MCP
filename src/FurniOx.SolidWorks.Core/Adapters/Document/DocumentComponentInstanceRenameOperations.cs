using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

public sealed class DocumentComponentInstanceRenameOperations : OperationHandlerBase
{
    public DocumentComponentInstanceRenameOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentComponentInstanceRenameOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return RenameComponentInstanceAsync(parameters);
    }

    private Task<ExecutionResult> RenameComponentInstanceAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return Task.FromResult(ExecutionResult.Failure("No active document"));
        if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return Task.FromResult(ExecutionResult.Failure("Active document must be an assembly to rename component instances"));
        }

        var assembly = (IAssemblyDoc)model;

        if (!parameters.TryGetValue("ComponentName", out var compNameObj) || compNameObj is not string componentName || string.IsNullOrWhiteSpace(componentName))
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'ComponentName'. Use the instance name from analyze_assembly (e.g., 'Part1-1')."));
        }

        if (!parameters.TryGetValue("NewInstanceName", out var newNameObj) || newNameObj is not string newInstanceName || string.IsNullOrWhiteSpace(newInstanceName))
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'NewInstanceName'. Provide the desired tree name (base name, instance suffix is auto-handled)."));
        }

        var autoSave = GetBoolParam(parameters, "AutoSave", true);
        var component = (IComponent2?)assembly.GetComponentByName(componentName);
        if (component == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' not found in assembly. Use the instance name from analyze_assembly."));
        }

        if (component.IsSuppressed())
        {
            return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' is suppressed. Unsuppress before renaming.", new
            {
                ComponentName = componentName,
                IsSuppressed = true
            }));
        }

        var componentModel = component.GetModelDoc2() as ModelDoc2;
        if (componentModel == null)
        {
            _logger.LogInformation("Component '{ComponentName}' is lightweight, attempting to resolve before renaming instance", componentName);
            var resolveStatus = component.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
            if (resolveStatus != (int)swComponentSuppressionState_e.swComponentFullyResolved)
            {
                return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' could not be resolved. Resolve manually, then retry.", new
                {
                    ComponentName = componentName,
                    ResolveStatus = resolveStatus
                }));
            }

            componentModel = component.GetModelDoc2() as ModelDoc2;
            if (componentModel == null)
            {
                return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' could not be loaded after resolve."));
            }
        }

        var originalTreeName = component.Name2 ?? string.Empty;
        var newNameForInstance = BuildInstanceName(originalTreeName, newInstanceName);

        try
        {
            _logger.LogDebug("Renaming component instance '{Original}' to '{New}'", originalTreeName, newNameForInstance);
            component.Name2 = newNameForInstance;

            var updatedName = component.Name2 ?? string.Empty;
            if (!updatedName.Equals(newNameForInstance, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ExecutionResult.Failure("Component instance rename did not take effect.", new
                {
                    RequestedName = newNameForInstance,
                    CurrentName = updatedName
                }));
            }

            bool saved = false;
            int saveErrors = 0;
            int saveWarnings = 0;
            var hasRenamedDocsBeforeSave = model.Extension.HasRenamedDocuments();

            if (autoSave)
            {
                if (hasRenamedDocsBeforeSave)
                {
                    _logger.LogDebug("Using SaveWithRenamedReferences (hasRenamedDocs=true)");
                    saved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
                }
                else
                {
                    saved = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
                    if (!saved || saveErrors != 0)
                    {
                        _logger.LogWarning(
                            "Save3 failed with error {Error} ({ErrorDecoded}), trying SaveWithRenamedReferences fallback",
                            saveErrors,
                            DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors));
                        saved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
                    }
                }

                if (!saved || saveErrors != 0)
                {
                    return Task.FromResult(ExecutionResult.Failure("Instance renamed but assembly save failed.", new
                    {
                        Renamed = true,
                        SaveSucceeded = saved,
                        SaveErrors = saveErrors,
                        SaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors),
                        SaveWarnings = saveWarnings,
                        HasRenamedDocuments = hasRenamedDocsBeforeSave,
                        Action = "Save the assembly manually to persist the instance name."
                    }));
                }
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Renamed = true,
                OriginalName = originalTreeName,
                NewName = updatedName,
                AutoSave = autoSave,
                Saved = autoSave ? saved : (bool?)null,
                SaveErrors = autoSave ? saveErrors : (int?)null,
                SaveErrorsDecoded = autoSave ? DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors) : null,
                SaveWarnings = autoSave ? saveWarnings : (int?)null,
                Method = "IComponent2.Name2",
                UsedRenamedReferenceSave = autoSave && hasRenamedDocsBeforeSave,
                SaveMethod = autoSave
                    ? (hasRenamedDocsBeforeSave ? "SaveWithRenamedReferences" : "Save3")
                    : null,
                HasRenamedDocuments = hasRenamedDocsBeforeSave
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename component instance '{ComponentName}'", componentName);
            return Task.FromResult(ExecutionResult.Failure($"Failed to rename component instance '{componentName}': {ex.Message}"));
        }
    }

    private static string BuildInstanceName(string originalTreeName, string newInstanceName)
    {
        if (newInstanceName.Contains('<', StringComparison.Ordinal))
        {
            return newInstanceName;
        }

        var suffixIndex = originalTreeName.LastIndexOf('<');
        if (suffixIndex > 0 && originalTreeName.EndsWith(">", StringComparison.Ordinal))
        {
            return $"{newInstanceName}{originalTreeName.Substring(suffixIndex)}";
        }

        return newInstanceName;
    }
}
