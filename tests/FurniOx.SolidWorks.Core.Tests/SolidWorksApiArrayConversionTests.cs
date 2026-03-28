using FurniOx.SolidWorks.Core.Extensions;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class SolidWorksApiArrayConversionTests
{
    [Fact]
    public void ToObjectArraySafe_HandlesNullStandardAndNonZeroBasedArrays()
    {
        object? nullInput = null;
        Assert.Null(nullInput.ToObjectArraySafe());

        object[] standard = ["alpha", 42, true];
        var standardResult = standard.ToObjectArraySafe();
        Assert.Equal(3, standardResult!.Length);
        Assert.Equal("alpha", standardResult[0]);
        Assert.Equal(42, standardResult[1]);
        Assert.Equal(true, standardResult[2]);

        object oneBased = SolidWorksApiExtensionTestSupport.CreateOneBasedArray(typeof(object), "first", "second", "third");
        var oneBasedResult = oneBased.ToObjectArraySafe();
        Assert.Equal(new object[] { "first", "second", "third" }, oneBasedResult!);
    }

    [Fact]
    public void ToObjectArraySafe_RejectsNonArrays_AndPreservesNullElements()
    {
        Assert.Null("not an array".ToObjectArraySafe());
        Assert.Null(99.ToObjectArraySafe());
        Assert.Empty(((object)System.Array.Empty<object>()).ToObjectArraySafe()!);

        object input = new object?[] { null, "value", null };
        var result = input.ToObjectArraySafe();
        Assert.Null(result![0]);
        Assert.Equal("value", result[1]);
        Assert.Null(result[2]);
    }

    [Fact]
    public void ToDoubleArraySafe_HandlesStandardAndSafeArrays()
    {
        object standard = new[] { 1.1, 2.2, 3.3 };
        var standardResult = standard.ToDoubleArraySafe();
        Assert.Equal(new[] { 1.1, 2.2, 3.3 }, standardResult!);

        object oneBased = SolidWorksApiExtensionTestSupport.CreateOneBasedArray(typeof(double), 9.81, 3.14);
        var oneBasedResult = oneBased.ToDoubleArraySafe();
        Assert.Equal(new[] { 9.81, 3.14 }, oneBasedResult!);
    }

    [Fact]
    public void ToDoubleArraySafe_RejectsNonArrays_AndHandlesEdgeCases()
    {
        object? nullInput = null;
        Assert.Null(nullInput.ToDoubleArraySafe());
        Assert.Null("not an array".ToDoubleArraySafe());
        Assert.Empty(((object)System.Array.Empty<double>()).ToDoubleArraySafe()!);
        Assert.Equal(new[] { 42.0 }, ((object)new[] { 42.0 }).ToDoubleArraySafe()!);
        Assert.Equal(new[] { -1.0, 0.0, 1.0 }, ((object)new[] { -1.0, 0.0, 1.0 }).ToDoubleArraySafe()!);
    }

    [Fact]
    public void ToIntArraySafe_HandlesStandardAndSafeArrays()
    {
        object standard = new[] { 10, 20, 30 };
        Assert.Equal(new[] { 10, 20, 30 }, standard.ToIntArraySafe()!);

        object oneBased = SolidWorksApiExtensionTestSupport.CreateOneBasedArray(typeof(int), 7, 14, 21);
        Assert.Equal(new[] { 7, 14, 21 }, oneBased.ToIntArraySafe()!);
    }

    [Fact]
    public void ToIntArraySafe_RejectsNonArrays_AndHandlesEmptyArrays()
    {
        object? nullInput = null;
        Assert.Null(nullInput.ToIntArraySafe());
        Assert.Null("not an array".ToIntArraySafe());
        Assert.Empty(((object)System.Array.Empty<int>()).ToIntArraySafe()!);
    }

    [Fact]
    public void ToStringArraySafe_HandlesNullStandardAndSafeArrays()
    {
        object? nullInput = null;
        Assert.Empty(nullInput.ToStringArraySafe());

        object standard = new[] { "apple", "banana", "cherry" };
        Assert.Equal(new[] { "apple", "banana", "cherry" }, standard.ToStringArraySafe());

        object oneBased = SolidWorksApiExtensionTestSupport.CreateOneBasedArray(typeof(string), "first", "second", "third");
        Assert.Equal(new[] { "first", "second", "third" }, oneBased.ToStringArraySafe());
    }

    [Fact]
    public void ToStringArraySafe_HandlesMixedAndInvalidInputs()
    {
        Assert.Empty(12345.ToStringArraySafe());
        Assert.Empty(((object)System.Array.Empty<string>()).ToStringArraySafe());

        object withNulls = new object?[] { "valid", null, "also valid" };
        Assert.Equal(new[] { "valid", string.Empty, "also valid" }, withNulls.ToStringArraySafe());

        object mixed = new object[] { 42, true, -7 };
        Assert.Equal(new[] { "42", "True", "-7" }, mixed.ToStringArraySafe());
    }
}
