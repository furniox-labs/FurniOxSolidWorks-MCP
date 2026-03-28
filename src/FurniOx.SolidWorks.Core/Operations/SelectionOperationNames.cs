using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class SelectionOperationNames
{
    public const string SelectByID2 = "Selection.SelectByID2";
    public const string SelectComponent = "Selection.SelectComponent";
    public const string ClearSelection2 = "Selection.ClearSelection2";
    public const string DeleteSelection2 = "Selection.DeleteSelection2";

    public static readonly IReadOnlyList<string> All =
    [
        SelectByID2,
        SelectComponent,
        ClearSelection2,
        DeleteSelection2
    ];
}
