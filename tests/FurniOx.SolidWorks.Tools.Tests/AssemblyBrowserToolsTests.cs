using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class AssemblyBrowserToolsAdditionalTests
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

    [Fact]
    public async Task ListAssemblyComponents_DefaultParams_PassesFalseForBothFlagsToRouter()
    {
        var router = CreateRouter("AssemblyBrowser.ListAssemblyComponents", p =>
            (bool)p["TopLevelOnly"]! == false &&
            (bool)p["IncludePaths"]! == false);

        var tools = new AssemblyBrowserTools(router.Object);
        await tools.ListAssemblyComponents();

        router.VerifyAll();
    }

    [Fact]
    public async Task ListAssemblyComponents_TopLevelWithPaths_PassesTrueForBothFlagsToRouter()
    {
        var router = CreateRouter("AssemblyBrowser.ListAssemblyComponents", p =>
            (bool)p["TopLevelOnly"]! == true &&
            (bool)p["IncludePaths"]! == true);

        var tools = new AssemblyBrowserTools(router.Object);
        await tools.ListAssemblyComponents(topLevelOnly: true, includePaths: true);

        router.VerifyAll();
    }
}
