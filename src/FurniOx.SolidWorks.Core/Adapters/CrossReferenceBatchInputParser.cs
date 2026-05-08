#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Adapters;

internal static partial class CrossReferenceBatchRunner
{
    private static CrossReferenceBatchInput LoadInput(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return new CrossReferenceBatchInput
            {
                IncludeActiveDocument = true,
                UseActiveAssemblyComponents = true
            };
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(inputPath));
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return new CrossReferenceBatchInput
            {
                Documents = ParseDocumentArray(root.EnumerateArray().ToList()),
                IncludeActiveDocument = false,
                UseActiveAssemblyComponents = false
            };
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Input JSON must be an object or array.");
        }

        var input = new CrossReferenceBatchInput();
        if (TryGetProperty(root, out var docs, "Documents", "documents", "Files", "files", "Targets", "targets")
            && docs.ValueKind == JsonValueKind.Array)
        {
            input = input with { Documents = ParseDocumentArray(docs.EnumerateArray().ToList()) };
        }

        if (TryReadBool(root, out var includeActive, "IncludeActiveDocument", "includeActiveDocument"))
        {
            input = input with { IncludeActiveDocument = includeActive };
        }
        if (TryReadBool(root, out var useActiveComponents, "UseActiveAssemblyComponents", "useActiveAssemblyComponents"))
        {
            input = input with { UseActiveAssemblyComponents = useActiveComponents };
        }
        if (TryReadBool(root, out var includeOpenDocuments, "IncludeOpenDocuments", "includeOpenDocuments"))
        {
            input = input with { IncludeOpenDocuments = includeOpenDocuments };
        }
        if (TryReadBool(root, out var openUnloaded, "OpenUnloadedDocuments", "openUnloadedDocuments", "OpenMissingDocuments", "openMissingDocuments"))
        {
            input = input with { OpenUnloadedDocuments = openUnloaded };
        }

        return input;
    }

    private static List<CrossReferenceDocumentInput> ParseDocumentArray(IReadOnlyList<JsonElement> elements)
    {
        var documents = new List<CrossReferenceDocumentInput>();
        foreach (var element in elements)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var path = element.GetString();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    documents.Add(new CrossReferenceDocumentInput { Path = path! });
                }
                continue;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var itemPath = ReadString(element, "Path", "path", "FilePath", "filePath", "DocumentPath", "documentPath");
            if (!string.IsNullOrWhiteSpace(itemPath))
            {
                documents.Add(new CrossReferenceDocumentInput { Path = itemPath! });
            }
        }

        return documents;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, out var value, name) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static bool TryReadBool(JsonElement element, out bool value, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, out var candidate, name))
            {
                continue;
            }

            if (candidate.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (candidate.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
