using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class SketchGeometryTools : ToolsBase
{
    public SketchGeometryTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Create sketch")]
    public async Task<object?> CreateSketch([Description("Plane (Front/Top/Right)")] string plane = "Front")
        => await ExecuteToolAsync("Sketch.CreateSketch", new Dictionary<string, object?> { ["Plane"] = plane });

    [McpServerTool, Description("Exit sketch editing mode.")]
    public async Task<object?> ExitSketch()
        => await ExecuteToolAsync("Sketch.ExitSketch", new Dictionary<string, object?>());

    [McpServerTool, Description("Edit existing sketch by name or selection. Enters sketch edit mode to allow adding/modifying geometry.")]
    public async Task<object?> EditSketch(
        [Description("Sketch name (e.g., 'Sketch1', '3DLayout'). If empty, uses selected sketch.")] string sketchName = "",
        [Description("If true, edit the currently selected sketch instead of searching by name")] bool useSelected = false)
        => await ExecuteToolAsync("Sketch.EditSketch", new Dictionary<string, object?> { ["SketchName"] = sketchName, ["UseSelected"] = useSelected });

    [McpServerTool, Description("Draw circle")]
    public async Task<object?> SketchCircle([Description("Center X in mm")] double centerX, [Description("Center Y in mm")] double centerY, [Description("Radius in mm")] double radius)
        => await ExecuteToolAsync("Sketch.SketchCircle", new Dictionary<string, object?> { ["CenterX"] = centerX, ["CenterY"] = centerY, ["Radius"] = radius });

    [McpServerTool, Description("Draw line")]
    public async Task<object?> SketchLine([Description("Start X in mm")] double x1, [Description("Start Y in mm")] double y1, [Description("End X in mm")] double x2, [Description("End Y in mm")] double y2)
        => await ExecuteToolAsync("Sketch.SketchLine", new Dictionary<string, object?> { ["X1"] = x1, ["Y1"] = y1, ["X2"] = x2, ["Y2"] = y2 });

    [McpServerTool, Description("Draw centerline")]
    public async Task<object?> SketchCenterLine([Description("Start X in mm")] double x1, [Description("Start Y in mm")] double y1, [Description("End X in mm")] double x2, [Description("End Y in mm")] double y2)
        => await ExecuteToolAsync("Sketch.SketchCenterLine", new Dictionary<string, object?> { ["X1"] = x1, ["Y1"] = y1, ["X2"] = x2, ["Y2"] = y2 });

    [McpServerTool, Description("Draw arc")]
    public async Task<object?> SketchArc(
        [Description("Center X in mm")] double centerX,
        [Description("Center Y in mm")] double centerY,
        [Description("Radius in mm")] double radius,
        [Description("Start angle deg")] double startAngle = 0,
        [Description("End angle deg")] double endAngle = 90,
        [Description("Clockwise")] bool clockwise = false)
        => await ExecuteToolAsync("Sketch.SketchArc", new Dictionary<string, object?>
        {
            ["CenterX"] = centerX,
            ["CenterY"] = centerY,
            ["Radius"] = radius,
            ["StartAngle"] = startAngle,
            ["EndAngle"] = endAngle,
            ["Clockwise"] = clockwise
        });

    [McpServerTool, Description("Draw 3-point arc")]
    public async Task<object?> Sketch3PointArc(
        [Description("Pt1 X in mm")] double x1,
        [Description("Pt1 Y in mm")] double y1,
        [Description("Pt2 X in mm")] double x2,
        [Description("Pt2 Y in mm")] double y2,
        [Description("Pt3 X in mm")] double x3,
        [Description("Pt3 Y in mm")] double y3)
        => await ExecuteToolAsync("Sketch.Sketch3PointArc", new Dictionary<string, object?> { ["X1"] = x1, ["Y1"] = y1, ["X2"] = x2, ["Y2"] = y2, ["X3"] = x3, ["Y3"] = y3 });

    [McpServerTool, Description("Draw tangent arc")]
    public async Task<object?> SketchTangentArc(
        [Description("Start X in mm")] double x1,
        [Description("Start Y in mm")] double y1,
        [Description("End X in mm")] double x2,
        [Description("End Y in mm")] double y2,
        [Description("Type (1-4)")] int arcType = 1)
        => await ExecuteToolAsync("Sketch.SketchTangentArc", new Dictionary<string, object?> { ["X1"] = x1, ["Y1"] = y1, ["X2"] = x2, ["Y2"] = y2, ["ArcType"] = arcType });

    [McpServerTool, Description("Draw rectangle")]
    public async Task<object?> SketchCornerRectangle(
        [Description("Corner1 X in mm")] double x1,
        [Description("Corner1 Y in mm")] double y1,
        [Description("Corner2 X in mm")] double x2,
        [Description("Corner2 Y in mm")] double y2)
        => await ExecuteToolAsync("Sketch.SketchCornerRectangle", new Dictionary<string, object?> { ["X1"] = x1, ["Y1"] = y1, ["X2"] = x2, ["Y2"] = y2 });

    [McpServerTool, Description("Create point")]
    public async Task<object?> SketchPoint(
        [Description("X in mm")] double x,
        [Description("Y in mm")] double y,
        [Description("Z in mm")] double z = 0)
        => await ExecuteToolAsync("Sketch.SketchPoint", new Dictionary<string, object?> { ["X"] = x, ["Y"] = y, ["Z"] = z });

    [McpServerTool, Description("Draw ellipse")]
    public async Task<object?> SketchEllipse(
        [Description("Center X in mm")] double xc,
        [Description("Center Y in mm")] double yc,
        [Description("Major X in mm")] double xmaj,
        [Description("Major Y in mm")] double ymaj,
        [Description("Minor X in mm")] double xmin,
        [Description("Minor Y in mm")] double ymin)
        => await ExecuteToolAsync("Sketch.SketchEllipse", new Dictionary<string, object?> { ["Xc"] = xc, ["Yc"] = yc, ["Xmaj"] = xmaj, ["Ymaj"] = ymaj, ["Xmin"] = xmin, ["Ymin"] = ymin });

    [McpServerTool, Description("Draw polygon")]
    public async Task<object?> SketchPolygon(
        [Description("Center X in mm")] double xc,
        [Description("Center Y in mm")] double yc,
        [Description("Vertex X in mm")] double xp,
        [Description("Vertex Y in mm")] double yp,
        [Description("Sides count")] int sides = 6,
        [Description("Inscribed")] bool inscribed = true)
        => await ExecuteToolAsync("Sketch.SketchPolygon", new Dictionary<string, object?> { ["Xc"] = xc, ["Yc"] = yc, ["Xp"] = xp, ["Yp"] = yp, ["Sides"] = sides, ["Inscribed"] = inscribed });

    [McpServerTool, Description("Draw spline")]
    public async Task<object?> SketchSpline([Description("Points [x,y,z...] mm")] double[] points)
        => await ExecuteToolAsync("Sketch.SketchSpline", new Dictionary<string, object?> { ["Points"] = points });
}
