#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Adapters;

internal static partial class CrossReferenceBatchRunner
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static CrossReferenceBatchResult BuildResult(
        CrossReferenceBatchRunOptions options,
        IReadOnlyList<CrossReferenceDocumentResult> documents)
    {
        var hardBroken = documents.Sum(d => d.HardBrokenCount);
        var softBroken = documents.Sum(d => d.SoftBrokenCount);
        var broken = hardBroken + softBroken;
        return new CrossReferenceBatchResult
        {
            IncludeExternalFileReferences = options.IncludeExternalFileReferences,
            IncludeDrawingReferences = EffectiveIncludeDrawingReferences(options),
            IncludeEquationReferences = EffectiveIncludeEquationReferences(options),
            AllConfigurations = options.AllConfigurations,
            OpenUnloadedDocuments = options.OpenUnloadedDocuments,
            CloseOpened = options.CloseOpened,
            HiddenInGui = options.HiddenInGui,
            LightWeightOpen = options.LightWeightOpen,
            DontLoadHiddenComponents = options.DontLoadHiddenComponents,
            QuickMode = options.QuickMode,
            MaxDocOpenTimeMs = options.MaxDocOpenTimeMs,
            BatchSize = options.BatchSize,
            ChunkCount = options.BatchSize <= 0
                ? (documents.Count > 0 ? 1 : 0)
                : (int)Math.Ceiling(documents.Count / (double)Math.Max(1, options.BatchSize)),
            ChunkCleanupClosedDocumentCount = options.ChunkCleanupClosedDocumentCount,
            DocumentCount = documents.Count,
            ProcessedDocumentCount = documents.Count(d => !d.Skipped && d.Error == null),
            SkippedDocumentCount = documents.Count(d => d.Skipped),
            OpenedDocumentCount = documents.Count(d => d.OpenedByTool),
            ClosedDocumentCount = documents.Count(d => d.ClosedByTool),
            SideEffectClosedDocumentCount = documents.Sum(d => d.SideEffectClosedDocumentCount),
            ReferenceCount = documents.Sum(d => d.References.Count),
            ExternalReferenceCount = documents.Sum(d => d.ExternalReferenceCount),
            AuxiliaryReferenceCount = documents.Sum(d => d.AuxiliaryReferenceCount),
            DrawingReferenceCount = documents.Sum(d => d.DrawingReferenceCount),
            EquationReferenceCount = documents.Sum(d => d.EquationReferenceCount),
            StalePathCount = documents.Sum(d => d.StalePathCount),
            HardBrokenCount = hardBroken,
            SoftBrokenCount = softBroken,
            BrokenReferenceCount = broken,
            Passed = broken == 0 && documents.All(d => d.Error == null && !d.Skipped),
            Documents = documents.ToList()
        };
    }

    private static CrossReferenceBatchResult Failure(string error, CrossReferenceBatchRunOptions options)
        => new()
        {
            IncludeExternalFileReferences = options.IncludeExternalFileReferences,
            IncludeDrawingReferences = EffectiveIncludeDrawingReferences(options),
            IncludeEquationReferences = EffectiveIncludeEquationReferences(options),
            AllConfigurations = options.AllConfigurations,
            OpenUnloadedDocuments = options.OpenUnloadedDocuments,
            CloseOpened = options.CloseOpened,
            HiddenInGui = options.HiddenInGui,
            LightWeightOpen = options.LightWeightOpen,
            DontLoadHiddenComponents = options.DontLoadHiddenComponents,
            QuickMode = options.QuickMode,
            MaxDocOpenTimeMs = options.MaxDocOpenTimeMs,
            BatchSize = options.BatchSize,
            Passed = false,
            Error = error
        };

    private static CrossReferenceBatchResult TryWriteOutput(string outputPath, CrossReferenceBatchResult result)
    {
        if (!IsPathSafe(outputPath, out var pathError))
        {
            return result with { Error = $"Invalid output path: {pathError}", Passed = false };
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
            return result with { Error = $"Failed to write output file: {ex.Message}", Passed = false };
        }
    }

    private static bool EffectiveIncludeDrawingReferences(CrossReferenceBatchRunOptions options)
        => !options.QuickMode && options.IncludeDrawingReferences;

    private static bool EffectiveIncludeEquationReferences(CrossReferenceBatchRunOptions options)
        => !options.QuickMode && options.IncludeEquationReferences;

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
