using System;
using System.Linq;
using FurniOx.SolidWorks.Core.Extensions;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Unit tests for SolidWorksApiExtensions.
///
/// Key scenario under test: .NET 10 marshals COM SafeArrays as System.Object[*]
/// (non-zero-based arrays) instead of object[] (zero-based vectors). These extension
/// methods must handle both standard zero-based .NET arrays and non-zero-based
/// SafeArrays without throwing InvalidCastException.
///
/// Non-zero-based arrays are simulated with Array.CreateInstance using explicit
/// lower bounds, which is exactly what COM interop produces at runtime.
/// </summary>
public class SolidWorksApiExtensionsTests
{
    // =========================================================================
    // ToObjectArraySafe
    // =========================================================================

    [Fact]
    public void ToObjectArraySafe_NullInput_ReturnsNull()
    {
        object? input = null;

        object[]? result = input.ToObjectArraySafe();

        Assert.Null(result);
    }

    [Fact]
    public void ToObjectArraySafe_StandardObjectArray_ReturnsSameElements()
    {
        object[] input = ["alpha", 42, true];

        object[]? result = input.ToObjectArraySafe();

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal("alpha", result[0]);
        Assert.Equal(42, result[1]);
        Assert.Equal(true, result[2]);
    }

    [Fact]
    public void ToObjectArraySafe_EmptyArray_ReturnsEmptyArray()
    {
        object[] input = [];

        object[]? result = input.ToObjectArraySafe();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToObjectArraySafe_NonZeroBasedArray_ReturnsCorrectElements()
    {
        // Simulate COM SafeArray: 3 elements with lower bound 1 (one-based index)
        Array oneBased = Array.CreateInstance(typeof(object), lengths: [3], lowerBounds: [1]);
        oneBased.SetValue("first", 1);
        oneBased.SetValue("second", 2);
        oneBased.SetValue("third", 3);

        object? comResult = oneBased;
        object[]? result = comResult.ToObjectArraySafe();

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal("first", result[0]);
        Assert.Equal("second", result[1]);
        Assert.Equal("third", result[2]);
    }

    [Fact]
    public void ToObjectArraySafe_NonArrayInput_ReturnsNull()
    {
        object input = "not an array";

        object[]? result = input.ToObjectArraySafe();

        Assert.Null(result);
    }

    [Fact]
    public void ToObjectArraySafe_IntegerInput_ReturnsNull()
    {
        object input = 99;

        object[]? result = input.ToObjectArraySafe();

        Assert.Null(result);
    }

    [Fact]
    public void ToObjectArraySafe_SingleElementArray_ReturnsSingleElement()
    {
        object[] input = ["only"];

        object[]? result = input.ToObjectArraySafe();

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("only", result[0]);
    }

    [Fact]
    public void ToObjectArraySafe_ArrayWithNullElements_PreservesNulls()
    {
        object?[] input = [null, "value", null];

        object[]? result = ((object)input).ToObjectArraySafe();

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Null(result[0]);
        Assert.Equal("value", result[1]);
        Assert.Null(result[2]);
    }

    // =========================================================================
    // ToDoubleArraySafe
    // =========================================================================

    [Fact]
    public void ToDoubleArraySafe_NullInput_ReturnsNull()
    {
        object? input = null;

        double[]? result = input.ToDoubleArraySafe();

        Assert.Null(result);
    }

    [Fact]
    public void ToDoubleArraySafe_StandardDoubleArray_PreservesValues()
    {
        double[] input = [1.1, 2.2, 3.3];

        double[]? result = ((object)input).ToDoubleArraySafe();

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal(1.1, result[0], precision: 10);
        Assert.Equal(2.2, result[1], precision: 10);
        Assert.Equal(3.3, result[2], precision: 10);
    }

    [Fact]
    public void ToDoubleArraySafe_NonZeroBasedDoubleArray_ReturnsCorrectValues()
    {
        // Simulate COM SafeArray of doubles with lower bound 1
        Array oneBased = Array.CreateInstance(typeof(double), lengths: [2], lowerBounds: [1]);
        oneBased.SetValue(9.81, 1);
        oneBased.SetValue(3.14, 2);

        object? comResult = oneBased;
        double[]? result = comResult.ToDoubleArraySafe();

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal(9.81, result[0], precision: 10);
        Assert.Equal(3.14, result[1], precision: 10);
    }

    [Fact]
    public void ToDoubleArraySafe_NonArrayInput_ReturnsNull()
    {
        object input = "not an array";

        double[]? result = input.ToDoubleArraySafe();

        Assert.Null(result);
    }

    [Fact]
    public void ToDoubleArraySafe_EmptyArray_ReturnsEmptyArray()
    {
        double[] input = [];

        double[]? result = ((object)input).ToDoubleArraySafe();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToDoubleArraySafe_SingleValue_ReturnsSingleElement()
    {
        double[] input = [42.0];

        double[]? result = ((object)input).ToDoubleArraySafe();

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(42.0, result[0]);
    }

    [Fact]
    public void ToDoubleArraySafe_NegativeValues_PreservesSign()
    {
        double[] input = [-1.0, 0.0, 1.0];

        double[]? result = ((object)input).ToDoubleArraySafe();

        Assert.NotNull(result);
        Assert.Equal(-1.0, result[0]);
        Assert.Equal(0.0, result[1]);
        Assert.Equal(1.0, result[2]);
    }

    // =========================================================================
    // ToIntArraySafe
    // =========================================================================

    [Fact]
    public void ToIntArraySafe_NullInput_ReturnsNull()
    {
        object? input = null;

        int[]? result = input.ToIntArraySafe();

        Assert.Null(result);
    }

    [Fact]
    public void ToIntArraySafe_StandardIntArray_ReturnsCorrectValues()
    {
        int[] input = [10, 20, 30];

        int[]? result = ((object)input).ToIntArraySafe();

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal(10, result[0]);
        Assert.Equal(20, result[1]);
        Assert.Equal(30, result[2]);
    }

    [Fact]
    public void ToIntArraySafe_NonZeroBasedIntArray_ReturnsCorrectValues()
    {
        // Simulate COM SafeArray of ints with lower bound 1
        Array oneBased = Array.CreateInstance(typeof(int), lengths: [3], lowerBounds: [1]);
        oneBased.SetValue(7, 1);
        oneBased.SetValue(14, 2);
        oneBased.SetValue(21, 3);

        object? comResult = oneBased;
        int[]? result = comResult.ToIntArraySafe();

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal(7, result[0]);
        Assert.Equal(14, result[1]);
        Assert.Equal(21, result[2]);
    }

    [Fact]
    public void ToIntArraySafe_NonArrayInput_ReturnsNull()
    {
        object input = "not an array";

        int[]? result = input.ToIntArraySafe();

        Assert.Null(result);
    }

    [Fact]
    public void ToIntArraySafe_EmptyArray_ReturnsEmptyArray()
    {
        int[] input = [];

        int[]? result = ((object)input).ToIntArraySafe();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // =========================================================================
    // ToStringArraySafe
    // =========================================================================

    [Fact]
    public void ToStringArraySafe_NullInput_ReturnsEmptyArray()
    {
        object? input = null;

        string[] result = input.ToStringArraySafe();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToStringArraySafe_StandardStringArray_ReturnsCorrectValues()
    {
        string[] input = ["apple", "banana", "cherry"];

        string[] result = ((object)input).ToStringArraySafe();

        Assert.Equal(3, result.Length);
        Assert.Equal("apple", result[0]);
        Assert.Equal("banana", result[1]);
        Assert.Equal("cherry", result[2]);
    }

    [Fact]
    public void ToStringArraySafe_NonZeroBasedStringArray_ReturnsCorrectValues()
    {
        // Simulate COM SafeArray of strings using lower bound of 1
        Array oneBased = Array.CreateInstance(typeof(string), lengths: [3], lowerBounds: [1]);
        oneBased.SetValue("first", 1);
        oneBased.SetValue("second", 2);
        oneBased.SetValue("third", 3);

        object? comResult = oneBased;
        string[] result = comResult.ToStringArraySafe();

        Assert.Equal(3, result.Length);
        Assert.Equal("first", result[0]);
        Assert.Equal("second", result[1]);
        Assert.Equal("third", result[2]);
    }

    [Fact]
    public void ToStringArraySafe_NonArrayInput_ReturnsEmptyArray()
    {
        object input = 12345;

        string[] result = input.ToStringArraySafe();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToStringArraySafe_EmptyArray_ReturnsEmptyArray()
    {
        string[] input = [];

        string[] result = ((object)input).ToStringArraySafe();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToStringArraySafe_ArrayWithNullElements_ConvertsNullsToEmptyString()
    {
        // object[] allows nulls at positions that would be null strings from COM
        object?[] input = ["valid", null, "also valid"];

        string[] result = ((object)input).ToStringArraySafe();

        Assert.Equal(3, result.Length);
        Assert.Equal("valid", result[0]);
        Assert.Equal(string.Empty, result[1]);
        Assert.Equal("also valid", result[2]);
    }

    [Fact]
    public void ToStringArraySafe_NonStringElements_ConvertsViaToString()
    {
        // COM can return mixed-type arrays; ToString() is used for conversion.
        // Integers and booleans produce locale-invariant strings; we avoid floating-
        // point values here because their ToString() output is locale-dependent
        // (e.g. "3.14" vs "3,14" depending on the system decimal separator).
        object[] input = [42, true, -7];

        string[] result = ((object)input).ToStringArraySafe();

        Assert.Equal(3, result.Length);
        Assert.Equal("42", result[0]);
        Assert.Equal("True", result[1]);
        Assert.Equal("-7", result[2]);
    }

    // =========================================================================
    // IsSafeArray
    // =========================================================================

    [Fact]
    public void IsSafeArray_NullInput_ReturnsFalse()
    {
        object? input = null;

        bool result = input.IsSafeArray();

        Assert.False(result);
    }

    [Fact]
    public void IsSafeArray_StandardArray_ReturnsTrue()
    {
        object input = new object[] { 1, 2, 3 };

        bool result = input.IsSafeArray();

        Assert.True(result);
    }

    [Fact]
    public void IsSafeArray_NonZeroBasedArray_ReturnsTrue()
    {
        Array oneBased = Array.CreateInstance(typeof(object), lengths: [2], lowerBounds: [1]);

        bool result = ((object)oneBased).IsSafeArray();

        Assert.True(result);
    }

    [Fact]
    public void IsSafeArray_StringInput_ReturnsFalse()
    {
        object input = "not an array";

        bool result = input.IsSafeArray();

        Assert.False(result);
    }

    [Fact]
    public void IsSafeArray_IntInput_ReturnsFalse()
    {
        object input = 42;

        bool result = input.IsSafeArray();

        Assert.False(result);
    }

    [Fact]
    public void IsSafeArray_EmptyArray_ReturnsTrue()
    {
        object input = Array.Empty<string>();

        bool result = input.IsSafeArray();

        Assert.True(result);
    }

    [Fact]
    public void IsSafeArray_DoubleArray_ReturnsTrue()
    {
        object input = new double[] { 1.0, 2.0 };

        bool result = input.IsSafeArray();

        Assert.True(result);
    }

    // =========================================================================
    // SafeArrayCount
    // =========================================================================

    [Fact]
    public void SafeArrayCount_NullInput_ReturnsZero()
    {
        object? input = null;

        int count = input.SafeArrayCount();

        Assert.Equal(0, count);
    }

    [Fact]
    public void SafeArrayCount_ThreeElementArray_ReturnsThree()
    {
        object input = new object[] { "a", "b", "c" };

        int count = input.SafeArrayCount();

        Assert.Equal(3, count);
    }

    [Fact]
    public void SafeArrayCount_EmptyArray_ReturnsZero()
    {
        object input = Array.Empty<object>();

        int count = input.SafeArrayCount();

        Assert.Equal(0, count);
    }

    [Fact]
    public void SafeArrayCount_NonArrayInput_ReturnsZero()
    {
        object input = "I am not an array";

        int count = input.SafeArrayCount();

        Assert.Equal(0, count);
    }

    [Fact]
    public void SafeArrayCount_NonZeroBasedArray_ReturnsCorrectLength()
    {
        // 5-element one-based array — Length still equals 5
        Array oneBased = Array.CreateInstance(typeof(object), lengths: [5], lowerBounds: [1]);

        int count = ((object)oneBased).SafeArrayCount();

        Assert.Equal(5, count);
    }

    [Fact]
    public void SafeArrayCount_SingleElementArray_ReturnsOne()
    {
        object input = new string[] { "only" };

        int count = input.SafeArrayCount();

        Assert.Equal(1, count);
    }
}
