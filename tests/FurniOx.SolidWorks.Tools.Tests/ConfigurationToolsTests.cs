using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class ConfigurationToolsAdditionalTests
{
    private static Mock<ISmartRouter> CreateRouter(string operation, Func<IDictionary<string, object?>, bool> predicate)
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                operation,
                It.Is<IDictionary<string, object?>>(p => predicate(p)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Ok = true }));
        return router;
    }

    private static Mock<ISmartRouter> CreateRouterAny(string operation)
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                operation,
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Ok = true }));
        return router;
    }

    [Fact]
    public async Task GetConfigurationNames_RoutesToCorrectOperation()
    {
        var router = CreateRouterAny("Configuration.GetConfigurationNames");
        var tools = new ConfigurationTools(router.Object);

        await tools.GetConfigurationNames();

        router.VerifyAll();
    }

    [Fact]
    public async Task ActivateConfiguration_PassesNameToRouter()
    {
        var router = CreateRouter("Configuration.ActivateConfiguration", p =>
            (string)p["Name"]! == "HighGloss");

        var tools = new ConfigurationTools(router.Object);
        await tools.ActivateConfiguration("HighGloss");

        router.VerifyAll();
    }

    [Fact]
    public async Task AddConfiguration_WithDescription_PassesNameAndDescriptionToRouter()
    {
        var router = CreateRouter("Configuration.AddConfiguration", p =>
            (string)p["Name"]! == "Variant-A" &&
            (string)p["Description"]! == "Alternate design");

        var tools = new ConfigurationTools(router.Object);
        await tools.AddConfiguration("Variant-A", description: "Alternate design");

        router.VerifyAll();
    }

    [Fact]
    public async Task CopyConfiguration_PassesSourceAndTargetNamesToRouter()
    {
        var router = CreateRouter("Configuration.CopyConfiguration", p =>
            (string)p["SourceName"]! == "Default" &&
            (string)p["TargetName"]! == "Copy-1");

        var tools = new ConfigurationTools(router.Object);
        await tools.CopyConfiguration("Default", "Copy-1");

        router.VerifyAll();
    }
}
