using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

internal sealed record EquationReferenceBatchRunOptions
{
    public string? InputPath { get; init; }
    public string? OutputPath { get; init; }
    public string? DocumentPath { get; init; }
    public bool DryRun { get; init; } = true;
    public bool SaveDocuments { get; init; }
    public bool AllConfigurations { get; init; }
    public bool OpenUnloadedDocuments { get; init; }
    public bool CloseOpened { get; init; } = true;
    public bool HiddenInGui { get; init; } = true;
    public bool? IncludeActiveDocument { get; init; }
    public bool? UseActiveAssemblyComponents { get; init; }
}

internal static class EquationReferenceBatchRunner
{
    private const int MaxDocuments = 10000;
    private const int MaxRenameRules = 10000;

    public static EquationReferenceBatchResult Execute(SldWorks app, EquationReferenceBatchRunOptions options)
    {
        if (app == null)
        {
            return EquationReferenceBatchSupport.Failure("Not connected to SolidWorks", options);
        }

        EquationReferenceBatchInput input;
        try
        {
            input = EquationReferenceInputParser.LoadInput(options.InputPath);
        }
        catch (Exception ex)
        {
            return EquationReferenceBatchSupport.Failure($"Failed to load equation reference batch input: {ex.Message}", options);
        }

        if (options.IncludeActiveDocument.HasValue)
        {
            input = input with { IncludeActiveDocument = options.IncludeActiveDocument.Value };
        }
        if (options.UseActiveAssemblyComponents.HasValue)
        {
            input = input with { UseActiveAssemblyComponents = options.UseActiveAssemblyComponents.Value };
        }
        input = input with { OpenUnloadedDocuments = options.OpenUnloadedDocuments };
        if (!string.IsNullOrWhiteSpace(options.DocumentPath))
        {
            input = input with
            {
                Documents = new List<EquationReferenceDocumentInput>
                {
                    new() { Path = options.DocumentPath! }
                },
                IncludeActiveDocument = false,
                UseActiveAssemblyComponents = false
            };
        }

        var rules = BuildReplacementRules(input.RenameMap);
        if (rules.Count > MaxRenameRules)
        {
            return EquationReferenceBatchSupport.Failure($"Rename rule count {rules.Count} exceeds maximum of {MaxRenameRules}", options);
        }

        IReadOnlyList<DocumentTarget> targets;
        try
        {
            targets = ResolveTargets(app, input);
        }
        catch (Exception ex)
        {
            return EquationReferenceBatchSupport.Failure($"Failed to resolve target documents: {ex.Message}", options);
        }

        if (targets.Count > MaxDocuments)
        {
            return EquationReferenceBatchSupport.Failure($"Document count {targets.Count} exceeds maximum of {MaxDocuments}", options);
        }

        var documentResults = new List<EquationReferenceDocumentResult>();
        foreach (var target in targets)
        {
            documentResults.Add(ProcessDocument(app, target, rules, options));
        }

        var result = EquationReferenceBatchSupport.BuildResult(options, rules.Count, documentResults);
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            result = EquationReferenceBatchSupport.TryWriteOutput(options.OutputPath!, result);
        }

        return result;
    }

    private static IReadOnlyList<DocumentTarget> ResolveTargets(SldWorks app, EquationReferenceBatchInput input)
    {
        var targets = new List<DocumentTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = EquationReferenceDocumentHelper.TryGetActiveDocument(app);

        if (input.IncludeActiveDocument && active != null)
        {
            EquationReferenceDocumentHelper.AddExistingTarget(targets, seen, active);
        }

        foreach (var document in input.Documents)
        {
            EquationReferenceDocumentHelper.AddPathTarget(targets, seen, document.Path);
        }

        if (input.UseActiveAssemblyComponents && active != null
            && ((IModelDoc2)active).GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            var assembly = (IAssemblyDoc)active;
            foreach (var component in EquationReferenceBatchSupport.ToObjectArray(assembly.GetComponents(false)).OfType<IComponent2>())
            {
                try
                {
                    var path = component.GetPathName();
                    var componentModel = component.GetModelDoc2() as ModelDoc2;
                    if (componentModel != null)
                    {
                        EquationReferenceDocumentHelper.AddExistingTarget(targets, seen, componentModel, path);
                    }
                    else if (!string.IsNullOrWhiteSpace(path))
                    {
                        EquationReferenceDocumentHelper.AddPathTarget(targets, seen, path);
                    }
                }
                catch
                {
                    // Individual component failures should not block the batch.
                }
            }
        }

        if (targets.Count == 0 && active != null)
        {
            EquationReferenceDocumentHelper.AddExistingTarget(targets, seen, active);
        }

        return targets;
    }

    private static List<ReplacementRule> BuildReplacementRules(IReadOnlyList<EquationReferenceRename> renames)
    {
        var rules = new List<ReplacementRule>();
        foreach (var rename in renames)
        {
            AddExactTokenRule(rules, rename.OldToken, rename.NewToken, "token");

            if (!string.IsNullOrWhiteSpace(rename.OldName) && !string.IsNullOrWhiteSpace(rename.NewName))
            {
                AddExactTokenRule(
                    rules,
                    "@" + TrimLeadingAt(rename.OldName),
                    "@" + TrimLeadingAt(rename.NewName),
                    "component-name");
            }

            if (!string.IsNullOrWhiteSpace(rename.OldRefPath) && !string.IsNullOrWhiteSpace(rename.NewRefPath))
            {
                var oldBase = System.IO.Path.GetFileNameWithoutExtension(rename.OldRefPath);
                var newBase = System.IO.Path.GetFileNameWithoutExtension(rename.NewRefPath);
                if (!string.IsNullOrWhiteSpace(oldBase) && !string.IsNullOrWhiteSpace(newBase))
                {
                    AddExactTokenRule(rules, "@" + oldBase, "@" + newBase, "reference-path-filename");
                }
            }
        }

        return rules
            .GroupBy(r => r.OldToken, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(r => r.OldToken.Length)
            .ToList();
    }

    private static void AddExactTokenRule(List<ReplacementRule> rules, string oldToken, string newToken, string source)
    {
        if (string.IsNullOrWhiteSpace(oldToken) || string.IsNullOrWhiteSpace(newToken))
        {
            return;
        }

        if (string.Equals(oldToken, newToken, StringComparison.Ordinal))
        {
            return;
        }

        rules.Add(new ReplacementRule(oldToken, newToken, source));
    }

    private static string TrimLeadingAt(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("@", StringComparison.Ordinal) ? trimmed.Substring(1) : trimmed;
    }

    private static EquationReferenceDocumentResult ProcessDocument(
        SldWorks app,
        DocumentTarget target,
        IReadOnlyList<ReplacementRule> rules,
        EquationReferenceBatchRunOptions options)
    {
        ModelDoc2? model = target.Model;
        var openedByTool = false;
        var closedByTool = false;

        try
        {
            if (model == null)
            {
                model = EquationReferenceDocumentHelper.TryGetOpenDocumentByName(app, target.Path);
            }

            if (model == null)
            {
                if (!options.OpenUnloadedDocuments)
                {
                    return new EquationReferenceDocumentResult
                    {
                        Path = target.Path,
                        Title = target.Title,
                        Skipped = true,
                        SkippedReason = "Document is not loaded; set openUnloadedDocuments=true to open it for equation scanning or repair."
                    };
                }

                var openResult = EquationReferenceDocumentHelper.OpenDocument(app, target.Path, options.HiddenInGui);
                if (openResult.Model == null)
                {
                    return new EquationReferenceDocumentResult
                    {
                        Path = target.Path,
                        Title = target.Title,
                        OpenedByTool = false,
                        Error = openResult.Error
                    };
                }

                model = openResult.Model;
                openedByTool = openResult.OpenedByTool;
            }

            var originalConfig = EquationReferenceDocumentHelper.GetActiveConfigurationName(model);
            var configNames = EquationReferenceDocumentHelper.GetConfigurationNames(model, options.AllConfigurations, originalConfig);
            var pendingMatches = new List<PendingEquationMatch>();
            var equationCount = 0;
            var changed = false;

            foreach (var configName in configNames)
            {
                if (options.AllConfigurations && !string.IsNullOrWhiteSpace(configName))
                {
                    model.ShowConfiguration2(configName);
                }

                var equationManager = model.GetEquationMgr();
                if (equationManager == null)
                {
                    continue;
                }

                var count = equationManager.GetCount();
                equationCount += count;
                for (var index = 0; index < count; index++)
                {
                    var beforeText = EquationReferenceBatchSupport.SafeString(() => equationManager.Equation[index]);
                    var beforeState = EvaluateEquation(equationManager, index);
                    var afterText = ApplyRules(beforeText, rules, out var replacements);
                    var wouldChange = replacements.Count > 0 && !string.Equals(beforeText, afterText, StringComparison.Ordinal);
                    var shouldReport = wouldChange || beforeState.Broken;
                    if (!shouldReport)
                    {
                        continue;
                    }

                    var wasChanged = false;
                    string? error = null;
                    if (wouldChange && !options.DryRun)
                    {
                        try
                        {
                            equationManager.Equation[index] = afterText;
                            wasChanged = true;
                            changed = true;
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                        }
                    }

                    pendingMatches.Add(new PendingEquationMatch(
                        index,
                        configName,
                        beforeText,
                        afterText,
                        wouldChange,
                        wasChanged,
                        beforeState,
                        replacements,
                        error));
                }

                if (changed && !options.DryRun)
                {
                    try { equationManager.EvaluateAll(); } catch { }
                    try { model.EditRebuild3(); } catch { }
                }
            }

            if (options.AllConfigurations && !string.IsNullOrWhiteSpace(originalConfig))
            {
                try { model.ShowConfiguration2(originalConfig); } catch { }
            }

            var matches = FinalizeMatches(model, pendingMatches, options);
            var modified = matches.Any(m => m.Changed);
            var saved = false;
            var saveErrors = 0;
            var saveWarnings = 0;
            if (modified && options.SaveDocuments && !options.DryRun)
            {
                try
                {
                    saved = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
                }
                catch (Exception ex)
                {
                    return BuildDocumentResult(model, target, openedByTool, closedByTool, equationCount, matches, modified, saved, saveErrors, saveWarnings, ex.Message);
                }
            }

            if (openedByTool && options.CloseOpened && (!modified || saved))
            {
                closedByTool = EquationReferenceDocumentHelper.TryCloseDocument(app, model);
            }

            return BuildDocumentResult(model, target, openedByTool, closedByTool, equationCount, matches, modified, saved, saveErrors, saveWarnings, null);
        }
        catch (Exception ex)
        {
            return new EquationReferenceDocumentResult
            {
                Path = target.Path,
                Title = target.Title,
                OpenedByTool = openedByTool,
                ClosedByTool = closedByTool,
                Error = ex.Message
            };
        }
    }

    private static List<EquationReferenceMatch> FinalizeMatches(
        ModelDoc2 model,
        IReadOnlyList<PendingEquationMatch> pendingMatches,
        EquationReferenceBatchRunOptions options)
    {
        if (pendingMatches.Count == 0)
        {
            return new List<EquationReferenceMatch>();
        }

        var originalConfig = EquationReferenceDocumentHelper.GetActiveConfigurationName(model);
        var matches = new List<EquationReferenceMatch>();
        foreach (var pending in pendingMatches)
        {
            EquationEvaluationState afterState = pending.BeforeState;
            if (pending.Changed && !options.DryRun)
            {
                try
                {
                    if (options.AllConfigurations && !string.IsNullOrWhiteSpace(pending.ConfigurationName))
                    {
                        model.ShowConfiguration2(pending.ConfigurationName);
                    }

                    var equationManager = model.GetEquationMgr();
                    afterState = equationManager == null
                        ? pending.BeforeState
                        : EvaluateEquation(equationManager, pending.EquationIndex);
                }
                catch (Exception ex)
                {
                    afterState = new EquationEvaluationState(true, null, null, ex.Message);
                }
            }

            matches.Add(new EquationReferenceMatch
            {
                EquationIndex = pending.EquationIndex,
                ConfigurationName = pending.ConfigurationName,
                EquationBefore = pending.EquationBefore,
                EquationAfter = pending.EquationAfter,
                WouldChange = pending.WouldChange,
                Changed = pending.Changed,
                BrokenBefore = pending.BeforeState.Broken,
                BrokenAfter = afterState.Broken,
                StatusBefore = pending.BeforeState.Status,
                StatusAfter = afterState.Status,
                ValueBefore = pending.BeforeState.Value,
                ValueAfter = afterState.Value,
                Error = pending.Error ?? afterState.Error,
                Replacements = pending.Replacements
                    .Select(r => new EquationReferenceTokenReplacement
                    {
                        OldToken = r.OldToken,
                        NewToken = r.NewToken,
                        Source = r.Source
                    })
                    .ToList()
            });
        }

        if (options.AllConfigurations && !string.IsNullOrWhiteSpace(originalConfig))
        {
            try { model.ShowConfiguration2(originalConfig); } catch { }
        }

        return matches;
    }

    private static EquationReferenceDocumentResult BuildDocumentResult(
        ModelDoc2 model,
        DocumentTarget target,
        bool openedByTool,
        bool closedByTool,
        int equationCount,
        IReadOnlyList<EquationReferenceMatch> matches,
        bool modified,
        bool saved,
        int saveErrors,
        int saveWarnings,
        string? error)
    {
        return new EquationReferenceDocumentResult
        {
            Path = EquationReferenceBatchSupport.SafeString(() => model.GetPathName(), target.Path),
            Title = EquationReferenceBatchSupport.SafeString(() => model.GetTitle(), target.Title),
            DocumentType = EquationReferenceBatchSupport.SafeInt(() => ((IModelDoc2)model).GetType()),
            OpenedByTool = openedByTool,
            ClosedByTool = closedByTool,
            Modified = modified,
            Saved = saved,
            SaveErrors = saveErrors,
            SaveWarnings = saveWarnings,
            ConfigurationCount = matches.Select(m => m.ConfigurationName).Distinct(StringComparer.Ordinal).Count(),
            EquationCount = equationCount,
            MatchedCount = matches.Count,
            ChangedCount = matches.Count(m => m.Changed),
            BrokenBeforeCount = matches.Count(m => m.BrokenBefore),
            BrokenAfterCount = matches.Count(m => m.BrokenAfter),
            Error = error,
            Matches = matches.ToList()
        };
    }

    private static EquationEvaluationState EvaluateEquation(EquationMgr equationManager, int index)
    {
        int? status = null;
        double? value = null;
        try
        {
            value = equationManager.Value[index];
            status = EquationReferenceBatchSupport.SafeNullableInt(() => equationManager.Status);
            return new EquationEvaluationState(status.HasValue && status.Value != 0, status, value, null);
        }
        catch (Exception ex)
        {
            status = EquationReferenceBatchSupport.SafeNullableInt(() => equationManager.Status);
            return new EquationEvaluationState(true, status, value, ex.Message);
        }
    }

    private static string ApplyRules(
        string equation,
        IReadOnlyList<ReplacementRule> rules,
        out List<ReplacementRule> replacements)
    {
        replacements = new List<ReplacementRule>();
        var updated = equation;
        foreach (var rule in rules)
        {
            if (updated.IndexOf(rule.OldToken, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            updated = ReplaceOrdinal(updated, rule.OldToken, rule.NewToken);
            replacements.Add(rule);
        }

        return updated;
    }

    private static string ReplaceOrdinal(string value, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldValue))
        {
            return value;
        }

        var startIndex = 0;
        var result = new StringBuilder(value.Length);
        while (true)
        {
            var matchIndex = value.IndexOf(oldValue, startIndex, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                result.Append(value, startIndex, value.Length - startIndex);
                return result.ToString();
            }

            result.Append(value, startIndex, matchIndex - startIndex);
            result.Append(newValue);
            startIndex = matchIndex + oldValue.Length;
        }
    }

    private sealed record ReplacementRule(string OldToken, string NewToken, string Source);
    private sealed record EquationEvaluationState(bool Broken, int? Status, double? Value, string? Error);
    private sealed record PendingEquationMatch(
        int EquationIndex,
        string ConfigurationName,
        string EquationBefore,
        string EquationAfter,
        bool WouldChange,
        bool Changed,
        EquationEvaluationState BeforeState,
        IReadOnlyList<ReplacementRule> Replacements,
        string? Error);
}
