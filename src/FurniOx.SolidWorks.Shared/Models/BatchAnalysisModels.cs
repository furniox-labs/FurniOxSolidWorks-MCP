namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Input item for batch analysis - represents one component to analyze
/// </summary>
public record BatchInputItem
{
    /// <summary>
    /// Instance path in assembly hierarchy (e.g., "SubAssy-1/Part-2")
    /// </summary>
    public string InstancePath { get; init; } = "";

    /// <summary>
    /// Full file path of the component
    /// </summary>
    public string FilePath { get; init; } = "";
}

/// <summary>
/// Metadata for batch analysis results
/// </summary>
public record BatchMetadata
{
    public int TotalRequested { get; init; }
    public int TotalAnalyzed { get; init; }
    public int TotalSkipped { get; init; }
    public int TotalFailed { get; init; }
    public string Timestamp { get; init; } = "";
}

/// <summary>
/// Item that was skipped during batch analysis
/// </summary>
public record BatchSkippedItem
{
    public string InstancePath { get; init; } = "";
    public string Reason { get; init; } = "";
}

/// <summary>
/// Item that failed during batch analysis
/// </summary>
public record BatchErrorItem
{
    public string InstancePath { get; init; } = "";
    public string Error { get; init; } = "";
}

/// <summary>
/// Result wrapper for individual batch item
/// </summary>
public record BatchResultItem
{
    public bool Success { get; init; }
    public object? Data { get; init; }
}

/// <summary>
/// Full batch analysis result (written to file)
/// </summary>
public record BatchAnalysisResult
{
    public BatchMetadata Metadata { get; init; } = new();
    public Dictionary<string, BatchResultItem> Results { get; init; } = new();
    public List<BatchSkippedItem> Skipped { get; init; } = new();
    public List<BatchErrorItem> Errors { get; init; } = new();
}

/// <summary>
/// Summary returned from batch analysis (to MCP)
/// </summary>
public record BatchAnalysisSummary
{
    public bool Success { get; init; }
    public int TotalAnalyzed { get; init; }
    public int TotalSkipped { get; init; }
    public int TotalFailed { get; init; }
    public string OutputPath { get; init; } = "";
    public string Message { get; init; } = "";
}
