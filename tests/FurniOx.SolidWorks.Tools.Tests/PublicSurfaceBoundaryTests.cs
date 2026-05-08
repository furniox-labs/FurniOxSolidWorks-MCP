using FurniOx.SolidWorks.MCP.Tools;
using ModelContextProtocol.Server;
using System.Reflection;
using Xunit;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class PublicSurfaceBoundaryTests
{
    [Theory]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.BatchAnalysisTools")]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.BatchCustomPropertyTools")]
    [InlineData("FurniOx.SolidWorks.MCP.Tools.FurniOxAddinTools")]
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

    [Fact]
    public void PublicMcpAssembly_ContainsSingleDocumentGovernanceTools()
    {
        Assert.NotNull(typeof(DocumentGovernanceTools).Assembly.GetType(
            "FurniOx.SolidWorks.MCP.Tools.DocumentGovernanceTools",
            throwOnError: false,
            ignoreCase: false));
    }

    [Fact]
    public void PublicMcpAssembly_DoesNotContainPrivateToolFamilies()
    {
        string[] privateFragments =
        [
            "Batch",
            "Swood",
            "Addin",
            "BridgeDiagnostics",
            "Diagnostic"
        ];

        var leakedTypes = typeof(AssemblyBrowserTools).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Where(type => privateFragments.Any(fragment =>
                type.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leakedTypes);
    }
}
