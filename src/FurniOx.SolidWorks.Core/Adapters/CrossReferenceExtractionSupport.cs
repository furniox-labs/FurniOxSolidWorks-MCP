#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed record CrossReferenceScanOptions
{
    public bool IncludeExternalFileReferences { get; init; } = true;
    public bool IncludeAuxiliaryReferences { get; init; } = true;
    public bool IncludeDrawingReferences { get; init; } = true;
    public bool IncludeEquationReferences { get; init; } = true;
    public bool AllConfigurations { get; init; }
}

public static class CrossReferenceExtractionSupport
{
    private static readonly Regex EquationCrossPartTokenRegex = new(
        @"@(?<token>[^""'=+\-*/(),\[\]]+?(?:<\d+>)?\.(?:Part|Assembly|SLDPRT|SLDASM))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<CrossReferenceItem> ScanDocumentReferences(
        ModelDoc2 model,
        CrossReferenceScanOptions options)
    {
        var references = new List<CrossReferenceItem>();
        if (options.IncludeExternalFileReferences) references.AddRange(ScanExternalFileReferences(model));
        if (options.IncludeAuxiliaryReferences) references.AddRange(ScanAuxiliaryExternalFileReferences(model));
        if (options.IncludeDrawingReferences) references.AddRange(ScanDrawingReferences(model));
        if (options.IncludeEquationReferences) references.AddRange(ScanEquationReferences(model, options.AllConfigurations));
        return references;
    }

    public static CrossReferenceSummary BuildSummary(IEnumerable<CrossReferenceItem> references)
        => CrossReferenceItemBuilder.BuildSummary(references);

    public static List<CrossReferenceItem> ScanComponentExternalReferences(
        ModelDoc2 active,
        string? componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            throw new InvalidOperationException("ComponentName is required for component-scoped reference scan.");
        }

        if (((IModelDoc2)active).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            throw new InvalidOperationException("Component-scoped reference scan requires an active assembly.");
        }

        var assembly = (IAssemblyDoc)active;
        var component = ToObjectArray(assembly.GetComponents(false))
            .OfType<IComponent2>()
            .FirstOrDefault(c => string.Equals(SafeString(() => c.Name2), componentName, StringComparison.OrdinalIgnoreCase));

        if (component == null)
        {
            throw new InvalidOperationException($"Component not found in active assembly: {componentName}");
        }

        object modelPathNames;
        object componentPathNames;
        object featureNames;
        object dataTypes;
        object statuses;
        object referencedEntities;
        object featureComponents;
        var configOption = 0;
        var configName = "";

        component.ListExternalFileReferences2(
            out modelPathNames, out componentPathNames, out featureNames, out dataTypes,
            out statuses, out referencedEntities, out featureComponents, out configOption, out configName);

        return BuildExternalReferenceItems(
            active, "Component.ListExternalFileReferences2", "Component",
            SafeString(() => component.Name2), modelPathNames, componentPathNames, featureNames,
            dataTypes, statuses, referencedEntities, featureComponents, configOption, configName);
    }

    public static List<CrossReferenceItem> ScanFeatureExternalReferences(
        ModelDoc2 active,
        string? featureName,
        bool requireSketch)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            throw new InvalidOperationException(requireSketch
                ? "SketchName is required for sketch-scoped reference scan."
                : "FeatureName is required for feature-scoped reference scan.");
        }

        var feature = CrossReferenceStorageSupport.FindFeature(active, featureName!, requireSketch, SafeString);
        if (feature == null)
        {
            throw new InvalidOperationException(requireSketch
                ? $"Sketch feature not found in active document: {featureName}"
                : $"Feature not found in active document: {featureName}");
        }

        object modelPathNames;
        object componentPathNames;
        object featureNames;
        object dataTypes;
        object statuses;
        object referencedEntities;
        object featureComponents;
        var configOption = 0;
        var configName = "";

        feature.ListExternalFileReferences2(
            out modelPathNames, out componentPathNames, out featureNames, out dataTypes,
            out statuses, out referencedEntities, out featureComponents, out configOption, out configName);

        return BuildExternalReferenceItems(
            active, "Feature.ListExternalFileReferences2", requireSketch ? "Sketch" : "Feature",
            SafeString(() => feature.Name), modelPathNames, componentPathNames, featureNames,
            dataTypes, statuses, referencedEntities, featureComponents, configOption, configName);
    }

    // -----------------------------------------------------------------------
    // Private scan methods
    // -----------------------------------------------------------------------

    private static List<CrossReferenceItem> ScanExternalFileReferences(ModelDoc2 model)
    {
        var references = new List<CrossReferenceItem>();
        var extension = model.Extension;
        if (extension == null) return references;

        object modelPathNames, componentPathNames, featureNames, dataTypes, statuses,
               referencedEntities, featureComponents, configOptions, configNames;

        extension.ListExternalFileReferences2(
            out modelPathNames, out componentPathNames, out featureNames, out dataTypes,
            out statuses, out referencedEntities, out featureComponents, out configOptions, out configNames);

        var models = ToObjectArray(modelPathNames);
        var components = ToObjectArray(componentPathNames);
        var features = ToObjectArray(featureNames);
        var types = ToObjectArray(dataTypes);
        var statusValues = ToObjectArray(statuses);
        var entities = ToObjectArray(referencedEntities);
        var featComps = ToObjectArray(featureComponents);
        var configOptionValues = ToObjectArray(configOptions);
        var configNameValues = ToObjectArray(configNames);
        var componentIndex = CrossReferenceStorageSupport.BuildComponentStorageIndex(model, ToObjectArray, SafeString);

        var count = new[]
        {
            models.Count, components.Count, features.Count, types.Count, statusValues.Count,
            entities.Count, featComps.Count, configOptionValues.Count, configNameValues.Count
        }.Max();

        var discoveryPath = SafeString(() => model.GetPathName());
        var discoveryTitle = SafeString(() => model.GetTitle());
        var discoveryType = SafeInt(() => ((IModelDoc2)model).GetType());

        for (var i = 0; i < count; i++)
        {
            var referencedModel = ReadIndexedString(models, i);
            var featureComponent = ReadIndexedString(featComps, i);
            var storage = CrossReferenceStorageSupport.ResolveStorageDocument(
                discoveryPath, discoveryTitle, discoveryType, componentIndex, featureComponent);

            references.Add(CrossReferenceItemBuilder.BuildExternalReferenceItem(
                discoveryPath, discoveryTitle, discoveryType, storage,
                "ListExternalFileReferences2", "", "",
                referencedModel,
                ReadIndexedString(components, i),
                ReadIndexedString(features, i),
                ReadIndexedString(types, i),
                ReadIndexedString(entities, i),
                featureComponent,
                ReadIndexedInt(statusValues, i),
                ReadIndexedInt(configOptionValues, i),
                ReadIndexedString(configNameValues, i),
                PathExists(referencedModel),
                IsStalePath(referencedModel)));
        }

        return references;
    }

    private static List<CrossReferenceItem> ScanAuxiliaryExternalFileReferences(ModelDoc2 model)
    {
        var references = new List<CrossReferenceItem>();
        object featureNames;
        object externalFileNames;

        try
        {
            ((IModelDoc2)model).ListAuxiliaryExternalFileReferences(out featureNames, out externalFileNames);
        }
        catch
        {
            return references;
        }

        var features = ToObjectArray(featureNames);
        var files = ToObjectArray(externalFileNames);
        var count = Math.Max(features.Count, files.Count);

        var docPath = SafeString(() => model.GetPathName());
        var docTitle = SafeString(() => model.GetTitle());
        var docType = SafeInt(() => ((IModelDoc2)model).GetType());

        for (var i = 0; i < count; i++)
        {
            var referencedModel = ReadIndexedString(files, i);
            var exists = PathExists(referencedModel);
            var stalePath = IsStalePath(referencedModel);
            references.Add(new CrossReferenceItem
            {
                Category = "AuxiliaryExternalFileReference",
                Source = "ListAuxiliaryExternalFileReferences",
                ReferencingDocumentPath = docPath,
                ReferencingDocumentTitle = docTitle,
                ReferencingDocumentType = docType,
                DiscoveredViaDocumentPath = docPath,
                DiscoveredViaDocumentTitle = docTitle,
                DiscoveredViaDocumentType = docType,
                ReferencingEntityType = "AuxiliaryReference",
                ReferencingEntityName = ReadIndexedString(features, i),
                ReferencedModelPath = referencedModel,
                ReferencedModelPathExists = exists,
                StalePath = stalePath,
                FeatureName = ReadIndexedString(features, i),
                StatusName = exists ? "Ok" : "Missing",
                IsBroken = stalePath,
                IsHardBroken = stalePath
            });
        }

        return references;
    }

    private static List<CrossReferenceItem> ScanDrawingReferences(ModelDoc2 model)
    {
        if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        {
            return new List<CrossReferenceItem>();
        }

        var references = new List<CrossReferenceItem>();
        if (model is not IDrawingDoc drawing) return references;

        var docPath = SafeString(() => model.GetPathName());
        var docTitle = SafeString(() => model.GetTitle());
        var docType = SafeInt(() => ((IModelDoc2)model).GetType());

        IView? view = null;
        try { view = drawing.GetFirstView() as IView; } catch { }

        while (view != null)
        {
            var viewName = SafeString(() => view.Name);
            var referencedModel = SafeString(() => view.GetReferencedModelName());
            var referencedConfig = SafeString(() => view.ReferencedConfiguration);

            if (!string.IsNullOrWhiteSpace(referencedModel))
            {
                var exists = PathExists(referencedModel);
                var stalePath = IsStalePath(referencedModel);
                references.Add(new CrossReferenceItem
                {
                    Category = "DrawingViewModelReference",
                    Source = "DrawingView",
                    ReferencingDocumentPath = docPath,
                    ReferencingDocumentTitle = docTitle,
                    ReferencingDocumentType = docType,
                    DiscoveredViaDocumentPath = docPath,
                    DiscoveredViaDocumentTitle = docTitle,
                    DiscoveredViaDocumentType = docType,
                    ReferencingEntityType = "DrawingView",
                    ReferencingEntityName = viewName,
                    ReferencedModelPath = referencedModel,
                    ReferencedModelPathExists = exists,
                    StalePath = stalePath,
                    FeatureName = viewName,
                    ConfigName = referencedConfig,
                    StatusName = exists ? "Ok" : "Missing",
                    IsBroken = stalePath,
                    IsHardBroken = stalePath
                });
            }

            try { view = view.GetNextView() as IView; }
            catch { break; }
        }

        return references;
    }

    private static List<CrossReferenceItem> ScanEquationReferences(ModelDoc2 model, bool allConfigurations)
    {
        var references = new List<CrossReferenceItem>();
        var originalConfig = CrossReferenceStorageSupport.GetActiveConfigurationName(model);
        var configs = CrossReferenceStorageSupport.GetConfigurationNames(model, allConfigurations, originalConfig, ToObjectArray);

        var docPath = SafeString(() => model.GetPathName());
        var docTitle = SafeString(() => model.GetTitle());
        var docType = SafeInt(() => ((IModelDoc2)model).GetType());

        foreach (var configName in configs)
        {
            if (allConfigurations && !string.IsNullOrWhiteSpace(configName))
            {
                try { model.ShowConfiguration2(configName); } catch { }
            }

            var equationManager = model.GetEquationMgr();
            if (equationManager == null) continue;

            var count = SafeInt(() => equationManager.GetCount());
            for (var index = 0; index < count; index++)
            {
                var equation = SafeString(() => equationManager.Equation[index]);
                var status = SafeNullableInt(() => equationManager.Status);
                var broken = false;
                var notes = "";
                try
                {
                    _ = equationManager.Value[index];
                    broken = status.HasValue && status.Value != 0;
                }
                catch (Exception ex)
                {
                    broken = true;
                    notes = ex.Message;
                }

                var matches = EquationCrossPartTokenRegex.Matches(equation);
                if (matches.Count == 0)
                {
                    if (broken)
                    {
                        references.Add(CrossReferenceItemBuilder.BuildEquationItem(
                            docPath, docTitle, docType, configName, index, equation, "", status, broken, notes));
                    }
                    continue;
                }

                foreach (Match match in matches)
                {
                    references.Add(CrossReferenceItemBuilder.BuildEquationItem(
                        docPath, docTitle, docType, configName, index, equation,
                        match.Groups["token"].Value, status, broken, notes));
                }
            }
        }

        if (allConfigurations && !string.IsNullOrWhiteSpace(originalConfig))
        {
            try { model.ShowConfiguration2(originalConfig); } catch { }
        }

        return references;
    }

    // -----------------------------------------------------------------------
    // Shared build helper (component/feature scoped external refs)
    // -----------------------------------------------------------------------

    private static List<CrossReferenceItem> BuildExternalReferenceItems(
        ModelDoc2 model,
        string source,
        string entityType,
        string entityName,
        object modelPathNames,
        object componentPathNames,
        object featureNames,
        object dataTypes,
        object statuses,
        object referencedEntities,
        object featureComponents,
        int configOption,
        string configName)
    {
        var references = new List<CrossReferenceItem>();
        var models = ToObjectArray(modelPathNames);
        var components = ToObjectArray(componentPathNames);
        var features = ToObjectArray(featureNames);
        var types = ToObjectArray(dataTypes);
        var statusValues = ToObjectArray(statuses);
        var entities = ToObjectArray(referencedEntities);
        var featComps = ToObjectArray(featureComponents);
        var componentIndex = CrossReferenceStorageSupport.BuildComponentStorageIndex(model, ToObjectArray, SafeString);
        var count = new[] { models.Count, components.Count, features.Count, types.Count, statusValues.Count, entities.Count, featComps.Count }.Max();

        var discoveryPath = SafeString(() => model.GetPathName());
        var discoveryTitle = SafeString(() => model.GetTitle());
        var discoveryType = SafeInt(() => ((IModelDoc2)model).GetType());

        for (var i = 0; i < count; i++)
        {
            var featureComponent = ReadIndexedString(featComps, i);
            var resolvedFeatureComponent = string.IsNullOrWhiteSpace(featureComponent) && entityType == "Component"
                ? entityName
                : featureComponent;
            var referencedModel = ReadIndexedString(models, i);
            var storage = CrossReferenceStorageSupport.ResolveStorageDocument(
                discoveryPath, discoveryTitle, discoveryType, componentIndex, resolvedFeatureComponent);

            references.Add(CrossReferenceItemBuilder.BuildExternalReferenceItem(
                discoveryPath, discoveryTitle, discoveryType, storage,
                source, entityType, entityName,
                referencedModel,
                ReadIndexedString(components, i),
                ReadIndexedString(features, i),
                ReadIndexedString(types, i),
                ReadIndexedString(entities, i),
                resolvedFeatureComponent,
                ReadIndexedInt(statusValues, i),
                configOption,
                configName,
                PathExists(referencedModel),
                IsStalePath(referencedModel)));
        }

        return references;
    }

    // -----------------------------------------------------------------------
    // Primitive helpers
    // -----------------------------------------------------------------------

    private static bool PathExists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    private static bool IsStalePath(string path) => !string.IsNullOrWhiteSpace(path) && !File.Exists(path);

    private static List<object> ToObjectArray(object? value)
    {
        if (value == null) return new List<object>();
        if (value is object[] objectArray) return objectArray.ToList();
        if (value is Array array) return array.Cast<object>().ToList();
        return new List<object> { value };
    }

    private static string ReadIndexedString(IReadOnlyList<object> values, int index)
        => index < values.Count ? values[index]?.ToString() ?? "" : "";

    private static int? ReadIndexedInt(IReadOnlyList<object> values, int index)
    {
        if (index >= values.Count || values[index] == null) return null;
        if (values[index] is int integer) return integer;
        if (int.TryParse(values[index].ToString(), out var parsed)) return parsed;
        return null;
    }

    private static string SafeString(Func<string?> getter, string fallback = "")
    {
        try { return getter() ?? fallback; }
        catch { return fallback; }
    }

    private static int SafeInt(Func<int> getter, int fallback = 0)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    private static int? SafeNullableInt(Func<int> getter)
    {
        try { return getter(); }
        catch { return null; }
    }
}
