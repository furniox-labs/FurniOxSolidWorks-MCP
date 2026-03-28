using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class FeatureShellTools : ToolsBase
{
    public FeatureShellTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Create shell feature (hollow out solid by removing faces). Simple 2-parameter API with uniform thickness. Most common for enclosures and hollow parts.")]
    public async Task<object?> CreateShell(
        [Description("Shell wall thickness in mm. Will be converted to meters for API. CRITICAL: Must be less than minimum radius of curvature. Typical ranges: Plastic 2-5mm, Sheet metal 1-2mm, Castings 5-10mm. Default: 3.0mm")] double thickness = 3.0,
        [Description("Shell direction: 0=Inward/Inside (preserves exterior, most common), 1=Outward/Outside (expands exterior). Default: 0 (Inward)")] int direction = 0,
        [Description("Face names to remove (open the shell). Example: [\"Face1@Part1\", \"Face2@Part1\"]. CRITICAL: Uses Mark=1 for selection (not Mark=-1, not Mark=0). REQUIRED: At least one face must be selected.")] string[]? faceNames = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Thickness"] = thickness,
            ["Direction"] = direction,
            ["FaceNames"] = faceNames
        };

        return await ExecuteToolAsync("Feature.CreateShell", parameters);
    }
}
