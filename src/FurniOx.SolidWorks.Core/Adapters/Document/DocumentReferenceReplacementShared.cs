using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Shared helpers for single-doc and batch reference-replacement handlers.
/// </summary>
internal static class DocumentReferenceReplacementShared
{
    /// <summary>
    /// Calls ISldWorks.ReplaceReferencedDocument and converts its boolean result
    /// plus any COM exception into a uniform (ok, error) tuple.
    /// </summary>
    internal static (bool Ok, string? Error) TryReplace(
        SldWorks app,
        string referencingDoc,
        string oldRef,
        string newRef,
        ILogger logger)
    {
        try
        {
            var result = app.ReplaceReferencedDocument(referencingDoc, oldRef, newRef);
            return result
                ? (true, null)
                : (false, "ReplaceReferencedDocument returned false (old reference path may not match what's stored in the file).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "ReplaceReferencedDocument threw for {ReferencingDoc} {OldRef} -> {NewRef}",
                referencingDoc, oldRef, newRef);
            return (false, $"ReplaceReferencedDocument threw: {ex.Message}");
        }
    }

    internal static bool ValidatePaths(string referencingDoc, string oldRef, string newRef, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(referencingDoc))
        {
            error = "ReferencingDocPath is empty.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(oldRef))
        {
            error = "OldRefPath is empty.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(newRef))
        {
            error = "NewRefPath is empty.";
            return false;
        }
        if (string.Equals(oldRef, newRef, StringComparison.OrdinalIgnoreCase))
        {
            error = "OldRefPath and NewRefPath are identical — nothing to replace.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Returns a set of (lowered) file paths and titles for every doc currently
    /// open in SolidWorks. Used to detect when a caller hands us a path SW already
    /// has loaded — ReplaceReferencedDocument requires the file to be closed.
    /// </summary>
    internal static HashSet<string> SnapshotOpenDocuments(SldWorks app)
    {
        var snapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var documents = app.GetDocuments().ToObjectArraySafe();
            if (documents == null) return snapshot;
            foreach (var docObj in documents)
            {
                if (docObj is not ModelDoc2 doc) continue;
                var path = doc.GetPathName();
                var title = doc.GetTitle();
                if (!string.IsNullOrEmpty(path)) snapshot.Add(path);
                if (!string.IsNullOrEmpty(title)) snapshot.Add(title);
            }
        }
        catch
        {
            // Snapshot failures degrade safely — handler proceeds without the guard.
        }
        return snapshot;
    }

    internal static bool EnforceClosed(HashSet<string> openTitles, IReadOnlyList<string> paths, out ExecutionResult? failure)
    {
        failure = null;
        var conflicts = new List<string>();
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            if (openTitles.Contains(p))
            {
                conflicts.Add(p);
                continue;
            }
            // Title-only match (when SW returned just the filename rather than full path).
            var fileName = Path.GetFileName(p);
            if (!string.IsNullOrEmpty(fileName) && openTitles.Contains(fileName))
            {
                conflicts.Add(p);
            }
        }

        if (conflicts.Count == 0) return true;

        failure = ExecutionResult.Failure(
            "ReplaceReferencedDocument requires the file to be CLOSED in SolidWorks; one or more paths are currently loaded. " +
            "Close them first via close_model or close_all_documents.",
            new { OpenPaths = conflicts });
        return false;
    }

    internal static HashSet<string> GetDependencyPaths(SldWorks app, string documentPath, bool searchRules)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var raw = app.GetDocumentDependencies2(documentPath, false, searchRules, false);
            if (raw is not Array array)
            {
                return paths;
            }

            var values = array.Cast<object>()
                .Select(v => v?.ToString() ?? string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToArray();

            for (var i = 1; i < values.Length; i += 2)
            {
                paths.Add(values[i]);
            }
        }
        catch
        {
        }

        return paths;
    }

    internal static object[] BuildDependencyResolutionDiff(
        IReadOnlyCollection<string> storedPaths,
        IReadOnlyCollection<string> resolvedPaths)
    {
        var byFileName = resolvedPaths
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key ?? string.Empty, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return storedPaths
            .Select(stored =>
            {
                var fileName = Path.GetFileName(stored);
                return !string.IsNullOrWhiteSpace(fileName)
                    && byFileName.TryGetValue(fileName, out var resolved)
                    && !string.Equals(stored, resolved, StringComparison.OrdinalIgnoreCase)
                    ? new { StoredPath = stored, ResolvedPath = resolved }
                    : null;
            })
            .Where(item => item != null)
            .Cast<object>()
            .ToArray();
    }

    internal static string? ReadProp(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }
        return null;
    }
}
