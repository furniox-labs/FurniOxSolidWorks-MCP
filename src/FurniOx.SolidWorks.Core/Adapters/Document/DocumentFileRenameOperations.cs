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

public sealed class DocumentFileRenameOperations : OperationHandlerBase
{
    public DocumentFileRenameOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentFileRenameOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return RenameDocumentAsync(parameters);
    }

    private Task<ExecutionResult> RenameDocumentAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return Task.FromResult(ExecutionResult.Failure("No active document"));

        if (!parameters.TryGetValue("NewName", out var newNameObj) || newNameObj is not string newName || string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'NewName' parameter. Provide filename with extension (e.g., 'NewPart.SLDPRT')"));
        }

        var autoSave = GetBoolParam(parameters, "AutoSave", true);

        var selMgr = (ISelectionMgr?)model.SelectionManager;
        IComponent2? selectedComponent = null;
        string? componentOriginalPath = null;
        string? componentOriginalName = null;
        var isComponentRename = false;

        if (selMgr != null && selMgr.GetSelectedObjectCount2(-1) > 0)
        {
            var selectionType = (swSelectType_e)selMgr.GetSelectedObjectType3(1, -1);
            if (selectionType == swSelectType_e.swSelCOMPONENTS)
            {
                selectedComponent = selMgr.GetSelectedObject6(1, -1) as IComponent2;
                if (selectedComponent != null)
                {
                    isComponentRename = true;
                    componentOriginalPath = selectedComponent.GetPathName();
                    componentOriginalName = selectedComponent.Name2;
                    _logger.LogInformation("Component selected for rename: {ComponentName}, Path: {Path}", componentOriginalName, componentOriginalPath);
                }
            }
        }

        var currentPath = isComponentRename && !string.IsNullOrEmpty(componentOriginalPath)
            ? componentOriginalPath
            : model.GetPathName();
        var docType = isComponentRename && !string.IsNullOrEmpty(componentOriginalPath)
            ? DocumentRenameSupport.GetDocumentTypeFromPath(componentOriginalPath)
            : ((IModelDoc2)model).GetType();

        if (string.IsNullOrEmpty(currentPath))
        {
            return Task.FromResult(ExecutionResult.Failure("Document has never been saved. Save the document first before renaming."));
        }

        if (docType == (int)swDocumentTypes_e.swDocDRAWING)
        {
            return Task.FromResult(ExecutionResult.Failure("Drawings cannot be renamed with RenameDocument. Use SaveAs instead."));
        }

        var expectedExtension = DocumentRenameSupport.GetExpectedExtension(docType);
        if (!DocumentRenameSupport.TryNormalizeNewName(newName, expectedExtension, $"{(swDocumentTypes_e)docType} documents", out var baseNameForApi, out var nameError))
        {
            return Task.FromResult(ExecutionResult.Failure($"{nameError} Provided: {newName}"));
        }

        var originalTitle = isComponentRename ? componentOriginalName : model.GetTitle();
        var originalPath = currentPath;
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

            var extension = model.Extension;
            var errorCode = extension.RenameDocument(baseNameForApi);
            var renameError = (swRenameDocumentError_e)errorCode;
            if (renameError != swRenameDocumentError_e.swRenameDocumentError_None)
            {
                return Task.FromResult(ExecutionResult.Failure($"RenameDocument failed: {DocumentRenameSupport.GetRenameErrorMessage(renameError)}", new
                {
                    ErrorCode = errorCode,
                    ErrorName = renameError.ToString(),
                    OriginalPath = originalPath,
                    AttemptedNewName = newName
                }));
            }

            var hasRenamedDocs = extension.HasRenamedDocuments();
            bool componentSaved = false;
            bool assemblySaved = false;
            int saveErrors = 0;
            int saveWarnings = 0;
            int componentSaveErrors = 0;
            int componentSaveWarnings = 0;

            if (autoSave)
            {
                if (isComponentRename && selectedComponent != null)
                {
                    var componentModel = selectedComponent.GetModelDoc2() as ModelDoc2;

                    if (hasRenamedDocs)
                    {
                        assemblySaved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
                        if (!assemblySaved || saveErrors != 0)
                        {
                            return Task.FromResult(ExecutionResult.Failure($"Assembly save with renamed references failed. Error: {DocumentSaveHelper.FormatSaveError(saveErrors)}", new
                            {
                                RenameSucceeded = true,
                                AssemblySaveSucceeded = false,
                                SaveErrors = saveErrors,
                                SaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors),
                                SaveWarnings = saveWarnings,
                                HasRenamedDocuments = hasRenamedDocs,
                                OriginalPath = originalPath,
                                NewName = newName,
                                Action = "Try Ctrl+Shift+S in SolidWorks to save all documents"
                            }));
                        }

                        if (componentModel != null)
                        {
                            componentSaved = componentModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref componentSaveErrors, ref componentSaveWarnings);
                        }
                    }
                    else
                    {
                        if (componentModel != null)
                        {
                            componentSaved = componentModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref componentSaveErrors, ref componentSaveWarnings);
                            if (!componentSaved || componentSaveErrors != 0)
                            {
                                assemblySaved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
                                if (assemblySaved && saveErrors == 0)
                                {
                                    componentSaved = componentModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref componentSaveErrors, ref componentSaveWarnings);
                                }

                                if (!assemblySaved || saveErrors != 0)
                                {
                                    return Task.FromResult(ExecutionResult.Failure($"Rename succeeded but save failed. Errors: {componentSaveErrors}", new
                                    {
                                        RenameSucceeded = true,
                                        ComponentSaveSucceeded = false,
                                        ComponentSaveErrors = componentSaveErrors,
                                        ComponentSaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(componentSaveErrors),
                                        ComponentSaveWarnings = componentSaveWarnings,
                                        OriginalPath = originalPath,
                                        NewName = newName,
                                        Action = "Try Ctrl+Shift+S in SolidWorks to save all documents"
                                    }));
                                }
                            }
                        }

                        if (!assemblySaved)
                        {
                            assemblySaved = model.Save3(
                                (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_SaveReferenced,
                                ref saveErrors,
                                ref saveWarnings);

                            if (!assemblySaved || saveErrors != 0)
                            {
                                assemblySaved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
                            }
                        }
                    }

                    if (!assemblySaved || saveErrors != 0)
                    {
                        return Task.FromResult(ExecutionResult.Failure($"Component renamed, but assembly save failed. Error: {DocumentSaveHelper.FormatSaveError(saveErrors)}", new
                        {
                            RenameSucceeded = true,
                            ComponentSaveSucceeded = componentSaved,
                            AssemblySaveSucceeded = false,
                            SaveErrors = saveErrors,
                            SaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors),
                            SaveWarnings = saveWarnings,
                            HasRenamedDocuments = hasRenamedDocs,
                            OriginalPath = originalPath,
                            NewName = newName,
                            Action = "Save the parent assembly manually to persist reference updates"
                        }));
                    }
                }
                else
                {
                    assemblySaved = hasRenamedDocs
                        ? DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings)
                        : model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);

                    if ((!assemblySaved || saveErrors != 0) && !hasRenamedDocs)
                    {
                        assemblySaved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
                    }

                    if (!assemblySaved || saveErrors != 0)
                    {
                        return Task.FromResult(ExecutionResult.Failure($"Rename succeeded in memory but save failed. File still exists with original name. Save errors: {saveErrors}", new
                        {
                            RenameSucceeded = true,
                            SaveSucceeded = false,
                            SaveErrors = saveErrors,
                            SaveErrorsDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors),
                            SaveWarnings = saveWarnings,
                            HasRenamedDocuments = hasRenamedDocs,
                            OriginalPath = originalPath,
                            NewName = newName,
                            Action = "Call SaveModel manually to persist the rename"
                        }));
                    }
                }
            }

            var newPath = isComponentRename && selectedComponent != null ? selectedComponent.GetPathName() : model.GetPathName();
            var saved = isComponentRename ? (componentSaved && assemblySaved) : assemblySaved;

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Renamed = true,
                IsComponentRename = isComponentRename,
                ComponentName = isComponentRename ? componentOriginalName : null,
                OriginalTitle = originalTitle,
                OriginalPath = originalPath,
                NewName = newName,
                NewPath = newPath,
                ParentAssembly = isComponentRename ? model.GetTitle() : null,
                Saved = saved,
                ComponentSaved = isComponentRename ? componentSaved : (bool?)null,
                ComponentSaveErrors = isComponentRename ? componentSaveErrors : (int?)null,
                ComponentSaveErrorsDecoded = isComponentRename ? DocumentSaveHelper.DecodeSaveErrorBitmask(componentSaveErrors) : null,
                AssemblySaved = assemblySaved,
                SaveErrors = saveErrors,
                SaveErrorsDecoded = autoSave ? DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors) : null,
                SaveWarnings = saveWarnings,
                HasRenamedDocuments = hasRenamedDocs,
                SaveMethod = autoSave ? (hasRenamedDocs ? "SaveWithRenamedReferences" : "Save3") : null,
                ReferencesUpdated = true,
                Note = isComponentRename
                    ? $"Component '{componentOriginalName}' renamed and saved. Parent assembly '{model.GetTitle()}' saved with {(hasRenamedDocs ? "SaveWithRenamedReferences" : "SaveReferenced")} to update references."
                    : (autoSave
                        ? "Rename complete. Open assemblies referencing this file have been updated in memory - save them to persist reference changes."
                        : "Rename complete IN MEMORY ONLY. Call SaveModel to persist the new filename to disk.")
            }));
        }
        finally
        {
            app.CommandInProgress = false;
            if (settingWasChanged)
            {
                app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, originalExtRefUpdateSetting);
            }
        }
    }
}
