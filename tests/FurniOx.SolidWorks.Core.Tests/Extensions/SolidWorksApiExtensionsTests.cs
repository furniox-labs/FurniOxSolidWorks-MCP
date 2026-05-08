using FurniOx.SolidWorks.Core.Extensions;

namespace FurniOx.SolidWorks.Core.Tests.Extensions;

/// <summary>
/// Named-convention tests for <see cref="SolidWorksApiExtensions"/> helpers.
///
/// These complement the broader coverage in
/// <c>SolidWorksApiArrayConversionTests</c> and
/// <c>SolidWorksApiArrayInspectionTests</c> with explicit
/// <c>{Method}_{Input}_{Result}</c> naming and focused single-assertion facts.
/// The critical .NET 10 case is the non-zero-based (1-based) <c>Array</c>
/// constructed via <c>Array.CreateInstance</c> with <c>lowerBounds: [1]</c>.
/// </summary>
public sealed class SolidWorksApiExtensionsTests
{
    // -------------------------------------------------------------------------
    // ToObjectArraySafe
    // -------------------------------------------------------------------------

    [Fact]
    public void ToObjectArraySafe_NullInput_ReturnsNull()
    {
        object? input = null;
        Assert.Null(input.ToObjectArraySafe());
    }

    [Fact]
    public void ToObjectArraySafe_ZeroBasedObjectArray_RoundTripsValues()
    {
        object input = new object[] { "x", 1, true };
        var result = input.ToObjectArraySafe();
        Assert.Equal(new object[] { "x", 1, true }, result);
    }

    [Fact]
    public void ToObjectArraySafe_OneBasedArray_HandlesNetTenSafeArray()
    {
        // Simulates a COM SafeArray marshaled as System.Object[*] in .NET 10
        Array oneBased = Array.CreateInstance(typeof(object), lengths: [3], lowerBounds: [1]);
        oneBased.SetValue("a", 1);
        oneBased.SetValue("b", 2);
        oneBased.SetValue("c", 3);

        var result = ((object)oneBased).ToObjectArraySafe();

        Assert.Equal(new object[] { "a", "b", "c" }, result);
    }

    [Fact]
    public void ToObjectArraySafe_EmptyArray_ReturnsEmptyArray()
    {
        object input = Array.Empty<object>();
        var result = input.ToObjectArraySafe();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToObjectArraySafe_NonArrayInput_ReturnsNull()
    {
        Assert.Null("not an array".ToObjectArraySafe());
        Assert.Null(42.ToObjectArraySafe());
    }

    // -------------------------------------------------------------------------
    // ToDoubleArraySafe
    // -------------------------------------------------------------------------

    [Fact]
    public void ToDoubleArraySafe_NullInput_ReturnsNull()
    {
        object? input = null;
        Assert.Null(input.ToDoubleArraySafe());
    }

    [Fact]
    public void ToDoubleArraySafe_OneBasedDoubleArray_HandlesNetTenSafeArray()
    {
        Array oneBased = Array.CreateInstance(typeof(double), lengths: [2], lowerBounds: [1]);
        oneBased.SetValue(1.5, 1);
        oneBased.SetValue(2.5, 2);

        var result = ((object)oneBased).ToDoubleArraySafe();

        Assert.Equal(new[] { 1.5, 2.5 }, result);
    }

    [Fact]
    public void ToDoubleArraySafe_EmptyArray_ReturnsEmptyArray()
    {
        object input = Array.Empty<double>();
        Assert.Empty(input.ToDoubleArraySafe()!);
    }

    // -------------------------------------------------------------------------
    // ToIntArraySafe
    // -------------------------------------------------------------------------

    [Fact]
    public void ToIntArraySafe_NullInput_ReturnsNull()
    {
        object? input = null;
        Assert.Null(input.ToIntArraySafe());
    }

    [Fact]
    public void ToIntArraySafe_OneBasedIntArray_HandlesNetTenSafeArray()
    {
        Array oneBased = Array.CreateInstance(typeof(int), lengths: [3], lowerBounds: [1]);
        oneBased.SetValue(10, 1);
        oneBased.SetValue(20, 2);
        oneBased.SetValue(30, 3);

        var result = ((object)oneBased).ToIntArraySafe();

        Assert.Equal(new[] { 10, 20, 30 }, result);
    }

    // -------------------------------------------------------------------------
    // ToStringArraySafe
    // -------------------------------------------------------------------------

    [Fact]
    public void ToStringArraySafe_NullInput_ReturnsEmptyArray()
    {
        object? input = null;
        Assert.Empty(input.ToStringArraySafe());
    }

    [Fact]
    public void ToStringArraySafe_OneBasedStringArray_HandlesNetTenSafeArray()
    {
        Array oneBased = Array.CreateInstance(typeof(string), lengths: [2], lowerBounds: [1]);
        oneBased.SetValue("hello", 1);
        oneBased.SetValue("world", 2);

        var result = ((object)oneBased).ToStringArraySafe();

        Assert.Equal(new[] { "hello", "world" }, result);
    }

    [Fact]
    public void ToStringArraySafe_NullElements_ReplacedWithEmptyString()
    {
        object input = new object?[] { "valid", null };
        var result = input.ToStringArraySafe();
        Assert.Equal("valid", result[0]);
        Assert.Equal(string.Empty, result[1]);
    }

    [Fact]
    public void ToStringArraySafe_NonArrayInput_ReturnsEmptyArray()
    {
        Assert.Empty(99.ToStringArraySafe());
    }

    // -------------------------------------------------------------------------
    // IsSafeArray
    // -------------------------------------------------------------------------

    [Fact]
    public void IsSafeArray_NullInput_ReturnsFalse()
    {
        object? input = null;
        Assert.False(input.IsSafeArray());
    }

    [Fact]
    public void IsSafeArray_OneBasedArray_ReturnsTrue()
    {
        Array oneBased = Array.CreateInstance(typeof(object), lengths: [2], lowerBounds: [1]);
        Assert.True(((object)oneBased).IsSafeArray());
    }

    [Fact]
    public void IsSafeArray_ZeroBasedArray_ReturnsTrue()
    {
        object input = new object[] { 1, 2 };
        Assert.True(input.IsSafeArray());
    }

    [Fact]
    public void IsSafeArray_NonArrayInput_ReturnsFalse()
    {
        Assert.False("string".IsSafeArray());
        Assert.False(0.IsSafeArray());
    }

    // -------------------------------------------------------------------------
    // SafeArrayCount
    // -------------------------------------------------------------------------

    [Fact]
    public void SafeArrayCount_NullInput_ReturnsZero()
    {
        object? input = null;
        Assert.Equal(0, input.SafeArrayCount());
    }

    [Fact]
    public void SafeArrayCount_NonArrayInput_ReturnsZero()
    {
        Assert.Equal(0, "not an array".SafeArrayCount());
    }

    [Fact]
    public void SafeArrayCount_EmptyArray_ReturnsZero()
    {
        object input = Array.Empty<object>();
        Assert.Equal(0, input.SafeArrayCount());
    }

    [Fact]
    public void SafeArrayCount_ZeroBasedArray_ReturnsLength()
    {
        object input = new object[] { "a", "b", "c" };
        Assert.Equal(3, input.SafeArrayCount());
    }

    [Fact]
    public void SafeArrayCount_OneBasedArray_ReturnsLength()
    {
        // Non-zero-based arrays still report correct Length
        Array oneBased = Array.CreateInstance(typeof(object), lengths: [4], lowerBounds: [1]);
        Assert.Equal(4, ((object)oneBased).SafeArrayCount());
    }
}
