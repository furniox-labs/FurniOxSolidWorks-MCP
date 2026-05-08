using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for SolidWorks custom property operations.
/// All tools work on active document OR selected component in assembly.
/// </summary>
[McpServerToolType]
public sealed class CustomPropertyTools : ToolsBase
{
    public CustomPropertyTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Get custom property value. Works on active document OR selected component in assembly.")]
    public async Task<object?> GetCustomProperty(
        [Description("Property name")] string name,
        [Description("Configuration name (empty = file-level)")] string configuration = "",
        [Description("If selected component not loaded, open it silently")] bool openIfNeeded = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = name,
            ["Configuration"] = configuration,
            ["OpenIfNeeded"] = openIfNeeded
        };
        return await ExecuteToolAsync("CustomProperty.Get", parameters);
    }

    [McpServerTool, Description("Set custom property value. Works on active document OR selected component in assembly.")]
    public async Task<object?> SetCustomProperty(
        [Description("Property name")] string name,
        [Description("Property value")] string value,
        [Description("Configuration name (empty = file-level)")] string configuration = "",
        [Description("Property type: text (default), number (integer), double (decimal), yesorno, date. 'number' auto-upgrades to 'double' if value has decimals.")] string type = "text",
        [Description("If selected component not loaded, open it silently")] bool openIfNeeded = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = name,
            ["Value"] = value,
            ["Configuration"] = configuration,
            ["Type"] = type,
            ["OpenIfNeeded"] = openIfNeeded
        };
        return await ExecuteToolAsync("CustomProperty.Set", parameters);
    }

    [McpServerTool, Description("Get all custom properties. Works on active document OR selected component in assembly.")]
    public async Task<object?> GetAllCustomProperties(
        [Description("Configuration name (empty = file-level)")] string configuration = "",
        [Description("If selected component not loaded, open it silently")] bool openIfNeeded = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Configuration"] = configuration,
            ["OpenIfNeeded"] = openIfNeeded
        };
        return await ExecuteToolAsync("CustomProperty.GetAll", parameters);
    }

    [McpServerTool, Description("[DESTRUCTIVE] Delete custom property. Works on active document OR selected component in assembly.")]
    public async Task<object?> DeleteCustomProperty(
        [Description("Property name")] string name,
        [Description("Configuration name (empty = file-level)")] string configuration = "",
        [Description("If selected component not loaded, open it silently")] bool openIfNeeded = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = name,
            ["Configuration"] = configuration,
            ["OpenIfNeeded"] = openIfNeeded
        };
        return await ExecuteToolAsync("CustomProperty.Delete", parameters);
    }
}
