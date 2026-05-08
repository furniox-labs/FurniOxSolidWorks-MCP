using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

internal static class AnalysisDocumentSupport
{
    public static ModelDoc2? TryOpenModelIfNeeded(ISldWorks app, string filePath, swDocumentTypes_e docType, out bool openedByUs)
    {
        openedByUs = false;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var existing = (ModelDoc2?)app.GetOpenDocumentByName(filePath);
        if (existing != null)
        {
            return existing;
        }

        var errors = 0;
        var warnings = 0;
        var options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
        var opened = (ModelDoc2?)app.OpenDoc6(filePath, (int)docType, options, string.Empty, ref errors, ref warnings);
        if (opened != null)
        {
            openedByUs = true;
        }

        return opened;
    }

    public static void CloseDocIfOpenedByUs(ISldWorks app, ModelDoc2? doc, bool openedByUs)
    {
        if (!openedByUs || doc == null)
        {
            return;
        }

        try { app.CloseDoc(doc.GetTitle()); } catch { }
    }

    public static Dictionary<string, string?> GetTopLevelComponentFolderMembership(
        ModelDoc2 model,
        AssemblyDoc assembly,
        object[]? prebuiltTopLevelComponents = null)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var seenFeatures = new HashSet<int>();

        void MapFolderContents(IFeature? feature, string? currentFolderPath)
        {
            if (feature == null)
            {
                return;
            }

            var featureId = feature.GetID();
            if (!seenFeatures.Add(featureId))
            {
                return;
            }

            string? featureTypeName = null;
            try { featureTypeName = feature.GetTypeName2(); } catch { }

            object? specific = null;
            try { specific = feature.GetSpecificFeature2(); } catch { }

            if (specific is IFeatureFolder folder)
            {
                var folderName = feature.Name ?? "Folder";
                var folderPath = string.IsNullOrEmpty(currentFolderPath) ? folderName : $"{currentFolderPath}/{folderName}";

                object? containedObject = null;
                try { containedObject = folder.GetFeatures(); } catch { }

                foreach (var contained in EnumerateFeatures(containedObject))
                {
                    MapFolderContents(contained, folderPath);
                }

                return;
            }

            if (string.Equals(featureTypeName, "Reference", StringComparison.OrdinalIgnoreCase))
            {
                var componentName = feature.Name ?? string.Empty;
                if (!string.IsNullOrEmpty(componentName) && !map.ContainsKey(componentName))
                {
                    map[componentName] = currentFolderPath;
                }
            }
        }

        var feature = model.FirstFeature() as IFeature;
        while (feature != null)
        {
            MapFolderContents(feature, null);
            feature = feature.GetNextFeature() as IFeature;
        }

        var componentArray = prebuiltTopLevelComponents ?? assembly.GetComponents(true).ToObjectArraySafe();
        if (componentArray != null)
        {
            foreach (var item in componentArray)
            {
                if (item is IComponent2 component)
                {
                    var name = component.Name2 ?? string.Empty;
                    if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                    {
                        map[name] = null;
                    }
                }
            }
        }

        return map;
    }

    public static IEnumerable<IFeature> EnumerateFeatures(object? featuresObject)
    {
        if (featuresObject is object[] objectArray)
        {
            foreach (var item in objectArray)
            {
                if (item is IFeature feature)
                {
                    yield return feature;
                }
            }

            yield break;
        }

        if (featuresObject is Array array)
        {
            foreach (var item in array)
            {
                if (item is IFeature feature)
                {
                    yield return feature;
                }
            }
        }
    }
}
