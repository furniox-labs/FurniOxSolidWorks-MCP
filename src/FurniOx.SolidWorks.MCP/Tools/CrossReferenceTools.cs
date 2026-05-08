using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for read-only single-document cross-reference scans.
/// File-input batch and post-rename repair flows are outside the public tool surface.
/// </summary>
[McpServerToolType]
public sealed class CrossReferenceTools : ToolsBase
{
    public CrossReferenceTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Scan cross references for one SolidWorks document. Includes standard external refs, auxiliary refs, drawing view model refs, and equation refs by default.")]
    public async Task<object?> ScanExternalReferences(
        [Description("Optional document path. Omit to scan the active document only.")] string? documentPath = null,
        [Description("Optional JSON output path for the scan result.")] string? outputPath = null,
        [Description("If true, include IModelDocExtension.ListExternalFileReferences2 rows.")] bool includeExternalFileReferences = true,
        [Description("If true, include IModelDoc2.ListAuxiliaryExternalFileReferences rows.")] bool includeAuxiliaryReferences = true,
        [Description("If true, include drawing view referenced model rows for drawings.")] bool includeDrawingReferences = true,
        [Description("If true, include equation-manager cross-part reference token rows.")] bool includeEquationReferences = true,
        [Description("If true, skip drawing/equation scans and only check fast file-reference surfaces. Default false.")] bool quickMode = false,
        [Description("If true, scan every configuration in the document for equations.")] bool allConfigurations = false,
        [Description("If true, open documentPath when it is not already loaded. Default false protects large projects from GUI/VRAM growth.")] bool openUnloadedDocuments = false,
        [Description("If true, close a document opened by this tool.")] bool closeOpened = true,
        [Description("If true, documents opened by this tool are hidden in the SolidWorks GUI. Default true.")] bool hiddenInGui = true,
        [Description("If true, open unloaded documents with SolidWorks lightweight options. Default true.")] bool lightWeightOpen = true,
        [Description("If true, do not load hidden components while opening assemblies for scan. Default true.")] bool dontLoadHiddenComponents = true,
        [Description("If >0, mark a document scan failed when a single OpenDoc6 call exceeds this many milliseconds. Cannot interrupt an already-running OpenDoc6.")] int maxDocOpenTimeMs = 0,
        [Description("Number of target documents to process before extra cleanup of side-effect opened docs. Default 20.")] int batchSize = 20)
    {
        return await ExecuteToolAsync(
            "CrossReference.ScanExternalReferences",
            new Dictionary<string, object?>
            {
                ["DocumentPath"] = documentPath,
                ["OutputPath"] = outputPath,
                ["IncludeExternalFileReferences"] = includeExternalFileReferences,
                ["IncludeAuxiliaryReferences"] = includeAuxiliaryReferences,
                ["IncludeDrawingReferences"] = includeDrawingReferences,
                ["IncludeEquationReferences"] = includeEquationReferences,
                ["QuickMode"] = quickMode,
                ["AllConfigurations"] = allConfigurations,
                ["OpenUnloadedDocuments"] = openUnloadedDocuments,
                ["CloseOpened"] = closeOpened,
                ["HiddenInGui"] = hiddenInGui,
                ["LightWeightOpen"] = lightWeightOpen,
                ["DontLoadHiddenComponents"] = dontLoadHiddenComponents,
                ["MaxDocOpenTimeMs"] = maxDocOpenTimeMs,
                ["BatchSize"] = batchSize,
                ["IncludeActiveDocument"] = true,
                ["UseActiveAssemblyComponents"] = false,
                ["IncludeOpenDocuments"] = false
            });
    }

    [McpServerTool, Description("Scan external references scoped to one component in the active assembly. Read-only diagnostic for debugging a specific component.")]
    public async Task<object?> ScanComponentExternalReferences(
        [Description("Component instance name from the active assembly.")] string componentName,
        [Description("Optional JSON output path.")] string? outputPath = null)
    {
        return await ExecuteToolAsync(
            "CrossReference.ScanComponentExternalReferences",
            new Dictionary<string, object?>
            {
                ["ComponentName"] = componentName,
                ["OutputPath"] = outputPath
            });
    }

    [McpServerTool, Description("Scan external references scoped to one feature in the active document. Read-only diagnostic.")]
    public async Task<object?> ScanFeatureExternalReferences(
        [Description("Feature name from the active document.")] string featureName,
        [Description("Optional JSON output path.")] string? outputPath = null)
    {
        return await ExecuteToolAsync(
            "CrossReference.ScanFeatureExternalReferences",
            new Dictionary<string, object?>
            {
                ["FeatureName"] = featureName,
                ["OutputPath"] = outputPath
            });
    }

    [McpServerTool, Description("Scan external references scoped to one sketch feature in the active document. Read-only diagnostic.")]
    public async Task<object?> ScanSketchExternalReferences(
        [Description("Sketch feature name from the active document.")] string sketchName,
        [Description("Optional JSON output path.")] string? outputPath = null)
    {
        return await ExecuteToolAsync(
            "CrossReference.ScanSketchExternalReferences",
            new Dictionary<string, object?>
            {
                ["SketchName"] = sketchName,
                ["OutputPath"] = outputPath
            });
    }

    [McpServerTool, Description("Verify one SolidWorks document has no broken cross references. Skipped/unloaded docs make Passed=false unless openUnloadedDocuments=true.")]
    public async Task<object?> VerifyNoBrokenReferences(
        [Description("Optional document path. Omit to verify the active document only.")] string? documentPath = null,
        [Description("Optional JSON output path for the verification result.")] string? outputPath = null,
        [Description("If true, open documentPath when it is not already loaded.")] bool openUnloadedDocuments = false,
        [Description("If true, close a document opened by this tool.")] bool closeOpened = true,
        [Description("If true, documents opened by this tool are hidden in the SolidWorks GUI. Default true.")] bool hiddenInGui = true,
        [Description("If true, open unloaded documents with SolidWorks lightweight options. Default true.")] bool lightWeightOpen = true,
        [Description("If true, do not load hidden components while opening assemblies for verification. Default true.")] bool dontLoadHiddenComponents = true,
        [Description("If true, skip drawing/equation scans and only check fast file-reference surfaces. Default false.")] bool quickMode = false,
        [Description("If >0, mark verification failed when a single OpenDoc6 call exceeds this many milliseconds. Cannot interrupt an already-running OpenDoc6.")] int maxDocOpenTimeMs = 0,
        [Description("Number of target documents to process before extra cleanup of side-effect opened docs. Default 20.")] int batchSize = 20)
    {
        return await ExecuteToolAsync(
            "CrossReference.VerifyNoBrokenReferencesSingle",
            new Dictionary<string, object?>
            {
                ["DocumentPath"] = documentPath,
                ["OutputPath"] = outputPath,
                ["OpenUnloadedDocuments"] = openUnloadedDocuments,
                ["CloseOpened"] = closeOpened,
                ["HiddenInGui"] = hiddenInGui,
                ["LightWeightOpen"] = lightWeightOpen,
                ["DontLoadHiddenComponents"] = dontLoadHiddenComponents,
                ["QuickMode"] = quickMode,
                ["MaxDocOpenTimeMs"] = maxDocOpenTimeMs,
                ["BatchSize"] = batchSize,
                ["IncludeActiveDocument"] = true,
                ["UseActiveAssemblyComponents"] = false,
                ["IncludeOpenDocuments"] = false
            });
    }
}
