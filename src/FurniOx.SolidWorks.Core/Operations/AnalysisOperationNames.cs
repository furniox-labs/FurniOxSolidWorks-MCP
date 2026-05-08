using System.Collections.Generic;
using System.Linq;

namespace FurniOx.SolidWorks.Core.Operations;

public static class AnalysisOperationNames
{
    public const string AnalyzePart = "Analysis.AnalyzePart";
    public const string AnalyzeAssembly = "Analysis.AnalyzeAssembly";
    public const string AnalyzeDrawing = "Analysis.AnalyzeDrawing";
    public const string GetMassProperties = "Analysis.GetMassProperties";

    public static readonly IReadOnlyList<string> Part =
    [
        AnalyzePart,
        GetMassProperties
    ];

    public static readonly IReadOnlyList<string> Assembly =
    [
        AnalyzeAssembly
    ];

    public static readonly IReadOnlyList<string> Drawing =
    [
        AnalyzeDrawing
    ];

    public static readonly IReadOnlyList<string> All =
        Part.Concat(Assembly).Concat(Drawing).ToArray();
}
