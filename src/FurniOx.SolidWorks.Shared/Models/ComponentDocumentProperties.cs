using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Properties extracted from a single component document (part or sub-assembly).
/// Keyed by normalized file path in the parent result to deduplicate across instances.
/// </summary>
public sealed record ComponentDocumentProperties
{
    public Dictionary<string, string> CustomProperties { get; init; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, Dictionary<string, string>>? ConfigurationCustomProperties { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DocumentSummaryInfo? SummaryInfo { get; init; }
}
