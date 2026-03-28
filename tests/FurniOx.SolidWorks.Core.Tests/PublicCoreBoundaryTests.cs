using FurniOx.SolidWorks.Core.Adapters;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class PublicCoreBoundaryTests
{
    [Theory]
    [InlineData("FurniOx.SolidWorks.Core.Bridge.BridgeAdapter")]
    [InlineData("FurniOx.SolidWorks.Core.Bridge.BridgeDiscovery")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.AnalysisOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.CustomPropertyOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.BatchCustomPropertyOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.SummaryInfoOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.TargetDocumentResolutionSupport")]
    public void PublicCoreAssembly_DoesNotContainPrivateImplementationTypes(string typeName)
    {
        Assert.Null(typeof(SolidWorks2023Adapter).Assembly.GetType(typeName, throwOnError: false, ignoreCase: false));
    }

    [Fact]
    public void PublicCoreAssembly_ContainsAssemblyBrowserOperations()
    {
        Assert.NotNull(typeof(SolidWorks2023Adapter).Assembly.GetType(
            "FurniOx.SolidWorks.Core.Adapters.AssemblyBrowserOperations",
            throwOnError: false,
            ignoreCase: false));
    }
}
