using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class CrossReferenceBatchOperationNames
{
    public const string ScanExternalReferencesFullBatch = "CrossReference.ScanExternalReferencesFullBatch";
    public const string VerifyNoBrokenReferences = "CrossReference.VerifyNoBrokenReferences";
    public const string RefreshCachedPathsBatch = "CrossReference.RefreshCachedPathsBatch";

    public static readonly IReadOnlyList<string> All =
    [
        ScanExternalReferencesFullBatch,
        VerifyNoBrokenReferences,
        RefreshCachedPathsBatch
    ];
}
