using System.Collections.Generic;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

/// <summary>
/// Thin facade that preserves the existing helper call sites while delegating to focused modules.
/// </summary>
internal static class AnalysisHelpers
{
    public static Dictionary<string, IComponent2> BuildComponentIndex(AssemblyDoc assembly)
        => AnalysisComponentIndexSupport.BuildComponentIndex(assembly);

    public static ModelDoc2? TryOpenModelIfNeeded(ISldWorks app, string filePath, swDocumentTypes_e docType, out bool openedByUs)
        => AnalysisDocumentSupport.TryOpenModelIfNeeded(app, filePath, docType, out openedByUs);

    public static void CloseDocIfOpenedByUs(ISldWorks app, ModelDoc2? doc, bool openedByUs)
        => AnalysisDocumentSupport.CloseDocIfOpenedByUs(app, doc, openedByUs);

    public static Dictionary<string, string?> GetTopLevelComponentFolderMembership(
        ModelDoc2 model,
        AssemblyDoc assembly,
        object[]? prebuiltTopLevelComponents = null)
        => AnalysisDocumentSupport.GetTopLevelComponentFolderMembership(model, assembly, prebuiltTopLevelComponents);

    public static IEnumerable<IFeature> EnumerateFeatures(object? featuresObj)
        => AnalysisDocumentSupport.EnumerateFeatures(featuresObj);

    public static List<AssemblyComponent> BuildHierarchyTree(List<AssemblyComponent> flatComponents)
        => AnalysisStructureSupport.BuildHierarchyTree(flatComponents);

    public static List<BatchInputItem> ParseBatchInputItems(string json)
        => AnalysisStructureSupport.ParseBatchInputItems(json);

    public static Dictionary<string, string> ExtractCustomProperties(ModelDoc2 model, ILogger? logger)
        => AnalysisExtractionSupport.ExtractCustomProperties(model, logger);

    public static List<PartFeature> ExtractFeatures(ModelDoc2 model)
        => AnalysisExtractionSupport.ExtractFeatures(model);

    public static List<PartFeature> ExtractFeaturesMinimal(ModelDoc2 model)
        => AnalysisExtractionSupport.ExtractFeaturesMinimal(model);

    public static int CountFeatures(ModelDoc2 model)
        => AnalysisExtractionSupport.CountFeatures(model);

    public static PartMassProperties? ExtractMassProperties(ModelDoc2 model, ILogger? logger)
        => AnalysisExtractionSupport.ExtractMassProperties(model, logger);

    public static string GetFeatureTypeName(string typeCode)
        => AnalysisNamingSupport.GetFeatureTypeName(typeCode);

    public static string GetMateTypeName(int typeCode)
        => AnalysisNamingSupport.GetMateTypeName(typeCode);

    public static string GetEntityTypeName(object? entity)
        => AnalysisNamingSupport.GetEntityTypeName(entity);

    public static string GetEntityName(object? entity)
        => AnalysisNamingSupport.GetEntityName(entity);

    public static string GetPaperSizeName(int code)
        => AnalysisNamingSupport.GetPaperSizeName(code);

    public static string GetViewTypeName(int type)
        => AnalysisNamingSupport.GetViewTypeName(type);

    public static string GetDimensionTypeName(int type)
        => AnalysisNamingSupport.GetDimensionTypeName(type);

    public static string GetBomTypeName(int type)
        => AnalysisNamingSupport.GetBomTypeName(type);

    public static double MmToMeters(double mm)
        => AnalysisTransformSupport.MmToMeters(mm);

    public static double MetersToMm(double meters)
        => AnalysisTransformSupport.MetersToMm(meters);

    public static AssemblyTransform? ExtractTransform(Component2 component, ILogger? logger)
        => AnalysisTransformSupport.ExtractTransform(component, logger);
}
