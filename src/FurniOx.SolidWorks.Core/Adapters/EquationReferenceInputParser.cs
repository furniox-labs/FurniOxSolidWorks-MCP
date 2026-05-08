using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Parses JSON input files and in-memory JSON elements into
/// <see cref="EquationReferenceBatchInput"/> structures for the batch runner.
/// </summary>
internal static class EquationReferenceInputParser
{
    /// <summary>
    /// Loads batch input from a file path or returns a default active-document
    /// input when <paramref name="inputPath"/> is null or empty.
    /// </summary>
    /// <param name="inputPath">Optional path to a JSON input file.</param>
    /// <returns>Parsed <see cref="EquationReferenceBatchInput"/>.</returns>
    /// <exception cref="FileNotFoundException">Input file not found.</exception>
    /// <exception cref="InvalidOperationException">JSON root is neither object nor array.</exception>
    internal static EquationReferenceBatchInput LoadInput(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return new EquationReferenceBatchInput
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
        if (root.ValueKind == JsonValueKind.Object)
        {
            return ParseObjectInput(root);
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            var items = root.EnumerateArray().ToList();
            if (items.Count == 0)
            {
                return new EquationReferenceBatchInput();
            }

            if (items.Any(IsRenameElement))
            {
                return new EquationReferenceBatchInput
                {
                    RenameMap = ParseRenameArray(items),
                    IncludeActiveDocument = true,
                    UseActiveAssemblyComponents = true
                };
            }

            return new EquationReferenceBatchInput
            {
                Documents = ParseDocumentArray(items),
                IncludeActiveDocument = false,
                UseActiveAssemblyComponents = false
            };
        }

        throw new InvalidOperationException("Input JSON must be an object or array.");
    }

    private static EquationReferenceBatchInput ParseObjectInput(JsonElement root)
    {
        var input = new EquationReferenceBatchInput();

        if (TryGetProperty(root, out var docs, "Documents", "documents", "Files", "files", "Targets", "targets")
            && docs.ValueKind == JsonValueKind.Array)
        {
            input = input with { Documents = ParseDocumentArray(docs.EnumerateArray().ToList()) };
        }

        if (TryGetProperty(root, out var renameMap, "RenameMap", "renameMap", "Renames", "renames", "Replacements", "replacements")
            && renameMap.ValueKind == JsonValueKind.Array)
        {
            input = input with { RenameMap = ParseRenameArray(renameMap.EnumerateArray().ToList()) };
        }

        if (TryReadBool(root, out var includeActive, "IncludeActiveDocument", "includeActiveDocument"))
        {
            input = input with { IncludeActiveDocument = includeActive };
        }

        if (TryReadBool(root, out var useActiveComponents, "UseActiveAssemblyComponents", "useActiveAssemblyComponents"))
        {
            input = input with { UseActiveAssemblyComponents = useActiveComponents };
        }

        if (TryReadBool(root, out var openUnloaded, "OpenUnloadedDocuments", "openUnloadedDocuments", "OpenMissingDocuments", "openMissingDocuments"))
        {
            input = input with { OpenUnloadedDocuments = openUnloaded };
        }

        return input;
    }

    internal static List<EquationReferenceDocumentInput> ParseDocumentArray(IReadOnlyList<JsonElement> elements)
    {
        var documents = new List<EquationReferenceDocumentInput>();
        foreach (var element in elements)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var path = element.GetString();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    documents.Add(new EquationReferenceDocumentInput { Path = path! });
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
                documents.Add(new EquationReferenceDocumentInput { Path = itemPath! });
            }
        }

        return documents;
    }

    internal static List<EquationReferenceRename> ParseRenameArray(IReadOnlyList<JsonElement> elements)
    {
        var renames = new List<EquationReferenceRename>();
        foreach (var element in elements)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            renames.Add(new EquationReferenceRename
            {
                OldName = ReadString(element, "OldName", "oldName", "ComponentName", "componentName", "FromName", "fromName") ?? "",
                NewName = ReadString(element, "NewName", "newName", "NewComponentName", "newComponentName", "ToName", "toName") ?? "",
                OldToken = ReadString(element, "OldToken", "oldToken", "FromToken", "fromToken") ?? "",
                NewToken = ReadString(element, "NewToken", "newToken", "ToToken", "toToken") ?? "",
                OldRefPath = ReadString(element, "OldRefPath", "oldRefPath", "OldPath", "oldPath", "FromPath", "fromPath") ?? "",
                NewRefPath = ReadString(element, "NewRefPath", "newRefPath", "NewPath", "newPath", "ToPath", "toPath") ?? ""
            });
        }

        return renames;
    }

    private static bool IsRenameElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return ReadString(element, "OldName", "oldName", "OldToken", "oldToken", "OldRefPath", "oldRefPath", "FromPath", "fromPath") != null;
    }

    internal static string? ReadString(JsonElement element, params string[] names)
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

    internal static bool TryReadBool(JsonElement element, out bool value, params string[] names)
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

    internal static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
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
