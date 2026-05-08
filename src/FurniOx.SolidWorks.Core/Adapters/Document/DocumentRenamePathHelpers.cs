#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Static path utilities, input parsing, and component/plan building
/// helpers extracted from DocumentRenameBatchOperations.
/// </summary>
internal static class DocumentRenamePathHelpers
{
    // ── Path utilities ────────────────────────────────────────────────────

    internal static string NormalizePath(string path) => Path.GetFullPath(path);

    internal static string EnsureExtension(string fileName, string extension)
        => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? fileName : fileName + extension;

    internal static void AddIfPath(HashSet<string> paths, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            paths.Add(NormalizePath(path));
        }
    }

    internal static void AddSnapshotPath(HashSet<string> paths, string? path)
    {
        var normalized = TryNormalizePath(path);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            paths.Add(normalized);
        }
    }

    internal static bool SamePath(string? left, string? right)
    {
        var normalizedLeft = TryNormalizePath(left);
        var normalizedRight = TryNormalizePath(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft)
            && !string.IsNullOrWhiteSpace(normalizedRight)
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? TryNormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return NormalizePath(path);
        }
        catch
        {
            return path;
        }
    }

    internal static bool WasOpenBefore(
        OpenDocumentSnapshot snapshot,
        string title,
        string? normalizedPath,
        IReadOnlyDictionary<string, string> renamedPathMap)
    {
        if (!string.IsNullOrWhiteSpace(title) && snapshot.Titles.Contains(title))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            if (snapshot.Paths.Contains(normalizedPath))
            {
                return true;
            }

            if (renamedPathMap.TryGetValue(normalizedPath, out var originalPath)
                && snapshot.Paths.Contains(originalPath))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool SafeBool(Func<bool> read)
    {
        try { return read(); }
        catch { return false; }
    }

    internal static void TryRestoreActiveDocument(SldWorks app, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        try
        {
            int errors = 0;
            app.ActivateDoc3(title, false, 0, ref errors);
        }
        catch { }
    }

    internal static string SuppressionStateName(int code) => code switch
    {
        0 => "Suppressed",
        1 => "Lightweight",
        2 => "FullyResolved",
        3 => "Resolved",
        4 => "FullyLightweight",
        5 => "InternalIdMismatch",
        _ => $"Unknown({code})"
    };

    // ── Component / plan helpers ──────────────────────────────────────────

    internal static List<ComponentOccurrence> BuildComponentOccurrences(IAssemblyDoc assembly)
    {
        var occurrences = new List<ComponentOccurrence>();
        var components = assembly.GetComponents(false).ToObjectArraySafe() ?? Array.Empty<object>();
        foreach (var componentObj in components)
        {
            if (componentObj is not IComponent2 component) continue;

            var path = component.GetPathName() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) continue;

            var instancePath = component.Name2 ?? string.Empty;
            var suppressionState = component.ReadSuppressionState();
            occurrences.Add(new ComponentOccurrence(
                component,
                instancePath,
                path,
                NormalizePath(path),
                instancePath.Count(ch => ch == '/'),
                component.IsVirtual,
                SafeBool(() => component.IsPatternInstance()),
                suppressionState,
                component.GetModelDoc2() != null));
        }

        return occurrences;
    }

    internal static object ToPlanOutput(RenamePlanItem item)
        => new
        {
            item.OldPath,
            item.NewPath,
            InstancePath = item.Occurrence?.InstancePath,
            item.Depth,
            SuppressionState = item.Occurrence?.SuppressionState,
            SuppressionStateName = item.Occurrence != null ? SuppressionStateName(item.Occurrence.SuppressionState) : null,
            HasLoadedModelDocument = item.Occurrence?.HasLoadedModelDocument,
            IsLightweight = item.Occurrence?.IsLightweight,
            RequiresResolveForRename = item.Occurrence != null && !item.Occurrence.IsResolvedForRename,
            CanRename = item.Blockers.Count == 0,
            item.Blockers
        };

    // ── Plan building ─────────────────────────────────────────────────────

    internal static List<RenamePlanItem> BuildBatchPlan(
        IReadOnlyList<RenameFileItem> requestedItems,
        IReadOnlyList<ComponentOccurrence> occurrences,
        bool requireFullyResolved)
    {
        var byPath = occurrences
            .GroupBy(item => item.NormalizedPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Depth).First(), StringComparer.OrdinalIgnoreCase);

        var duplicateTargets = requestedItems
            .Select(item => NormalizePath(item.NewPath))
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var plan = new List<RenamePlanItem>();
        foreach (var requested in requestedItems)
        {
            var oldPath = NormalizePath(requested.OldPath);
            var newPath = NormalizePath(requested.NewPath);
            var blockers = new List<string>();
            byPath.TryGetValue(oldPath, out var occurrence);

            if (occurrence == null) blockers.Add("MissingComponent");
            if (duplicateTargets.Contains(newPath)) blockers.Add("DuplicateTarget");
            if (!string.Equals(Path.GetExtension(oldPath), Path.GetExtension(newPath), StringComparison.OrdinalIgnoreCase)) blockers.Add("ExtensionMismatch");
            if (!string.Equals(Path.GetDirectoryName(oldPath), Path.GetDirectoryName(newPath), StringComparison.OrdinalIgnoreCase)) blockers.Add("MoveNotSupported");
            if (File.Exists(newPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) blockers.Add("TargetFileExists");
            if (File.Exists(oldPath) && new FileInfo(oldPath).IsReadOnly) blockers.Add("ReadOnly");
            if (occurrence?.IsVirtual == true) blockers.Add("VirtualComponent");
            if (occurrence?.IsPatternInstance == true) blockers.Add("PatternedComponent");
            if (requireFullyResolved && occurrence != null && !occurrence.IsResolvedForRename)
            {
                blockers.Add("RequiresResolveForRename");
            }

            plan.Add(new RenamePlanItem(oldPath, newPath, occurrence, blockers));
        }

        return plan;
    }

    // ── Input parsing ─────────────────────────────────────────────────────

    internal static bool TryReadRenameItems(
        IDictionary<string, object?> parameters,
        out List<RenameFileItem> items,
        out string? error)
    {
        items = new List<RenameFileItem>();
        error = null;

        try
        {
            var inputPath = GetStringParam(parameters, "InputPath");
            var json = GetStringParam(parameters, "ItemsJson");

            if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(inputPath))
            {
                json = File.ReadAllText(inputPath);
            }

            if (string.IsNullOrWhiteSpace(json)
                && parameters.TryGetValue("Items", out var itemsObj)
                && itemsObj is JsonElement element)
            {
                json = element.GetRawText();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Provide InputPath, ItemsJson, or Items.";
                return false;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var array = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
                    ? itemsElement
                    : default;

            if (array.ValueKind != JsonValueKind.Array)
            {
                error = "Rename input must be a JSON array or an object with an items array.";
                return false;
            }

            foreach (var item in array.EnumerateArray())
            {
                var oldPath = ReadString(item, "OldPath", "oldPath", "CurrentPath", "currentPath", "Path", "path");
                var newPath = ReadString(item, "NewPath", "newPath", "TargetPath", "targetPath");
                if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
                {
                    error = "Each rename item must include oldPath and newPath.";
                    return false;
                }

                items.Add(new RenameFileItem(oldPath!, newPath!));
            }

            if (items.Count == 0)
            {
                error = "Rename input contains no items.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static IReadOnlyList<ModelDoc2> EnumerateOpenDocuments(SldWorks app)
    {
        var docs = new List<ModelDoc2>();
        var documents = app.GetDocuments().ToObjectArraySafe();
        if (documents == null) return docs;
        foreach (var docObj in documents)
        {
            if (docObj is ModelDoc2 doc)
            {
                docs.Add(doc);
            }
        }

        return docs;
    }

    // ── Private utilities ─────────────────────────────────────────────────

    private static string? GetStringParam(IDictionary<string, object?> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
