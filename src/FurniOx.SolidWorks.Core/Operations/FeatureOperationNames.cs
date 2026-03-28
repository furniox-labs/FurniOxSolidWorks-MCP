using System.Collections.Generic;
using System.Linq;

namespace FurniOx.SolidWorks.Core.Operations;

public static class FeatureOperationNames
{
    public const string CreateExtrusion = "Feature.CreateExtrusion";
    public const string CreateCutExtrusion = "Feature.CreateCutExtrusion";
    public const string CreateRevolve = "Feature.CreateRevolve";
    public const string CreateFillet = "Feature.CreateFillet";
    public const string CreateShell = "Feature.CreateShell";

    public static readonly IReadOnlyList<string> Extrusion =
    [
        CreateExtrusion,
        CreateCutExtrusion
    ];

    public static readonly IReadOnlyList<string> Revolve =
    [
        CreateRevolve
    ];

    public static readonly IReadOnlyList<string> Fillet =
    [
        CreateFillet
    ];

    public static readonly IReadOnlyList<string> Shell =
    [
        CreateShell
    ];

    public static readonly IReadOnlyList<string> All =
        Extrusion.Concat(Revolve).Concat(Fillet).Concat(Shell).ToArray();
}
