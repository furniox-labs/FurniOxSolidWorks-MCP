using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Sorting;

internal static class SortingFeatureSupport
{
    public static List<SortingFeatureTreeFeature> GetReorderableFeaturesInTreeOrder(ModelDoc2 model, string featureType)
    {
        var folderMembershipById = new Dictionary<int, string?>();
        var seenFolderPass = new HashSet<int>();

        void MapFolderContents(IFeature? feature, string? currentFolderPath)
        {
            if (feature == null)
            {
                return;
            }

            var featureId = feature.GetID();
            if (!seenFolderPass.Add(featureId))
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

                foreach (var contained in EnumerateFeatures(containedObject))
                {
                    MapFolderContents(contained, folderPath);
                }

                return;
            }

            if (!folderMembershipById.ContainsKey(featureId))
            {
                folderMembershipById[featureId] = currentFolderPath;
            }
        }

        var first = model.FirstFeature() as IFeature;
        while (first != null)
        {
            MapFolderContents(first, null);
            first = first.GetNextFeature() as IFeature;
        }

        var result = new List<SortingFeatureTreeFeature>();
        var seenCollect = new HashSet<int>();
        var feature = model.FirstFeature() as IFeature;

        while (feature != null)
        {
            var typeName = feature.GetTypeName2() ?? string.Empty;
            var name = feature.Name ?? string.Empty;
            var featureId = feature.GetID();

            if (string.IsNullOrEmpty(featureType) ||
                typeName.Equals(featureType, StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains(featureType, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(name) &&
                    !IsSystemFeature(typeName) &&
                    seenCollect.Add(featureId))
                {
                    object? specific = null;
                    try { specific = feature.GetSpecificFeature2(); } catch { }

                    if (specific is not IFeatureFolder)
                    {
                        folderMembershipById.TryGetValue(featureId, out var folderPath);
                        result.Add(new SortingFeatureTreeFeature(feature, folderPath));
                    }
                }
            }

            feature = feature.GetNextFeature() as IFeature;
        }

        return result;
    }

    public static SortingFeatureFolderGroupState ComputeFeatureFolderGroupState(
        List<SortingFeatureTreeFeature> currentFeaturesInTree,
        List<IFeature> targetOrder)
    {
        var desiredIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < targetOrder.Count; i++)
        {
            var name = targetOrder[i].Name;
            if (!string.IsNullOrEmpty(name) && !desiredIndexByName.ContainsKey(name))
            {
                desiredIndexByName[name] = i;
            }
        }

        var groups = new List<(string? FolderPath, List<IFeature> Features)>();
        foreach (var entry in currentFeaturesInTree)
        {
            if (groups.Count == 0 || !string.Equals(groups[^1].FolderPath, entry.FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                groups.Add((entry.FolderPath, new List<IFeature> { entry.Feature }));
            }
            else
            {
                groups[^1].Features.Add(entry.Feature);
            }
        }

        return new SortingFeatureFolderGroupState(desiredIndexByName, groups);
    }

    public static List<IFeature> BuildEffectiveFeatureOrderFromState(SortingFeatureFolderGroupState state)
    {
        var effective = new List<IFeature>();

        foreach (var group in state.Groups)
        {
            var sortedGroup = group.Features
                .OrderBy(feature =>
                {
                    var name = feature.Name ?? string.Empty;
                    return state.DesiredIndexByName.TryGetValue(name, out var index) ? index : int.MaxValue;
                })
                .ToList();

            effective.AddRange(sortedGroup);
        }

        return effective;
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

    private static bool IsSystemFeature(string typeName)
    {
        return typeName switch
        {
            "HistoryFolder" => true,
            "SensorFolder" => true,
            "DetailCabinet" => true,
            "ProfileFeature" => true,
            "RefPlane" => true,
            "OriginProfileFeature" => true,
            "MaterialFolder" => true,
            _ => false
        };
    }
}
