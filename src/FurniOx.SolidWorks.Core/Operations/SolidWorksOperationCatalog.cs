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
            .ToArray();

    public static readonly ISet<string> Known = new HashSet<string>(All);
}
