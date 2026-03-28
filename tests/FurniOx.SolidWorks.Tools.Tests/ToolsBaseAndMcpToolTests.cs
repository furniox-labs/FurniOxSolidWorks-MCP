using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace FurniOx.SolidWorks.Tools.Tests;

file sealed class TestableToolsBase : ToolsBase
{
    public TestableToolsBase(ISmartRouter router) : base(router) { }

    public static object? TestMapExecutionResult(ExecutionResult result) => MapExecutionResult(result);
}

file static class ToolTestHelpers
{
    public static string ToJson(object? value) => JsonSerializer.Serialize(value);

    public static Mock<ISmartRouter> CreateRouterReturning(ExecutionResult result)
    {
        var mock = new Mock<ISmartRouter>(MockBehavior.Strict);
        mock.Setup(router => router.RouteAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    public static McpServerToolAttribute GetToolAttribute<TTool>(string methodName)
    {
        var method = typeof(TTool).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
            .Cast<McpServerToolAttribute>()
            .Single();

        return attribute;
    }
}

public sealed class ToolsBaseMappingTests
{
    [Fact]
    public void MapExecutionResult_WithDataAndNoMessage_ReturnsRawData()
    {
        var data = new { Name = "Part1", Type = 1 };

        var mapped = TestableToolsBase.TestMapExecutionResult(ExecutionResult.SuccessResult(data));

        var json = ToolTestHelpers.ToJson(mapped);
        Assert.Contains("\"Name\":\"Part1\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Success\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MapExecutionResult_WithMessage_ReturnsEnvelope()
    {
        var mapped = TestableToolsBase.TestMapExecutionResult(
            ExecutionResult.SuccessResult(new { Saved = true }, "Saved model"));

        var json = ToolTestHelpers.ToJson(mapped);
        Assert.Contains("\"Success\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"Message\":\"Saved model\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Saved\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MapExecutionResult_WithoutDataOrMessage_ReturnsSuccessEnvelope()
    {
        var mapped = TestableToolsBase.TestMapExecutionResult(ExecutionResult.SuccessResult());

        Assert.Equal("{\"Success\":true,\"Message\":null,\"Data\":null}", ToolTestHelpers.ToJson(mapped));
    }

    [Fact]
    public void MapExecutionResult_OnFailure_ThrowsMcpException()
    {
        var ex = Assert.Throws<McpException>(() =>
            TestableToolsBase.TestMapExecutionResult(ExecutionResult.Failure("SolidWorks not connected")));

        Assert.Contains("SolidWorks not connected", ex.Message, StringComparison.Ordinal);
    }
}

public sealed class DocumentToolsTests
{
    [Fact]
    public async Task OpenModel_RoutesToDocumentOpenModel_AndReturnsStructuredData()
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                "Document.OpenModel",
                It.Is<IDictionary<string, object?>>(p =>
                    (string)p["Path"]! == "test.sldprt" &&
                    (int)p["Type"]! == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Opened = true }));

        var tools = new DocumentTools(router.Object);
        var result = await tools.OpenModel("test.sldprt", 1);

        Assert.Contains("\"Opened\":true", ToolTestHelpers.ToJson(result), StringComparison.Ordinal);
        router.VerifyAll();
    }

    [Fact]
    public void GetDocumentInfo_HasMcpServerToolAttribute()
    {
        Assert.NotNull(ToolTestHelpers.GetToolAttribute<DocumentTools>(nameof(DocumentTools.GetDocumentInfo)));
    }
}

public sealed class ConfigurationToolsTests
{
    [Fact]
    public async Task GetConfigurationNames_RoutesToCurrentOperationName()
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                "Configuration.GetConfigurationNames",
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Names = new[] { "Default" } }));

        var tools = new ConfigurationTools(router.Object);
        var result = await tools.GetConfigurationNames();

        Assert.Contains("Default", ToolTestHelpers.ToJson(result), StringComparison.Ordinal);
        router.VerifyAll();
    }
}

public sealed class AssemblyBrowserToolsTests
{
    [Fact]
    public async Task ListAssemblyComponents_RoutesToAssemblyBrowserOperation()
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                "AssemblyBrowser.ListAssemblyComponents",
                It.Is<IDictionary<string, object?>>(p =>
                    (bool)p["TopLevelOnly"]! == false &&
                    (bool)p["IncludePaths"]! == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { TotalComponents = 2 }));

        var tools = new AssemblyBrowserTools(router.Object);
        var result = await tools.ListAssemblyComponents(includePaths: true);

        Assert.Contains("\"TotalComponents\":2", ToolTestHelpers.ToJson(result), StringComparison.Ordinal);
        router.VerifyAll();
    }

    [Fact]
    public void ListAssemblyComponents_HasMcpServerToolAttribute()
    {
        Assert.NotNull(ToolTestHelpers.GetToolAttribute<AssemblyBrowserTools>(nameof(AssemblyBrowserTools.ListAssemblyComponents)));
    }
}
