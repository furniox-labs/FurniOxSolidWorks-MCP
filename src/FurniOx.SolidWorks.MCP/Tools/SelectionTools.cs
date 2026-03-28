using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// Selection management tools.
/// </summary>
[McpServerToolType]
public sealed class SelectionTools : ToolsBase
{
    public SelectionTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Select an assembly component by instance name. Use list_assembly_components to discover valid names. Active document must be an assembly.")]
    public async Task<object?> SelectComponent(
        [Description("Component instance name from list_assembly_components (for example 'Part1-1' or nested path 'SubAssy-1/Part1-1').")] string name,
        [Description("Append to existing selection")] bool append = false,
        [Description("Selection mark for identifying selections")] int mark = 0)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = name,
            ["Append"] = append,
            ["Mark"] = mark
        };

        return await ExecuteToolAsync("Selection.SelectComponent", parameters);
    }

    [McpServerTool, Description("Select entity by name and type")]
    public async Task<object?> SelectByID2(
        [Description("Entity name")] string name,
        [Description("Entity type")] string type,
        [Description("X coordinate")] double x = 0,
        [Description("Y coordinate")] double y = 0,
        [Description("Z coordinate")] double z = 0,
        [Description("Append to selection")] bool append = false,
        [Description("Selection mark")] int mark = 0,
        [Description("Select option")] int selectOption = 0)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = name,
            ["Type"] = type,
            ["X"] = x,
            ["Y"] = y,
            ["Z"] = z,
            ["Append"] = append,
            ["Mark"] = mark,
            ["SelectOption"] = selectOption
        };

        return await ExecuteToolAsync("Selection.SelectByID2", parameters);
    }

    [McpServerTool, Description("[IDEMPOTENT] Clear all selections")]
    public async Task<object?> ClearSelection2()
    {
        return await ExecuteToolAsync(
            "Selection.ClearSelection2",
            new Dictionary<string, object?>());
    }

    [McpServerTool, Description("[DESTRUCTIVE] Delete selected entities")]
    public async Task<object?> DeleteSelection2(
        [Description("Delete options (1-3)")] int options = 1)
    {
        return await ExecuteToolAsync(
            "Selection.DeleteSelection2",
            new Dictionary<string, object?> { ["Options"] = options });
    }
}
