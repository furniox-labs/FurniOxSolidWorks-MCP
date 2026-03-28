using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Adapters;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class AdapterRoutingTests
{
    public static IEnumerable<object[]> KnownOperations()
        => SolidWorksOperationCatalog.All.Select(operation => new object[] { operation });

    public static IEnumerable<object[]> UnknownOperations()
    {
        yield return new object[] { "Document." };
        yield return new object[] { "Feature." };
        yield return new object[] { "Sketch." };
        yield return new object[] { "Unknown.Foo" };
        yield return new object[] { string.Empty };
        yield return new object[] { "   " };
        yield return new object[] { "Document" };
        yield return new object[] { "document.CreateDocument" };
        yield return new object[] { " Sketch.CreateSketch" };
    }

    [Theory]
    [MemberData(nameof(KnownOperations))]
    public void CanHandle_ReturnsTrue_ForEveryKnownOperation(string operation)
    {
        using var adapter = CreateAdapter();
        Assert.True(adapter.CanHandle(operation));
    }

    [Theory]
    [MemberData(nameof(UnknownOperations))]
    public void CanHandle_ReturnsFalse_ForUnknownOrIncompleteOperations(string operation)
    {
        using var adapter = CreateAdapter();
        Assert.False(adapter.CanHandle(operation));
    }

    [Fact]
    public void KnownCatalog_ContainsOnlyUniqueOperations()
    {
        Assert.Equal(SolidWorksOperationCatalog.All.Count, SolidWorksOperationCatalog.Known.Count);
    }

    private static SolidWorks2023Adapter CreateAdapter()
    {
        var settings = new SolidWorksSettings();
        var connection = new SolidWorksConnection(NullLogger<SolidWorksConnection>.Instance, settings);

        return new SolidWorks2023Adapter(
            NullLogger<SolidWorks2023Adapter>.Instance,
            connection,
            settings,
            NullLoggerFactory.Instance);
    }
}
