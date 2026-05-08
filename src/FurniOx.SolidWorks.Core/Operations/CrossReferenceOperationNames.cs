using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class CrossReferenceOperationNames
{
    public const string ScanExternalReferences = "CrossReference.ScanExternalReferences";
    public const string ScanComponentExternalReferences = "CrossReference.ScanComponentExternalReferences";
    public const string ScanFeatureExternalReferences = "CrossReference.ScanFeatureExternalReferences";
    public const string ScanSketchExternalReferences = "CrossReference.ScanSketchExternalReferences";
    public const string VerifyNoBrokenReferencesSingle = "CrossReference.VerifyNoBrokenReferencesSingle";

    public static readonly IReadOnlyList<string> All =
    [
        ScanExternalReferences,
        ScanComponentExternalReferences,
        ScanFeatureExternalReferences,
        ScanSketchExternalReferences,
        VerifyNoBrokenReferencesSingle
    ];
}
