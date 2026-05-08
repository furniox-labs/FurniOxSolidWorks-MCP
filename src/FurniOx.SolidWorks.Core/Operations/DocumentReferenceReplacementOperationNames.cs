using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

/// <summary>
/// Public single-document reference-replacement operation name. Batch sibling
/// lives in DocumentReferenceReplacementBatchOperationNames (private).
/// </summary>
public static class DocumentReferenceReplacementOperationNames
{
    public const string ReplaceReferencedDocument = "Document.ReplaceReferencedDocument";

    public static readonly IReadOnlyList<string> All =
    [
        ReplaceReferencedDocument
    ];
}
