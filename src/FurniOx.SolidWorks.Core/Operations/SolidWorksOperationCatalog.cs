using System.Collections.Generic;
using System.Linq;

namespace FurniOx.SolidWorks.Core.Operations;

public static class SolidWorksOperationCatalog
{
    public static readonly IReadOnlyList<string> All =
        DocumentOperationNames.All
            .Concat(ExportOperationNames.All)
            .Concat(ConfigurationOperationNames.All)
            .Concat(SelectionOperationNames.All)
            .Concat(AssemblyBrowserOperationNames.All)
            .Concat(SketchOperationNames.All)
            .Concat(FeatureOperationNames.All)
            .Concat(SortingOperationNames.All)
            .Concat(AnalysisOperationNames.All)
            .Concat(CustomPropertyOperationNames.All)
            .Concat(SummaryInfoOperationNames.All)
            .Concat(CrossReferenceOperationNames.All)
            .Concat(EquationOperationNames.All)
            .Concat(DocumentGovernanceOperationNames.All)
            .Concat(DocumentSuppressionOperationNames.All)
            .Concat(DocumentReferenceReplacementOperationNames.All)
            .Concat(DocumentReferenceSearchPathOperationNames.All)
            .ToArray();

    public static readonly ISet<string> Known = new HashSet<string>(All);
}
