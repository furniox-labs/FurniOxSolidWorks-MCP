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

public sealed class DocumentSessionOperations : OperationHandlerBase
{
    public DocumentSessionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentSessionOperations> logger)
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
            var op when op == DocumentOperationNames.CloseModel => CloseModelAsync(parameters),
            var op when op == DocumentOperationNames.ActivateDocument => ActivateDocumentAsync(parameters),
            var op when op == DocumentOperationNames.RebuildModel => RebuildModelAsync(parameters),
            var op when op == DocumentOperationNames.CloseAllDocuments => CloseAllDocumentsAsync(parameters),
            var op when op == DocumentOperationNames.EditUndo => EditUndoAsync(parameters),
            var op when op == DocumentOperationNames.EditRedo => EditRedoAsync(parameters),
            var op when op == DocumentOperationNames.HideDocument => HideDocumentAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown document session operation: {operation}"))
        };
    }

    private Task<ExecutionResult> CloseModelAsync(IDictionary<string, object?> parameters)
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

        var hasUnsavedChanges = model.GetSaveFlag();
        var docTitle = GetStringParam(parameters, "Title");
        var saveOption = GetIntParam(parameters, "SaveOption", 0);

        if (hasUnsavedChanges && saveOption == 1)
        {
            int saveErrors = 0;
            int saveWarnings = 0;
            var saved = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
            if (!saved || saveErrors != 0)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to save before closing (Errors: {saveErrors})"));
            }
        }

        app.CloseDoc(docTitle);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Closed = true,
            HadUnsavedChanges = hasUnsavedChanges,
            SavedBeforeClose = hasUnsavedChanges && saveOption == 1
        }));
    }

    private Task<ExecutionResult> ActivateDocumentAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        if (!parameters.TryGetValue("Title", out var titleObj) || titleObj is not string title)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Title' parameter"));
        }

        int errors = 0;
        var model = (ModelDoc2?)app.ActivateDoc3(title, false, 0, ref errors);
        if (model == null || errors != 0)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to activate document: {title} (Error code: {errors})"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Title = title,
            Activated = true,
            ModelName = model.GetTitle()
        }));
    }

    private Task<ExecutionResult> RebuildModelAsync(IDictionary<string, object?> parameters)
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

        var forceRebuild = GetBoolParam(parameters, "Force", true);
        var result = model.ForceRebuild3(forceRebuild);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Rebuilt = result,
            Forced = forceRebuild
        }));
    }

    private Task<ExecutionResult> CloseAllDocumentsAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var includeUnsaved = GetBoolParam(parameters, "IncludeUnsaved");
        var result = app.CloseAllDocuments(includeUnsaved);

        return result
            ? Task.FromResult(ExecutionResult.SuccessResult(new { ClosedAll = true, IncludedUnsaved = includeUnsaved }))
            : Task.FromResult(ExecutionResult.Failure("Failed to close all documents"));
    }

    private Task<ExecutionResult> EditUndoAsync(IDictionary<string, object?> parameters)
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

        var count = GetIntParam(parameters, "Count", 1);
        if (count < 1)
        {
            return Task.FromResult(ExecutionResult.Failure("Count must be at least 1"));
        }

        model.EditUndo((uint)count);
        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Undone = true,
            Count = count
        }));
    }

    private Task<ExecutionResult> EditRedoAsync(IDictionary<string, object?> parameters)
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

        var count = GetIntParam(parameters, "Count", 1);
        if (count < 1)
        {
            return Task.FromResult(ExecutionResult.Failure("Count must be at least 1"));
        }

        model.EditRedo((uint)count);
        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Redone = true,
            Count = count
        }));
    }

    private Task<ExecutionResult> HideDocumentAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var title = GetStringParam(parameters, "Title");
        ModelDoc2? targetDoc = null;

        if (string.IsNullOrEmpty(title))
        {
            targetDoc = (ModelDoc2?)app.ActiveDoc;
            if (targetDoc == null)
            {
                return Task.FromResult(ExecutionResult.Failure("No active document and no Title specified"));
            }
        }
        else
        {
            var documents = app.GetDocuments().ToObjectArraySafe();
            if (documents != null)
            {
                foreach (var docObj in documents)
                {
                    if (docObj is ModelDoc2 doc && doc.GetTitle().Equals(title, StringComparison.OrdinalIgnoreCase))
                    {
                        targetDoc = doc;
                        break;
                    }
                }
            }

            if (targetDoc == null)
            {
                return Task.FromResult(ExecutionResult.Failure($"Document '{title}' not found in open documents"));
            }
        }

        var wasVisible = targetDoc.Visible;
        var docTitle = targetDoc.GetTitle();
        var docPath = targetDoc.GetPathName();

        if (!wasVisible)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Title = docTitle,
                Path = docPath,
                Hidden = true,
                WasAlreadyHidden = true,
                Note = "Document was already hidden"
            }));
        }

        targetDoc.Visible = false;
        var isNowHidden = !targetDoc.Visible;

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Title = docTitle,
            Path = docPath,
            Hidden = isNowHidden,
            WasAlreadyHidden = false,
            Note = isNowHidden
                ? "Document window closed but document remains loaded in memory. UI resources are NOT released - close document when done."
                : "Failed to hide document"
        }));
    }
}
