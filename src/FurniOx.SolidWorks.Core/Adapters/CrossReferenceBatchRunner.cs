#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

internal sealed record CrossReferenceBatchRunOptions
{
    public string? InputPath { get; init; }
    public string? OutputPath { get; init; }
    public string? DocumentPath { get; init; }
    public string TargetScope { get; init; } = "Documents";
    public string? ComponentName { get; init; }
    public string? FeatureName { get; init; }
    public bool IncludeExternalFileReferences { get; init; } = true;
    public bool IncludeAuxiliaryReferences { get; init; } = true;
    public bool IncludeDrawingReferences { get; init; } = true;
    public bool IncludeEquationReferences { get; init; } = true;
    public bool AllConfigurations { get; init; }
    public bool OpenUnloadedDocuments { get; init; }
    public bool CloseOpened { get; init; } = true;
    public bool HiddenInGui { get; init; } = true;
    public bool LightWeightOpen { get; init; } = true;
    public bool DontLoadHiddenComponents { get; init; } = true;
    public bool QuickMode { get; init; }
    public int MaxDocOpenTimeMs { get; init; }
    public int BatchSize { get; init; } = 20;
    public int ChunkCleanupClosedDocumentCount { get; init; }
    public bool? IncludeActiveDocument { get; init; }
    public bool? UseActiveAssemblyComponents { get; init; }
    public bool? IncludeOpenDocuments { get; init; }
}

internal static partial class CrossReferenceBatchRunner
{
    private const int MaxDocuments = 10000;

    private static readonly Regex EquationCrossPartTokenRegex = new(
        @"@(?<token>[^""'=+\-*/(),\[\]]+?(?:<\d+>)?\.(?:Part|Assembly|SLDPRT|SLDASM))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CrossReferenceBatchResult Execute(SldWorks app, CrossReferenceBatchRunOptions options)
    {
        if (app == null)
        {
            return Failure("Not connected to SolidWorks", options);
        }

        CrossReferenceBatchInput input;
        try
        {
            input = LoadInput(options.InputPath);
        }
        catch (Exception ex)
        {
            return Failure($"Failed to load cross-reference batch input: {ex.Message}", options);
        }

        if (options.IncludeActiveDocument.HasValue)
        {
            input = input with { IncludeActiveDocument = options.IncludeActiveDocument.Value };
        }
        if (options.UseActiveAssemblyComponents.HasValue)
        {
            input = input with { UseActiveAssemblyComponents = options.UseActiveAssemblyComponents.Value };
        }
        if (options.IncludeOpenDocuments.HasValue)
        {
            input = input with { IncludeOpenDocuments = options.IncludeOpenDocuments.Value };
        }
        input = input with { OpenUnloadedDocuments = options.OpenUnloadedDocuments };

        if (!string.IsNullOrWhiteSpace(options.DocumentPath))
        {
            input = input with
            {
                Documents = new List<CrossReferenceDocumentInput>
                {
                    new() { Path = options.DocumentPath! }
                },
                IncludeActiveDocument = false,
                UseActiveAssemblyComponents = false,
                IncludeOpenDocuments = false
            };
        }

        if (!string.Equals(options.TargetScope, "Documents", StringComparison.OrdinalIgnoreCase))
        {
            var scoped = ExecuteScopedScan(app, options);
            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                scoped = TryWriteOutput(options.OutputPath!, scoped);
            }

            return scoped;
        }

        IReadOnlyList<DocumentTarget> targets;
        try
        {
            targets = ResolveTargets(app, input);
        }
        catch (Exception ex)
        {
            return Failure($"Failed to resolve target documents: {ex.Message}", options);
        }

        if (targets.Count > MaxDocuments)
        {
            return Failure($"Document count {targets.Count} exceeds maximum of {MaxDocuments}", options);
        }

        var documents = new List<CrossReferenceDocumentResult>();
        var chunkCleanupClosedCount = 0;
        var batchSize = options.BatchSize <= 0 ? targets.Count : Math.Max(1, options.BatchSize);
        for (var start = 0; start < targets.Count; start += batchSize)
        {
            var chunkOpenBefore = options.CloseOpened
                ? SnapshotOpenDocumentKeys(app)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in targets.Skip(start).Take(batchSize))
            {
                documents.Add(ProcessDocument(app, target, options));
            }

            if (options.CloseOpened)
            {
                chunkCleanupClosedCount += CloseDocumentsOpenedSince(app, chunkOpenBefore);
            }
        }

        var result = BuildResult(options with { ChunkCleanupClosedDocumentCount = chunkCleanupClosedCount }, documents);
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            result = TryWriteOutput(options.OutputPath!, result);
        }

        return result;
    }

    private static IReadOnlyList<DocumentTarget> ResolveTargets(SldWorks app, CrossReferenceBatchInput input)
    {
        var targets = new List<DocumentTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = TryGetActiveDocument(app);

        if (input.IncludeActiveDocument && active != null)
        {
            AddExistingTarget(targets, seen, active);
        }

        foreach (var document in input.Documents)
        {
            AddPathTarget(targets, seen, document.Path);
        }

        if (input.IncludeOpenDocuments)
        {
            foreach (var doc in GetOpenDocuments(app))
            {
                AddExistingTarget(targets, seen, doc);
            }
        }

        if (input.UseActiveAssemblyComponents && active != null
            && ((IModelDoc2)active).GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            var assembly = (IAssemblyDoc)active;
            foreach (var component in ToObjectArray(assembly.GetComponents(false)).OfType<IComponent2>())
            {
                try
                {
                    var path = component.GetPathName();
                    var componentModel = component.GetModelDoc2() as ModelDoc2;
                    if (componentModel != null)
                    {
                        AddExistingTarget(targets, seen, componentModel, path);
                    }
                    else if (!string.IsNullOrWhiteSpace(path))
                    {
                        AddPathTarget(targets, seen, path);
                    }
                }
                catch
                {
                    // Individual component failures should not block a project scan.
                }
            }
        }

        if (targets.Count == 0 && active != null)
        {
            AddExistingTarget(targets, seen, active);
        }

        return targets;
    }

    private static CrossReferenceDocumentResult ProcessDocument(
        SldWorks app,
        DocumentTarget target,
        CrossReferenceBatchRunOptions options)
    {
        ModelDoc2? model = target.Model;
        var openedByTool = false;
        var closedByTool = false;
        long? openElapsedMs = null;
        var openExceededMaxTime = false;
        var openBefore = options.CloseOpened ? SnapshotOpenDocumentKeys(app) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sideEffectClosedCount = 0;

        try
        {
            if (model == null)
            {
                model = TryGetOpenDocumentByName(app, target.Path);
            }

            if (model == null)
            {
                if (!options.OpenUnloadedDocuments)
                {
                    return new CrossReferenceDocumentResult
                    {
                        Path = target.Path,
                        Title = target.Title,
                        Skipped = true,
                        SkippedReason = "Document is not loaded; set openUnloadedDocuments=true to open it for scanning."
                    };
                }

                var openResult = OpenDocument(app, target.Path, options);
                if (openResult.Model == null)
                {
                    sideEffectClosedCount = options.CloseOpened
                        ? CloseDocumentsOpenedSince(app, openBefore)
                        : 0;

                    return new CrossReferenceDocumentResult
                    {
                        Path = target.Path,
                        Title = target.Title,
                        OpenedByTool = false,
                        SideEffectClosedDocumentCount = sideEffectClosedCount,
                        OpenElapsedMs = openResult.OpenElapsedMs,
                        OpenExceededMaxTime = openResult.OpenExceededMaxTime,
                        Error = openResult.Error
                    };
                }

                model = openResult.Model;
                openedByTool = openResult.OpenedByTool;
                openElapsedMs = openResult.OpenElapsedMs;
                openExceededMaxTime = openResult.OpenExceededMaxTime;
                if (openResult.OpenExceededMaxTime)
                {
                    if (openedByTool && options.CloseOpened)
                    {
                        closedByTool = TryCloseDocument(app, model);
                    }
                    sideEffectClosedCount = options.CloseOpened
                        ? CloseDocumentsOpenedSince(app, openBefore)
                        : 0;

                    return new CrossReferenceDocumentResult
                    {
                        Path = target.Path,
                        Title = target.Title,
                        OpenedByTool = openedByTool,
                        ClosedByTool = closedByTool,
                        SideEffectClosedDocumentCount = sideEffectClosedCount,
                        OpenElapsedMs = openElapsedMs,
                        OpenExceededMaxTime = true,
                        Error = $"OpenDoc6 exceeded maxDocOpenTimeMs ({openResult.OpenElapsedMs}ms > {options.MaxDocOpenTimeMs}ms)."
                    };
                }
            }

            var references = CrossReferenceExtractionSupport.ScanDocumentReferences(
                model,
                new CrossReferenceScanOptions
                {
                    IncludeExternalFileReferences = options.IncludeExternalFileReferences,
                    IncludeAuxiliaryReferences = options.IncludeAuxiliaryReferences,
                    IncludeDrawingReferences = EffectiveIncludeDrawingReferences(options),
                    IncludeEquationReferences = EffectiveIncludeEquationReferences(options),
                    AllConfigurations = options.AllConfigurations
                });

            if (openedByTool && options.CloseOpened)
            {
                closedByTool = TryCloseDocument(app, model);
            }
            sideEffectClosedCount = options.CloseOpened
                ? CloseDocumentsOpenedSince(app, openBefore)
                : 0;

            return new CrossReferenceDocumentResult
            {
                Path = SafeString(() => model.GetPathName(), target.Path),
                Title = SafeString(() => model.GetTitle(), target.Title),
                DocumentType = SafeInt(() => ((IModelDoc2)model).GetType()),
                OpenedByTool = openedByTool,
                ClosedByTool = closedByTool,
                SideEffectClosedDocumentCount = sideEffectClosedCount,
                OpenElapsedMs = openElapsedMs,
                OpenExceededMaxTime = openExceededMaxTime,
                ExternalReferenceCount = references.Count(r => r.Source == "ListExternalFileReferences2"),
                AuxiliaryReferenceCount = references.Count(r => r.Source == "ListAuxiliaryExternalFileReferences"),
                DrawingReferenceCount = references.Count(r => r.Source == "DrawingView"),
                EquationReferenceCount = references.Count(r => r.Source == "EquationManager"),
                StalePathCount = references.Count(r => r.StalePath),
                HardBrokenCount = references.Count(r => r.IsHardBroken),
                SoftBrokenCount = references.Count(r => r.IsSoftBroken),
                BrokenReferenceCount = references.Count(r => r.IsBroken),
                References = references
            };
        }
        catch (Exception ex)
        {
            if (openedByTool && options.CloseOpened && model != null && !closedByTool)
            {
                closedByTool = TryCloseDocument(app, model);
            }
            sideEffectClosedCount = options.CloseOpened
                ? CloseDocumentsOpenedSince(app, openBefore)
                : 0;

            return new CrossReferenceDocumentResult
            {
                Path = target.Path,
                Title = target.Title,
                OpenedByTool = openedByTool,
                ClosedByTool = closedByTool,
                SideEffectClosedDocumentCount = sideEffectClosedCount,
                OpenElapsedMs = openElapsedMs,
                OpenExceededMaxTime = openExceededMaxTime,
                Error = ex.Message
            };
        }
    }

    private static CrossReferenceBatchResult ExecuteScopedScan(SldWorks app, CrossReferenceBatchRunOptions options)
    {
        var active = TryGetActiveDocument(app);
        if (active == null)
        {
            return Failure("No active SolidWorks document.", options);
        }

        try
        {
            var references = options.TargetScope.Equals("Component", StringComparison.OrdinalIgnoreCase)
                ? CrossReferenceExtractionSupport.ScanComponentExternalReferences(active, options.ComponentName)
                : CrossReferenceExtractionSupport.ScanFeatureExternalReferences(
                    active,
                    options.FeatureName,
                    options.TargetScope.Equals("Sketch", StringComparison.OrdinalIgnoreCase));

            return BuildResult(
                options,
                new[]
                {
                    new CrossReferenceDocumentResult
                    {
                        Path = SafeString(() => active.GetPathName()),
                        Title = SafeString(() => active.GetTitle()),
                        DocumentType = SafeInt(() => ((IModelDoc2)active).GetType()),
                        ExternalReferenceCount = references.Count(r =>
                            r.Source == "Component.ListExternalFileReferences2"
                            || r.Source == "Feature.ListExternalFileReferences2"),
                        StalePathCount = references.Count(r => r.StalePath),
                        HardBrokenCount = references.Count(r => r.IsHardBroken),
                        SoftBrokenCount = references.Count(r => r.IsSoftBroken),
                        BrokenReferenceCount = references.Count(r => r.IsBroken),
                        References = references
                    }
                });
        }
        catch (Exception ex)
        {
            return Failure(ex.Message, options);
        }
    }
}
