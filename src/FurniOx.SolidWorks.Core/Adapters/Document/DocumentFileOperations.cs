using System;
using System.Collections.Generic;
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
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

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
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        if (!parameters.TryGetValue("Path", out var pathObj) || pathObj is not string path)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Path' parameter"));
        }

        if (!IsPathSafe(path, out var openPathError))
            return Task.FromResult(ExecutionResult.Failure($"Invalid path: {openPathError}"));

        var docType = GetIntParam(parameters, "Type", (int)swDocumentTypes_e.swDocPART);
        if (docType < 1 || docType > 3)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Invalid document type: {docType}. Must be 1 (Part), 2 (Assembly), or 3 (Drawing)"));
        }

        var options = GetIntParam(parameters, "Options", (int)swOpenDocOptions_e.swOpenDocOptions_Silent);
        var configuration = GetStringParam(parameters, "Configuration");

        var specification = (IDocumentSpecification?)app.GetOpenDocSpec(path);
        if (specification == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to create document specification for: {path}"));
        }

        specification.DocumentType = docType;
        specification.ReadOnly = (options & (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly) != 0;
        specification.Silent = (options & (int)swOpenDocOptions_e.swOpenDocOptions_Silent) != 0;
        specification.ViewOnly = (options & (int)swOpenDocOptions_e.swOpenDocOptions_ViewOnly) != 0;

        if (!string.IsNullOrEmpty(configuration))
        {
            specification.ConfigurationName = configuration;
        }

        var model = (ModelDoc2?)app.OpenDoc7(specification);
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
            ConfigurationName = configuration
        }));
    }

    private Task<ExecutionResult> SaveModelAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return Task.FromResult(ExecutionResult.Failure("No active document"));

        var extension = model.Extension;
        var hasRenamedDocs = extension.HasRenamedDocuments();
        var saveMethod = "Save3";

        if (parameters.TryGetValue("Path", out var pathObj) && pathObj is string path)
        {
            if (!IsPathSafe(path, out var savePathError))
                return Task.FromResult(ExecutionResult.Failure($"Invalid save path: {savePathError}"));

            int errors = 0;
            int warnings = 0;
            var result = model.Extension.SaveAs3(
                path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
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
                    HasRenamedDocuments = hasRenamedDocs
                }));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Saved = result,
                Path = path,
                Errors = errors,
                Warnings = warnings,
                SaveMethod = "SaveAs3"
            }));
        }

        int saveErrors = 0;
        int saveWarnings = 0;
        bool saved;

        if (hasRenamedDocs)
        {
            _logger.LogDebug("HasRenamedDocuments=true: Using SaveWithRenamedReferences for SaveModel");
            saved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
            saveMethod = "SaveWithRenamedReferences";
        }
        else
        {
            saved = model.Save3(
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                ref saveErrors,
                ref saveWarnings);

            if (!saved && (saveErrors & 0x2000) != 0)
            {
                _logger.LogWarning(
                    "Save3 failed with error {Errors} ({ErrorDecoded}), trying SaveWithRenamedReferences fallback",
                    saveErrors,
                    DocumentSaveHelper.DecodeSaveErrorBitmask(saveErrors));
                saved = DocumentSaveHelper.SaveWithRenamedReferences(model, _logger, out saveErrors, out saveWarnings);
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
                SaveMethod = saveMethod
            }));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Saved = saved,
            Errors = saveErrors,
            Warnings = saveWarnings,
            HasRenamedDocuments = hasRenamedDocs,
            SaveMethod = saveMethod
        }));
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
