using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class FeatureExtrusionTools : ToolsBase
{
    public FeatureExtrusionTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Create extrusion feature")]
    public async Task<object?> CreateExtrusion(
        [Description("Depth in mm")] double depth = 10.0,
        [Description("Reverse direction")] bool reverseDirection = false,
        [Description("Single direction")] bool singleDirection = true,
        [Description("Depth 2 in mm")] double depth2 = 10.0,
        [Description("End condition 1 (0-6)")] int endCondition1 = 0,
        [Description("End condition 2 (0-6)")] int endCondition2 = 0,
        [Description("Up to entity 1 name")] string? upToEntity1 = null,
        [Description("Up to entity 2 name")] string? upToEntity2 = null,
        [Description("Offset distance 1 in mm")] double offsetDistance1 = 0.0,
        [Description("Offset distance 2 in mm")] double offsetDistance2 = 0.0,
        [Description("Reverse offset 1")] bool offsetReverse1 = false,
        [Description("Reverse offset 2")] bool offsetReverse2 = false,
        [Description("Start condition (0-3)")] int startCondition = 0,
        [Description("Start offset in mm")] double startOffset = 0.0,
        [Description("Start entity name")] string? startEntity = null,
        [Description("Flip start offset")] bool flipStartOffset = false,
        [Description("Use draft 1")] bool useDraft1 = false,
        [Description("Use draft 2")] bool useDraft2 = false,
        [Description("Draft angle 1 in degrees")] double draftAngle1 = 0.0,
        [Description("Draft angle 2 in degrees")] double draftAngle2 = 0.0,
        [Description("Draft outward 1")] bool draftOutward1 = true,
        [Description("Draft outward 2")] bool draftOutward2 = true,
        [Description("Merge result")] bool mergeResult = true,
        [Description("Auto-select sketch")] bool autoSelect = true,
        [Description("Flip side to cut")] bool flipSideToCut = false,
        [Description("Use feature scope")] bool useFeatureScope = false,
        [Description("Translate surface 1")] bool translateSurface1 = false,
        [Description("Translate surface 2")] bool translateSurface2 = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Depth"] = depth,
            ["ReverseDirection"] = reverseDirection,
            ["SingleDirection"] = singleDirection,
            ["Depth2"] = depth2,
            ["EndCondition1"] = endCondition1,
            ["EndCondition2"] = endCondition2,
            ["UpToEntity1"] = upToEntity1,
            ["UpToEntity2"] = upToEntity2,
            ["OffsetDistance1"] = offsetDistance1,
            ["OffsetDistance2"] = offsetDistance2,
            ["OffsetReverse1"] = offsetReverse1,
            ["OffsetReverse2"] = offsetReverse2,
            ["StartCondition"] = startCondition,
            ["StartOffset"] = startOffset,
            ["StartEntity"] = startEntity,
            ["FlipStartOffset"] = flipStartOffset,
            ["UseDraft1"] = useDraft1,
            ["UseDraft2"] = useDraft2,
            ["DraftAngle1"] = draftAngle1,
            ["DraftAngle2"] = draftAngle2,
            ["DraftOutward1"] = draftOutward1,
            ["DraftOutward2"] = draftOutward2,
            ["MergeResult"] = mergeResult,
            ["AutoSelect"] = autoSelect,
            ["FlipSideToCut"] = flipSideToCut,
            ["UseFeatureScope"] = useFeatureScope,
            ["TranslateSurface1"] = translateSurface1,
            ["TranslateSurface2"] = translateSurface2
        };

        return await ExecuteToolAsync("Feature.CreateExtrusion", parameters);
    }

    [McpServerTool, Description("Create cut extrusion feature")]
    public async Task<object?> CreateCutExtrusion(
        [Description("Cut depth in mm")] double depth = 10.0,
        [Description("Reverse direction")] bool reverseDirection = false,
        [Description("Single direction")] bool singleDirection = true,
        [Description("Depth 2 in mm")] double depth2 = 10.0,
        [Description("Flip side to cut")] bool flipSideToCut = false,
        [Description("End condition 1 (0-11)")] int endCondition1 = 1,
        [Description("End condition 2 (0-11)")] int endCondition2 = 0,
        [Description("Up to entity 1 name")] string? upToEntity1 = null,
        [Description("Up to entity 2 name")] string? upToEntity2 = null,
        [Description("Offset distance 1 in mm")] double offsetDistance1 = 0.0,
        [Description("Offset distance 2 in mm")] double offsetDistance2 = 0.0,
        [Description("Reverse offset 1")] bool offsetReverse1 = false,
        [Description("Reverse offset 2")] bool offsetReverse2 = false,
        [Description("Translate surface 1")] bool translateSurface1 = false,
        [Description("Translate surface 2")] bool translateSurface2 = false,
        [Description("Start condition (0-3)")] int startCondition = 0,
        [Description("Start offset in mm")] double startOffset = 0.0,
        [Description("Start entity name")] string? startEntity = null,
        [Description("Flip start offset")] bool flipStartOffset = false,
        [Description("Use draft 1")] bool useDraft1 = false,
        [Description("Use draft 2")] bool useDraft2 = false,
        [Description("Draft angle 1 in degrees")] double draftAngle1 = 0.0,
        [Description("Draft angle 2 in degrees")] double draftAngle2 = 0.0,
        [Description("Draft inward 1")] bool draftInward1 = false,
        [Description("Draft inward 2")] bool draftInward2 = false,
        [Description("Normal cut")] bool normalCut = false,
        [Description("Optimize geometry")] bool optimizeGeometry = false,
        [Description("Use feature scope")] bool useFeatureScope = true,
        [Description("Auto-select bodies")] bool useAutoSelect = true,
        [Description("Assembly feature scope")] bool assemblyFeatureScope = true,
        [Description("Auto-select components")] bool autoSelectComponents = true,
        [Description("Propagate to parts")] bool propagateFeatureToParts = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Depth"] = depth,
            ["ReverseDirection"] = reverseDirection,
            ["SingleDirection"] = singleDirection,
            ["Depth2"] = depth2,
            ["FlipSideToCut"] = flipSideToCut,
            ["EndCondition1"] = endCondition1,
            ["EndCondition2"] = endCondition2,
            ["UpToEntity1"] = upToEntity1,
            ["UpToEntity2"] = upToEntity2,
            ["OffsetDistance1"] = offsetDistance1,
            ["OffsetDistance2"] = offsetDistance2,
            ["OffsetReverse1"] = offsetReverse1,
            ["OffsetReverse2"] = offsetReverse2,
            ["TranslateSurface1"] = translateSurface1,
            ["TranslateSurface2"] = translateSurface2,
            ["StartCondition"] = startCondition,
            ["StartOffset"] = startOffset,
            ["StartEntity"] = startEntity,
            ["FlipStartOffset"] = flipStartOffset,
            ["UseDraft1"] = useDraft1,
            ["UseDraft2"] = useDraft2,
            ["DraftAngle1"] = draftAngle1,
            ["DraftAngle2"] = draftAngle2,
            ["DraftInward1"] = draftInward1,
            ["DraftInward2"] = draftInward2,
            ["NormalCut"] = normalCut,
            ["OptimizeGeometry"] = optimizeGeometry,
            ["UseFeatureScope"] = useFeatureScope,
            ["UseAutoSelect"] = useAutoSelect,
            ["AssemblyFeatureScope"] = assemblyFeatureScope,
            ["AutoSelectComponents"] = autoSelectComponents,
            ["PropagateFeatureToParts"] = propagateFeatureToParts
        };

        return await ExecuteToolAsync("Feature.CreateCutExtrusion", parameters);
    }
}
