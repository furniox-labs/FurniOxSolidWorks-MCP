using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FurniOx.SolidWorks.Core.Extensions;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Assembly;

internal static class AssemblyComponentSupport
{
    internal static (List<Shared.Models.AssemblyComponent> Components, int TotalCount) ExtractFiltered(
        SldWorks? application,
        ILogger logger,
        ModelDoc2 model,
        AssemblyDoc assembly,
        bool includeHierarchy,
        string? pathFilter,
        string? namePathFilter,
        bool includeSuppressed,
        bool includeHidden,
        bool includeEnvelope,
        bool includeVirtual,
        bool minimalMode,
        bool includeComponentFolders,
        bool openReferencedDocs,
        Dictionary<string, IComponent2>? prebuiltIndex = null)
    {
        var components = new List<Shared.Models.AssemblyComponent>();
        object[]? componentArray;
        var totalCount = 0;
        Dictionary<string, string?>? componentFolderByName = null;
        var openedDocsByUs = new List<ModelDoc2>();

        if (includeHierarchy)
        {
            var componentIndex = prebuiltIndex ?? AnalysisHelpers.BuildComponentIndex(assembly);
            componentArray = componentIndex.Values.Cast<object>().ToArray();
            totalCount = componentArray.Length;

            if (includeComponentFolders && !minimalMode)
            {
                componentFolderByName = BuildComponentFolderMembershipMap(
                    application,
                    model,
                    assembly,
                    componentIndex,
                    openReferencedDocs,
                    openedDocsByUs);
            }
        }
        else
        {
            componentArray = assembly.GetComponents(true).ToObjectArraySafe();
            totalCount = componentArray?.Length ?? 0;

            if (includeComponentFolders && !minimalMode)
            {
                componentFolderByName = BuildComponentFolderMembershipMap(
                    application,
                    model,
                    assembly,
                    componentIndex: null,
                    openReferencedDocs: false,
                    openedDocsByUs);
            }
        }

        if (componentArray == null)
        {
            return (components, 0);
        }

        var assemblyTitle = model.GetTitle();
        if (assemblyTitle.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
        {
            assemblyTitle = assemblyTitle[..^7];
        }

        foreach (var componentObject in componentArray)
        {
            if (componentObject is not IComponent2 component)
            {
                continue;
            }

            var name2 = component.Name2 ?? string.Empty;
            if (!string.IsNullOrEmpty(namePathFilter) &&
                !name2.Contains(namePathFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = component.GetPathName();
            if (!string.IsNullOrEmpty(pathFilter) &&
                !path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!includeVirtual && component.IsVirtual)
            {
                continue;
            }

            var isVisible = component.Visible == (int)swComponentVisibilityState_e.swComponentVisible;
            if (!includeHidden && !isVisible)
            {
                continue;
            }

            var suppressed = false;
            if (!includeSuppressed || !minimalMode)
            {
                // Use GetSuppression2() rather than IsSuppressed() to avoid false positives:
                // IsSuppressed() returns true for lightweight/unresolved components that are NOT
                // actually suppressed. GetSuppression2() returns the definitive suppression state.
                var suppressionState = component.GetSuppression2();
                suppressed = suppressionState == (int)swComponentSuppressionState_e.swComponentSuppressed;
            }

            if (!includeSuppressed && suppressed)
            {
                continue;
            }

            var envelope = false;
            if (!includeEnvelope || !minimalMode)
            {
                envelope = component.IsEnvelope();
            }

            if (!includeEnvelope && envelope)
            {
                continue;
            }

            if (minimalMode)
            {
                components.Add(new Shared.Models.AssemblyComponent
                {
                    Name = name2,
                    Path = path,
                    Type = path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? "Assembly" : "Part"
                });

                continue;
            }

            var typeName = "Unknown";
            if (!string.IsNullOrEmpty(path))
            {
                typeName = Path.GetExtension(path).ToUpperInvariant() switch
                {
                    ".SLDPRT" => "Part",
                    ".SLDASM" => "Assembly",
                    _ => "Unknown"
                };
            }

            string? folderPath = null;
            componentFolderByName?.TryGetValue(name2, out folderPath);
            var component2 = component as Component2;

            components.Add(new Shared.Models.AssemblyComponent
            {
                Name = name2,
                Path = path,
                Type = typeName,
                FeatureManagerFolderPath = folderPath,
                Suppressed = suppressed,
                Hidden = !isVisible,
                IsVirtual = component.IsVirtual,
                IsEnvelope = envelope,
                ConfigurationName = component.ReferencedConfiguration ?? string.Empty,
                InstanceCount = 1,
                Transform = component2 != null ? AnalysisHelpers.ExtractTransform(component2, logger) : null,
                SelectByIDString = $"{component.Name2}@{assemblyTitle}"
            });
        }

        foreach (var openedDoc in openedDocsByUs)
        {
            try
            {
                application?.CloseDoc(openedDoc.GetTitle());
            }
            catch
            {
            }
        }

        return (components, totalCount);
    }

    private static Dictionary<string, string?> BuildComponentFolderMembershipMap(
        SldWorks? application,
        ModelDoc2 rootModel,
        AssemblyDoc rootAssembly,
        IDictionary<string, IComponent2>? componentIndex,
        bool openReferencedDocs,
        List<ModelDoc2> openedDocsByUs)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var rootMap = AnalysisHelpers.GetTopLevelComponentFolderMembership(rootModel, rootAssembly);
        foreach (var entry in rootMap)
        {
            if (!result.ContainsKey(entry.Key))
            {
                result[entry.Key] = entry.Value;
            }
        }

        if (componentIndex == null || application == null)
        {
            return result;
        }

        var perDocumentChildMap = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in componentIndex.Values)
        {
            var instancePath = component.Name2 ?? string.Empty;
            if (string.IsNullOrEmpty(instancePath))
            {
                continue;
            }

            var filePath = component.GetPathName() ?? string.Empty;
            if (!filePath.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var subModel = component.GetModelDoc2() as ModelDoc2;
            var openedByUs = false;
            if (subModel == null && openReferencedDocs)
            {
                subModel = AnalysisHelpers.TryOpenModelIfNeeded(application, filePath, swDocumentTypes_e.swDocASSEMBLY, out openedByUs);
                if (openedByUs && subModel != null)
                {
                    openedDocsByUs.Add(subModel);
                }
            }

            if (subModel == null || subModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                continue;
            }

            if (!perDocumentChildMap.TryGetValue(filePath, out var childMap))
            {
                childMap = AnalysisHelpers.GetTopLevelComponentFolderMembership(subModel, (AssemblyDoc)subModel);
                perDocumentChildMap[filePath] = childMap;
            }

            foreach (var entry in childMap)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                var fullName = $"{instancePath}/{entry.Key}";
                if (!result.ContainsKey(fullName))
                {
                    result[fullName] = entry.Value;
                }
            }
        }

        return result;
    }
}
