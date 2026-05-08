using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class ExportToolsTests
{
    private static Mock<ISmartRouter> CreateRouter(string operation, string expectedPath)
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                operation,
                It.Is<IDictionary<string, object?>>(p => (string)p["Path"]! == expectedPath),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.SuccessResult(new { Ok = true }));
        return router;
    }

    [Fact]
    public async Task ExportToSTEP_PassesPathToRouter()
    {
        var router = CreateRouter("Export.ExportToSTEP", "C:/output/model.step");

        var tools = new ExportTools(router.Object);
        await tools.ExportToSTEP("C:/output/model.step");

        router.VerifyAll();
    }

    [Fact]
    public async Task ExportToSTEP_RouterReturnsFailure_ThrowsMcpException()
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                "Export.ExportToSTEP",
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.Failure("No active document"));

        var tools = new ExportTools(router.Object);
        await Assert.ThrowsAsync<McpException>(() => tools.ExportToSTEP("out.step"));
    }

    [Fact]
    public async Task ExportToIGES_PassesPathToRouter()
    {
        var router = CreateRouter("Export.ExportToIGES", "C:/output/model.igs");

        var tools = new ExportTools(router.Object);
        await tools.ExportToIGES("C:/output/model.igs");

        router.VerifyAll();
    }

    [Fact]
    public async Task ExportToSTL_PassesPathToRouter()
    {
        var router = CreateRouter("Export.ExportToSTL", "C:/output/model.stl");

        var tools = new ExportTools(router.Object);
        await tools.ExportToSTL("C:/output/model.stl");

        router.VerifyAll();
    }

    [Fact]
    public async Task ExportToPDF_PassesPathToRouter()
    {
        var router = CreateRouter("Export.ExportToPDF", "C:/output/drawing.pdf");

        var tools = new ExportTools(router.Object);
        await tools.ExportToPDF("C:/output/drawing.pdf");

        router.VerifyAll();
    }

    [Fact]
    public async Task ExportToDXF_PassesPathToRouter()
    {
        var router = CreateRouter("Export.ExportToDXF", "C:/output/drawing.dxf");

        var tools = new ExportTools(router.Object);
        await tools.ExportToDXF("C:/output/drawing.dxf");

        router.VerifyAll();
    }
}
