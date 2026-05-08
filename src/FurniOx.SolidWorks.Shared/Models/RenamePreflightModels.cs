namespace FurniOx.SolidWorks.Shared.Models;

public record RenamePreflightPlanItem
{
    public string ComponentName { get; init; } = "";
    public string NewFileName { get; init; } = "";
    public string NewPath { get; init; } = "";
}

public record RenamePreflightRenameMapItem
{
    public string OldName { get; init; } = "";
    public string NewName { get; init; } = "";
    public string OldToken { get; init; } = "";
    public string NewToken { get; init; } = "";
    public string OldRefPath { get; init; } = "";
    public string NewRefPath { get; init; } = "";
}

public record RenamePreflightItem
{
    public string ComponentName { get; init; } = "";
    public string ConfigurationName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string CurrentFileName { get; init; } = "";
    public string ProposedFileName { get; init; } = "";
    public string ProposedPath { get; init; } = "";
    public string SuppressionState { get; init; } = "";
    public int SuppressionStateCode { get; init; }
    public bool IsSuppressed { get; init; }
    public bool IsLightweight { get; init; }
    public bool IsResolved { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsPatternInstance { get; init; }
    public bool CurrentFileExists { get; init; }
    public bool TargetFileExists { get; init; }
    public bool IsReadOnly { get; init; }
    public bool RenameNeeded { get; init; }
    public bool RequiresResolveForRename { get; init; }
    public bool CanRename { get; init; }
    public List<string> Reasons { get; init; } = new();
    public List<string> BlockingIssues { get; init; } = new();
    public RenamePreflightRenameMapItem? RenameMap { get; init; }
}

public record RenamePreflightResult
{
    public string ActiveAssemblyPath { get; init; } = "";
    public string ActiveAssemblyTitle { get; init; } = "";
    public string RequiredPrefix { get; init; } = "";
    public bool AllConfigurations { get; init; }
    public bool OpenUnloadedDocuments { get; init; }
    public int ComponentCount { get; init; }
    public int RenameNeededCount { get; init; }
    public int BlockingIssueCount { get; init; }
    public bool CanApply { get; init; }
    public string? OutputPath { get; init; }
    public long? OutputFileSizeBytes { get; init; }
    public string? Error { get; init; }
    public List<RenamePreflightItem> Items { get; init; } = new();
}
