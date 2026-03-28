using System.Collections.Generic;
using System.Linq;

namespace FurniOx.SolidWorks.Core.Operations;

public static class DocumentOperationNames
{
    public const string CreateDocument = "Document.CreateDocument";
    public const string OpenModel = "Document.OpenModel";
    public const string SaveModel = "Document.SaveModel";
    public const string CloseModel = "Document.CloseModel";
    public const string ActivateDocument = "Document.ActivateDocument";
    public const string RebuildModel = "Document.RebuildModel";
    public const string CloseAllDocuments = "Document.CloseAllDocuments";
    public const string EditUndo = "Document.EditUndo";
    public const string EditRedo = "Document.EditRedo";
    public const string HideDocument = "Document.HideDocument";
    public const string GetDocumentInfo = "Document.GetDocumentInfo";
    public const string GetAllOpenDocuments = "Document.GetAllOpenDocuments";
    public const string GetDocumentCount = "Document.GetDocumentCount";

    public static readonly IReadOnlyList<string> File =
    [
        CreateDocument,
        OpenModel,
        SaveModel
    ];

    public static readonly IReadOnlyList<string> Session =
    [
        CloseModel,
        ActivateDocument,
        RebuildModel,
        CloseAllDocuments,
        EditUndo,
        EditRedo,
        HideDocument
    ];

    public static readonly IReadOnlyList<string> Query =
    [
        GetDocumentInfo,
        GetAllOpenDocuments,
        GetDocumentCount
    ];

    public static readonly IReadOnlyList<string> Lifecycle = File.Concat(Session).ToArray();
    public static readonly IReadOnlyList<string> All = Lifecycle.Concat(Query).ToArray();
}
