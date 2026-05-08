using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

/// <summary>
/// Public single-document governance operation names (rename + read-only governance queries).
/// Batch siblings live in DocumentGovernanceBatchOperationNames (private).
/// </summary>
public static class DocumentGovernanceOperationNames
{
    public const string RenameDocument = "Document.RenameDocument";
    public const string RenameComponentFile = "Document.RenameComponentFile";
    public const string RenameComponentInstance = "Document.RenameComponentInstance";
    public const string RenameComponentAnywhere = "Document.RenameComponentAnywhere";
    public const string GetRenamedDocuments = "Document.GetRenamedDocuments";
    public const string DetectOrphanFiles = "Document.DetectOrphanFiles";

    public static readonly IReadOnlyList<string> All =
    [
        RenameDocument,
        RenameComponentFile,
        RenameComponentInstance,
        RenameComponentAnywhere,
        GetRenamedDocuments,
        DetectOrphanFiles
    ];
}
