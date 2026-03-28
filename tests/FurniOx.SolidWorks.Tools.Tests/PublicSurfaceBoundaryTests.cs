using FurniOx.SolidWorks.MCP.Tools;
using Xunit;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class PublicSurfaceBoundaryTests
{
    [Theory]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.AnalysisTools")]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.BatchAnalysisTools")]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.BatchCustomPropertyTools")]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.CustomPropertyTools")]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.SummaryInfoTools")]
    [InlineData("FurniOx.SolidWorks.MCP.BridgeBootstrapService")]
    public void PublicMcpAssembly_DoesNotContainPrivateToolTypes(string typeName)
    {
        Assert.Null(typeof(AssemblyBrowserTools).Assembly.GetType(typeName, throwOnError: false, ignoreCase: false));
    }

    [Fact]
    public void PublicMcpAssembly_ContainsAssemblyBrowserTools()
    {
        Assert.NotNull(typeof(AssemblyBrowserTools).Assembly.GetType(
            "FurniOx.SolidWorks.MCP.Tools.AssemblyBrowserTools",
            throwOnError: false,
            ignoreCase: false));
    }
}
