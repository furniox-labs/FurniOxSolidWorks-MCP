using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class FeatureRevolveTools : ToolsBase
{
    public FeatureRevolveTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Create revolve feature")]
    public async Task<object?> CreateRevolve(
        [Description("Axis entity name")] string? axisEntity = null,
        [Description("Axis entity type")] string axisEntityType = "AXIS",
        [Description("Single direction")] bool singleDirection = true,
        [Description("Is solid")] bool isSolid = true,
        [Description("Is thin")] bool isThin = false,
        [Description("Is cut")] bool isCut = false,
        [Description("Reverse direction")] bool reverseDirection = false,
        [Description("Angle 1 in degrees")] double angle1 = 360.0,
        [Description("Angle 2 in degrees")] double angle2 = 360.0,
        [Description("End condition 1 (0-11)")] int endCondition1 = 0,
        [Description("End condition 2 (0-11)")] int endCondition2 = 0,
        [Description("Both directions to same entity")] bool bothDirectionUpToSameEntity = false,
        [Description("Up to entity 1 name")] string? upToEntity1 = null,
        [Description("Up to entity 2 name")] string? upToEntity2 = null,
        [Description("Offset distance 1 in mm")] double offsetDistance1 = 0.0,
        [Description("Offset distance 2 in mm")] double offsetDistance2 = 0.0,
        [Description("Reverse offset 1")] bool offsetReverse1 = false,
        [Description("Reverse offset 2")] bool offsetReverse2 = false,
        [Description("Thin type (0-3)")] int thinType = 2,
        [Description("Thin thickness 1 in mm")] double thinThickness1 = 2.0,
        [Description("Thin thickness 2 in mm")] double thinThickness2 = 2.0,
        [Description("Merge result")] bool mergeResult = true,
        [Description("Use feature scope")] bool useFeatureScope = false,
        [Description("Auto-select bodies")] bool useAutoSelect = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["AxisEntity"] = axisEntity,
            ["AxisEntityType"] = axisEntityType,
            ["SingleDirection"] = singleDirection,
            ["IsSolid"] = isSolid,
            ["IsThin"] = isThin,
            ["IsCut"] = isCut,
            ["ReverseDirection"] = reverseDirection,
            ["Angle1"] = angle1,
            ["Angle2"] = angle2,
            ["EndCondition1"] = endCondition1,
            ["EndCondition2"] = endCondition2,
            ["BothDirectionUpToSameEntity"] = bothDirectionUpToSameEntity,
            ["UpToEntity1"] = upToEntity1,
            ["UpToEntity2"] = upToEntity2,
            ["OffsetDistance1"] = offsetDistance1,
            ["OffsetDistance2"] = offsetDistance2,
            ["OffsetReverse1"] = offsetReverse1,
            ["OffsetReverse2"] = offsetReverse2,
            ["ThinType"] = thinType,
            ["ThinThickness1"] = thinThickness1,
            ["ThinThickness2"] = thinThickness2,
            ["MergeResult"] = mergeResult,
            ["UseFeatureScope"] = useFeatureScope,
            ["UseAutoSelect"] = useAutoSelect
        };

        return await ExecuteToolAsync("Feature.CreateRevolve", parameters);
    }
}
