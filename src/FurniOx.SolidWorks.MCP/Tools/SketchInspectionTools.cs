using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class SketchInspectionTools : ToolsBase
{
    public SketchInspectionTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("List sketch segments")]
    public async Task<object?> ListSketchSegments()
        => await ExecuteToolAsync("Sketch.ListSketchSegments");

    [McpServerTool, Description("Get segment info")]
    public async Task<object?> GetSketchSegmentInfo([Description("Segment ID")] int segmentId)
        => await ExecuteToolAsync("Sketch.GetSketchSegmentInfo", new Dictionary<string, object?> { ["SegmentId"] = segmentId });

    [McpServerTool, Description("Analyze sketch")]
    public async Task<object?> AnalyzeSketch(
        [Description("Fields: 'minimal' (metadata only), 'standard' (default), 'full' (all with stats)")] string fields = "standard",
        [Description("Include points")] bool includePoints = true,
        [Description("Include segments")] bool includeSegments = true,
        [Description("Include relations")] bool includeRelations = true,
        [Description("Include dimensions")] bool includeDimensions = true,
        [Description("Include metadata")] bool includeMetadata = true,
        [Description("Include construction geometry")] bool includeConstructionGeometry = true,
        [Description("Calculate statistics")] bool calculateStatistics = false,
        [Description("Run connectivity check (open endpoints / gaps)")] bool includeConnectivity = false,
        [Description("Connectivity endpoint tolerance in mm")] double gapToleranceMm = 0.01,
        [Description("Include construction geometry in connectivity check")] bool connectivityIncludeConstruction = false,
        [Description("Save full JSON to file path (returns summary only). If null, returns full response.")] string? outputPath = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Fields"] = fields,
            ["IncludePoints"] = includePoints,
            ["IncludeSegments"] = includeSegments,
            ["IncludeRelations"] = includeRelations,
            ["IncludeDimensions"] = includeDimensions,
            ["IncludeMetadata"] = includeMetadata,
            ["IncludeConstructionGeometry"] = includeConstructionGeometry,
            ["CalculateStatistics"] = calculateStatistics,
            ["IncludeConnectivity"] = includeConnectivity,
            ["GapToleranceMm"] = gapToleranceMm,
            ["ConnectivityIncludeConstruction"] = connectivityIncludeConstruction,
            ["OutputPath"] = outputPath
        };

        return await ExecuteToolAsync("Sketch.AnalyzeSketch", parameters);
    }
}
