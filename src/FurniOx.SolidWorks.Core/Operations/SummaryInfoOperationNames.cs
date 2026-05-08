using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class SummaryInfoOperationNames
{
    public const string GetAll = "SummaryInfo.GetAll";
    public const string Get = "SummaryInfo.Get";
    public const string Set = "SummaryInfo.Set";

    public static readonly IReadOnlyList<string> All =
    [
        GetAll,
        Get,
        Set
    ];
}
