using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;
using Xunit;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class DocumentGovernanceToolsTests
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
    public async Task RenameComponentAnywhere_PassesInstancePathAndNewNameToRouter()
    {
        var router = CreateRouter("Document.RenameComponentAnywhere", p =>
            (string)p["InstancePath"]! == "Asm-1/Part-2" &&
            (string)p["NewName"]! == "Shelf.SLDPRT" &&
            (bool)p["AutoSave"]! == true &&
            (bool)p["DryRun"]! == false);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.RenameComponentAnywhere("Asm-1/Part-2", "Shelf.SLDPRT");

        router.VerifyAll();
    }

    [Fact]
    public async Task GetRenamedDocuments_RoutesToRouter()
    {
        var router = CreateRouter("Document.GetRenamedDocuments", p => p.Count == 0);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.GetRenamedDocuments();

        router.VerifyAll();
    }

    [Fact]
    public async Task DetectOrphanFiles_WithFolder_PassesFolderPathAndDefaultsToRouter()
    {
        var router = CreateRouter("Document.DetectOrphanFiles", p =>
            (string)p["FolderPath"]! == "C:/cad" &&
            (bool)p["Recursive"]! == false &&
            (bool)p["IncludeDrawings"]! == true);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.DetectOrphanFiles("C:/cad");

        router.VerifyAll();
    }

    [Fact]
    public async Task GetComponentSuppression_PassesComponentNameToRouter()
    {
        var router = CreateRouter("Document.GetComponentSuppression", p =>
            (string)p["ComponentName"]! == "Part1-1");

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.GetComponentSuppression("Part1-1");

        router.VerifyAll();
    }

    [Fact]
    public async Task SetComponentSuppression_PassesComponentNameAndStateToRouter()
    {
        var router = CreateRouter("Document.SetComponentSuppression", p =>
            (string)p["ComponentName"]! == "Part1-1" &&
            (string)p["State"]! == "Resolved");

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.SetComponentSuppression("Part1-1", "Resolved");

        router.VerifyAll();
    }

    [Fact]
    public async Task GetReferenceSearchPath_RoutesToRouter()
    {
        var router = CreateRouter("Document.GetReferenceSearchPath", p => p.Count == 0);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.GetReferenceSearchPath();

        router.VerifyAll();
    }

    [Fact]
    public async Task SetReferenceSearchPath_PassesPathsAndAppendToRouter()
    {
        var router = CreateRouter("Document.SetReferenceSearchPath", p =>
            p["Paths"] is string[] paths &&
            paths.Length == 2 &&
            paths[0] == "C:/refs" &&
            paths[1] == "D:/legacy" &&
            (bool)p["Append"]! == true);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.SetReferenceSearchPath(new[] { "C:/refs", "D:/legacy" }, append: true);

        router.VerifyAll();
    }

    [Fact]
    public async Task ReplaceReferencedDocument_PassesAllThreePathsToRouter()
    {
        var router = CreateRouter("Document.ReplaceReferencedDocument", p =>
            (string)p["ReferencingDocPath"]! == "C:/asm.SLDASM" &&
            (string)p["OldRefPath"]! == "C:/old/part.SLDPRT" &&
            (string)p["NewRefPath"]! == "C:/new/part.SLDPRT");

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.ReplaceReferencedDocument("C:/asm.SLDASM", "C:/old/part.SLDPRT", "C:/new/part.SLDPRT");

        router.VerifyAll();
    }

    [Fact]
    public async Task RenameDocument_PassesNewNameToRouter()
    {
        var router = CreateRouter("Document.RenameDocument", p =>
            (string)p["NewName"]! == "Renamed.SLDPRT" &&
            (bool)p["AutoSave"]! == true);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.RenameDocument("Renamed.SLDPRT");

        router.VerifyAll();
    }

    [Fact]
    public async Task RenameComponentInstance_PassesNamesToRouter()
    {
        var router = CreateRouter("Document.RenameComponentInstance", p =>
            (string)p["ComponentName"]! == "Part1-1" &&
            (string)p["NewInstanceName"]! == "Part1-Renamed" &&
            (bool)p["AutoSave"]! == true);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.RenameComponentInstance("Part1-1", "Part1-Renamed");

        router.VerifyAll();
    }

    [Fact]
    public async Task RenameComponentFile_PassesNamesToRouter()
    {
        var router = CreateRouter("Document.RenameComponentFile", p =>
            (string)p["ComponentName"]! == "Part1-1" &&
            (string)p["NewName"]! == "RenamedPart.SLDPRT" &&
            (bool)p["AutoSave"]! == true);

        var tools = new DocumentGovernanceTools(router.Object);
        await tools.RenameComponentFile("Part1-1", "RenamedPart.SLDPRT");

        router.VerifyAll();
    }
}
