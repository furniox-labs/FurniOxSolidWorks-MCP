using System.Collections.Generic;

namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Result of a component sorting operation
/// </summary>
public sealed record SortingResult
{
    /// <summary>
    /// Whether the sorting operation was performed (false if dry run)
    /// </summary>
    public bool Applied { get; init; }

    /// <summary>
    /// Number of components that were/would be reordered
    /// </summary>
    public int ReorderedCount { get; init; }

    /// <summary>
    /// Total number of components considered
    /// </summary>
    public int TotalComponentCount { get; init; }

    /// <summary>
    /// Number of components that were skipped (e.g., in folders, dependencies)
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Sort criteria used
    /// </summary>
    public string SortBy { get; init; } = "name";

    /// <summary>
    /// Sort order used
    /// </summary>
    public string SortOrder { get; init; } = "ascending";

    /// <summary>
    /// Whether natural sorting was used
    /// </summary>
    public bool NaturalSort { get; init; }

    /// <summary>
    /// Scope of sorting (top_level or all)
    /// </summary>
    public string Scope { get; init; } = "top_level";

    /// <summary>
    /// Preview of changes (component name -> new position)
    /// </summary>
    public List<SortingChange> Changes { get; init; } = new();

    /// <summary>
    /// Any warnings or issues encountered
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Individual component reorder change
/// </summary>
public sealed record SortingChange
{
    /// <summary>
    /// Component name
    /// </summary>
    public string ComponentName { get; init; } = string.Empty;

    /// <summary>
    /// Original position in tree (0-based)
    /// </summary>
    public int OriginalPosition { get; init; }

    /// <summary>
    /// New position in tree (0-based)
    /// </summary>
    public int NewPosition { get; init; }

    /// <summary>
    /// Sort key value used for ordering
    /// </summary>
    public string SortKey { get; init; } = string.Empty;
}
