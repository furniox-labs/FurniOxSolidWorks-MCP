using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for SolidWorks component and feature sorting operations.
/// </summary>
[McpServerToolType]
public sealed class SortingTools : ToolsBase
{
    public SortingTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Reorder assembly components to specific positions. Pass a JSON array of {name, position} objects where position is 1-based (1 = first). Components not in the list keep their relative order after the specified ones.")]
    public async Task<object?> ReorderByPositions(
        [Description("JSON array of position entries. Format: [{\"name\": \"ComponentName-1\", \"position\": 1}, ...]. Use instance names from list_assembly_components, not full '@Assembly' strings.")] string positions,
        [Description("Preview changes without applying (returns what would be reordered)")] bool dryRun = false,
        [Description("Preserve existing FeatureManager folder membership (prevents accidentally moving components into/out of folders like 'Hardware')")] bool preserveFolders = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Positions"] = positions,
            ["DryRun"] = dryRun,
            ["PreserveFolders"] = preserveFolders
        };

        return await ExecuteToolAsync("Sorting.ReorderByPositions", parameters);
    }

    [McpServerTool, Description("Reorder features (not components) in the FeatureManager tree to specific positions. Pass a JSON array of {name, position} objects where position is 1-based.")]
    public async Task<object?> ReorderFeaturesByPositions(
        [Description("JSON array of position entries. Format: [{\"name\": \"FeatureName\", \"position\": 1}, ...]. Use exact feature names as shown in the FeatureManager tree.")] string positions,
        [Description("Filter by feature type (for example 'MacroFeature' or 'ProfileFeature'). Leave empty to include all reorderable features.")] string featureType = "",
        [Description("Preserve existing FeatureManager folder membership (prevents moving features into/out of folders)")] bool preserveFolders = true,
        [Description("Preview changes without applying (returns what would be reordered)")] bool dryRun = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Positions"] = positions,
            ["FeatureType"] = featureType,
            ["PreserveFolders"] = preserveFolders,
            ["DryRun"] = dryRun
        };

        return await ExecuteToolAsync("Sorting.ReorderFeaturesByPositions", parameters);
    }

    [McpServerTool, Description("List top-level FeatureManager component folders in the active assembly, in current tree order.")]
    public async Task<object?> ListComponentFolders()
    {
        return await ExecuteToolAsync("Sorting.ListComponentFolders");
    }
}
