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

namespace FurniOx.SolidWorks.Core.Adapters.Document;

public sealed class DocumentComponentFileRenameOperations : OperationHandlerBase
{
    public DocumentComponentFileRenameOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentComponentFileRenameOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return RenameComponentFileAsync(parameters);
    }

    private Task<ExecutionResult> RenameComponentFileAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return Task.FromResult(ExecutionResult.Failure("No active document"));
        if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return Task.FromResult(ExecutionResult.Failure("Active document must be an assembly to rename component files"));
        }

        var assembly = (IAssemblyDoc)model;

        if (!parameters.TryGetValue("ComponentName", out var compNameObj) || compNameObj is not string componentName || string.IsNullOrWhiteSpace(componentName))
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'ComponentName' parameter. Provide component name from analyze_assembly (e.g., 'Part1-1')"));
        }

        if (!parameters.TryGetValue("NewName", out var newNameObj) || newNameObj is not string newName || string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'NewName' parameter. Provide filename with extension (e.g., 'NewPart.SLDPRT')"));
        }

        var autoSave = GetBoolParam(parameters, "AutoSave", true);
        var component = (IComponent2?)assembly.GetComponentByName(componentName);
        if (component == null)
        {
            var availableComponents = new List<string>();
            if (assembly.GetComponents(false) is Array componentArray)
            {
                foreach (var compObj in componentArray)
                {
                    if (compObj is IComponent2 comp)
                    {
                        availableComponents.Add(comp.Name2 ?? string.Empty);
                    }
                }
            }

            return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' not found in assembly", new
            {
                RequestedName = componentName,
                AvailableComponents = availableComponents.Take(20).ToList(),
                TotalComponentCount = availableComponents.Count,
                Hint = "Use the Name field from analyze_assembly results (e.g., 'Part1-1', not 'Part1-1@Assembly')"
            }));
        }

        var componentModel = component.GetModelDoc2() as ModelDoc2;
        if (componentModel == null)
        {
            _logger.LogInformation("Component '{ComponentName}' is lightweight, attempting to resolve", componentName);
            var resolveStatus = component.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
            if (resolveStatus != (int)swComponentSuppressionState_e.swComponentFullyResolved)
            {
                return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' is lightweight and could not be resolved. Try resolving manually first.", new
                {
                    ComponentName = componentName,
                    ResolveStatus = resolveStatus,
                    Action = "Right-click component in SolidWorks and select 'Set to Resolved'"
                }));
            }

            componentModel = component.GetModelDoc2() as ModelDoc2;
            if (componentModel == null)
            {
                return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' could not be loaded into memory even after resolving"));
            }
        }

        if (component.IsSuppressed())
        {
            return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' is suppressed. Unsuppress it first.", new
            {
                ComponentName = componentName,
                IsSuppressed = true,
                Action = "Right-click component in SolidWorks and select 'Unsuppress'"
            }));
        }

        var originalPath = component.GetPathName();
        var originalTreeName = component.Name2 ?? string.Empty;
        if (string.IsNullOrEmpty(originalPath))
        {
            return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' has no file path. It may be a virtual component."));
        }

        var expectedExtension = DocumentRenameSupport.GetExpectedExtension(DocumentRenameSupport.GetDocumentTypeFromPath(originalPath));
        if (string.IsNullOrEmpty(expectedExtension))
        {
            return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' has unknown file type: {originalPath}"));
        }

        if (!DocumentRenameSupport.TryNormalizeNewName(newName, expectedExtension, "this component", out var baseNameForApi, out var nameError))
        {
            return Task.FromResult(ExecutionResult.Failure(nameError!));
        }

        var originalExtRefUpdateSetting = false;
        var settingWasChanged = false;

        try
        {
            originalExtRefUpdateSetting = app.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames);
            if (originalExtRefUpdateSetting)
            {
                app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, false);
                settingWasChanged = true;
            }

            app.CommandInProgress = true;
            model.ClearSelection2(true);

            var selMgr = (ISelectionMgr?)model.SelectionManager;
            var selectData = (SelectData?)selMgr?.CreateSelectData();
            if (selectData != null)
            {
                selectData.Mark = 0;
            }

            if (!component.Select4(false, selectData, false))
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to select component '{componentName}'. Select4 returned false.", new
                {
                    ComponentName = componentName,
                    ComponentPath = originalPath,
                    Hint = "Component may be hidden, locked, or in an incompatible state"
                }));
            }

            if ((selMgr?.GetSelectedObjectCount2(-1) ?? 0) == 0)
            {
                return Task.FromResult(ExecutionResult.Failure("Select4 returned true but selection count is 0. Component selection failed."));
            }

            var renameErrorCode = model.Extension.RenameDocument(baseNameForApi);
            var renameError = (swRenameDocumentError_e)renameErrorCode;
            if (renameError != swRenameDocumentError_e.swRenameDocumentError_None)
            {
                return Task.FromResult(ExecutionResult.Failure($"RenameDocument failed: {DocumentRenameSupport.GetRenameErrorMessage(renameError)}", new
                {
                    ErrorCode = renameErrorCode,
                    ErrorName = renameError.ToString(),
                    ComponentName = componentName,
                    OriginalPath = originalPath,
                    AttemptedNewName = newName
                }));
            }

            var hasRenamedDocs = model.Extension.HasRenamedDocuments();
            var newTreeName = component.Name2 ?? string.Empty;
            var newComponentPath = component.GetPathName();

            if (newTreeName.Equals(originalTreeName, StringComparison.OrdinalIgnoreCase)
                && newComponentPath.Equals(originalPath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "RenameDocument returned success but component name/path unchanged. This can happen for read-only, PDM, Toolbox, or patterned components.",
                    new
                    {
                        RenameReturnCode = renameErrorCode,
                        OriginalTreeName = originalTreeName,
                        NewTreeName = newTreeName,
                        OriginalPath = originalPath,
                        NewPath = newComponentPath,
                        ComponentName = componentName
                    }));
            }

            if (!autoSave)
            {
                return Task.FromResult(ExecutionResult.SuccessResult(new
                {
                    Renamed = true,
                    ComponentName = componentName,
                    OriginalPath = originalPath,
                    NewPath = newComponentPath,
                    NewName = newName,
                    ParentAssembly = model.GetTitle(),
                    Saved = false,
                    TreeNameBefore = originalTreeName,
                    TreeNameAfter = newTreeName,
                    Method = "Atomic (Select4 + RenameDocument in single operation)",
                    Warning = "AutoSave=false: Rename is IN MEMORY ONLY. Call SaveModel on component and assembly to persist.",
                    Action = "Save component first, then save assembly with SaveReferenced option"
                }));
            }

            bool componentSaved = false;
            int componentErrors = 0;
            int componentWarnings = 0;

            componentModel = component.GetModelDoc2() as ModelDoc2;
            if (componentModel != null)
            {
                componentSaved = componentModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref componentErrors, ref componentWarnings);
                if (!componentSaved || componentErrors != 0)
                {
                    return Task.FromResult(ExecutionResult.Failure(
                        $"Component renamed in memory but save failed. Error: {DocumentSaveHelper.FormatSaveError(componentErrors)}. The file still exists with the original name on disk.", new
                        {
                            RenameSucceeded = true,
                            ComponentSaveSucceeded = false,
                            ComponentSaveErrors = componentErrors,
                            ComponentSaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(componentErrors),
                            ComponentSaveWarnings = componentWarnings,
                            OriginalPath = originalPath,
                            NewName = newName,
                            Action = "Try saving the component manually in SolidWorks"
                        }));
                }
            }

            int assemblyErrors = 0;
            int assemblyWarnings = 0;
            var assemblySaved = hasRenamedDocs
                ? DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out assemblyErrors, out assemblyWarnings)
                : model.Save3(
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_SaveReferenced,
                    ref assemblyErrors,
                    ref assemblyWarnings);

            if ((!assemblySaved || assemblyErrors != 0) && !hasRenamedDocs)
            {
                assemblySaved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out assemblyErrors, out assemblyWarnings);
            }

            if (!assemblySaved || assemblyErrors != 0)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    $"Component renamed and saved, but assembly save failed. Error: {DocumentSaveHelper.FormatSaveError(assemblyErrors)}. Assembly references may not be updated on disk.", new
                    {
                        RenameSucceeded = true,
                        ComponentSaveSucceeded = componentSaved,
                        AssemblySaveSucceeded = false,
                        AssemblySaveErrors = assemblyErrors,
                        AssemblySaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(assemblyErrors),
                        OriginalPath = originalPath,
                        NewPath = newComponentPath,
                        Action = "Save the assembly manually in SolidWorks"
                    }));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Renamed = true,
                ComponentName = componentName,
                OriginalPath = originalPath,
                NewPath = component.GetPathName(),
                NewName = newName,
                ParentAssembly = model.GetTitle(),
                ComponentSaved = componentSaved,
                ComponentSaveErrors = componentErrors,
                AssemblySaved = assemblySaved,
                AssemblySaveErrors = assemblyErrors,
                TreeNameBefore = originalTreeName,
                TreeNameAfter = newTreeName,
                Method = "Atomic (Select4 + RenameDocument + Save in single operation)",
                Note = "Component file renamed and all saves completed successfully"
            }));
        }
        finally
        {
            app.CommandInProgress = false;
            model.ClearSelection2(true);

            if (settingWasChanged)
            {
                app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, originalExtRefUpdateSetting);
            }
        }
    }
}
