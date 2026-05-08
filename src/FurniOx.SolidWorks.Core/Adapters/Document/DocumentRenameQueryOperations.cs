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
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Read-only governance queries: detect documents whose in-memory path differs
/// from disk (HasRenamedDocuments / dirty-no-disk-path), and detect SolidWorks
/// files on disk that the active root assembly does not reference.
/// </summary>
public sealed class DocumentRenameQueryOperations : OperationHandlerBase
{
    public DocumentRenameQueryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentRenameQueryOperations> logger)
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
            DocumentGovernanceOperationNames.GetRenamedDocuments => GetRenamedDocumentsAsync(parameters),
            DocumentGovernanceOperationNames.DetectOrphanFiles => DetectOrphanFilesAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown rename query operation: {operation}"))
        };
    }

    private Task<ExecutionResult> GetRenamedDocumentsAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var includeAllOpenDocuments = GetBoolParam(parameters, "IncludeAllOpenDocuments", false);
        var documents = new List<object>();
        var pendingComponents = new List<object>();

        foreach (var doc in DocumentRenamePathHelpers.EnumerateOpenDocuments(app))
        {
            var path = doc.GetPathName() ?? string.Empty;
            var hasRenamedDocuments = DocumentRenamePathHelpers.SafeBool(() => doc.Extension.HasRenamedDocuments());
            var pathExists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            var dirty = DocumentRenamePathHelpers.SafeBool(() => doc.GetSaveFlag());

            if (includeAllOpenDocuments || hasRenamedDocuments || (dirty && !pathExists))
            {
                documents.Add(new
                {
                    Title = doc.GetTitle() ?? string.Empty,
                    Path = path,
                    Type = ((IModelDoc2)doc).GetType(),
                    HasRenamedDocuments = hasRenamedDocuments,
                    HasUnsavedChanges = dirty,
                    PathExistsOnDisk = pathExists
                });
            }

            if (((IModelDoc2)doc).GetType() == (int)swDocumentTypes_e.swDocASSEMBLY && doc is IAssemblyDoc assembly)
            {
                foreach (var occurrence in DocumentRenamePathHelpers.BuildComponentOccurrences(assembly))
                {
                    var componentPathExists = !string.IsNullOrWhiteSpace(occurrence.Path) && File.Exists(occurrence.Path);
                    if (hasRenamedDocuments || !componentPathExists)
                    {
                        pendingComponents.Add(new
                        {
                            ParentTitle = doc.GetTitle() ?? string.Empty,
                            occurrence.InstancePath,
                            occurrence.Path,
                            PathExistsOnDisk = componentPathExists,
                            Signal = !componentPathExists ? "ComponentPathMissingOnDisk" : "ParentHasRenamedDocuments"
                        });
                    }
                }
            }
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            DocumentCount = documents.Count,
            PendingComponentCount = pendingComponents.Count,
            Documents = documents,
            PendingComponents = pendingComponents,
            Note = "SolidWorks does not expose original load paths retroactively; this reports HasRenamedDocuments plus in-memory component paths that do not exist on disk."
        }));
    }

    private Task<ExecutionResult> DetectOrphanFilesAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return Task.FromResult(ExecutionResult.Failure("No active document"));

        var rootPath = model.GetPathName();
        var folderPath = GetStringParam(parameters, "FolderPath");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            folderPath = Path.GetDirectoryName(rootPath) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return Task.FromResult(ExecutionResult.Failure($"Folder not found: {folderPath}"));
        }

        var recursive = GetBoolParam(parameters, "Recursive", false);
        var includeDrawings = GetBoolParam(parameters, "IncludeDrawings", true);
        var referenced = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        DocumentRenamePathHelpers.AddIfPath(referenced, rootPath);
        if (((IModelDoc2)model).GetType() == (int)swDocumentTypes_e.swDocASSEMBLY && model is IAssemblyDoc assembly)
        {
            foreach (var occurrence in DocumentRenamePathHelpers.BuildComponentOccurrences(assembly))
            {
                DocumentRenamePathHelpers.AddIfPath(referenced, occurrence.Path);
            }
        }

        var allowedExtensions = includeDrawings
            ? new System.Collections.Generic.HashSet<string>(new[] { ".SLDPRT", ".SLDASM", ".SLDDRW" }, System.StringComparer.OrdinalIgnoreCase)
            : new System.Collections.Generic.HashSet<string>(new[] { ".SLDPRT", ".SLDASM" }, System.StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(
                folderPath,
                "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(path => allowedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !Path.GetFileName(path).StartsWith("~$", System.StringComparison.Ordinal))
            .Select(DocumentRenamePathHelpers.NormalizePath)
            .ToArray();

        var orphans = files
            .Where(path => !referenced.Contains(path))
            .OrderBy(path => path, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            RootPath = rootPath,
            FolderPath = Path.GetFullPath(folderPath),
            Recursive = recursive,
            IncludeDrawings = includeDrawings,
            DiskFileCount = files.Length,
            ReferencedFileCount = referenced.Count,
            OrphanCount = orphans.Length,
            Orphans = orphans
        }));
    }
}
