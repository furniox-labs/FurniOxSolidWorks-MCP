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

internal static partial class CrossReferenceBatchRunner
{
    private static List<CrossReferenceItem> ScanExternalFileReferences(ModelDoc2 model)
    {
        var references = new List<CrossReferenceItem>();
        var extension = model.Extension;
        if (extension == null)
        {
            return references;
        }

        object modelPathNames;
        object componentPathNames;
        object featureNames;
        object dataTypes;
        object statuses;
        object referencedEntities;
        object featureComponents;
        object configOptions;
        object configNames;

        extension.ListExternalFileReferences2(
            out modelPathNames,
            out componentPathNames,
            out featureNames,
            out dataTypes,
            out statuses,
            out referencedEntities,
            out featureComponents,
            out configOptions,
            out configNames);

        var models = ToObjectArray(modelPathNames);
        var components = ToObjectArray(componentPathNames);
        var features = ToObjectArray(featureNames);
        var types = ToObjectArray(dataTypes);
        var statusValues = ToObjectArray(statuses);
        var entities = ToObjectArray(referencedEntities);
        var featComps = ToObjectArray(featureComponents);
        var configOptionValues = ToObjectArray(configOptions);
        var configNameValues = ToObjectArray(configNames);

        var count = new[]
        {
            models.Count, components.Count, features.Count, types.Count, statusValues.Count,
            entities.Count, featComps.Count, configOptionValues.Count, configNameValues.Count
        }.Max();

        for (var i = 0; i < count; i++)
        {
            var referencedModel = ReadIndexedString(models, i);
            var componentPath = ReadIndexedString(components, i);
            var feature = ReadIndexedString(features, i);
            var dataType = ReadIndexedString(types, i);
            var status = ReadIndexedInt(statusValues, i);
            var refEntity = ReadIndexedString(entities, i);
            var featureComponent = ReadIndexedString(featComps, i);
            var configOption = ReadIndexedInt(configOptionValues, i);
            var configName = ReadIndexedString(configNameValues, i);

            references.Add(new CrossReferenceItem
            {
                Category = ClassifyExternalReference(feature, dataType, refEntity, featureComponent, componentPath),
                Source = "ListExternalFileReferences2",
                ReferencingDocumentPath = SafeString(() => model.GetPathName()),
                ReferencingDocumentTitle = SafeString(() => model.GetTitle()),
                ReferencingDocumentType = SafeInt(() => ((IModelDoc2)model).GetType()),
                ReferencedModelPath = referencedModel,
                ComponentPath = componentPath,
                FeatureName = feature,
                DataType = dataType,
                ReferencedEntity = refEntity,
                FeatureComponent = featureComponent,
                Status = status,
                StatusName = DecodeExternalReferenceStatus(status),
                IsBroken = status == (int)swExternalReferenceStatus_e.swExternalReferenceBroken
                    || status == (int)swExternalReferenceStatus_e.swExternalReferenceDangling,
                ConfigOption = configOption,
                ConfigOptionName = DecodeConfigOption(configOption),
                ConfigName = configName
            });
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

        for (var i = 0; i < count; i++)
        {
            references.Add(new CrossReferenceItem
            {
                Category = "AuxiliaryExternalFileReference",
                Source = "ListAuxiliaryExternalFileReferences",
                ReferencingDocumentPath = SafeString(() => model.GetPathName()),
                ReferencingDocumentTitle = SafeString(() => model.GetTitle()),
                ReferencingDocumentType = SafeInt(() => ((IModelDoc2)model).GetType()),
                ReferencingEntityType = "AuxiliaryReference",
                ReferencingEntityName = ReadIndexedString(features, i),
                ReferencedModelPath = ReadIndexedString(files, i),
                FeatureName = ReadIndexedString(features, i)
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
        if (model is not IDrawingDoc drawing)
        {
            return references;
        }

        IView? view = null;
        try { view = drawing.GetFirstView() as IView; } catch { }

        while (view != null)
        {
            var viewName = SafeString(() => view.Name);
            var referencedModel = SafeString(() => view.GetReferencedModelName());
            var referencedConfig = SafeString(() => view.ReferencedConfiguration);

            if (!string.IsNullOrWhiteSpace(referencedModel))
            {
                references.Add(new CrossReferenceItem
                {
                    Category = "DrawingViewModelReference",
                    Source = "DrawingView",
                    ReferencingDocumentPath = SafeString(() => model.GetPathName()),
                    ReferencingDocumentTitle = SafeString(() => model.GetTitle()),
                    ReferencingDocumentType = SafeInt(() => ((IModelDoc2)model).GetType()),
                    ReferencingEntityType = "DrawingView",
                    ReferencingEntityName = viewName,
                    ReferencedModelPath = referencedModel,
                    FeatureName = viewName,
                    ConfigName = referencedConfig,
                    StatusName = File.Exists(referencedModel) ? "Ok" : "Missing",
                    IsBroken = !File.Exists(referencedModel)
                });
            }

            try { view = view.GetNextView() as IView; }
            catch { break; }
        }

        return references;
    }

    private static List<CrossReferenceItem> ScanComponentExternalReferences(ModelDoc2 active, string? componentName)
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
            out modelPathNames,
            out componentPathNames,
            out featureNames,
            out dataTypes,
            out statuses,
            out referencedEntities,
            out featureComponents,
            out configOption,
            out configName);

        return BuildExternalReferenceItems(
            active,
            "Component.ListExternalFileReferences2",
            "Component",
            SafeString(() => component.Name2),
            modelPathNames,
            componentPathNames,
            featureNames,
            dataTypes,
            statuses,
            referencedEntities,
            featureComponents,
            configOption,
            configName);
    }

    private static List<CrossReferenceItem> ScanFeatureExternalReferences(
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

        var feature = FindFeature(active, featureName!, requireSketch);
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
            out modelPathNames,
            out componentPathNames,
            out featureNames,
            out dataTypes,
            out statuses,
            out referencedEntities,
            out featureComponents,
            out configOption,
            out configName);

        return BuildExternalReferenceItems(
            active,
            "Feature.ListExternalFileReferences2",
            requireSketch ? "Sketch" : "Feature",
            SafeString(() => feature.Name),
            modelPathNames,
            componentPathNames,
            featureNames,
            dataTypes,
            statuses,
            referencedEntities,
            featureComponents,
            configOption,
            configName);
    }

    private static IFeature? FindFeature(ModelDoc2 model, string featureName, bool requireSketch)
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
            if (string.Equals(SafeString(() => current.Name), featureName, StringComparison.OrdinalIgnoreCase))
            {
                var typeName = SafeString(() => current.GetTypeName2());
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
        var componentIndex = BuildComponentStorageIndex(model);
        var count = new[] { models.Count, components.Count, features.Count, types.Count, statusValues.Count, entities.Count, featComps.Count }.Max();

        for (var i = 0; i < count; i++)
        {
            var feature = ReadIndexedString(features, i);
            var dataType = ReadIndexedString(types, i);
            var refEntity = ReadIndexedString(entities, i);
            var featureComponent = ReadIndexedString(featComps, i);
            var componentPath = ReadIndexedString(components, i);
            var status = ReadIndexedInt(statusValues, i);

            references.Add(BuildExternalReferenceItem(
                model,
                componentIndex,
                source,
                entityType,
                entityName,
                ReadIndexedString(models, i),
                componentPath,
                feature,
                dataType,
                refEntity,
                string.IsNullOrWhiteSpace(featureComponent) && entityType == "Component" ? entityName : featureComponent,
                status,
                configOption,
                configName));
        }

        return references;
    }

    private static CrossReferenceItem BuildExternalReferenceItem(
        ModelDoc2 discoveryModel,
        IReadOnlyDictionary<string, ComponentStorageInfo> componentIndex,
        string source,
        string entityType,
        string entityName,
        string referencedModel,
        string componentPath,
        string feature,
        string dataType,
        string refEntity,
        string featureComponent,
        int? status,
        int? configOption,
        string configName)
    {
        var discoveryPath = SafeString(() => discoveryModel.GetPathName());
        var discoveryTitle = SafeString(() => discoveryModel.GetTitle());
        var discoveryType = SafeInt(() => ((IModelDoc2)discoveryModel).GetType());
        var storage = ResolveStorageDocument(discoveryModel, componentIndex, featureComponent);
        var hardBroken = IsHardBrokenStatus(status);
        var stalePath = IsStalePath(referencedModel);
        var softBroken = stalePath && !hardBroken;

        return new CrossReferenceItem
        {
            Category = ClassifyExternalReference(feature, dataType, refEntity, featureComponent, componentPath),
            Source = source,
            ReferencingDocumentPath = storage.Path,
            ReferencingDocumentTitle = storage.Title,
            ReferencingDocumentType = storage.Type,
            DiscoveredViaDocumentPath = discoveryPath,
            DiscoveredViaDocumentTitle = discoveryTitle,
            DiscoveredViaDocumentType = discoveryType,
            ReferencingEntityType = entityType,
            ReferencingEntityName = entityName,
            ReferencedModelPath = referencedModel,
            ReferencedModelPathExists = PathExists(referencedModel),
            StalePath = stalePath,
            ComponentPath = componentPath,
            FeatureName = feature,
            DataType = dataType,
            ReferencedEntity = refEntity,
            FeatureComponent = featureComponent,
            Status = status,
            StatusName = DecodeExternalReferenceStatus(status),
            IsBroken = hardBroken || softBroken,
            IsHardBroken = hardBroken,
            IsSoftBroken = softBroken,
            ConfigOption = configOption,
            ConfigOptionName = DecodeConfigOption(configOption),
            ConfigName = configName
        };
    }

    private static List<CrossReferenceItem> ScanEquationReferences(ModelDoc2 model, bool allConfigurations)
    {
        var references = new List<CrossReferenceItem>();
        var originalConfig = GetActiveConfigurationName(model);
        var configs = GetConfigurationNames(model, allConfigurations, originalConfig);

        foreach (var configName in configs)
        {
            if (allConfigurations && !string.IsNullOrWhiteSpace(configName))
            {
                try { model.ShowConfiguration2(configName); } catch { }
            }

            var equationManager = model.GetEquationMgr();
            if (equationManager == null)
            {
                continue;
            }

            var count = SafeInt(() => equationManager.GetCount());
            for (var index = 0; index < count; index++)
            {
                var equation = SafeString(() => equationManager.Equation[index]);
                var status = SafeNullableInt(() => equationManager.Status);
                var broken = false;
                double? value = null;
                string notes = "";
                try
                {
                    value = equationManager.Value[index];
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
                        references.Add(BuildEquationItem(model, configName, index, equation, "", status, true, notes));
                    }
                    continue;
                }

                foreach (Match match in matches)
                {
                    var token = match.Groups["token"].Value;
                    references.Add(BuildEquationItem(model, configName, index, equation, token, status, broken, notes));
                }
            }
        }

        if (allConfigurations && !string.IsNullOrWhiteSpace(originalConfig))
        {
            try { model.ShowConfiguration2(originalConfig); } catch { }
        }

        return references;
    }

    private static CrossReferenceItem BuildEquationItem(
        ModelDoc2 model,
        string configName,
        int equationIndex,
        string equation,
        string token,
        int? status,
        bool broken,
        string notes)
        => new()
        {
            Category = "EquationCrossPartReference",
            Source = "EquationManager",
            ReferencingDocumentPath = SafeString(() => model.GetPathName()),
            ReferencingDocumentTitle = SafeString(() => model.GetTitle()),
            ReferencingDocumentType = SafeInt(() => ((IModelDoc2)model).GetType()),
            Status = status,
            StatusName = status.HasValue && status.Value == 0 ? "Ok" : status.HasValue ? $"EquationStatus{status.Value}" : "",
            IsBroken = broken,
            ConfigName = configName,
            EquationIndex = equationIndex,
            EquationText = equation,
            EquationReferenceToken = token,
            Notes = notes
        };

    private static string ClassifyExternalReference(
        string feature,
        string dataType,
        string refEntity,
        string featureComponent,
        string componentPath)
    {
        var text = $"{feature} {dataType} {refEntity} {featureComponent}".ToLowerInvariant();
        if (text.Contains("mate"))
        {
            return "MateReference";
        }
        if (text.Contains("dimension") || text.Contains("dim"))
        {
            return "SketchDimensionExternalReference";
        }
        if (text.Contains("sketch"))
        {
            return "SketchExternalEntityReference";
        }
        if (text.Contains("derived") || text.Contains("import") || text.Contains("insert") || text.Contains("body"))
        {
            return "ImportedOrDerivedBodyReference";
        }
        if (text.Contains("context") || text.Contains("extrude") || text.Contains("cut") || text.Contains("surface"))
        {
            return "InContextFeatureReference";
        }
        if (!string.IsNullOrWhiteSpace(componentPath))
        {
            return "FilePathReference";
        }

        return "ExternalReference";
    }
}
