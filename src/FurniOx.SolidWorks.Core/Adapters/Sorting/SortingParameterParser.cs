using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FurniOx.SolidWorks.Core.Adapters.Sorting;

internal static class SortingParameterParser
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<SortingPositionEntry> ParsePositionEntries(object? positionsParam)
    {
        var entries = new List<SortingPositionEntry>();

        if (positionsParam is string jsonString)
        {
            var parsed = JsonSerializer.Deserialize<List<SortingPositionEntry>>(
                jsonString,
                CaseInsensitiveOptions);
            if (parsed != null)
            {
                entries.AddRange(parsed);
            }
        }
        else if (positionsParam is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in jsonElement.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? string.Empty;
                var position = item.GetProperty("position").GetInt32();
                entries.Add(new SortingPositionEntry { Name = name, Position = position });
            }
        }
        else if (positionsParam is IEnumerable<object> objectList)
        {
            foreach (var item in objectList)
            {
                if (item is IDictionary<string, object?> dict)
                {
                    dict.TryGetValue("name", out var nameValue);
                    dict.TryGetValue("position", out var positionValue);
                    entries.Add(new SortingPositionEntry
                    {
                        Name = nameValue?.ToString() ?? string.Empty,
                        Position = Convert.ToInt32(positionValue ?? 0)
                    });
                }
            }
        }

        return entries;
    }
}
