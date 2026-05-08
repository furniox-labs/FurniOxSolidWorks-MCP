namespace FurniOx.SolidWorks.Shared.Models;

public record CrossReferenceDocumentInput
{
    public string Path { get; init; } = "";
}

public record CrossReferenceBatchInput
{
    public List<CrossReferenceDocumentInput> Documents { get; init; } = new();
    public bool IncludeActiveDocument { get; init; } = true;
    public bool UseActiveAssemblyComponents { get; init; } = true;
    public bool IncludeOpenDocuments { get; init; }
    public bool OpenUnloadedDocuments { get; init; }
}

public record CrossReferenceItem
{
    public string Category { get; init; } = "";
    public string Source { get; init; } = "";
    public string ReferencingDocumentPath { get; init; } = "";
    public string ReferencingDocumentTitle { get; init; } = "";
    public int ReferencingDocumentType { get; init; }
    public string DiscoveredViaDocumentPath { get; init; } = "";
    public string DiscoveredViaDocumentTitle { get; init; } = "";
    public int DiscoveredViaDocumentType { get; init; }
    public string ReferencingEntityType { get; init; } = "";
    public string ReferencingEntityName { get; init; } = "";
    public string ReferencedModelPath { get; init; } = "";
    public bool ReferencedModelPathExists { get; init; }
    public bool StalePath { get; init; }
    public string ComponentPath { get; init; } = "";
    public string FeatureName { get; init; } = "";
    public string DataType { get; init; } = "";
    public string ReferencedEntity { get; init; } = "";
    public string FeatureComponent { get; init; } = "";
    public int? Status { get; init; }
    public string StatusName { get; init; } = "";
    public bool IsBroken { get; init; }
    public bool IsHardBroken { get; init; }
    public bool IsSoftBroken { get; init; }
    public int? ConfigOption { get; init; }
    public string ConfigOptionName { get; init; } = "";
    public string ConfigName { get; init; } = "";
    public int? EquationIndex { get; init; }
    public string EquationText { get; init; } = "";
    public string EquationReferenceToken { get; init; } = "";
    public string Notes { get; init; } = "";
}

public record CrossReferenceSummary
{
    public int ReferenceCount { get; init; }
    public int ExternalReferenceCount { get; init; }
    public int AuxiliaryReferenceCount { get; init; }
    public int DrawingReferenceCount { get; init; }
    public int EquationReferenceCount { get; init; }
    public int StalePathCount { get; init; }
    public int HardBrokenCount { get; init; }
    public int SoftBrokenCount { get; init; }
    public int BrokenReferenceCount { get; init; }
}

public record CrossReferenceDocumentResult
{
    public string Path { get; init; } = "";
    public string Title { get; init; } = "";
    public int DocumentType { get; init; }
    public bool Skipped { get; init; }
    public string SkippedReason { get; init; } = "";
    public bool OpenedByTool { get; init; }
    public bool ClosedByTool { get; init; }
    public int SideEffectClosedDocumentCount { get; init; }
    public long? OpenElapsedMs { get; init; }
    public bool OpenExceededMaxTime { get; init; }
    public int ExternalReferenceCount { get; init; }
    public int AuxiliaryReferenceCount { get; init; }
    public int DrawingReferenceCount { get; init; }
    public int EquationReferenceCount { get; init; }
    public int StalePathCount { get; init; }
    public int HardBrokenCount { get; init; }
    public int SoftBrokenCount { get; init; }
    public int BrokenReferenceCount { get; init; }
    public string? Error { get; init; }
    public List<CrossReferenceItem> References { get; init; } = new();
}

public record CrossReferenceBatchResult
{
    public bool IncludeExternalFileReferences { get; init; }
    public bool IncludeDrawingReferences { get; init; }
    public bool IncludeEquationReferences { get; init; }
    public bool AllConfigurations { get; init; }
    public bool OpenUnloadedDocuments { get; init; }
    public bool CloseOpened { get; init; }
    public bool HiddenInGui { get; init; }
    public bool LightWeightOpen { get; init; }
    public bool DontLoadHiddenComponents { get; init; }
    public bool QuickMode { get; init; }
    public int MaxDocOpenTimeMs { get; init; }
    public int BatchSize { get; init; }
    public int ChunkCount { get; init; }
    public int ChunkCleanupClosedDocumentCount { get; init; }
    public int DocumentCount { get; init; }
    public int ProcessedDocumentCount { get; init; }
    public int SkippedDocumentCount { get; init; }
    public int OpenedDocumentCount { get; init; }
    public int ClosedDocumentCount { get; init; }
    public int SideEffectClosedDocumentCount { get; init; }
    public int ReferenceCount { get; init; }
    public int ExternalReferenceCount { get; init; }
    public int AuxiliaryReferenceCount { get; init; }
    public int DrawingReferenceCount { get; init; }
    public int EquationReferenceCount { get; init; }
    public int StalePathCount { get; init; }
    public int HardBrokenCount { get; init; }
    public int SoftBrokenCount { get; init; }
    public int BrokenReferenceCount { get; init; }
    public bool Passed { get; init; }
    public string? OutputPath { get; init; }
    public long? OutputFileSizeBytes { get; init; }
    public string? Error { get; init; }
    public List<CrossReferenceDocumentResult> Documents { get; init; } = new();
}
