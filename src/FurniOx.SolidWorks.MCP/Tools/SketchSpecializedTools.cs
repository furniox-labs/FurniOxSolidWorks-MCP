using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class SketchSpecializedTools : ToolsBase
{
    public SketchSpecializedTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("List constraints")]
    public async Task<object?> ListConstraints()
        => await ExecuteToolAsync("Sketch.ListConstraints");

    [McpServerTool, Description("[DESTRUCTIVE] Delete sketch constraint")]
    public async Task<object?> DeleteConstraint([Description("Constraint index")] int constraintIndex)
        => await ExecuteToolAsync("Sketch.DeleteConstraint", new Dictionary<string, object?> { ["ConstraintIndex"] = constraintIndex });

    [McpServerTool, Description("[IDEMPOTENT] Show or hide sketch constraints")]
    public async Task<object?> DisplayConstraints([Description("Show/hide")] bool show)
        => await ExecuteToolAsync("Sketch.DisplayConstraints", new Dictionary<string, object?> { ["Show"] = show });

    [McpServerTool, Description("Insert block")]
    public async Task<object?> InsertBlock(
        [Description("Block file path")] string filePath,
        [Description("X in mm")] double x,
        [Description("Y in mm")] double y,
        [Description("Scale")] double scale = 1.0,
        [Description("Angle deg")] double angle = 0)
        => await ExecuteToolAsync("Sketch.InsertBlock", new Dictionary<string, object?> { ["FilePath"] = filePath, ["X"] = x, ["Y"] = y, ["Scale"] = scale, ["Angle"] = angle });

    [McpServerTool, Description("Create block")]
    public async Task<object?> MakeBlock([Description("X in mm")] double x, [Description("Y in mm")] double y)
        => await ExecuteToolAsync("Sketch.MakeBlock", new Dictionary<string, object?> { ["X"] = x, ["Y"] = y });

    [McpServerTool, Description("Explode block")]
    public async Task<object?> ExplodeBlock()
        => await ExecuteToolAsync("Sketch.ExplodeBlock", new Dictionary<string, object?>());

    [McpServerTool, Description("Insert text")]
    public async Task<object?> SketchText(
        [Description("Text content")] string text,
        [Description("Height mm")] double charHeight,
        [Description("Width mm")] double charWidth,
        [Description("Angle deg")] double angle,
        [Description("Font name")] string fontName,
        [Description("Flip H")] bool flipX = false,
        [Description("Flip V")] bool flipY = false,
        [Description("Oblique deg")] double obliqAngle = 0)
        => await ExecuteToolAsync("Sketch.SketchText", new Dictionary<string, object?>
        {
            ["Text"] = text,
            ["CharHeight"] = charHeight,
            ["CharWidth"] = charWidth,
            ["Angle"] = angle,
            ["FontName"] = fontName,
            ["FlipX"] = flipX,
            ["FlipY"] = flipY,
            ["ObliqAngle"] = obliqAngle
        });
}
