using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for single-document equation reference scans and repairs.
/// File-input batch flows are outside the public tool surface.
/// </summary>
[McpServerToolType]
public sealed class EquationReferenceTools : ToolsBase
{
    public EquationReferenceTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Scan one SolidWorks document for broken or stale equation cross-document/component-name references. Omit documentPath to scan the active document.")]
    public async Task<object?> ScanEquationReferences(
        [Description("Optional document path. Omit to scan the active document.")] string? documentPath = null,
        [Description("Optional JSON output path for the scan result.")] string? outputPath = null,
        [Description("If true, scan every configuration in the document; otherwise scan the active configuration only.")] bool allConfigurations = false,
        [Description("If true, open documentPath when it is not already loaded. Default false protects large projects from GUI/VRAM growth.")] bool openUnloadedDocuments = false,
        [Description("If true, close a document opened by this tool and not modified.")] bool closeOpened = true,
        [Description("If true, documents opened by this tool are hidden in the SolidWorks GUI. Default true.")] bool hiddenInGui = true)
    {
        return await ExecuteToolAsync(
            "Equation.ScanReferencesBatch",
            new Dictionary<string, object?>
            {
                ["DocumentPath"] = documentPath,
                ["OutputPath"] = outputPath,
                ["AllConfigurations"] = allConfigurations,
                ["OpenUnloadedDocuments"] = openUnloadedDocuments,
                ["CloseOpened"] = closeOpened,
                ["HiddenInGui"] = hiddenInGui,
                ["IncludeActiveDocument"] = true,
                ["UseActiveAssemblyComponents"] = false
            });
    }

    [McpServerTool, Description("Repair one SolidWorks document's equations after component/file renames by replacing exact equation reference tokens. Dry-run defaults true; set dryRun=false and saveDocuments=true to apply.")]
    public async Task<object?> RepairEquationReferences(
        [Description("JSON input path containing renameMap.")] string inputPath,
        [Description("Optional document path. Omit to repair the active document.")] string? documentPath = null,
        [Description("Optional JSON output path for the repair result.")] string? outputPath = null,
        [Description("If true, only reports proposed equation edits. Set false to apply.")] bool dryRun = true,
        [Description("If true and dryRun=false, save the document if modified.")] bool saveDocuments = false,
        [Description("If true, process every configuration in the document; otherwise process the active configuration only.")] bool allConfigurations = false,
        [Description("If true, open documentPath when it is not already loaded.")] bool openUnloadedDocuments = false,
        [Description("If true, close a document opened by this tool after a no-op or successful save.")] bool closeOpened = true,
        [Description("If true, documents opened by this tool are hidden in the SolidWorks GUI. Default true.")] bool hiddenInGui = true)
    {
        return await ExecuteToolAsync(
            "Equation.RepairReferencesBatch",
            new Dictionary<string, object?>
            {
                ["InputPath"] = inputPath,
                ["DocumentPath"] = documentPath,
                ["OutputPath"] = outputPath,
                ["DryRun"] = dryRun,
                ["SaveDocuments"] = saveDocuments,
                ["AllConfigurations"] = allConfigurations,
                ["OpenUnloadedDocuments"] = openUnloadedDocuments,
                ["CloseOpened"] = closeOpened,
                ["HiddenInGui"] = hiddenInGui,
                ["IncludeActiveDocument"] = true,
                ["UseActiveAssemblyComponents"] = false
            });
    }
}
