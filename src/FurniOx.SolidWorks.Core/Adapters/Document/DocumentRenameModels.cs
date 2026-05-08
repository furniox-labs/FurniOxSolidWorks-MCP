#nullable enable

using System.Collections.Generic;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

// Shared value types used across DocumentRenameBatchOperations and its helpers.

internal sealed record BatchContext(SldWorks? App, ModelDoc2? Model, IAssemblyDoc? Assembly, ExecutionResult? Result)
{
    public static BatchContext Fail(ExecutionResult result) => new(null, null, null, result);
}

internal sealed record RenameFileItem(string OldPath, string NewPath);

internal sealed record ComponentOccurrence(
    IComponent2 Component,
    string InstancePath,
    string Path,
    string NormalizedPath,
    int Depth,
    bool IsVirtual,
    bool IsPatternInstance,
    int SuppressionState,
    bool HasLoadedModelDocument)
{
    public bool IsResolvedForRename =>
        SuppressionState == (int)swComponentSuppressionState_e.swComponentResolved
        || SuppressionState == (int)swComponentSuppressionState_e.swComponentFullyResolved;

    public bool IsLightweight =>
        SuppressionState == (int)swComponentSuppressionState_e.swComponentLightweight
        || SuppressionState == (int)swComponentSuppressionState_e.swComponentFullyLightweight;
}

internal sealed record RenamePlanItem(
    string OldPath,
    string NewPath,
    ComponentOccurrence? Occurrence,
    IReadOnlyList<string> Blockers)
{
    public int Depth => Occurrence?.Depth ?? 0;
}

internal sealed record AppliedRename(string OldPath, string NewPath, string InstancePath);

internal sealed record SaveCollectionResult(bool Success, object Summary);

internal sealed record OpenDocumentSnapshot(
    IReadOnlySet<string> Paths,
    IReadOnlySet<string> Titles);

internal sealed record RenameAttempt(bool Success, string? FinalPath, string? Error, int? ErrorCode, string? ErrorName)
{
    public static RenameAttempt Ok(string finalPath) => new(true, finalPath, null, null, null);

    public static RenameAttempt Fail(string error, int? errorCode = null, string? errorName = null)
        => new(false, null, error, errorCode, errorName);
}
