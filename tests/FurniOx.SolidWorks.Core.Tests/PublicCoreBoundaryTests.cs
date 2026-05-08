using FurniOx.SolidWorks.Core.Adapters;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class PublicCoreBoundaryTests
{
    [Theory]
    [InlineData("FurniOx.SolidWorks.Core.Bridge.BridgeAdapter")]
    [InlineData("FurniOx.SolidWorks.Core.Bridge.BridgeDiscovery")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.BatchCustomPropertyOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.Analysis.BatchAnalysisOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.Analysis.PartBatchAnalysisOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.Analysis.AssemblyBatchAnalysisOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.Document.DocumentRenameBatchOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.Document.DocumentSuppressionBatchOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Adapters.Document.DocumentReferenceReplacementBatchOperations")]
    [InlineData("FurniOx.SolidWorks.Core.Operations.AddinOperationNames")]
    [InlineData("FurniOx.SolidWorks.Core.Operations.AnalysisBatchOperationNames")]
    [InlineData("FurniOx.SolidWorks.Core.Operations.CustomPropertyBatchOperationNames")]
    [InlineData("FurniOx.SolidWorks.Core.DocManager.DocManagerPropertyReader")]
    [InlineData("FurniOx.SolidWorks.Core.DocManager.CompositePropertyReader")]
    [InlineData("FurniOx.SolidWorks.Core.DocManager.FallbackPropertyReader")]
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
