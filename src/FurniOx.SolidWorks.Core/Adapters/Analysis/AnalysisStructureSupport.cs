using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

internal static class AnalysisStructureSupport
{
    public static List<AssemblyComponent> BuildHierarchyTree(List<AssemblyComponent> flatComponents)
    {
        if (flatComponents.Count == 0)
        {
            return new List<AssemblyComponent>();
        }

        var indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < flatComponents.Count; i++)
        {
            var name = flatComponents[i].Name ?? string.Empty;
            if (!string.IsNullOrEmpty(name) && !indexByName.ContainsKey(name))
            {
                indexByName[name] = i;
            }
        }

        var componentByName = new Dictionary<string, AssemblyComponent>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in flatComponents)
        {
            if (!string.IsNullOrEmpty(component.Name))
            {
                componentByName[component.Name] = component;
            }
        }

        var childrenByParent = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var rootNames = new List<string>();

        foreach (var component in flatComponents)
        {
            var name = component.Name ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var lastSlash = name.LastIndexOf('/');
            if (lastSlash < 0)
            {
                rootNames.Add(name);
                continue;
            }

            var parent = name.Substring(0, lastSlash);
            if (!childrenByParent.TryGetValue(parent, out var list))
            {
                list = new List<string>();
                childrenByParent[parent] = list;
            }

            list.Add(name);
        }

        static int SortKey(Dictionary<string, int> indexMap, string name)
            => indexMap.TryGetValue(name, out var index) ? index : int.MaxValue;

        rootNames.Sort((left, right) => SortKey(indexByName, left).CompareTo(SortKey(indexByName, right)));
        foreach (var pair in childrenByParent)
        {
            pair.Value.Sort((left, right) => SortKey(indexByName, left).CompareTo(SortKey(indexByName, right)));
        }

        AssemblyComponent BuildNode(string name)
        {
            if (!componentByName.TryGetValue(name, out var baseNode))
            {
                return new AssemblyComponent { Name = name };
            }

            if (!childrenByParent.TryGetValue(name, out var childNames) || childNames.Count == 0)
            {
                return baseNode with { Children = null };
            }

            var children = new List<AssemblyComponent>(childNames.Count);
            foreach (var childName in childNames)
            {
                children.Add(BuildNode(childName));
            }

            return baseNode with { Children = children };
        }

        var roots = new List<AssemblyComponent>(rootNames.Count);
        foreach (var rootName in rootNames)
        {
            roots.Add(BuildNode(rootName));
        }

        return roots;
    }

    public static List<BatchInputItem> ParseBatchInputItems(string json)
    {
        var items = new List<BatchInputItem>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return items;
        }

        var rootNode = JsonNode.Parse(json);
        if (rootNode is not JsonArray array)
        {
            return items;
        }

        foreach (var node in array)
        {
            if (node is not JsonObject obj)
            {
                continue;
            }

            static string ReadString(JsonObject jsonObject, params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (jsonObject.TryGetPropertyValue(key, out var valueNode) && valueNode is JsonValue value)
                    {
                        var text = value.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }

                return string.Empty;
            }

            var instancePath = ReadString(obj, "instance_path", "InstancePath", "instancePath");
            var filePath = ReadString(obj, "file_path", "FilePath", "filePath");
            if (string.IsNullOrWhiteSpace(instancePath))
            {
                continue;
            }

            items.Add(new BatchInputItem
            {
                InstancePath = instancePath,
                FilePath = filePath
            });
        }

        return items;
    }
}
