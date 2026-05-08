#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FurniOx.SolidWorks.Core.Adapters.Document;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

internal static partial class CrossReferenceBatchRunner
{
    private sealed record DocumentTarget(string Path, string Title, ModelDoc2? Model);

    private sealed record OpenDocumentResult(
        ModelDoc2? Model,
        bool OpenedByTool,
        long? OpenElapsedMs,
        bool OpenExceededMaxTime,
        string? Error);

    private static ModelDoc2? TryGetActiveDocument(SldWorks app)
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

    private static IReadOnlyList<ModelDoc2> GetOpenDocuments(SldWorks app)
    {
        return ToObjectArray(app.GetDocuments()).OfType<ModelDoc2>().ToList();
    }

    private static void AddExistingTarget(
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

    private static void AddPathTarget(List<DocumentTarget> targets, HashSet<string> seen, string path)
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

    private static OpenDocumentResult OpenDocument(SldWorks app, string path, CrossReferenceBatchRunOptions options)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new OpenDocumentResult(null, false, null, false, "Document path is empty.");
        }

        var existing = TryGetOpenDocumentByName(app, path);
        if (existing != null)
        {
            return new OpenDocumentResult(existing, false, null, false, null);
        }

        if (!File.Exists(path))
        {
            return new OpenDocumentResult(null, false, null, false, $"Document file not found: {path}");
        }

        var docType = GetDocumentTypeFromPath(path);
        if (docType == 0)
        {
            return new OpenDocumentResult(null, false, null, false, $"Unsupported SolidWorks document extension: {path}");
        }

        var errors = 0;
        var warnings = 0;
        var openOptions = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
        if (options.LightWeightOpen)
        {
            openOptions |= (int)swOpenDocOptions_e.swOpenDocOptions_LoadLightweight
                | (int)swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight;
        }
        if (options.DontLoadHiddenComponents)
        {
            openOptions |= (int)swOpenDocOptions_e.swOpenDocOptions_DontLoadHiddenComponents;
        }

        using var visibilityScope = DocumentVisibilityScope.HideNewDocuments(app, options.HiddenInGui, docType);
        var sw = Stopwatch.StartNew();
        var opened = app.OpenDoc6(
            path,
            docType,
            openOptions,
            "",
            ref errors,
            ref warnings) as ModelDoc2;
        sw.Stop();

        if (opened == null)
        {
            return new OpenDocumentResult(null, false, sw.ElapsedMilliseconds, false, $"OpenDoc6 failed for {path}. Errors={errors}, Warnings={warnings}");
        }

        if (options.HiddenInGui)
        {
            DocumentVisibilityScope.TryHide(opened);
        }

        var exceeded = options.MaxDocOpenTimeMs > 0 && sw.ElapsedMilliseconds > options.MaxDocOpenTimeMs;
        return new OpenDocumentResult(opened, true, sw.ElapsedMilliseconds, exceeded, null);
    }

    private static ModelDoc2? TryGetOpenDocumentByName(SldWorks app, string path)
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

    private static bool TryCloseDocument(SldWorks app, ModelDoc2 model)
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

    private static HashSet<string> SnapshotOpenDocumentKeys(SldWorks app)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var model in GetOpenDocuments(app))
            {
                foreach (var key in GetDocumentKeys(model))
                {
                    keys.Add(key);
                }
            }
        }
        catch
        {
        }

        return keys;
    }

    private static int CloseDocumentsOpenedSince(SldWorks app, HashSet<string> before)
    {
        var closed = 0;
        try
        {
            foreach (var model in GetOpenDocuments(app))
            {
                var keys = GetDocumentKeys(model);
                if (keys.Count == 0 || keys.Any(before.Contains))
                {
                    continue;
                }

                if (TryCloseDocument(app, model))
                {
                    closed++;
                }
            }
        }
        catch
        {
        }

        return closed;
    }

    private static List<string> GetDocumentKeys(ModelDoc2 model)
    {
        var keys = new List<string>();
        var path = SafeString(() => model.GetPathName());
        var title = SafeString(() => model.GetTitle());
        if (!string.IsNullOrWhiteSpace(path))
        {
            keys.Add(path);
        }
        if (!string.IsNullOrWhiteSpace(title))
        {
            keys.Add(title);
        }

        return keys;
    }
}
