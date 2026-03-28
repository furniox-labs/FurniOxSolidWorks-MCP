using System;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Extensions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Sorting;

internal static class SortingComponentSupport
{
    public static List<SortingTopLevelTreeItem> GetTopLevelComponentItemsInTreeOrder(ModelDoc2 model)
    {
        var result = new List<SortingTopLevelTreeItem>();
        var seenFeatures = new HashSet<int>();

        var feature = model.FirstFeature() as IFeature;
        while (feature != null)
        {
            var featureId = feature.GetID();
            if (!seenFeatures.Add(featureId))
            {
                feature = feature.GetNextFeature() as IFeature;
                continue;
            }

            object? specific = null;
            try { specific = feature.GetSpecificFeature2(); } catch { }

            if (specific is IFeatureFolder folder)
            {
                if (FolderContainsComponent(folder))
                {
                    var name = feature.Name ?? "Folder";
                    result.Add(new SortingTopLevelTreeItem(feature, name, true, name, null));
                }

                feature = feature.GetNextFeature() as IFeature;
                continue;
            }

            if (specific is IComponent2 component)
            {
                var name2 = component.Name2 ?? string.Empty;
                if (!string.IsNullOrEmpty(name2))
                {
                    result.Add(new SortingTopLevelTreeItem(feature, StripAssemblySuffix(name2), false, null, name2));
                }
            }

            feature = feature.GetNextFeature() as IFeature;
        }

        return result;
    }

    public static List<SortingFeatureTreeComponent> GetTopLevelComponentsInFeatureTreeOrder(ModelDoc2 model, IAssemblyDoc assembly)
    {
        var result = new List<SortingFeatureTreeComponent>();
        var seenComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFeatures = new HashSet<int>();
        var componentFolderMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

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

            object? specific = null;
            try { specific = feature.GetSpecificFeature2(); } catch { }

            if (specific is IFeatureFolder folder)
            {
                var folderName = feature.Name ?? "Folder";
                var folderPath = string.IsNullOrEmpty(currentFolderPath) ? folderName : $"{currentFolderPath}/{folderName}";

                object? containedObject = null;
                try { containedObject = folder.GetFeatures(); } catch { }

                foreach (var contained in SortingFeatureSupport.EnumerateFeatures(containedObject))
                {
                    MapFolderContents(contained, folderPath);
                }

                return;
            }

            if (specific is IComponent2 component)
            {
                var name = component.Name2 ?? string.Empty;
                if (!string.IsNullOrEmpty(name) && !componentFolderMap.ContainsKey(name))
                {
                    componentFolderMap[name] = currentFolderPath;
                }
            }
        }

        var feature = model.FirstFeature() as IFeature;
        while (feature != null)
        {
            MapFolderContents(feature, null);
            feature = feature.GetNextFeature() as IFeature;
        }

        seenFeatures.Clear();

        void CollectComponents(IFeature? feat)
        {
            if (feat == null)
            {
                return;
            }

            var featureId = feat.GetID();
            if (!seenFeatures.Add(featureId))
            {
                return;
            }

            object? specific = null;
            try { specific = feat.GetSpecificFeature2(); } catch { }

            if (specific is IFeatureFolder folder)
            {
                object? containedObject = null;
                try { containedObject = folder.GetFeatures(); } catch { }

                foreach (var contained in SortingFeatureSupport.EnumerateFeatures(containedObject))
                {
                    CollectComponents(contained);
                }

                return;
            }

            if (specific is IComponent2 component)
            {
                var name = component.Name2 ?? string.Empty;
                if (!string.IsNullOrEmpty(name) && seenComponents.Add(name))
                {
                    componentFolderMap.TryGetValue(name, out var folderPath);
                    result.Add(new SortingFeatureTreeComponent(component, folderPath));
                }
            }
        }

        feature = model.FirstFeature() as IFeature;
        while (feature != null)
        {
            CollectComponents(feature);
            feature = feature.GetNextFeature() as IFeature;
        }

        if (result.Count == 0)
        {
            var components = assembly.GetComponents(true).ToObjectArraySafe();
            if (components != null)
            {
                foreach (var item in components)
                {
                    if (item is IComponent2 component)
                    {
                        var name = component.Name2 ?? string.Empty;
                        if (!string.IsNullOrEmpty(name) && seenComponents.Add(name))
                        {
                            result.Add(new SortingFeatureTreeComponent(component, null));
                        }
                    }
                }
            }
        }

        return result;
    }

    public static SortingFolderGroupState ComputeFolderGroupState(
        List<SortingFeatureTreeComponent> currentComponentsInTree,
        List<IComponent2> targetOrder)
    {
        var desiredIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < targetOrder.Count; i++)
        {
            var name = targetOrder[i].Name2;
            if (!string.IsNullOrEmpty(name) && !desiredIndexByName.ContainsKey(name))
            {
                desiredIndexByName[name] = i;
            }
        }

        var groups = new List<(string? FolderPath, List<IComponent2> Components)>();
        foreach (var entry in currentComponentsInTree)
        {
            if (groups.Count == 0 || !string.Equals(groups[^1].FolderPath, entry.FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                groups.Add((entry.FolderPath, new List<IComponent2> { entry.Component }));
            }
            else
            {
                groups[^1].Components.Add(entry.Component);
            }
        }

        return new SortingFolderGroupState(desiredIndexByName, groups);
    }

    public static List<IComponent2> BuildEffectiveOrderFromState(SortingFolderGroupState state)
    {
        var effective = new List<IComponent2>();

        foreach (var group in state.Groups)
        {
            var sortedGroup = group.Components
                .OrderBy(component =>
                {
                    var name = component.Name2 ?? string.Empty;
                    return state.DesiredIndexByName.TryGetValue(name, out var index) ? index : int.MaxValue;
                })
                .ToList();

            effective.AddRange(sortedGroup);
        }

        return effective;
    }

    public static bool IsSuppressedSafe(IComponent2 component)
    {
        try
        {
            var suppressionState = component.GetSuppression2();
            return suppressionState == (int)swComponentSuppressionState_e.swComponentSuppressed;
        }
        catch
        {
            return false;
        }
    }

    public static string? ConvertInstanceNameFormat(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var angleBracketMatch = System.Text.RegularExpressions.Regex.Match(name, @"^(.+)<(\d+)>$");
        if (angleBracketMatch.Success)
        {
            return $"{angleBracketMatch.Groups[1].Value}-{angleBracketMatch.Groups[2].Value}";
        }

        var hyphenMatch = System.Text.RegularExpressions.Regex.Match(name, @"^(.+)-(\d+)$");
        if (hyphenMatch.Success)
        {
            return $"{hyphenMatch.Groups[1].Value}<{hyphenMatch.Groups[2].Value}>";
        }

        return null;
    }

    private static bool FolderContainsComponent(IFeatureFolder folder)
    {
        object? featuresObject = null;
        try { featuresObject = folder.GetFeatures(); } catch { }

        foreach (var feature in SortingFeatureSupport.EnumerateFeatures(featuresObject))
        {
            object? specific = null;
            try { specific = feature.GetSpecificFeature2(); } catch { }

            if (specific is IComponent2)
            {
                return true;
            }

            if (specific is IFeatureFolder nested && FolderContainsComponent(nested))
            {
                return true;
            }
        }

        return false;
    }

    private static string StripAssemblySuffix(string name2)
    {
        var atIndex = name2.IndexOf('@', StringComparison.Ordinal);
        return atIndex >= 0 ? name2.Substring(0, atIndex) : name2;
    }
}
