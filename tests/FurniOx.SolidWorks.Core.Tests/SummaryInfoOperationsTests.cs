using FurniOx.SolidWorks.Core.Adapters;
using SolidWorks.Interop.swconst;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class SummaryInfoOperationsTests
{
    [Fact]
    public void TryResolveField_RecognizesWritableField()
    {
        var resolved = SummaryInfoOperations.TryResolveField("author", out var field);

        Assert.True(resolved);
        Assert.Equal(swSummInfoField_e.swSumInfoAuthor, field);
    }

    [Fact]
    public void TryResolveField_RecognizesReadOnlyField()
    {
        var resolved = SummaryInfoOperations.TryResolveField("savedby", out var field);

        Assert.True(resolved);
        Assert.Equal(swSummInfoField_e.swSumInfoSavedBy, field);
    }

    [Fact]
    public void TryResolveField_RejectsUnknownField()
    {
        var resolved = SummaryInfoOperations.TryResolveField("revision", out _);

        Assert.False(resolved);
    }
}
