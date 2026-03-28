using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Tools;

[McpServerToolType]
public sealed class FeatureFilletTools : ToolsBase
{
    public FeatureFilletTools(ISmartRouter router) : base(router) { }

    [McpServerTool, Description("Create fillet feature with full FeatureFillet3 API support (14 parameters). Supports constant radius, variable radius, face fillets, asymmetric, setback, and conic profiles.")]
    public async Task<object?> CreateFillet(
        [Description("Fillet radius in mm (for constant radius fillets). Will be converted to meters for API. Default: 2.0mm")] double radius = 2.0,
        [Description("Fillet type: 0=ConstantRadius (most common), 1=VariableRadius, 2=FaceFillet, 3=FullRound. Default: 0")] int type = 0,
        [Description("Edge names to fillet (for constant/variable radius). Example: [\"Edge1@Part1\", \"Edge2@Part1\"]. Selection uses Mark=-1 (no specific mark). Required for type 0 and 1.")] string[]? edgeNames = null,
        [Description("First face set names (for face fillets type=2). Example: [\"Face1@Part1\", \"Face2@Part1\"]. CRITICAL: Uses Mark=2 for selection. Required for type 2.")] string[]? faceSet1Names = null,
        [Description("Second face set names (for face fillets type=2). Example: [\"Face3@Part1\", \"Face4@Part1\"]. CRITICAL: Uses Mark=4 for selection. Required for type 2.")] string[]? faceSet2Names = null,
        [Description("Secondary radius in mm for asymmetric fillets (when Options includes 0x4000). Creates elliptical cross-section. Default: 2.0mm")] double asymmetricRadius = 2.0,
        [Description("Conic shape value [0.05-0.95] for conic fillets (when ProfileType is 1 or 2). Values >0.5: sharper, <0.5: flatter. Default: 0.5")] double rho = 0.5,
        [Description("Combined bit flags: 0x1=Propagate, 0x2=UniformRadius (REQUIRED), 0x40=AttachEdges, 0x80=KeepFeatures, 0x4000=Asymmetric. Common: 195 (0x1|0x2|0x40|0x80). Default: 195")] int options = 195,
        [Description("Overflow handling: 0=Default, 1=KeepEdge (more predictable), 2=KeepSurface (smoother). Default: 0")] int overflowType = 0,
        [Description("Profile type: 0=Circular (standard), 1=ConicRho, 2=ConicRadius, 3=ConicRhoZeroChamfer. Default: 0")] int profileType = 0,
        [Description("Array of radii in mm for variable radius fillets (type=1). Each value corresponds to control point along edge. Will be converted to meters.")] double[]? variableRadii = null,
        [Description("Array of secondary radii in mm for asymmetric variable radius (type=1, Options includes 0x4000). Same length as VariableRadii.")] double[]? variableAsymmetricRadii = null,
        [Description("Array of Rho values [0.05-0.95] for variable radius conic fillets (type=1, ProfileType=1 or 2). Same length as VariableRadii.")] double[]? variableRhoValues = null,
        [Description("Array of setback distances in mm for vertex setbacks. Used when three filleted edges meet at common vertex. Controls smoothness of corner transition.")] double[]? setbackDistances = null,
        [Description("Array of radius values in mm at specific control points along edge. More advanced than VariableRadii.")] double[]? pointRadii = null,
        [Description("Array of secondary radii in mm at control points for asymmetric. Only used with PointRadii when Options includes 0x4000.")] double[]? pointAsymmetricRadii = null,
        [Description("Array of Rho values at control points. Only used with PointRadii when ProfileType is 1 or 2.")] double[]? pointRhoValues = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Radius"] = radius,
            ["Type"] = type,
            ["AsymmetricRadius"] = asymmetricRadius,
            ["Rho"] = rho,
            ["Options"] = options,
            ["OverflowType"] = overflowType,
            ["ProfileType"] = profileType,
            ["EdgeNames"] = edgeNames,
            ["FaceSet1Names"] = faceSet1Names,
            ["FaceSet2Names"] = faceSet2Names,
            ["VariableRadii"] = variableRadii,
            ["VariableAsymmetricRadii"] = variableAsymmetricRadii,
            ["VariableRhoValues"] = variableRhoValues,
            ["SetbackDistances"] = setbackDistances,
            ["PointRadii"] = pointRadii,
            ["PointAsymmetricRadii"] = pointAsymmetricRadii,
            ["PointRhoValues"] = pointRhoValues
        };

        return await ExecuteToolAsync("Feature.CreateFillet", parameters);
    }
}
