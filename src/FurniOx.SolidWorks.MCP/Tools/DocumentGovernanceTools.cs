using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// Single-document governance tools (rename, suppression, reference rewire, search paths).
/// Batch siblings and bridge-only selected-document rename are outside the public tool surface.
/// </summary>
[McpServerToolType]
public sealed class DocumentGovernanceTools : ToolsBase
{
    public DocumentGovernanceTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Rename a component file anywhere below the active root assembly by full root-relative instance path.")]
    public async Task<object?> RenameComponentAnywhere(
        [Description("Root-relative instance path, e.g. Foo-1/Bar-2/Part-3.")] string instancePath,
        [Description("New filename with extension.")] string newName,
        [Description("Auto-save after rename.")] bool autoSave = true,
        [Description("Dry run/preflight only. Default false.")] bool dryRun = false,
        [Description("Open tool-opened docs hidden in GUI. Default true.")] bool hiddenInGui = true,
        [Description("Close tool-opened docs after live rename. Default true.")] bool closeOpenedDocs = true,
        [Description("Suppress save dialogs. Default true.")] bool suppressSaveDialogs = true,
        [Description("Require fully resolved component before live rename. Default true.")] bool requireFullyResolved = true)
    {
        return await ExecuteToolAsync(
            "Document.RenameComponentAnywhere",
            new Dictionary<string, object?>
            {
                ["InstancePath"] = instancePath,
                ["NewName"] = newName,
                ["AutoSave"] = autoSave,
                ["DryRun"] = dryRun,
                ["HiddenInGui"] = hiddenInGui,
                ["CloseOpenedDocs"] = closeOpenedDocs,
                ["SuppressSaveDialogs"] = suppressSaveDialogs,
                ["RequireFullyResolved"] = requireFullyResolved
            });
    }

    [McpServerTool, Description("List SolidWorks documents whose current in-memory path differs from the path loaded from disk.")]
    public async Task<object?> GetRenamedDocuments()
    {
        return await ExecuteToolAsync("Document.GetRenamedDocuments");
    }

    [McpServerTool, Description("Detect SolidWorks files on disk that are not referenced by the open root assembly.")]
    public async Task<object?> DetectOrphanFiles(
        [Description("Folder to scan. If empty, uses the active root folder.")] string folderPath = "",
        [Description("Scan recursively. Default false.")] bool recursive = false,
        [Description("Include drawings in orphan scan. Default true.")] bool includeDrawings = true)
    {
        return await ExecuteToolAsync(
            "Document.DetectOrphanFiles",
            new Dictionary<string, object?>
            {
                ["FolderPath"] = folderPath,
                ["Recursive"] = recursive,
                ["IncludeDrawings"] = includeDrawings
            });
    }

    [McpServerTool, Description("Get suppression state for one component in the active assembly.")]
    public async Task<object?> GetComponentSuppression(
        [Description("Component instance name.")] string componentName)
    {
        return await ExecuteToolAsync(
            "Document.GetComponentSuppression",
            new Dictionary<string, object?>
            {
                ["ComponentName"] = componentName
            });
    }

    [McpServerTool, Description("Set suppression state for one component in the active assembly. State can be Suppressed, Lightweight, Resolved, FullyResolved, FullyLightweight, or 0-4.")]
    public async Task<object?> SetComponentSuppression(
        [Description("Component instance name.")] string componentName,
        [Description("Suppression state: Suppressed, Lightweight, Resolved, FullyResolved, FullyLightweight, or 0-4.")] string state)
    {
        return await ExecuteToolAsync(
            "Document.SetComponentSuppression",
            new Dictionary<string, object?>
            {
                ["ComponentName"] = componentName,
                ["State"] = state
            });
    }

    [McpServerTool, Description("Get SolidWorks referenced-document search folders used to resolve missing references.")]
    public async Task<object?> GetReferenceSearchPath()
    {
        return await ExecuteToolAsync("Document.GetReferenceSearchPath");
    }

    [McpServerTool, Description("Set SolidWorks referenced-document search folders. Pass an empty array to clear; append=true preserves existing entries.")]
    public async Task<object?> SetReferenceSearchPath(
        [Description("Reference search folders. Empty or omitted clears when append=false.")] string[]? paths = null,
        [Description("Append to existing folders instead of replacing them. Default false.")] bool append = false)
    {
        return await ExecuteToolAsync(
            "Document.SetReferenceSearchPath",
            new Dictionary<string, object?>
            {
                ["Paths"] = paths ?? System.Array.Empty<string>(),
                ["Append"] = append
            });
    }

    [McpServerTool, Description("Rename the active document in place.")]
    public async Task<object?> RenameDocument(
        [Description("New filename with extension, for example 'NewPart.SLDPRT'.")] string newName,
        [Description("Auto-save after rename.")] bool autoSave = true)
    {
        return await ExecuteToolAsync(
            "Document.RenameDocument",
            new Dictionary<string, object?>
            {
                ["NewName"] = newName,
                ["AutoSave"] = autoSave
            });
    }

    [McpServerTool, Description("Rename a component instance inside the active assembly.")]
    public async Task<object?> RenameComponentInstance(
        [Description("Component instance name.")] string componentName,
        [Description("New instance/tree name.")] string newInstanceName,
        [Description("Auto-save the assembly after rename.")] bool autoSave = true)
    {
        return await ExecuteToolAsync(
            "Document.RenameComponentInstance",
            new Dictionary<string, object?>
            {
                ["ComponentName"] = componentName,
                ["NewInstanceName"] = newInstanceName,
                ["AutoSave"] = autoSave
            });
    }

    [McpServerTool, Description("Rename a component file from the active assembly.")]
    public async Task<object?> RenameComponentFile(
        [Description("Component instance name.")] string componentName,
        [Description("New filename with extension.")] string newName,
        [Description("Auto-save after rename.")] bool autoSave = true)
    {
        return await ExecuteToolAsync(
            "Document.RenameComponentFile",
            new Dictionary<string, object?>
            {
                ["ComponentName"] = componentName,
                ["NewName"] = newName,
                ["AutoSave"] = autoSave
            });
    }

    [McpServerTool, Description("Rewire one referenced document path inside a closed SolidWorks file. Wraps ISldWorks.ReplaceReferencedDocument; use for component-level stored references, not sketch-internal external entity refs.")]
    public async Task<object?> ReplaceReferencedDocument(
        [Description("Full path to the assembly/part/drawing whose stored reference should be updated. Must be closed in SolidWorks for live replacement.")] string referencingDocPath,
        [Description("Old referenced path exactly as stored in the file.")] string oldRefPath,
        [Description("New referenced path that already exists on disk.")] string newRefPath)
    {
        return await ExecuteToolAsync(
            "Document.ReplaceReferencedDocument",
            new Dictionary<string, object?>
            {
                ["ReferencingDocPath"] = referencingDocPath,
                ["OldRefPath"] = oldRefPath,
                ["NewRefPath"] = newRefPath
            });
    }
}
