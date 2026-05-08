using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class EquationOperationNames
{
    public const string ScanReferencesBatch = "Equation.ScanReferencesBatch";
    public const string RepairReferencesBatch = "Equation.RepairReferencesBatch";

    public static readonly IReadOnlyList<string> All =
    [
        ScanReferencesBatch,
        RepairReferencesBatch
    ];
}
