using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class CustomPropertyOperationNames
{
    public const string Get = "CustomProperty.Get";
    public const string Set = "CustomProperty.Set";
    public const string GetAll = "CustomProperty.GetAll";
    public const string Delete = "CustomProperty.Delete";

    public static readonly IReadOnlyList<string> Single =
    [
        Get,
        Set,
        GetAll,
        Delete
    ];

    public static readonly IReadOnlyList<string> All = Single;
}
