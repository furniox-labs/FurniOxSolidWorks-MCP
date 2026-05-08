#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Component storage index, feature traversal, and path utility helpers for cross-reference scanning.
/// All methods are pure or perform read-only COM queries.
/// </summary>
internal static class CrossReferenceStorageSupport
{
    /// <summary>
    /// Builds a lookup from component name/path variants to storage info for fast resolution.
    /// Returns an empty dictionary if <paramref name="model"/> is not an assembly.
    /// </summary>
    internal static Dictionary<string, ComponentStorageInfo> BuildComponentStorageIndex(
        ModelDoc2 model,
        Func<object?, List<object>> toObjectArray,
        Func<Func<string?>, string, string> safeString)
    {
        var index = new Dictionary<string, ComponentStorageInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                return index;
            }

            var assembly = (IAssemblyDoc)model;
            foreach (var component in toObjectArray(assembly.GetComponents(false)).OfType<IComponent2>())
            {
                var name = safeString(() => component.Name2, "");
                var path = safeString(() => component.GetPathName(), "");
                if (string.IsNullOrWhiteSpace(path)) continue;

                var info = new ComponentStorageInfo(path, Path.GetFileName(path), GetDocumentTypeFromPath(path));
                AddKey(index, name, info);
                AddKey(index, LastPathSegment(name), info);
                AddKey(index, Path.GetFileName(path), info);
                AddKey(index, Path.GetFileNameWithoutExtension(path), info);
                AddKey(index, StripInstanceSuffix(Path.GetFileNameWithoutExtension(path)), info);
                AddKey(index, StripInstanceSuffix(LastPathSegment(name)), info);
            }
        }
        catch
        {
        }

        return index;
    }

    internal static ComponentStorageInfo ResolveStorageDocument(
        string discoveryPath,
        string discoveryTitle,
        int discoveryType,
        IReadOnlyDictionary<string, ComponentStorageInfo> componentIndex,
        string featureComponent)
    {
        if (!string.IsNullOrWhiteSpace(featureComponent))
        {
            var keys = new[]
            {
                featureComponent,
                LastPathSegment(featureComponent),
                StripInstanceSuffix(featureComponent),
                StripInstanceSuffix(LastPathSegment(featureComponent))
            };

            foreach (var key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key) && componentIndex.TryGetValue(key, out var info))
                {
                    return info;
                }
            }
        }

        return new ComponentStorageInfo(discoveryPath, discoveryTitle, discoveryType);
    }

    /// <summary>
    /// Walks the feature tree of <paramref name="model"/> and returns the first feature
    /// whose name matches <paramref name="featureName"/> (case-insensitive).
    /// If <paramref name="requireSketch"/> is true, the feature type name must contain "Sketch".
    /// </summary>
    internal static IFeature? FindFeature(
        ModelDoc2 model,
        string featureName,
        bool requireSketch,
        Func<Func<string?>, string, string> safeString)
    {
        var stack = new Stack<IFeature>();
        var feature = model.FirstFeature() as IFeature;
        while (feature != null)
        {
            stack.Push(feature);
            feature = feature.GetNextFeature() as IFeature;
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (string.Equals(safeString(() => current.Name, ""), featureName, StringComparison.OrdinalIgnoreCase))
            {
                var typeName = safeString(() => current.GetTypeName2(), "");
                if (!requireSketch || typeName.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current;
                }
            }

            var subFeature = current.GetFirstSubFeature() as IFeature;
            while (subFeature != null)
            {
                stack.Push(subFeature);
                subFeature = subFeature.GetNextSubFeature() as IFeature;
            }
        }

        return null;
    }

    internal static string GetActiveConfigurationName(ModelDoc2 model)
    {
        try
        {
            var configuration = model.GetActiveConfiguration() as Configuration;
            return configuration?.Name ?? "";
        }
        catch
        {
            return "";
        }
    }

    internal static List<string> GetConfigurationNames(
        ModelDoc2 model,
        bool allConfigurations,
        string activeConfig,
        Func<object?, List<object>> toObjectArray)
    {
        if (!allConfigurations)
        {
            return new List<string> { activeConfig };
        }

        var names = toObjectArray(model.GetConfigurationNames())
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

    internal static string LastPathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.Replace('/', '\\');
        var index = normalized.LastIndexOf('\\');
        return index >= 0 && index + 1 < normalized.Length
            ? normalized.Substring(index + 1)
            : normalized;
    }

    internal static string StripInstanceSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var dash = value.LastIndexOf('-');
        return dash > 0 && dash + 1 < value.Length && value.Substring(dash + 1).All(char.IsDigit)
            ? value.Substring(0, dash)
            : value;
    }

    private static int GetDocumentTypeFromPath(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".sldprt", StringComparison.OrdinalIgnoreCase)) return (int)swDocumentTypes_e.swDocPART;
        if (extension.Equals(".sldasm", StringComparison.OrdinalIgnoreCase)) return (int)swDocumentTypes_e.swDocASSEMBLY;
        if (extension.Equals(".slddrw", StringComparison.OrdinalIgnoreCase)) return (int)swDocumentTypes_e.swDocDRAWING;
        return 0;
    }

    private static void AddKey(IDictionary<string, ComponentStorageInfo> index, string key, ComponentStorageInfo info)
    {
        if (!string.IsNullOrWhiteSpace(key) && !index.ContainsKey(key))
        {
            index.Add(key, info);
        }
    }
}

/// <summary>Lightweight storage descriptor resolved from the component index.</summary>
internal sealed record ComponentStorageInfo(string Path, string Title, int Type);
