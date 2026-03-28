using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// Configuration management tools (7 tools)
/// Handles configuration operations for parametric design workflows
/// </summary>
[McpServerToolType]
public sealed class ConfigurationTools : ToolsBase
{
    public ConfigurationTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("List all configurations")]
    public async Task<object?> GetConfigurationNames()
    {
        return await ExecuteToolAsync("Configuration.GetConfigurationNames");
    }

    [McpServerTool, Description("Activate configuration")]
    public async Task<object?> ActivateConfiguration(
        [Description("Configuration name")] string name)
    {
        return await ExecuteToolAsync(
            "Configuration.ActivateConfiguration",
            new Dictionary<string, object?> { ["Name"] = name });
    }

    [McpServerTool, Description("Create configuration")]
    public async Task<object?> AddConfiguration(
        [Description("Configuration name")] string name,
        [Description("Configuration description")] string? description = null,
        [Description("Alternate name")] string? alternateName = null,
        [Description("Base configuration")] string? baseConfiguration = null)
    {
        var parameters = new Dictionary<string, object?> { ["Name"] = name };

        if (!string.IsNullOrEmpty(description))
        {
            parameters["Description"] = description;
        }

        if (!string.IsNullOrEmpty(alternateName))
        {
            parameters["AlternateName"] = alternateName;
        }

        if (!string.IsNullOrEmpty(baseConfiguration))
        {
            parameters["BaseConfiguration"] = baseConfiguration;
        }

        return await ExecuteToolAsync("Configuration.AddConfiguration", parameters);
    }

    [McpServerTool, Description("[DESTRUCTIVE] Delete configuration")]
    public async Task<object?> DeleteConfiguration(
        [Description("Configuration name")] string name)
    {
        return await ExecuteToolAsync(
            "Configuration.DeleteConfiguration",
            new Dictionary<string, object?> { ["Name"] = name });
    }

    [McpServerTool, Description("Copy configuration")]
    public async Task<object?> CopyConfiguration(
        [Description("Source configuration name")] string sourceName,
        [Description("Target configuration name")] string targetName,
        [Description("Configuration description")] string? description = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["SourceName"] = sourceName,
            ["TargetName"] = targetName
        };

        if (!string.IsNullOrEmpty(description))
        {
            parameters["Description"] = description;
        }

        return await ExecuteToolAsync("Configuration.CopyConfiguration", parameters);
    }

    [McpServerTool, Description("Count configurations")]
    public async Task<object?> GetConfigurationCount()
    {
        return await ExecuteToolAsync("Configuration.GetConfigurationCount");
    }

    [McpServerTool, Description("Show configuration")]
    public async Task<object?> ShowConfiguration(
        [Description("Configuration name")] string name)
    {
        return await ExecuteToolAsync(
            "Configuration.ShowConfiguration",
            new Dictionary<string, object?> { ["Name"] = name });
    }
}
