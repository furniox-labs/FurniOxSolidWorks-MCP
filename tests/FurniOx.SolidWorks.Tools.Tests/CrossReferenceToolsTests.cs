using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;
using Xunit;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class CrossReferenceToolsTests
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
    public async Task ScanExternalReferences_WithDocumentPath_PassesDocumentPathAndFixedFlagsToRouter()
    {
        var router = CreateRouter("CrossReference.ScanExternalReferences", p =>
            (string)p["DocumentPath"]! == "C:/temp/part.sldprt" &&
            (bool)p["IncludeAuxiliaryReferences"]! == true &&
            (bool)p["IncludeDrawingReferences"]! == true &&
            (bool)p["IncludeActiveDocument"]! == true &&
            (bool)p["UseActiveAssemblyComponents"]! == false &&
            (bool)p["IncludeOpenDocuments"]! == false);

        var tools = new CrossReferenceTools(router.Object);
        await tools.ScanExternalReferences(documentPath: "C:/temp/part.sldprt");

        router.VerifyAll();
    }

    [Fact]
    public async Task ScanExternalReferences_QuickMode_PassesTrueQuickModeToRouter()
    {
        var router = CreateRouter("CrossReference.ScanExternalReferences", p =>
            (bool)p["QuickMode"]! == true);

        var tools = new CrossReferenceTools(router.Object);
        await tools.ScanExternalReferences(quickMode: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task ScanComponentExternalReferences_PassesComponentNameToRouter()
    {
        var router = CreateRouter("CrossReference.ScanComponentExternalReferences", p =>
            (string)p["ComponentName"]! == "6_0-1<1>" &&
            p["OutputPath"] == null);

        var tools = new CrossReferenceTools(router.Object);
        await tools.ScanComponentExternalReferences("6_0-1<1>");

        router.VerifyAll();
    }

    [Fact]
    public async Task VerifyNoBrokenReferences_WithDocumentPath_PassesDocumentPathToRouter()
    {
        var router = CreateRouter("CrossReference.VerifyNoBrokenReferencesSingle", p =>
            (string)p["DocumentPath"]! == "C:/parts/panel.SLDPRT" &&
            (bool)p["IncludeActiveDocument"]! == true &&
            (bool)p["UseActiveAssemblyComponents"]! == false &&
            (bool)p["IncludeOpenDocuments"]! == false);

        var tools = new CrossReferenceTools(router.Object);
        await tools.VerifyNoBrokenReferences(documentPath: "C:/parts/panel.SLDPRT");

        router.VerifyAll();
    }
}
