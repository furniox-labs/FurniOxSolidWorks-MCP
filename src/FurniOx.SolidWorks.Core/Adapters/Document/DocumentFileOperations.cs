using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

public sealed class DocumentFileOperations : OperationHandlerBase
{
    public DocumentFileOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentFileOperations> logger)
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
            var op when op == DocumentOperationNames.CreateDocument => CreateDocumentAsync(parameters),
            var op when op == DocumentOperationNames.OpenModel => OpenModelAsync(parameters),
            var op when op == DocumentOperationNames.SaveModel => SaveModelAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown document file operation: {operation}"))
        };
    }

    private Task<ExecutionResult> CreateDocumentAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var docType = GetIntParam(parameters, "Type", (int)swDocumentTypes_e.swDocPART);
        if (docType < 1 || docType > 3)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Invalid document type: {docType}. Must be 1 (Part), 2 (Assembly), or 3 (Drawing)"));
        }

        var templatePath = ResolveTemplatePath(app, docType);
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"No template is configured for {(swDocumentTypes_e)docType}. " +
                "Configure the SolidWorks default template in the application or set an explicit template path in appsettings.json."));
        }

        var model = (ModelDoc2?)app.NewDocument(templatePath, 0, 0, 0);
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to create {(swDocumentTypes_e)docType} document"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            ModelName = model.GetTitle(),
            ModelType = ((swDocumentTypes_e)docType).ToString(),
            Type = docType
        }));
    }

    private Task<ExecutionResult> OpenModelAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        if (!parameters.TryGetValue("Path", out var pathObj) || pathObj is not string path)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Path' parameter"));
        }

        if (!IsPathSafe(path, out var openPathError))
        {
            return Task.FromResult(ExecutionResult.Failure($"Invalid path: {openPathError}"));
        }

        var docType = GetIntParam(parameters, "Type", (int)swDocumentTypes_e.swDocPART);
        if (docType < 1 || docType > 3)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Invalid document type: {docType}. Must be 1 (Part), 2 (Assembly), or 3 (Drawing)"));
        }

        var options = GetIntParam(parameters, "Options", (int)swOpenDocOptions_e.swOpenDocOptions_Silent);
        var configuration = GetStringParam(parameters, "Configuration");
        var silent = GetBoolParam(parameters, "Silent", HasFlag(options, swOpenDocOptions_e.swOpenDocOptions_Silent));
        var readOnly = GetBoolParam(parameters, "ReadOnly", HasFlag(options, swOpenDocOptions_e.swOpenDocOptions_ReadOnly));
        var ignoreHiddenComponents = GetBoolParam(parameters, "IgnoreHiddenComponents", HasFlag(options, swOpenDocOptions_e.swOpenDocOptions_DontLoadHiddenComponents));
        var lightWeight = GetBoolParam(parameters, "LightWeight",
            HasFlag(options, swOpenDocOptions_e.swOpenDocOptions_LoadLightweight)
            || HasFlag(options, swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight));
        var visible = GetBoolParam(parameters, "Visible", true);

        var specification = (IDocumentSpecification?)app.GetOpenDocSpec(path);
        if (specification == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to create document specification for: {path}"));
        }

        specification.DocumentType = docType;
        specification.ReadOnly = readOnly;
        specification.Silent = silent;
        specification.ViewOnly = (options & (int)swOpenDocOptions_e.swOpenDocOptions_ViewOnly) != 0;
        specification.IgnoreHiddenComponents = ignoreHiddenComponents;
        specification.LightWeight = lightWeight;

        if (!string.IsNullOrEmpty(configuration))
        {
            specification.ConfigurationName = configuration;
        }

        ModelDoc2? model;
        using (DocumentVisibilityScope.HideNewDocuments(app, !visible, docType))
        using (silent ? new DialogSuppressionScope(app, _logger) : null)
        {
            model = (ModelDoc2?)app.OpenDoc7(specification);
            if (!visible)
            {
                DocumentVisibilityScope.TryHide(model);
            }
        }

        var errors = specification.Error;
        var warnings = specification.Warning;

        if (model == null || errors != 0)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Failed to open model: {path} (Error code: {errors}, Warnings: {warnings})"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            ModelName = model.GetTitle(),
            Path = path,
            Type = docType,
            TypeName = ((swDocumentTypes_e)docType).ToString(),
            Errors = errors,
            Warnings = warnings,
            ConfigurationName = configuration,
            Silent = silent,
            ReadOnly = readOnly,
            IgnoreHiddenComponents = ignoreHiddenComponents,
            LightWeight = lightWeight,
            Visible = SafeBool(() => model.Visible)
        }));
    }

    private Task<ExecutionResult> SaveModelAsync(IDictionary<string, object?> parameters)
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

        var silent = GetBoolParam(parameters, "Silent", true);
        var suppressSaveDialogs = GetBoolParam(parameters, "SuppressSaveDialogs", silent);
        var saveReferences = GetBoolParam(parameters, "SaveReferences", false);
        var forceRebuildBeforeSave = GetBoolParam(parameters, "ForceRebuildBeforeSave", false);
        var includeCleanReferences = GetBoolParam(parameters, "IncludeCleanReferences", false);

        using var suppressionScope = suppressSaveDialogs ? new DialogSuppressionScope(app, _logger) : null;
        var extension = model.Extension;
        var hasRenamedDocs = extension.HasRenamedDocuments();
        var saveMethod = "Save3";
        bool? rebuiltBeforeSave = null;

        if (forceRebuildBeforeSave)
        {
            rebuiltBeforeSave = SafeBool(() => model.ForceRebuild3(false));
        }

        if (parameters.TryGetValue("Path", out var pathObj) && pathObj is string path)
        {
            if (!IsPathSafe(path, out var savePathError))
            {
                return Task.FromResult(ExecutionResult.Failure($"Invalid save path: {savePathError}"));
            }

            int errors = 0;
            int warnings = 0;
            var result = model.Extension.SaveAs3(
                path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                BuildSaveOptions(silent, saveReferences: false),
                null,
                null,
                ref errors,
                ref warnings);

            if (!result || errors != 0)
            {
                return Task.FromResult(ExecutionResult.Failure($"SaveAs failed (Error code: {errors}, Warnings: {warnings})", new
                {
                    ErrorCode = errors,
                    ErrorDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(errors),
                    Warnings = warnings,
                    HasRenamedDocuments = hasRenamedDocs,
                    AutoDismissedDialogCount = suppressionScope?.ClosedDialogCount ?? 0
                }));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Saved = result,
                Path = path,
                Errors = errors,
                Warnings = warnings,
                SaveMethod = "SaveAs3",
                RebuiltBeforeSave = rebuiltBeforeSave,
                ForceRebuildBeforeSave = forceRebuildBeforeSave,
                Silent = silent,
                SuppressSaveDialogs = suppressSaveDialogs,
                AutoDismissedDialogCount = suppressionScope?.ClosedDialogCount ?? 0
            }));
        }

        int saveErrors = 0;
        int saveWarnings = 0;
        bool saved;

        if (hasRenamedDocs)
        {
            _logger.LogDebug("HasRenamedDocuments=true: Using SaveWithRenamedReferences for SaveModel");
            saved = DocumentSaveHelper.SaveWithRenamedReferences(
                model,
                _logger,
                out saveErrors,
                out saveWarnings,
                null,
                suppressSaveDialogs ? app : null);
            saveMethod = "SaveWithRenamedReferences";
        }
        else
        {
            saved = model.Save3(
                BuildSaveOptions(silent, saveReferences),
                ref saveErrors,
                ref saveWarnings);

            if (!saved && (saveErrors & 0x2000) != 0)
            {
                _logger.LogWarning(
                    "Save3 failed with error {Errors} ({ErrorDecoded}), trying SaveWithRenamedReferences fallback",
                    saveErrors,
                    DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors));
                saved = DocumentSaveHelper.SaveWithRenamedReferences(
                    model,
                    _logger,
                    out saveErrors,
                    out saveWarnings,
                    null,
                    suppressSaveDialogs ? app : null);
                saveMethod = "SaveWithRenamedReferences (fallback)";
            }
        }

        if (!saved || saveErrors != 0)
        {
            return Task.FromResult(ExecutionResult.Failure($"Save failed (Errors: {saveErrors}, Warnings: {saveWarnings})", new
            {
                ErrorCode = saveErrors,
                ErrorDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors),
                Warnings = saveWarnings,
                HasRenamedDocuments = hasRenamedDocs,
                SaveMethod = saveMethod,
                RebuiltBeforeSave = rebuiltBeforeSave,
                ForceRebuildBeforeSave = forceRebuildBeforeSave,
                Silent = silent,
                SuppressSaveDialogs = suppressSaveDialogs,
                AutoDismissedDialogCount = suppressionScope?.ClosedDialogCount ?? 0
            }));
        }

        var referenceResults = saveReferences
            ? SaveLoadedReferencedDocuments(
                app,
                model,
                silent,
                suppressSaveDialogs,
                forceRebuildBeforeSave,
                includeCleanReferences)
            : null;

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Saved = saved,
            Errors = saveErrors,
            Warnings = saveWarnings,
            HasRenamedDocuments = hasRenamedDocs,
            SaveMethod = saveMethod,
            References = referenceResults,
            DirtyAfterSave = SafeBool(() => model.GetSaveFlag()),
            RebuiltBeforeSave = rebuiltBeforeSave,
            ForceRebuildBeforeSave = forceRebuildBeforeSave,
            IncludeCleanReferences = includeCleanReferences,
            Silent = silent,
            SuppressSaveDialogs = suppressSaveDialogs,
            AutoDismissedDialogCount = suppressionScope?.ClosedDialogCount ?? 0
        }));
    }

    private static object SaveLoadedReferencedDocuments(
        SldWorks app,
        ModelDoc2 topModel,
        bool silent,
        bool suppressSaveDialogs,
        bool forceRebuildBeforeSave,
        bool includeCleanReferences)
    {
        var topPath = SafeString(() => topModel.GetPathName());
        var saved = new List<object>();
        var skipped = new List<object>();
        var failed = new List<object>();

        foreach (var doc in EnumerateOpenDocuments(app))
        {
            var path = SafeString(() => doc.GetPathName());
            var title = SafeString(() => doc.GetTitle());
            if (string.Equals(path, topPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dirtyBeforeRebuild = SafeBool(() => doc.GetSaveFlag());
            bool? rebuildResult = null;
            if (forceRebuildBeforeSave)
            {
                rebuildResult = SafeBool(() => doc.ForceRebuild3(false));
            }

            var dirtyBeforeSave = SafeBool(() => doc.GetSaveFlag());
            if (!dirtyBeforeSave && !includeCleanReferences && !forceRebuildBeforeSave)
            {
                skipped.Add(new { Title = title, Path = path, Reason = "not dirty" });
                continue;
            }

            if (SafeBool(() => doc.IsOpenedReadOnly()))
            {
                skipped.Add(new { Title = title, Path = path, Reason = "read-only" });
                continue;
            }

            var hasRenamedDocuments = SafeBool(() => doc.Extension.HasRenamedDocuments());
            var saveMethod = hasRenamedDocuments ? "SaveWithRenamedReferences" : "Save3";
            int errors = 0;
            int warnings = 0;
            var ok = hasRenamedDocuments
                ? DocumentSaveHelper.SaveWithRenamedReferences(
                    doc,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                    out errors,
                    out warnings,
                    null,
                    suppressSaveDialogs ? app : null)
                : doc.Save3(BuildSaveOptions(silent, saveReferences: false), ref errors, ref warnings);

            if ((!ok || errors != 0) && (errors & 0x2000) != 0)
            {
                saveMethod = "SaveWithRenamedReferences (fallback)";
                ok = DocumentSaveHelper.SaveWithRenamedReferences(
                    doc,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                    out errors,
                    out warnings,
                    null,
                    suppressSaveDialogs ? app : null);
            }

            var result = new
            {
                Title = title,
                Path = path,
                SaveMethod = saveMethod,
                HasRenamedDocuments = hasRenamedDocuments,
                DirtyBeforeRebuild = dirtyBeforeRebuild,
                RebuildResult = rebuildResult,
                DirtyBeforeSave = dirtyBeforeSave,
                DirtyAfterSave = SafeBool(() => doc.GetSaveFlag()),
                Errors = errors,
                ErrorDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(errors),
                Warnings = warnings
            };

            if (ok && errors == 0)
            {
                saved.Add(result);
            }
            else
            {
                failed.Add(result);
            }
        }

        return new
        {
            SavedCount = saved.Count,
            SkippedCount = skipped.Count,
            FailedCount = failed.Count,
            Saved = saved,
            Skipped = skipped,
            Failed = failed
        };
    }

    private static IReadOnlyList<ModelDoc2> EnumerateOpenDocuments(SldWorks app)
    {
        try
        {
            return app.GetDocuments().ToObjectArraySafe()?
                .OfType<ModelDoc2>()
                .ToArray() ?? Array.Empty<ModelDoc2>();
        }
        catch
        {
            return Array.Empty<ModelDoc2>();
        }
    }

    private static int BuildSaveOptions(bool silent, bool saveReferences)
    {
        var options = 0;
        if (silent)
        {
            options |= (int)swSaveAsOptions_e.swSaveAsOptions_Silent;
        }

        if (saveReferences)
        {
            options |= (int)swSaveAsOptions_e.swSaveAsOptions_SaveReferenced;
        }

        return options;
    }

    private static bool HasFlag(int value, swOpenDocOptions_e flag)
        => (value & (int)flag) != 0;

    private static bool SafeBool(Func<bool> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return false;
        }
    }

    private static string SafeString(Func<string?> read, string fallback = "")
    {
        try
        {
            return read() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private string? ResolveTemplatePath(SldWorks app, int docType)
    {
        var configuredPath = docType switch
        {
            (int)swDocumentTypes_e.swDocPART => _settings.GetPartTemplatePath(),
            (int)swDocumentTypes_e.swDocASSEMBLY => _settings.GetAssemblyTemplatePath(),
            (int)swDocumentTypes_e.swDocDRAWING => _settings.GetDrawingTemplatePath(),
            _ => null
        };

        if (TryUseTemplatePath(configuredPath, out var resolvedConfiguredPath))
        {
            return resolvedConfiguredPath;
        }

        var defaultPreference = docType switch
        {
            (int)swDocumentTypes_e.swDocPART => swUserPreferenceStringValue_e.swDefaultTemplatePart,
            (int)swDocumentTypes_e.swDocASSEMBLY => swUserPreferenceStringValue_e.swDefaultTemplateAssembly,
            (int)swDocumentTypes_e.swDocDRAWING => swUserPreferenceStringValue_e.swDefaultTemplateDrawing,
            _ => (swUserPreferenceStringValue_e?)null
        };

        if (defaultPreference.HasValue)
        {
            try
            {
                var preferredTemplate = app.GetUserPreferenceStringValue((int)defaultPreference.Value);
                if (TryUseTemplatePath(preferredTemplate, out var resolvedPreferencePath))
                {
                    return resolvedPreferencePath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to query SolidWorks default template preference for document type {DocType}", docType);
            }
        }

        return null;
    }

    private bool TryUseTemplatePath(string? templatePath, out string? resolvedPath)
    {
        resolvedPath = null;

        if (string.IsNullOrWhiteSpace(templatePath))
        {
            return false;
        }

        if (!System.IO.File.Exists(templatePath))
        {
            _logger.LogWarning("Configured SolidWorks template path does not exist: {TemplatePath}", templatePath);
            return false;
        }

        resolvedPath = templatePath;
        return true;
    }
}
