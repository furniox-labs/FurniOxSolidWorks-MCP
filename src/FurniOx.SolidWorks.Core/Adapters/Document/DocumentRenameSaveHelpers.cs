#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FurniOx.SolidWorks.Core.Connection;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Save, snapshot, and opened-document cleanup helpers extracted from
/// DocumentRenameBatchOperations.
/// </summary>
internal static class DocumentRenameSaveHelpers
{
    /// <summary>Records all currently-open document paths and titles.</summary>
    internal static OpenDocumentSnapshot SnapshotOpenDocuments(SldWorks app)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in DocumentRenamePathHelpers.EnumerateOpenDocuments(app))
        {
            DocumentRenamePathHelpers.AddSnapshotPath(paths, doc.GetPathName());
            var title = doc.GetTitle();
            if (!string.IsNullOrWhiteSpace(title))
            {
                titles.Add(title);
            }
        }

        return new OpenDocumentSnapshot(paths, titles);
    }

    /// <summary>
    /// Saves each renamed child document that is open, dirty, or has pending
    /// renamed references.
    /// </summary>
    internal static SaveCollectionResult SaveRenamedChildren(
        SldWorks app,
        IReadOnlyList<AppliedRename> applied,
        IReadOnlyList<string> searchFolders,
        bool suppressSaveDialogs,
        ILogger logger)
    {
        var saved = new List<object>();
        var skipped = new List<object>();
        var failed = new List<object>();
        var documents = DocumentRenamePathHelpers.EnumerateOpenDocuments(app);

        foreach (var rename in applied
                     .GroupBy(item => DocumentRenamePathHelpers.NormalizePath(item.NewPath), StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.Last()))
        {
            var doc = documents.FirstOrDefault(candidate => DocumentRenamePathHelpers.SamePath(candidate.GetPathName(), rename.NewPath))
                ?? documents.FirstOrDefault(candidate => DocumentRenamePathHelpers.SamePath(candidate.GetPathName(), rename.OldPath));

            if (doc == null)
            {
                skipped.Add(new
                {
                    rename.OldPath,
                    rename.NewPath,
                    rename.InstancePath,
                    Reason = "document not open"
                });
                continue;
            }

            var title = doc.GetTitle() ?? string.Empty;
            var path = doc.GetPathName() ?? string.Empty;
            var hasRenamedDocuments = DocumentRenamePathHelpers.SafeBool(() => doc.Extension.HasRenamedDocuments());
            var dirty = DocumentRenamePathHelpers.SafeBool(() => doc.GetSaveFlag());

            if (!dirty && !hasRenamedDocuments)
            {
                skipped.Add(new
                {
                    Title = title,
                    Path = path,
                    rename.InstancePath,
                    Reason = "not dirty and no pending renamed references"
                });
                continue;
            }

            if (DocumentRenamePathHelpers.SafeBool(() => doc.IsOpenedReadOnly()))
            {
                skipped.Add(new
                {
                    Title = title,
                    Path = path,
                    rename.InstancePath,
                    Reason = "read-only"
                });
                continue;
            }

            int errors = 0;
            int warnings = 0;
            var saveMethod = hasRenamedDocuments ? "SaveWithRenamedReferences" : "Save3";
            var ok = hasRenamedDocuments
                ? DocumentSaveHelper.SaveWithRenamedReferences(
                    doc,
                    logger,
                    out errors,
                    out warnings,
                    searchFolders,
                    suppressSaveDialogs ? app : null)
                : doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);

            if ((!ok || errors != 0) && (errors & 0x2000) != 0)
            {
                saveMethod = "SaveWithRenamedReferences (fallback)";
                ok = DocumentSaveHelper.SaveWithRenamedReferences(
                    doc,
                    logger,
                    out errors,
                    out warnings,
                    searchFolders,
                    suppressSaveDialogs ? app : null);
            }

            if (ok && errors == 0)
            {
                saved.Add(new
                {
                    Title = title,
                    Path = path,
                    rename.InstancePath,
                    SaveMethod = saveMethod,
                    HasRenamedDocuments = hasRenamedDocuments,
                    Warnings = warnings
                });
            }
            else
            {
                failed.Add(new
                {
                    Title = title,
                    Path = path,
                    rename.InstancePath,
                    SaveMethod = saveMethod,
                    HasRenamedDocuments = hasRenamedDocuments,
                    Errors = errors,
                    ErrorDecoded = DocumentSaveHelper.DecodeSaveErrorBitmask(errors),
                    Warnings = warnings
                });
            }
        }

        return new SaveCollectionResult(
            failed.Count == 0,
            new
            {
                SavedCount = saved.Count,
                SkippedCount = skipped.Count,
                FailedCount = failed.Count,
                Saved = saved,
                Skipped = skipped,
                Failed = failed
            });
    }

    /// <summary>
    /// Closes or hides documents that were opened by the rename operation
    /// (i.e. not in the pre-rename snapshot).
    /// </summary>
    internal static object CleanupOpenedDocuments(
        SldWorks app,
        OpenDocumentSnapshot snapshot,
        IReadOnlyList<AppliedRename> applied,
        bool closeOpenedDocs,
        bool hiddenInGui)
    {
        var renamedPathMap = applied
            .GroupBy(item => DocumentRenamePathHelpers.NormalizePath(item.NewPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => DocumentRenamePathHelpers.NormalizePath(group.Last().OldPath),
                StringComparer.OrdinalIgnoreCase);

        var closed = new List<object>();
        var hidden = new List<object>();
        var skipped = new List<object>();

        foreach (var doc in DocumentRenamePathHelpers.EnumerateOpenDocuments(app))
        {
            var title = doc.GetTitle() ?? string.Empty;
            var path = doc.GetPathName() ?? string.Empty;
            var normalizedPath = DocumentRenamePathHelpers.TryNormalizePath(path);

            if (DocumentRenamePathHelpers.WasOpenBefore(snapshot, title, normalizedPath, renamedPathMap))
            {
                continue;
            }

            if (closeOpenedDocs)
            {
                if (DocumentRenamePathHelpers.SafeBool(() => doc.GetSaveFlag()))
                {
                    skipped.Add(new { Title = title, Path = path, Reason = "dirty" });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    skipped.Add(new { Title = title, Path = path, Reason = "missing title" });
                    continue;
                }

                try
                {
                    app.CloseDoc(title);
                    closed.Add(new { Title = title, Path = path });
                    continue;
                }
                catch (Exception ex)
                {
                    skipped.Add(new { Title = title, Path = path, Reason = "close failed", Error = ex.Message });
                    continue;
                }
            }

            if (hiddenInGui)
            {
                var didHide = DocumentVisibilityScope.TryHide(doc);
                if (didHide)
                {
                    hidden.Add(new { Title = title, Path = path });
                }
                else
                {
                    skipped.Add(new { Title = title, Path = path, Reason = "hide failed" });
                }
            }
        }

        return new
        {
            CloseOpenedDocs = closeOpenedDocs,
            HiddenInGui = hiddenInGui,
            ClosedCount = closed.Count,
            HiddenCount = hidden.Count,
            SkippedCount = skipped.Count,
            Closed = closed,
            Hidden = hidden,
            Skipped = skipped
        };
    }
}
