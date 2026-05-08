namespace FurniOx.SolidWorks.Shared.Models;

public record EquationReferenceDocumentInput
{
    public string Path { get; init; } = "";
}

public record EquationReferenceRename
{
    public string OldName { get; init; } = "";
    public string NewName { get; init; } = "";
    public string OldToken { get; init; } = "";
    public string NewToken { get; init; } = "";
    public string OldRefPath { get; init; } = "";
    public string NewRefPath { get; init; } = "";
}

public record EquationReferenceBatchInput
{
    public List<EquationReferenceDocumentInput> Documents { get; init; } = new();
    public List<EquationReferenceRename> RenameMap { get; init; } = new();
    public bool IncludeActiveDocument { get; init; } = true;
    public bool UseActiveAssemblyComponents { get; init; }
    public bool OpenUnloadedDocuments { get; init; }
}

public record EquationReferenceTokenReplacement
{
    public string OldToken { get; init; } = "";
    public string NewToken { get; init; } = "";
    public string Source { get; init; } = "";
}

public record EquationReferenceMatch
{
    public int EquationIndex { get; init; }
    public string ConfigurationName { get; init; } = "";
    public string EquationBefore { get; init; } = "";
    public string EquationAfter { get; init; } = "";
    public bool WouldChange { get; init; }
    public bool Changed { get; init; }
    public bool BrokenBefore { get; init; }
    public bool BrokenAfter { get; init; }
    public int? StatusBefore { get; init; }
    public int? StatusAfter { get; init; }
    public double? ValueBefore { get; init; }
    public double? ValueAfter { get; init; }
    public string? Error { get; init; }
    public List<EquationReferenceTokenReplacement> Replacements { get; init; } = new();
}

public record EquationReferenceDocumentResult
{
    public string Path { get; init; } = "";
    public string Title { get; init; } = "";
    public int DocumentType { get; init; }
    public bool Skipped { get; init; }
    public string SkippedReason { get; init; } = "";
    public bool OpenedByTool { get; init; }
    public bool ClosedByTool { get; init; }
    public bool Modified { get; init; }
    public bool Saved { get; init; }
    public int SaveErrors { get; init; }
    public int SaveWarnings { get; init; }
    public int ConfigurationCount { get; init; }
    public int EquationCount { get; init; }
    public int MatchedCount { get; init; }
    public int ChangedCount { get; init; }
    public int BrokenBeforeCount { get; init; }
    public int BrokenAfterCount { get; init; }
    public string? Error { get; init; }
    public List<EquationReferenceMatch> Matches { get; init; } = new();
}

public record EquationReferenceBatchResult
{
    public bool DryRun { get; init; }
    public bool SaveDocuments { get; init; }
    public bool AllConfigurations { get; init; }
    public bool OpenUnloadedDocuments { get; init; }
    public bool CloseOpened { get; init; }
    public int RenameRuleCount { get; init; }
    public int DocumentCount { get; init; }
    public int ProcessedDocumentCount { get; init; }
    public int SkippedDocumentCount { get; init; }
    public int OpenedDocumentCount { get; init; }
    public int ClosedDocumentCount { get; init; }
    public int ModifiedDocumentCount { get; init; }
    public int SavedDocumentCount { get; init; }
    public int EquationCount { get; init; }
    public int MatchedEquationCount { get; init; }
    public int ChangedEquationCount { get; init; }
    public int BrokenBeforeCount { get; init; }
    public int BrokenAfterCount { get; init; }
    public string? OutputPath { get; init; }
    public long? OutputFileSizeBytes { get; init; }
    public string? Error { get; init; }
    public List<EquationReferenceDocumentResult> Documents { get; init; } = new();
}
