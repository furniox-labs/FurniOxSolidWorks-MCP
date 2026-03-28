using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Operations;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class OperationCatalogOwnershipTests
{
    [Fact]
    public void DocumentCatalog_Subgroups_AreDisjoint_AndCoverAll()
        => AssertDisjointAndCovered(
            DocumentOperationNames.All,
            DocumentOperationNames.File,
            DocumentOperationNames.Session,
            DocumentOperationNames.Query);

    [Fact]
    public void FeatureCatalog_Subgroups_AreDisjoint_AndCoverAll()
        => AssertDisjointAndCovered(
            FeatureOperationNames.All,
            FeatureOperationNames.Extrusion,
            FeatureOperationNames.Revolve,
            FeatureOperationNames.Fillet,
            FeatureOperationNames.Shell);

    [Fact]
    public void SortingCatalog_Subgroups_AreDisjoint_AndCoverAll()
        => AssertDisjointAndCovered(
            SortingOperationNames.All,
            SortingOperationNames.Component,
            SortingOperationNames.FeatureTree,
            SortingOperationNames.Inspection);

    [Fact]
    public void AssemblyBrowserCatalog_ContainsExpectedOperation()
    {
        Assert.Single(AssemblyBrowserOperationNames.All);
        Assert.Equal(AssemblyBrowserOperationNames.ListAssemblyComponents, AssemblyBrowserOperationNames.All[0]);
    }

    [Fact]
    public void GlobalCatalog_ContainsEveryPublicDomainOperationExactlyOnce()
    {
        var allDomainOperations = DocumentOperationNames.All
            .Concat(ExportOperationNames.All)
            .Concat(ConfigurationOperationNames.All)
            .Concat(SelectionOperationNames.All)
            .Concat(AssemblyBrowserOperationNames.All)
            .Concat(SketchOperationNames.All)
            .Concat(FeatureOperationNames.All)
            .Concat(SortingOperationNames.All)
            .ToArray();

        Assert.Equal(allDomainOperations.Length, allDomainOperations.Distinct().Count());
        Assert.Equal(allDomainOperations.OrderBy(name => name), SolidWorksOperationCatalog.All.OrderBy(name => name));
    }

    private static void AssertDisjointAndCovered(IReadOnlyList<string> expectedAll, params IReadOnlyList<string>[] groups)
    {
        var flattened = groups.SelectMany(group => group).ToArray();

        Assert.Equal(flattened.Length, flattened.Distinct().Count());
        Assert.Equal(expectedAll.OrderBy(name => name), flattened.OrderBy(name => name));
    }
}
