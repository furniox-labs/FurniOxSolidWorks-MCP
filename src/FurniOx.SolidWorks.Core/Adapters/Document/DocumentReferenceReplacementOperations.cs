using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Single-document referenced-doc rewire via ISldWorks.ReplaceReferencedDocument.
/// Requires the referencing document to be CLOSED — guarded explicitly so the
/// caller gets a clear error instead of a silent no-op when SW already has the
/// file loaded. Batch sibling lives in DocumentReferenceReplacementBatchOperations.
/// </summary>
public sealed class DocumentReferenceReplacementOperations : OperationHandlerBase
{
    public DocumentReferenceReplacementOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentReferenceReplacementOperations> logger)
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
            DocumentReferenceReplacementOperationNames.ReplaceReferencedDocument
                => ReplaceSingleAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown reference replacement operation: {operation}"))
        };
    }

    private Task<ExecutionResult> ReplaceSingleAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var referencingDoc = GetStringParam(parameters, "ReferencingDocPath");
        var oldRef = GetStringParam(parameters, "OldRefPath");
        var newRef = GetStringParam(parameters, "NewRefPath");

        if (!DocumentReferenceReplacementShared.ValidatePaths(referencingDoc, oldRef, newRef, out var validationError))
        {
            return Task.FromResult(ExecutionResult.Failure(validationError!));
        }

        var openTitles = DocumentReferenceReplacementShared.SnapshotOpenDocuments(app);
        if (!DocumentReferenceReplacementShared.EnforceClosed(openTitles, new[] { referencingDoc, oldRef }, out var closedError))
        {
            return Task.FromResult(closedError!);
        }

        if (!File.Exists(referencingDoc))
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"ReferencingDocPath does not exist on disk: {referencingDoc}"));
        }
        if (!File.Exists(newRef))
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"NewRefPath does not exist on disk: {newRef}. Rename the child first, then call this."));
        }

        var (ok, error) = DocumentReferenceReplacementShared.TryReplace(app, referencingDoc, oldRef, newRef, _logger);
        if (!ok)
        {
            return Task.FromResult(ExecutionResult.Failure(error!, new
            {
                ReferencingDoc = referencingDoc,
                OldRef = oldRef,
                NewRef = newRef
            }));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Replaced = true,
            ReferencingDoc = referencingDoc,
            OldRef = oldRef,
            NewRef = newRef
        }));
    }
}
