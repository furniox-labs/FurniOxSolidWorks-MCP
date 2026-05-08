using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for SolidWorks document summary information (Author, Title, Subject, etc.)
/// These are built-in OLE document properties, separate from custom properties.
/// </summary>
[McpServerToolType]
public sealed class SummaryInfoTools : ToolsBase
{
    public SummaryInfoTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Get all document summary properties (Title, Author, Subject, Keywords, Comments, SavedBy, CreateDate, SaveDate). Works on active document OR selected component in assembly.")]
    public async Task<object?> GetAllSummaryProperties(
        [Description("If selected component not loaded, open it silently")] bool openIfNeeded = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["OpenIfNeeded"] = openIfNeeded
        };
        return await ExecuteToolAsync("SummaryInfo.GetAll", parameters);
    }

    [McpServerTool, Description("Get a single document summary property. Works on active document OR selected component in assembly. Fields: title, subject, author, keywords, comments, savedby, createdate, savedate")]
    public async Task<object?> GetSummaryProperty(
        [Description("Field name: title, subject, author, keywords, comments, savedby, createdate, savedate")] string field,
        [Description("If selected component not loaded, open it silently")] bool openIfNeeded = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Field"] = field,
            ["OpenIfNeeded"] = openIfNeeded
        };
        return await ExecuteToolAsync("SummaryInfo.Get", parameters);
    }

    [McpServerTool, Description("Set a document summary property. Works on active document OR selected component in assembly. Writable fields: title, subject, author, keywords, comments")]
    public async Task<object?> SetSummaryProperty(
        [Description("Field name: title, subject, author, keywords, comments")] string field,
        [Description("Value to set")] string value,
        [Description("If selected component not loaded, open it silently")] bool openIfNeeded = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Field"] = field,
            ["Value"] = value,
            ["OpenIfNeeded"] = openIfNeeded
        };
        return await ExecuteToolAsync("SummaryInfo.Set", parameters);
    }
}
