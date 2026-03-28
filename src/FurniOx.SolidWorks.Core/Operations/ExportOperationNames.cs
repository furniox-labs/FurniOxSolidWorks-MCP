using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class ExportOperationNames
{
    public const string ExportToSTEP = "Export.ExportToSTEP";
    public const string ExportToIGES = "Export.ExportToIGES";
    public const string ExportToSTL = "Export.ExportToSTL";
    public const string ExportToPDF = "Export.ExportToPDF";
    public const string ExportToDXF = "Export.ExportToDXF";

    public static readonly IReadOnlyList<string> All =
    [
        ExportToSTEP,
        ExportToIGES,
        ExportToSTL,
        ExportToPDF,
        ExportToDXF
    ];
}
