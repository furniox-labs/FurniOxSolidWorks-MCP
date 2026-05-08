using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.MCP.Tools;
using FurniOx.SolidWorks.Shared.Models;
using Moq;

namespace FurniOx.SolidWorks.Tools.Tests;

public sealed class SketchParametricToolsTests
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
    public async Task AddConstraint_PassesTypeAndEntityIdsToRouter()
    {
        var ids = new[] { 1, 2 };
        var router = CreateRouter("Sketch.AddConstraint", p =>
            (string)p["ConstraintType"]! == "coincident" &&
            ReferenceEquals(p["EntityIds"], ids));

        var tools = new SketchParametricTools(router.Object);
        await tools.AddConstraint("coincident", ids);

        router.VerifyAll();
    }

    [Fact]
    public async Task AddDimension_WithoutValue_DoesNotIncludeValueKey()
    {
        var ids = new[] { 3 };
        var router = CreateRouter("Sketch.AddDimension", p =>
            (string)p["DimensionType"]! == "distance" &&
            (double)p["X"]! == 10 &&
            (double)p["Y"]! == 20 &&
            (double)p["Z"]! == 0 &&
            !p.ContainsKey("Value"));

        var tools = new SketchParametricTools(router.Object);
        await tools.AddDimension("distance", ids, 10, 20);

        router.VerifyAll();
    }

    [Fact]
    public async Task AddDimension_WithValue_IncludesValueInRouter()
    {
        var ids = new[] { 5 };
        var router = CreateRouter("Sketch.AddDimension", p =>
            (string)p["DimensionType"]! == "angle" &&
            (double)p["Value"]! == 45.0);

        var tools = new SketchParametricTools(router.Object);
        await tools.AddDimension("angle", ids, 0, 0, value: 45.0);

        router.VerifyAll();
    }
}
