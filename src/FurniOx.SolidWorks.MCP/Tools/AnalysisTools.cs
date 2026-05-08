using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for SolidWorks analysis and performance monitoring
/// </summary>
[McpServerToolType]
public sealed class AnalysisTools : ToolsBase
{
    public AnalysisTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Get mass properties")]
    public async Task<object?> GetMassProperties()
    {
        return await ExecuteToolAsync("Analysis.GetMassProperties");
    }

    [McpServerTool, Description("Get performance metrics")]
    public async Task<object?> GetPerformanceMetrics()
    {
        await Task.CompletedTask; // Metrics are synchronous
        var metrics = Router.GetPerformanceMetrics();

        var formattedMetrics = new
        {
            TotalOperations = metrics.Count,
            Metrics = metrics.Select(m => new
            {
                m.Operation,
                m.Invocations,
                m.Successes,
                Failures = m.Invocations - m.Successes,
                SuccessRate = m.Invocations > 0 ? (double)m.Successes / m.Invocations * 100 : 0,
                TotalDurationMs = m.TotalDuration.TotalMilliseconds,
                AverageDurationMs = m.Invocations > 0 ? m.TotalDuration.TotalMilliseconds / m.Invocations : 0
            }).OrderByDescending(m => m.Invocations)
        };

        return new
        {
            Message = "Performance metrics",
            Data = formattedMetrics
        };
    }

    // ========== Comprehensive Analysis Tools ==========

    [McpServerTool, Description("Analyze part document. If a part component is selected in an assembly, analyzes that part. Otherwise analyzes active document.")]
    public async Task<object?> AnalyzePart(
        [Description("Fields: 'minimal' (name,path,type only), 'standard' (default), 'full' (all)")] string fields = "standard",
        [Description("Include features")] bool includeFeatures = true,
        [Description("Include mass properties")] bool includeMassProperties = true,
        [Description("Include bodies")] bool includeBodies = true,
        [Description("Include custom properties")] bool includeCustomProperties = true,
        [Description("Include FeatureManager folder membership when analyzing a selected component in an assembly (FeatureManagerFolderPath). For nested components, this may open the owning sub-assembly. Ignored when analyzing a standalone part.")] bool includeComponentFolders = false,
        [Description("If analyzing a selected component and its model is not loaded, open it silently by file path.")] bool openReferencedDocs = true,
        [Description("Save full JSON to file path (returns summary only). If null, returns full response.")] string? outputPath = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Fields"] = fields,
            ["IncludeFeatures"] = includeFeatures,
            ["IncludeMassProperties"] = includeMassProperties,
            ["IncludeBodies"] = includeBodies,
            ["IncludeCustomProperties"] = includeCustomProperties,
            ["IncludeComponentFolders"] = includeComponentFolders,
            ["OpenReferencedDocs"] = openReferencedDocs,
            ["OutputPath"] = outputPath
        };

        return await ExecuteToolAsync("Analysis.AnalyzePart", parameters);
    }

    [McpServerTool, Description("Analyze assembly. If sub-assembly component is selected, analyzes that. Otherwise analyzes active document.")]
    public async Task<object?> AnalyzeAssembly(
        [Description("Filter components by document file path substring (e.g., 'Projektai', 'BCL001')")] string? pathFilter = null,
        [Description("Filter components by instance name path substring (e.g., 'SubAssy-1/Part-2')")] string? namePathFilter = null,
        [Description("Fields: 'minimal' (name,path,type only), 'standard' (default), 'full' (all)")] string fields = "standard",
        [Description("Include assembly-level features (3D sketches, reference planes, etc.)")] bool includeFeatures = true,
        [Description("Include components")] bool includeComponents = true,
        [Description("Include mates")] bool includeMates = true,
        [Description("Include mass properties")] bool includeMassProperties = true,
        [Description("Include interference check")] bool includeInterferenceCheck = false,
        [Description("Include custom properties")] bool includeCustomProperties = true,
        [Description("Include nested components (recursive enumeration). If false, only top-level components are returned.")] bool includeHierarchy = true,
        [Description("Include a hierarchy tree in response (in addition to the flat Components list).")] bool includeTree = false,
        [Description("Include suppressed components")] bool includeSuppressed = true,
        [Description("Include hidden components")] bool includeHidden = true,
        [Description("Include envelope components")] bool includeEnvelope = true,
        [Description("Include virtual components")] bool includeVirtual = true,
        [Description("Include FeatureManager folder membership for components (FeatureManagerFolderPath). Expensive for deep hierarchies because it may open sub-assemblies. Ignored in fields='minimal'.")] bool includeComponentFolders = false,
        [Description("If analyzing a selected component and its model is not loaded, open it silently by file path.")] bool openReferencedDocs = true,
        [Description("Include custom properties, configuration properties, and summary info for every unique component document. Deduplicates by file path. Respects openReferencedDocs for unloaded models. Disabled in 'minimal' mode.")] bool includeComponentProperties = false,
        [Description("Save full JSON to file path (returns summary only). If null, returns full response.")] string? outputPath = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["PathFilter"] = pathFilter,
            ["NamePathFilter"] = namePathFilter,
            ["Fields"] = fields,
            ["IncludeFeatures"] = includeFeatures,
            ["IncludeComponents"] = includeComponents,
            ["IncludeMates"] = includeMates,
            ["IncludeMassProperties"] = includeMassProperties,
            ["IncludeInterferenceCheck"] = includeInterferenceCheck,
            ["IncludeCustomProperties"] = includeCustomProperties,
            ["IncludeHierarchy"] = includeHierarchy,
            ["IncludeTree"] = includeTree,
            ["IncludeSuppressed"] = includeSuppressed,
            ["IncludeHidden"] = includeHidden,
            ["IncludeEnvelope"] = includeEnvelope,
            ["IncludeVirtual"] = includeVirtual,
            ["IncludeComponentFolders"] = includeComponentFolders,
            ["OpenReferencedDocs"] = openReferencedDocs,
            ["IncludeComponentProperties"] = includeComponentProperties,
            ["OutputPath"] = outputPath
        };

        return await ExecuteToolAsync("Analysis.AnalyzeAssembly", parameters);
    }

    [McpServerTool, Description("Analyze drawing document")]
    public async Task<object?> AnalyzeDrawing(
        [Description("Include sheets")] bool includeSheets = true,
        [Description("Include views")] bool includeViews = true,
        [Description("Include annotations")] bool includeAnnotations = true,
        [Description("Include BOM tables")] bool includeBomTables = true,
        [Description("Include referenced models")] bool includeReferencedModels = true,
        [Description("Include custom properties")] bool includeCustomProperties = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["IncludeSheets"] = includeSheets,
            ["IncludeViews"] = includeViews,
            ["IncludeAnnotations"] = includeAnnotations,
            ["IncludeBomTables"] = includeBomTables,
            ["IncludeReferencedModels"] = includeReferencedModels,
            ["IncludeCustomProperties"] = includeCustomProperties
        };

        return await ExecuteToolAsync("Analysis.AnalyzeDrawing", parameters);
    }
}
