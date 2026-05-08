namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Built-in SolidWorks document summary information properties.
/// These are OLE properties (Author, Title, Subject, etc.) distinct from custom properties.
/// </summary>
public sealed record DocumentSummaryInfo
{
    public string Title { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Keywords { get; init; } = string.Empty;
    public string Comments { get; init; } = string.Empty;
    public string SavedBy { get; init; } = string.Empty;
    public string CreateDate { get; init; } = string.Empty;
    public string SaveDate { get; init; } = string.Empty;
}
