using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class DocumentToolsAdditionalTests
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
    public async Task SaveModel_DefaultParams_PassesSaveReferencesAndSilentToRouter()
    {
        var router = CreateRouter("Document.SaveModel", p =>
            (bool)p["SaveReferences"]! == false &&
            (bool)p["Silent"]! == true &&
            (bool)p["SuppressSaveDialogs"]! == true);

        var tools = new DocumentTools(router.Object);
        await tools.SaveModel();

        router.VerifyAll();
    }

    [Fact]
    public async Task SaveModel_WithSaveReferences_PassesTrueToRouter()
    {
        var router = CreateRouter("Document.SaveModel", p =>
            (bool)p["SaveReferences"]! == true);

        var tools = new DocumentTools(router.Object);
        await tools.SaveModel(saveReferences: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task CloseModel_WithTitle_PassesTitleAndSaveOptionToRouter()
    {
        var router = CreateRouter("Document.CloseModel", p =>
            (string)p["Title"]! == "Part1.sldprt" &&
            (int)p["SaveOption"]! == 1);

        var tools = new DocumentTools(router.Object);
        await tools.CloseModel("Part1.sldprt", saveOption: 1);

        router.VerifyAll();
    }

    [Fact]
    public async Task CloseModel_DefaultTitle_PassesEmptyStringToRouter()
    {
        var router = CreateRouter("Document.CloseModel", p =>
            (string)p["Title"]! == "" &&
            (int)p["SaveOption"]! == 0);

        var tools = new DocumentTools(router.Object);
        await tools.CloseModel();

        router.VerifyAll();
    }

    [Fact]
    public async Task GetAllOpenDocuments_Default_PassesVisibleOnlyFalseToRouter()
    {
        var router = CreateRouter("Document.GetAllOpenDocuments", p =>
            (bool)p["VisibleOnly"]! == false);

        var tools = new DocumentTools(router.Object);
        await tools.GetAllOpenDocuments();

        router.VerifyAll();
    }

    [Fact]
    public async Task GetAllOpenDocuments_VisibleOnly_PassesTrueToRouter()
    {
        var router = CreateRouter("Document.GetAllOpenDocuments", p =>
            (bool)p["VisibleOnly"]! == true);

        var tools = new DocumentTools(router.Object);
        await tools.GetAllOpenDocuments(visibleOnly: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task ActivateDocument_PassesTitleToRouter()
    {
        var router = CreateRouter("Document.ActivateDocument", p =>
            (string)p["Title"]! == "Assembly1.sldasm");

        var tools = new DocumentTools(router.Object);
        await tools.ActivateDocument("Assembly1.sldasm");

        router.VerifyAll();
    }

    [Fact]
    public async Task CreateDocument_PassesTypeToRouter()
    {
        var router = CreateRouter("Document.CreateDocument", p =>
            (int)p["Type"]! == 2);

        var tools = new DocumentTools(router.Object);
        await tools.CreateDocument(type: 2);

        router.VerifyAll();
    }

    [Fact]
    public async Task RebuildModel_Default_PassesForceToRouter()
    {
        var router = CreateRouter("Document.RebuildModel", p =>
            (bool)p["Force"]! == true);

        var tools = new DocumentTools(router.Object);
        await tools.RebuildModel();

        router.VerifyAll();
    }

    [Fact]
    public async Task GetDocumentCount_RoutesToCorrectOperation()
    {
        var router = CreateRouterAny("Document.GetDocumentCount");

        var tools = new DocumentTools(router.Object);
        await tools.GetDocumentCount();

        router.VerifyAll();
    }

    [Fact]
    public async Task CloseAllDocuments_PassesIncludeUnsavedToRouter()
    {
        var router = CreateRouter("Document.CloseAllDocuments", p =>
            (bool)p["IncludeUnsaved"]! == true);

        var tools = new DocumentTools(router.Object);
        await tools.CloseAllDocuments(includeUnsaved: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task EditUndo2_PassesCountToRouter()
    {
        var router = CreateRouter("Document.EditUndo", p =>
            (int)p["Count"]! == 3);

        var tools = new DocumentTools(router.Object);
        await tools.EditUndo2(count: 3);

        router.VerifyAll();
    }

    [Fact]
    public async Task EditRedo2_PassesCountToRouter()
    {
        var router = CreateRouter("Document.EditRedo", p =>
            (int)p["Count"]! == 2);

        var tools = new DocumentTools(router.Object);
        await tools.EditRedo2(count: 2);

        router.VerifyAll();
    }

    [Fact]
    public async Task HideDocument_PassesTitleToRouter()
    {
        var router = CreateRouter("Document.HideDocument", p =>
            (string)p["Title"]! == "Part2.sldprt");

        var tools = new DocumentTools(router.Object);
        await tools.HideDocument("Part2.sldprt");

        router.VerifyAll();
    }

    [Fact]
    public async Task SetDocumentPropertyTemplate_PassesTemplatePathAndIsWeldmentToRouter()
    {
        var router = CreateRouter("Document.SetPropertyTemplate", p =>
            (string)p["TemplatePath"]! == "C:/templates/standard.prtprp" &&
            (bool)p["IsWeldment"]! == false);

        var tools = new DocumentTools(router.Object);
        await tools.SetDocumentPropertyTemplate("C:/templates/standard.prtprp");

        router.VerifyAll();
    }

    [Fact]
    public async Task OpenModel_RouterReturnsFailure_ThrowsMcpException()
    {
        var router = new Mock<ISmartRouter>(MockBehavior.Strict);
        router.Setup(r => r.RouteAsync(
                "Document.OpenModel",
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.Failure("File not found"));

        var tools = new DocumentTools(router.Object);
        await Assert.ThrowsAsync<McpException>(() => tools.OpenModel("missing.sldprt"));
    }

    [Fact]
    public async Task OpenModel_AllParams_PassesAllKeysToRouter()
    {
        var router = CreateRouter("Document.OpenModel", p =>
            (string)p["Path"]! == "C:/parts/box.sldprt" &&
            (int)p["Type"]! == 1 &&
            (bool)p["Silent"]! == false &&
            (bool)p["ReadOnly"]! == true &&
            (bool)p["IgnoreHiddenComponents"]! == true &&
            (bool)p["LightWeight"]! == true &&
            (bool)p["Visible"]! == false);

        var tools = new DocumentTools(router.Object);
        await tools.OpenModel("C:/parts/box.sldprt", type: 1, silent: false, readOnly: true,
            ignoreHiddenComponents: true, lightWeight: true, visible: false);

        router.VerifyAll();
    }
}
