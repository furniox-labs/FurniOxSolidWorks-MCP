using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FurniOx.SolidWorks.Core.Adapters.Document;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>Represents a resolved document target for batch processing.</summary>
internal sealed record DocumentTarget(string Path, string Title, ModelDoc2? Model);

/// <summary>Represents the outcome of an attempt to open a SolidWorks document.</summary>
internal sealed record OpenDocumentResult(ModelDoc2? Model, bool OpenedByTool, string? Error);

/// <summary>
/// Helpers for SolidWorks document lifecycle operations used by the equation
/// reference batch runner: resolving targets, opening, closing, and reading
/// configuration information.
/// </summary>
internal static class EquationReferenceDocumentHelper
{
    /// <summary>
    /// Attempts to retrieve the currently active SolidWorks document via three
    /// different COM APIs, returning <see langword="null"/> if none is open.
    /// </summary>
    internal static ModelDoc2? TryGetActiveDocument(SldWorks app)
    {
        try
        {
            if (app.ActiveDoc is ModelDoc2 activeDoc)
            {
                return activeDoc;
            }
        }
        catch { }

        try
        {
            var activeDoc = app.IActiveDoc2;
            if (activeDoc != null)
            {
                return activeDoc;
            }
        }
        catch { }

        try
        {
            if (app.IActiveDoc is ModelDoc2 activeDoc)
            {
                return activeDoc;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Adds <paramref name="model"/> to <paramref name="targets"/> if it has not
    /// already been seen (keyed by path then title).
    /// </summary>
    internal static void AddExistingTarget(
        List<DocumentTarget> targets,
        HashSet<string> seen,
        ModelDoc2 model,
        string? pathHint = null)
    {
        var path = SafeString(() => model.GetPathName(), pathHint ?? "");
        var title = SafeString(() => model.GetTitle());
        var key = !string.IsNullOrWhiteSpace(path) ? path : title;
        if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
        {
            return;
        }

        targets.Add(new DocumentTarget(path, title, model));
    }

    /// <summary>
    /// Adds a path-only target to <paramref name="targets"/> if it has not
    /// already been seen.
    /// </summary>
    internal static void AddPathTarget(List<DocumentTarget> targets, HashSet<string> seen, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        if (!seen.Add(fullPath))
        {
            return;
        }

        targets.Add(new DocumentTarget(fullPath, Path.GetFileName(fullPath), null));
    }

    /// <summary>
    /// Opens a SolidWorks document from <paramref name="path"/>, optionally
    /// hiding it in the GUI. Returns an <see cref="OpenDocumentResult"/> that
    /// indicates success, reuse of an already-open document, or a failure reason.
    /// </summary>
    internal static OpenDocumentResult OpenDocument(SldWorks app, string path, bool hiddenInGui)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new OpenDocumentResult(null, false, "Document path is empty.");
        }

        var existing = TryGetOpenDocumentByName(app, path);
        if (existing != null)
        {
            return new OpenDocumentResult(existing, false, null);
        }

        if (!File.Exists(path))
        {
            return new OpenDocumentResult(null, false, $"Document file not found: {path}");
        }

        var docType = GetDocumentTypeFromPath(path);
        if (docType == 0)
        {
            return new OpenDocumentResult(null, false, $"Unsupported SolidWorks document extension: {path}");
        }

        var errors = 0;
        var warnings = 0;
        using var visibilityScope = DocumentVisibilityScope.HideNewDocuments(app, hiddenInGui, docType);
        var opened = app.OpenDoc6(
            path,
            docType,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            "",
            ref errors,
            ref warnings) as ModelDoc2;

        if (opened == null)
        {
            return new OpenDocumentResult(null, false, $"OpenDoc6 failed for {path}. Errors={errors}, Warnings={warnings}");
        }

        if (hiddenInGui)
        {
            DocumentVisibilityScope.TryHide(opened);
        }

        return new OpenDocumentResult(opened, true, null);
    }

    /// <summary>
    /// Returns the already-open <see cref="ModelDoc2"/> matching
    /// <paramref name="path"/>, or <see langword="null"/> if not open.
    /// </summary>
    internal static ModelDoc2? TryGetOpenDocumentByName(SldWorks app, string path)
    {
        try
        {
            return app.GetOpenDocumentByName(path) as ModelDoc2;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Closes the document identified by its title. Returns <see langword="true"/>
    /// on success, <see langword="false"/> if the COM call throws.
    /// </summary>
    internal static bool TryCloseDocument(SldWorks app, ModelDoc2 model)
    {
        try
        {
            app.CloseDoc(model.GetTitle());
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a list of configuration names to process. When
    /// <paramref name="allConfigurations"/> is <see langword="false"/> the list
    /// contains only <paramref name="activeConfig"/>.
    /// </summary>
    internal static List<string> GetConfigurationNames(ModelDoc2 model, bool allConfigurations, string activeConfig)
    {
        if (!allConfigurations)
        {
            return new List<string> { activeConfig };
        }

        var names = ToObjectArray(model.GetConfigurationNames())
            .OfType<string>()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
        {
            names.Add(activeConfig);
        }

        return names;
    }

    /// <summary>
    /// Returns the name of the active configuration, or an empty string on error.
    /// </summary>
    internal static string GetActiveConfigurationName(ModelDoc2 model)
    {
        try
        {
            var configuration = model.GetActiveConfiguration() as global::SolidWorks.Interop.sldworks.Configuration;
            return configuration?.Name ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static int GetDocumentTypeFromPath(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".sldprt", StringComparison.OrdinalIgnoreCase))
        {
            return (int)swDocumentTypes_e.swDocPART;
        }

        if (extension.Equals(".sldasm", StringComparison.OrdinalIgnoreCase))
        {
            return (int)swDocumentTypes_e.swDocASSEMBLY;
        }

        if (extension.Equals(".slddrw", StringComparison.OrdinalIgnoreCase))
        {
            return (int)swDocumentTypes_e.swDocDRAWING;
        }

        return 0;
    }

    private static IReadOnlyList<object> ToObjectArray(object? value)
    {
        if (value == null)
        {
            return Array.Empty<object>();
        }

        if (value is object[] objects)
        {
            return objects;
        }

        if (value is Array array)
        {
            return array.Cast<object>().ToArray();
        }

        return new[] { value };
    }

    private static string SafeString(Func<string?> getter, string fallback = "")
    {
        try
        {
            return getter() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
