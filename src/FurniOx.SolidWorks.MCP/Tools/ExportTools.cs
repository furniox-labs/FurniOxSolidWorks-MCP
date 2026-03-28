using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for SolidWorks export operations
/// </summary>
[McpServerToolType]
public sealed class ExportTools : ToolsBase
{
    public ExportTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Export to STEP format")]
    public async Task<object?> ExportToSTEP(
        [Description("Output file path")] string path)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Path"] = path
        };
        return await ExecuteToolAsync("Export.ExportToSTEP", parameters);
    }

    [McpServerTool, Description("Export to IGES format")]
    public async Task<object?> ExportToIGES(
        [Description("Output file path")] string path)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Path"] = path
        };
        return await ExecuteToolAsync("Export.ExportToIGES", parameters);
    }

    [McpServerTool, Description("Export to STL format")]
    public async Task<object?> ExportToSTL(
        [Description("Output file path")] string path)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Path"] = path
        };
        return await ExecuteToolAsync("Export.ExportToSTL", parameters);
    }

    [McpServerTool, Description("Export to PDF format")]
    public async Task<object?> ExportToPDF(
        [Description("Output file path")] string path)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Path"] = path
        };
        return await ExecuteToolAsync("Export.ExportToPDF", parameters);
    }

    [McpServerTool, Description("Export to DXF/DWG format")]
    public async Task<object?> ExportToDXF(
        [Description("Output file path")] string path)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Path"] = path
        };
        return await ExecuteToolAsync("Export.ExportToDXF", parameters);
    }
}

