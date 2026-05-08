using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

/// <summary>
/// Public single-component suppression operation names. Batch siblings live in
/// DocumentSuppressionBatchOperationNames (private).
/// </summary>
public static class DocumentSuppressionOperationNames
{
    public const string GetComponentSuppression = "Document.GetComponentSuppression";
    public const string SetComponentSuppression = "Document.SetComponentSuppression";

    public static readonly IReadOnlyList<string> All =
    [
        GetComponentSuppression,
        SetComponentSuppression
    ];
}
