using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class AssemblyBrowserTools : ToolsBase
{
    public AssemblyBrowserTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("List component instances in the active assembly so they can be passed to select_component and sorting tools.")]
    public async Task<object?> ListAssemblyComponents(
        [Description("If true, return only top-level components. Default: false (all component instances, including nested).")]
        bool topLevelOnly = false,
        [Description("If true, include resolved file paths when available.")]
        bool includePaths = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["TopLevelOnly"] = topLevelOnly,
            ["IncludePaths"] = includePaths
        };

        return await ExecuteToolAsync("AssemblyBrowser.ListAssemblyComponents", parameters);
    }
}
