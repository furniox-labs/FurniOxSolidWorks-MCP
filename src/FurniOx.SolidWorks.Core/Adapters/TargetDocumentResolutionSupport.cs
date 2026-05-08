using System;
using System.Collections.Generic;
using System.Text.Json;
using FurniOx.SolidWorks.Core.Adapters.Analysis;
using FurniOx.SolidWorks.Core.Connection;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

internal sealed class TargetDocumentScope : IDisposable
{
    private readonly ISldWorks _app;
    private readonly bool _openedByUs;
    private readonly ModelDoc2? _openedDoc;

    public TargetDocumentScope(
        ISldWorks app,
        ModelDoc2 targetModel,
        string? componentName,
        string? componentPath,
        bool openedByUs,
        ModelDoc2? openedDoc)
    {
        _app = app;
        TargetModel = targetModel;
        ComponentName = componentName;
        ComponentPath = componentPath;
        _openedByUs = openedByUs;
        _openedDoc = openedDoc;
    }

    public ModelDoc2 TargetModel { get; }
    public string? ComponentName { get; }
    public string? ComponentPath { get; }

    public void Dispose()
    {
        AnalysisDocumentSupport.CloseDocIfOpenedByUs(_app, _openedDoc, _openedByUs);
    }
}

internal static class TargetDocumentResolutionSupport
{
    public static (TargetDocumentScope? scope, string? failure) TryCreateScope(
        SolidWorksConnection connection,
        IDictionary<string, object?> parameters,
        bool openIfNeededDefault = true)
    {
        var app = connection.Application;
        if (app == null)
        {
            return (null, "Not connected to SolidWorks");
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return (null, "No active document");
        }

        if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            var selMgr = (ISelectionMgr?)model.SelectionManager;
            if (selMgr != null && selMgr.GetSelectedObjectCount2(-1) > 0)
            {
                var selectedObj = selMgr.GetSelectedObject6(1, -1);
                var selectedComponent = selectedObj as IComponent2
                    ?? selMgr.GetSelectedObjectsComponent4(1, -1) as IComponent2;

                if (selectedComponent != null)
                {
                    var componentModel = selectedComponent.GetModelDoc2() as ModelDoc2;
                    var filePath = selectedComponent.GetPathName();
                    var openIfNeeded = GetBoolParam(parameters, "OpenIfNeeded", openIfNeededDefault);
                    var openedByUs = false;
                    ModelDoc2? openedDoc = null;

                    if (componentModel == null && openIfNeeded)
                    {
                        componentModel = AnalysisDocumentSupport.TryOpenModelIfNeeded(
                            app,
                            filePath,
                            InferDocumentType(filePath),
                            out openedByUs);
                        openedDoc = componentModel;
                    }

                    if (componentModel != null)
                    {
                        return (new TargetDocumentScope(
                            app,
                            componentModel,
                            selectedComponent.Name2,
                            filePath,
                            openedByUs,
                            openedDoc), null);
                    }

                    var failure = openIfNeeded
                        ? $"Selected component '{selectedComponent.Name2}' is not loaded and could not be opened"
                        : $"Selected component '{selectedComponent.Name2}' is not loaded. Set 'OpenIfNeeded' to true to open it silently.";
                    return (null, failure);
                }
            }
        }

        return (new TargetDocumentScope(app, model, null, null, openedByUs: false, openedDoc: null), null);
    }

    internal static swDocumentTypes_e InferDocumentType(string? filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath ?? string.Empty).ToUpperInvariant();
        return extension switch
        {
            ".SLDASM" => swDocumentTypes_e.swDocASSEMBLY,
            ".SLDDRW" => swDocumentTypes_e.swDocDRAWING,
            _ => swDocumentTypes_e.swDocPART
        };
    }

    private static bool GetBoolParam(IDictionary<string, object?> parameters, string key, bool defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (jsonElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return defaultValue;
    }
}
