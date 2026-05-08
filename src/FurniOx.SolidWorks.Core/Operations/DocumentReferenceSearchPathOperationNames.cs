using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

/// <summary>
/// Public reference-search-path operation names. SolidWorks reference-search
/// folders apply globally and have no batch sibling.
/// </summary>
public static class DocumentReferenceSearchPathOperationNames
{
    public const string GetReferenceSearchPath = "Document.GetReferenceSearchPath";
    public const string SetReferenceSearchPath = "Document.SetReferenceSearchPath";

    public static readonly IReadOnlyList<string> All =
    [
        GetReferenceSearchPath,
        SetReferenceSearchPath
    ];
}
