#nullable enable
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Pure item-construction helpers for cross-reference scanning.
/// Builds <see cref="CrossReferenceItem"/> and <see cref="CrossReferenceSummary"/> instances
/// from raw SolidWorks API output without performing any COM calls.
/// </summary>
internal static class CrossReferenceItemBuilder
{
    internal static CrossReferenceSummary BuildSummary(IEnumerable<CrossReferenceItem> references)
    {
        var items = references as IReadOnlyList<CrossReferenceItem> ?? references.ToList();
        return new CrossReferenceSummary
        {
            ReferenceCount = items.Count,
            ExternalReferenceCount = items.Count(r =>
                r.Source == "ListExternalFileReferences2"
                || r.Source == "Component.ListExternalFileReferences2"
                || r.Source == "Feature.ListExternalFileReferences2"),
            AuxiliaryReferenceCount = items.Count(r => r.Source == "ListAuxiliaryExternalFileReferences"),
            DrawingReferenceCount = items.Count(r => r.Source == "DrawingView"),
            EquationReferenceCount = items.Count(r => r.Source == "EquationManager"),
            StalePathCount = items.Count(r => r.StalePath),
            HardBrokenCount = items.Count(r => r.IsHardBroken),
            SoftBrokenCount = items.Count(r => r.IsSoftBroken),
            BrokenReferenceCount = items.Count(r => r.IsBroken)
        };
    }

    internal static CrossReferenceItem BuildEquationItem(
        string documentPath,
        string documentTitle,
        int documentType,
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
            ReferencingDocumentPath = documentPath,
            ReferencingDocumentTitle = documentTitle,
            ReferencingDocumentType = documentType,
            DiscoveredViaDocumentPath = documentPath,
            DiscoveredViaDocumentTitle = documentTitle,
            DiscoveredViaDocumentType = documentType,
            ReferencingEntityType = "Equation",
            ReferencingEntityName = equationIndex.ToString(),
            Status = status,
            StatusName = status.HasValue && status.Value == 0 ? "Ok" : status.HasValue ? $"EquationStatus{status.Value}" : "",
            IsBroken = broken,
            IsHardBroken = broken,
            ConfigName = configName,
            EquationIndex = equationIndex,
            EquationText = equation,
            EquationReferenceToken = token,
            Notes = notes
        };

    internal static CrossReferenceItem BuildExternalReferenceItem(
        string discoveryPath,
        string discoveryTitle,
        int discoveryType,
        ComponentStorageInfo storage,
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
        string configName,
        bool pathExists,
        bool stalePath)
    {
        var hardBroken = IsHardBrokenStatus(status);
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
            ReferencedModelPathExists = pathExists,
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

    private static string ClassifyExternalReference(
        string feature,
        string dataType,
        string refEntity,
        string featureComponent,
        string componentPath)
    {
        var text = $"{feature} {dataType} {refEntity} {featureComponent}".ToLowerInvariant();
        if (text.Contains("mate")) return "MateReference";
        if (text.Contains("dimension") || text.Contains("dim")) return "SketchDimensionExternalReference";
        if (text.Contains("sketch")) return "SketchExternalEntityReference";
        if (text.Contains("derived") || text.Contains("import") || text.Contains("insert") || text.Contains("body"))
            return "ImportedOrDerivedBodyReference";
        if (text.Contains("context") || text.Contains("extrude") || text.Contains("cut") || text.Contains("surface"))
            return "InContextFeatureReference";
        if (!string.IsNullOrWhiteSpace(componentPath)) return "FilePathReference";

        return "ExternalReference";
    }

    private static bool IsHardBrokenStatus(int? status)
        => status == (int)swExternalReferenceStatus_e.swExternalReferenceBroken
            || status == (int)swExternalReferenceStatus_e.swExternalReferenceDangling;

    private static string DecodeExternalReferenceStatus(int? status) => status switch
    {
        (int)swExternalReferenceStatus_e.swExternalReferenceBroken => "Broken",
        (int)swExternalReferenceStatus_e.swExternalReferenceLocked => "Locked",
        (int)swExternalReferenceStatus_e.swExternalReferenceDangling => "Dangling",
        (int)swExternalReferenceStatus_e.swExternalReferenceOutOfContext => "OutOfContext",
        (int)swExternalReferenceStatus_e.swExternalReferenceInContext => "InContext",
        null => "",
        _ => $"Status{status!.Value}"
    };

    private static string DecodeConfigOption(int? configOption) => configOption switch
    {
        0 => "",
        1 => "AllConfigurations",
        2 => "NamedConfiguration",
        3 => "CurrentConfiguration",
        null => "",
        _ => $"ConfigOption{configOption!.Value}"
    };
}
