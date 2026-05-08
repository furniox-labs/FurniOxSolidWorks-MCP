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

    [McpServerTool, Description("Open existing model. With silent=true (default) missing references become suppressed components instead of triggering a dialog. visible=false opens/hides with lower GUI footprint.")]
    public async Task<object?> OpenModel(
        [Description("File path")] string path,
        [Description("Type: 1=Part, 2=Assembly, 3=Drawing")] int type = 1,
        [Description("Suppress dialogs. Default true.")] bool silent = true,
        [Description("Open read-only. Default false.")] bool readOnly = false,
        [Description("Skip loading hidden components. Default false.")] bool ignoreHiddenComponents = false,
        [Description("Open lightweight. Default false.")] bool lightWeight = false,
        [Description("If false, hide the model window after load. Default true.")] bool visible = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Path"] = path,
            ["Type"] = type,
            ["Silent"] = silent,
            ["ReadOnly"] = readOnly,
            ["IgnoreHiddenComponents"] = ignoreHiddenComponents,
            ["LightWeight"] = lightWeight,
            ["Visible"] = visible
        };
        return await ExecuteToolAsync("Document.OpenModel", parameters);
    }

    [McpServerTool, Description("[IDEMPOTENT] Save the active model. For assemblies whose referenced sub-assemblies or parts are also dirty, set saveReferences=true to save them too. forceRebuildBeforeSave/includeCleanReferences can rewrite stale cached reference paths in loaded docs.")]
    public async Task<object?> SaveModel(
        [Description("If true, also save loaded, non-read-only referenced documents. Default false.")] bool saveReferences = false,
        [Description("Use SolidWorks silent save options where available. Default true.")] bool silent = true,
        [Description("Suppress save dialogs and return error details instead of allowing SolidWorks modal prompts. Default true.")] bool suppressSaveDialogs = true,
        [Description("Force a rebuild before saving the active document and, when saveReferences=true, each loaded referenced document. Default false.")] bool forceRebuildBeforeSave = false,
        [Description("When saveReferences=true, also attempt to save loaded referenced documents even if SolidWorks does not mark them dirty. Default false.")] bool includeCleanReferences = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["SaveReferences"] = saveReferences,
            ["Silent"] = silent,
            ["SuppressSaveDialogs"] = suppressSaveDialogs,
            ["ForceRebuildBeforeSave"] = forceRebuildBeforeSave,
            ["IncludeCleanReferences"] = includeCleanReferences
        };
        return await ExecuteToolAsync("Document.SaveModel", parameters);
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

    [McpServerTool, Description("[DESTRUCTIVE] Close all documents. Defaults includeUnsaved=true for batch/repair workflows; pass false to preserve dirty documents and get a clear failure if they remain open.")]
    public async Task<object?> CloseAllDocuments(
        [Description("If true, force-close dirty documents too. Default true for batch workflows.")] bool includeUnsaved = true)
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

    [McpServerTool, Description("Set the custom property template (.prtprp/.asmprp/.drwprp) for the active document. This controls which Property Tab Builder form SolidWorks shows. It selects the template SolidWorks uses for the document but does not itself write custom properties.")]
    public async Task<object?> SetDocumentPropertyTemplate(
        [Description("Full path to template file (.prtprp, .asmprp, or .drwprp)")] string templatePath,
        [Description("True for weldment templates, false for standard")] bool isWeldment = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["TemplatePath"] = templatePath,
            ["IsWeldment"] = isWeldment
        };
        return await ExecuteToolAsync("Document.SetPropertyTemplate", parameters);
    }
}
