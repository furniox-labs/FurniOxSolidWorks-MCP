using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class SketchParametricTools : ToolsBase
{
    public SketchParametricTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Add constraint")]
    public async Task<object?> AddConstraint(
        [Description("Type: fixed, coincident, etc")] string constraintType,
        [Description("Entity IDs array")] int[] entityIds)
        => await ExecuteToolAsync("Sketch.AddConstraint", new Dictionary<string, object?> { ["ConstraintType"] = constraintType, ["EntityIds"] = entityIds });

    [McpServerTool, Description("Add dimension")]
    public async Task<object?> AddDimension(
        [Description("Type (distance/angle/etc)")] string dimensionType,
        [Description("Entity IDs array")] int[] entityIds,
        [Description("Label X in mm")] double x,
        [Description("Label Y in mm")] double y,
        [Description("Label Z in mm")] double z = 0,
        [Description("Dimension value mm")] double? value = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["DimensionType"] = dimensionType,
            ["EntityIds"] = entityIds,
            ["X"] = x,
            ["Y"] = y,
            ["Z"] = z
        };

        if (value.HasValue)
        {
            parameters["Value"] = value.Value;
        }

        return await ExecuteToolAsync("Sketch.AddDimension", parameters);
    }
}
