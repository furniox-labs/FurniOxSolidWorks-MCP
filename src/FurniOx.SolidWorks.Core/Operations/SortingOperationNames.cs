using System.Collections.Generic;
using System.Linq;

namespace FurniOx.SolidWorks.Core.Operations;

public static class SortingOperationNames
{
    public const string ReorderByPositions = "Sorting.ReorderByPositions";
    public const string ReorderFeaturesByPositions = "Sorting.ReorderFeaturesByPositions";
    public const string ListComponentFolders = "Sorting.ListComponentFolders";

    public static readonly IReadOnlyList<string> Component =
    [
        ReorderByPositions
    ];

    public static readonly IReadOnlyList<string> FeatureTree =
    [
        ReorderFeaturesByPositions
    ];

    public static readonly IReadOnlyList<string> Inspection =
    [
        ListComponentFolders
    ];

    public static readonly IReadOnlyList<string> All =
        Component.Concat(FeatureTree).Concat(Inspection).ToArray();
}
