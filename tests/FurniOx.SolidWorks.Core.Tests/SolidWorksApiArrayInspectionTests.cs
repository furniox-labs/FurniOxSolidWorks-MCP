using FurniOx.SolidWorks.Core.Extensions;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class SolidWorksApiArrayInspectionTests
{
    [Fact]
    public void IsSafeArray_RecognizesStandardAndNonZeroBasedArrays()
    {
        object standard = new object[] { 1, 2, 3 };
        object oneBased = SolidWorksApiExtensionTestSupport.CreateOneBasedArray(typeof(object), "a", "b");
        object doubles = new[] { 1.0, 2.0 };
        object empty = System.Array.Empty<string>();

        Assert.True(standard.IsSafeArray());
        Assert.True(oneBased.IsSafeArray());
        Assert.True(doubles.IsSafeArray());
        Assert.True(empty.IsSafeArray());
    }

    [Fact]
    public void IsSafeArray_RejectsNullAndNonArrays()
    {
        object? nullInput = null;
        Assert.False(nullInput.IsSafeArray());
        Assert.False("not an array".IsSafeArray());
        Assert.False(42.IsSafeArray());
    }

    [Fact]
    public void SafeArrayCount_ReturnsExpectedCounts()
    {
        object? nullInput = null;
        object threeItems = new object[] { "a", "b", "c" };
        object empty = System.Array.Empty<object>();
        object oneBased = SolidWorksApiExtensionTestSupport.CreateOneBasedArray(typeof(object), 1, 2, 3, 4, 5);
        object single = new[] { "only" };

        Assert.Equal(0, nullInput.SafeArrayCount());
        Assert.Equal(3, threeItems.SafeArrayCount());
        Assert.Equal(0, empty.SafeArrayCount());
        Assert.Equal(0, "I am not an array".SafeArrayCount());
        Assert.Equal(5, oneBased.SafeArrayCount());
        Assert.Equal(1, single.SafeArrayCount());
    }
}
