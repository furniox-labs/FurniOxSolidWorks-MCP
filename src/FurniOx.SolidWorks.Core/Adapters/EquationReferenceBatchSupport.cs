using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Result-building, output-writing, and safe-accessor helpers used by
/// <see cref="EquationReferenceBatchRunner"/>.
/// </summary>
internal static class EquationReferenceBatchSupport
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Builds a top-level batch result that represents a hard failure before any
    /// documents were processed.
    /// </summary>
    internal static EquationReferenceBatchResult Failure(string error, EquationReferenceBatchRunOptions options)
    {
        return new EquationReferenceBatchResult
        {
            DryRun = options.DryRun,
            SaveDocuments = options.SaveDocuments,
            AllConfigurations = options.AllConfigurations,
            OpenUnloadedDocuments = options.OpenUnloadedDocuments,
            CloseOpened = options.CloseOpened,
            Error = error
        };
    }

    /// <summary>
    /// Aggregates per-document results into a top-level
    /// <see cref="EquationReferenceBatchResult"/>.
    /// </summary>
    internal static EquationReferenceBatchResult BuildResult(
        EquationReferenceBatchRunOptions options,
        int ruleCount,
        IReadOnlyList<EquationReferenceDocumentResult> documents)
    {
        return new EquationReferenceBatchResult
        {
            DryRun = options.DryRun,
            SaveDocuments = options.SaveDocuments,
            AllConfigurations = options.AllConfigurations,
            OpenUnloadedDocuments = options.OpenUnloadedDocuments,
            CloseOpened = options.CloseOpened,
            RenameRuleCount = ruleCount,
            DocumentCount = documents.Count,
            ProcessedDocumentCount = documents.Count(d => !d.Skipped && d.Error == null),
            SkippedDocumentCount = documents.Count(d => d.Skipped),
            OpenedDocumentCount = documents.Count(d => d.OpenedByTool),
            ClosedDocumentCount = documents.Count(d => d.ClosedByTool),
            ModifiedDocumentCount = documents.Count(d => d.Modified),
            SavedDocumentCount = documents.Count(d => d.Saved),
            EquationCount = documents.Sum(d => d.EquationCount),
            MatchedEquationCount = documents.Sum(d => d.MatchedCount),
            ChangedEquationCount = documents.Sum(d => d.ChangedCount),
            BrokenBeforeCount = documents.Sum(d => d.BrokenBeforeCount),
            BrokenAfterCount = documents.Sum(d => d.BrokenAfterCount),
            Documents = documents.ToList()
        };
    }

    /// <summary>
    /// Serializes <paramref name="result"/> to <paramref name="outputPath"/> and
    /// returns an updated result with <c>OutputPath</c> and
    /// <c>OutputFileSizeBytes</c> set, or an error message on failure.
    /// </summary>
    internal static EquationReferenceBatchResult TryWriteOutput(string outputPath, EquationReferenceBatchResult result)
    {
        if (!IsPathSafe(outputPath, out var pathError))
        {
            return result with { Error = $"Invalid output path: {pathError}" };
        }

        try
        {
            var fullPath = Path.GetFullPath(outputPath);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(result, OutputJsonOptions));
            return result with
            {
                OutputPath = fullPath,
                OutputFileSizeBytes = new FileInfo(fullPath).Length
            };
        }
        catch (Exception ex)
        {
            return result with { Error = $"Failed to write output file: {ex.Message}" };
        }
    }

    /// <summary>
    /// Calls <paramref name="getter"/> and returns its result, or
    /// <paramref name="fallback"/> if an exception is thrown or the value is null.
    /// </summary>
    internal static string SafeString(Func<string?> getter, string fallback = "")
    {
        try
        {
            return getter() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Calls <paramref name="getter"/> and returns its result, or 0 on exception.
    /// </summary>
    internal static int SafeInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Calls <paramref name="getter"/> and returns its result, or
    /// <see langword="null"/> on exception.
    /// </summary>
    internal static int? SafeNullableInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a COM SafeArray, <c>object[]</c>, or scalar into a typed
    /// <see cref="IReadOnlyList{T}"/> of <see cref="object"/>.
    /// </summary>
    internal static IReadOnlyList<object> ToObjectArray(object? value)
    {
        if (value == null)
        {
            return Array.Empty<object>();
        }

        if (value is object[] objects)
        {
            return objects;
        }

        if (value is Array array)
        {
            return array.Cast<object>().ToArray();
        }

        return new[] { value };
    }

    private static bool IsPathSafe(string path, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "Path is empty or whitespace";
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                errorMessage = "UNC paths are not allowed";
                return false;
            }

            if (fullPath.StartsWith(@"\\.\", StringComparison.Ordinal) || fullPath.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                errorMessage = "Device paths are not allowed";
                return false;
            }

            var dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir))
            {
                errorMessage = "Invalid path - no parent directory";
                return false;
            }

            Directory.CreateDirectory(dir);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid path: {ex.Message}";
            return false;
        }
    }
}
