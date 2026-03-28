using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// MCP tools for SolidWorks document operations.
/// Public/basic profile excludes rename and governance workflows.
/// </summary>
[McpServerToolType]
public sealed class DocumentTools : ToolsBase
{
    public DocumentTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Open existing model")]
    public async Task<object?> OpenModel(
        [Description("File path")] string path,
        [Description("Type: 1=Part, 2=Assembly, 3=Drawing")] int type = 1)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Path"] = path,
            ["Type"] = type
        };
        return await ExecuteToolAsync("Document.OpenModel", parameters);
    }

    [McpServerTool, Description("[IDEMPOTENT] Save model")]
    public async Task<object?> SaveModel()
    {
        return await ExecuteToolAsync("Document.SaveModel");
    }

    [McpServerTool, Description("[DESTRUCTIVE] Close model (unsaved changes may be lost)")]
    public async Task<object?> CloseModel(
        [Description("Document title (from get_all_open_documents). If empty, closes active document.")] string title = "",
        [Description("Save: 0=No, 1=Yes, 2=Ask")] int saveOption = 0)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Title"] = title,
            ["SaveOption"] = saveOption
        };
        return await ExecuteToolAsync("Document.CloseModel", parameters);
    }

    [McpServerTool, Description("Get document info")]
    public async Task<object?> GetDocumentInfo()
    {
        return await ExecuteToolAsync("Document.GetDocumentInfo");
    }

    [McpServerTool, Description("Activate document")]
    public async Task<object?> ActivateDocument(
        [Description("Document title")] string title)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Title"] = title
        };
        return await ExecuteToolAsync("Document.ActivateDocument", parameters);
    }

    [McpServerTool, Description("Create document")]
    public async Task<object?> CreateDocument(
        [Description("Type: 1=Part, 2=Assembly, 3=Drawing")] int type = 1)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Type"] = type
        };
        return await ExecuteToolAsync("Document.CreateDocument", parameters);
    }

    [McpServerTool, Description("[IDEMPOTENT] Rebuild model")]
    public async Task<object?> RebuildModel(
        [Description("Full rebuild")] bool force = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Force"] = force
        };
        return await ExecuteToolAsync("Document.RebuildModel", parameters);
    }

    [McpServerTool, Description("List open documents")]
    public async Task<object?> GetAllOpenDocuments(
        [Description("If true, only return documents with visible windows in GUI. Default: false (all documents including references loaded in memory).")]
        bool visibleOnly = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["VisibleOnly"] = visibleOnly
        };
        return await ExecuteToolAsync("Document.GetAllOpenDocuments", parameters);
    }

    [McpServerTool, Description("Count open documents")]
    public async Task<object?> GetDocumentCount()
    {
        return await ExecuteToolAsync("Document.GetDocumentCount");
    }

    [McpServerTool, Description("[DESTRUCTIVE] Close all documents")]
    public async Task<object?> CloseAllDocuments(
        [Description("Include unsaved")] bool includeUnsaved = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["IncludeUnsaved"] = includeUnsaved
        };
        return await ExecuteToolAsync("Document.CloseAllDocuments", parameters);
    }

    [McpServerTool, Description("Undo actions")]
    public async Task<object?> EditUndo2(
        [Description("Count")] int count = 1)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Count"] = count
        };
        return await ExecuteToolAsync("Document.EditUndo", parameters);
    }

    [McpServerTool, Description("Redo actions")]
    public async Task<object?> EditRedo2(
        [Description("Count")] int count = 1)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Count"] = count
        };
        return await ExecuteToolAsync("Document.EditRedo", parameters);
    }

    [McpServerTool, Description("Hide a document window without closing it. Sets Visible=false to close the GUI window while keeping the document loaded in memory. WARNING: UI resources are not released; close the document when done.")]
    public async Task<object?> HideDocument(
        [Description("Document title (from get_all_open_documents). If empty, hides active document.")] string title = "")
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Title"] = title
        };
        return await ExecuteToolAsync("Document.HideDocument", parameters);
    }
}
