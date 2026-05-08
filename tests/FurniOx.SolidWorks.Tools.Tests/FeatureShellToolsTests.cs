using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class FeatureShellToolsTests
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
    public async Task CreateShell_DefaultParams_PassesThicknessAndInwardDirectionToRouter()
    {
        var router = CreateRouter("Feature.CreateShell", p =>
            (double)p["Thickness"]! == 3.0 &&
            (int)p["Direction"]! == 0 &&
            p["FaceNames"] == null);

        var tools = new FeatureShellTools(router.Object);
        await tools.CreateShell();

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateShell_CustomThicknessAndFaces_PassesFaceNamesToRouter()
    {
        var faces = new[] { "Face1@Part1", "Face2@Part1" };
        var router = CreateRouter("Feature.CreateShell", p =>
            (double)p["Thickness"]! == 5.0 &&
            ReferenceEquals(p["FaceNames"], faces));

        var tools = new FeatureShellTools(router.Object);
        await tools.CreateShell(thickness: 5.0, faceNames: faces);

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateShell_OutwardDirection_PassesDirectionOneToRouter()
    {
        var router = CreateRouter("Feature.CreateShell", p =>
            (int)p["Direction"]! == 1);

        var tools = new FeatureShellTools(router.Object);
        await tools.CreateShell(direction: 1);

        router.VerifyAll();
    }
}
