using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class AssemblyBrowserOperationNames
{
    public const string ListAssemblyComponents = "AssemblyBrowser.ListAssemblyComponents";

    public static readonly IReadOnlyList<string> All =
    [
        ListAssemblyComponents
    ];
}
