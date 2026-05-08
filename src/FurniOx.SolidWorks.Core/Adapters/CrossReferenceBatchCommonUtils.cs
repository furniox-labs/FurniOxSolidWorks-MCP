#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

internal static partial class CrossReferenceBatchRunner
{
    private sealed record ComponentStorageInfo(string Path, string Title, int Type);

    private static List<string> GetConfigurationNames(ModelDoc2 model, bool allConfigurations, string activeConfig)
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

    private static string GetActiveConfigurationName(ModelDoc2 model)
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

    private static Dictionary<string, ComponentStorageInfo> BuildComponentStorageIndex(ModelDoc2 model)
    {
        var index = new Dictionary<string, ComponentStorageInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                return index;
            }

            var assembly = (IAssemblyDoc)model;
            foreach (var component in ToObjectArray(assembly.GetComponents(false)).OfType<IComponent2>())
            {
                var name = SafeString(() => component.Name2);
                var path = SafeString(() => component.GetPathName());
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var info = new ComponentStorageInfo(path, Path.GetFileName(path), GetDocumentTypeFromPath(path));
                AddComponentKey(index, name, info);
                AddComponentKey(index, LastPathSegment(name), info);
                AddComponentKey(index, Path.GetFileName(path), info);
                AddComponentKey(index, Path.GetFileNameWithoutExtension(path), info);
                AddComponentKey(index, StripInstanceSuffix(Path.GetFileNameWithoutExtension(path)), info);
                AddComponentKey(index, StripInstanceSuffix(LastPathSegment(name)), info);
            }
        }
        catch
        {
        }

        return index;
    }

    private static ComponentStorageInfo ResolveStorageDocument(
        ModelDoc2 discoveryModel,
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

        return new ComponentStorageInfo(
            SafeString(() => discoveryModel.GetPathName()),
            SafeString(() => discoveryModel.GetTitle()),
            SafeInt(() => ((IModelDoc2)discoveryModel).GetType()));
    }

    private static void AddComponentKey(
        IDictionary<string, ComponentStorageInfo> index,
        string key,
        ComponentStorageInfo info)
    {
        if (!string.IsNullOrWhiteSpace(key) && !index.ContainsKey(key))
        {
            index.Add(key, info);
        }
    }

    private static string LastPathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var normalized = value.Replace('/', '\\');
        var index = normalized.LastIndexOf('\\');
        return index >= 0 && index + 1 < normalized.Length
            ? normalized.Substring(index + 1)
            : normalized;
    }

    private static string StripInstanceSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var dash = value.LastIndexOf('-');
        return dash > 0 && dash + 1 < value.Length && value.Substring(dash + 1).All(char.IsDigit)
            ? value.Substring(0, dash)
            : value;
    }

    private static bool IsHardBrokenStatus(int? status)
        => status == (int)swExternalReferenceStatus_e.swExternalReferenceBroken
            || status == (int)swExternalReferenceStatus_e.swExternalReferenceDangling;

    private static bool PathExists(string path)
        => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static bool IsStalePath(string path)
        => !string.IsNullOrWhiteSpace(path) && !File.Exists(path);

    private static string DecodeExternalReferenceStatus(int? status) => status switch
    {
        (int)swExternalReferenceStatus_e.swExternalReferenceBroken => "Broken",
        (int)swExternalReferenceStatus_e.swExternalReferenceLocked => "Locked",
        (int)swExternalReferenceStatus_e.swExternalReferenceInContext => "InContext",
        (int)swExternalReferenceStatus_e.swExternalReferenceOutOfContext => "OutOfContext",
        (int)swExternalReferenceStatus_e.swExternalReferenceDangling => "Dangling",
        null => "",
        _ => $"Unknown({status.Value})"
    };

    private static string DecodeConfigOption(int? configOption) => configOption switch
    {
        (int)swExternalFileReferencesConfig_e.swExternalFileReferencesConfigNone => "None",
        (int)swExternalFileReferencesConfig_e.swExternalFileReferencesCurrentConfig => "CurrentConfig",
        (int)swExternalFileReferencesConfig_e.swExternalFileReferencesNamedConfig => "NamedConfig",
        null => "",
        _ => $"Unknown({configOption.Value})"
    };

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

    private static string ReadIndexedString(IReadOnlyList<object> values, int index)
    {
        if (index < 0 || index >= values.Count)
        {
            return "";
        }

        return values[index]?.ToString() ?? "";
    }

    private static int? ReadIndexedInt(IReadOnlyList<object> values, int index)
    {
        if (index < 0 || index >= values.Count || values[index] == null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(values[index]);
        }
        catch
        {
            return null;
        }
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

    private static int SafeInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }

    private static int? SafeNullableInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }
}
