using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Comprehensive unit tests for OperationHandlerBase helper methods.
/// Because all target members are protected static, a minimal concrete subclass
/// exposes them via thin public wrappers without any COM interaction.
/// </summary>
public sealed class OperationHandlerBaseTests
{
    // ---------------------------------------------------------------------------
    // Testable subclass — exposes every protected static helper as public static.
    // The constructor satisfies the base-class guard clauses with real (but
    // un-connected) objects so that no COM call is ever made.
    // ---------------------------------------------------------------------------

    private sealed class TestableHandler : OperationHandlerBase
    {
        public TestableHandler()
            : base(
                new SolidWorksConnection(
                    NullLogger<SolidWorksConnection>.Instance,
                    new SolidWorksSettings()),
                new SolidWorksSettings(),
                NullLogger.Instance)
        { }

        public override Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken)
            => throw new NotImplementedException();

        // --- parameter helpers ---
        public static new double GetDoubleParam(IDictionary<string, object?> p, string k, double d = 0.0)
            => OperationHandlerBase.GetDoubleParam(p, k, d);

        public static new int GetIntParam(IDictionary<string, object?> p, string k, int d = 0)
            => OperationHandlerBase.GetIntParam(p, k, d);

        public static new bool GetBoolParam(IDictionary<string, object?> p, string k, bool d = false)
            => OperationHandlerBase.GetBoolParam(p, k, d);

        public static new string GetStringParam(IDictionary<string, object?> p, string k, string d = "")
            => OperationHandlerBase.GetStringParam(p, k, d);

        public static new string? GetStringParamNullable(IDictionary<string, object?> p, string k)
            => OperationHandlerBase.GetStringParamNullable(p, k);

        // --- unit-conversion helpers ---
        public static new double MmToMeters(double mm)
            => OperationHandlerBase.MmToMeters(mm);

        public static new double MetersToMm(double m)
            => OperationHandlerBase.MetersToMm(m);

        public static new double DegreesToRadians(double d)
            => OperationHandlerBase.DegreesToRadians(d);

        public static new double RadiansToDegrees(double r)
            => OperationHandlerBase.RadiansToDegrees(r);

        // --- path-safety helper ---
        public static new bool IsPathSafe(string p, out string? e)
            => OperationHandlerBase.IsPathSafe(p, out e);
    }

    // ---------------------------------------------------------------------------
    // Helpers for building JsonElement test values
    // ---------------------------------------------------------------------------

    private static JsonElement JsonNumber(string literal)
        => JsonDocument.Parse(literal).RootElement;

    private static JsonElement JsonBool(bool value)
        => JsonDocument.Parse(value ? "true" : "false").RootElement;

    private static JsonElement JsonString(string value)
        => JsonDocument.Parse($"\"{value}\"").RootElement;

    // ---------------------------------------------------------------------------
    // GetDoubleParam — 10 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetDoubleParam_MissingKey_ReturnsDefault()
    {
        var p = new Dictionary<string, object?>();
        Assert.Equal(3.14, TestableHandler.GetDoubleParam(p, "missing", 3.14));
    }

    [Fact]
    public void GetDoubleParam_NullValue_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = null };
        Assert.Equal(9.9, TestableHandler.GetDoubleParam(p, "k", 9.9));
    }

    [Fact]
    public void GetDoubleParam_DoubleValue_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = 2.718 };
        Assert.Equal(2.718, TestableHandler.GetDoubleParam(p, "k"));
    }

    [Fact]
    public void GetDoubleParam_IntValue_ReturnsValueAsDouble()
    {
        var p = new Dictionary<string, object?> { ["k"] = 42 };
        Assert.Equal(42.0, TestableHandler.GetDoubleParam(p, "k"));
    }

    [Fact]
    public void GetDoubleParam_LongValue_ReturnsValueAsDouble()
    {
        var p = new Dictionary<string, object?> { ["k"] = 1_000_000L };
        Assert.Equal(1_000_000.0, TestableHandler.GetDoubleParam(p, "k"));
    }

    [Fact]
    public void GetDoubleParam_JsonElementNumber_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonNumber("1.5") };
        Assert.Equal(1.5, TestableHandler.GetDoubleParam(p, "k"));
    }

    [Fact]
    public void GetDoubleParam_JsonElementInteger_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonNumber("7") };
        Assert.Equal(7.0, TestableHandler.GetDoubleParam(p, "k"));
    }

    [Fact]
    public void GetDoubleParam_NegativeDouble_ReturnsNegativeValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = -99.5 };
        Assert.Equal(-99.5, TestableHandler.GetDoubleParam(p, "k"));
    }

    [Fact]
    public void GetDoubleParam_NonNumericString_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = "not-a-number" };
        Assert.Equal(0.0, TestableHandler.GetDoubleParam(p, "k"));
    }

    [Fact]
    public void GetDoubleParam_ZeroDouble_ReturnsZero()
    {
        var p = new Dictionary<string, object?> { ["k"] = 0.0 };
        Assert.Equal(0.0, TestableHandler.GetDoubleParam(p, "k", 99.0));
    }

    // ---------------------------------------------------------------------------
    // GetIntParam — 9 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetIntParam_MissingKey_ReturnsDefault()
    {
        var p = new Dictionary<string, object?>();
        Assert.Equal(5, TestableHandler.GetIntParam(p, "missing", 5));
    }

    [Fact]
    public void GetIntParam_NullValue_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = null };
        Assert.Equal(7, TestableHandler.GetIntParam(p, "k", 7));
    }

    [Fact]
    public void GetIntParam_IntValue_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = 100 };
        Assert.Equal(100, TestableHandler.GetIntParam(p, "k"));
    }

    [Fact]
    public void GetIntParam_LongValue_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = 255L };
        Assert.Equal(255, TestableHandler.GetIntParam(p, "k"));
    }

    [Fact]
    public void GetIntParam_JsonElementInteger_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonNumber("42") };
        Assert.Equal(42, TestableHandler.GetIntParam(p, "k"));
    }

    /// <summary>
    /// CRITICAL: MCP sends integer parameters as JSON floats (e.g. 1.0, 2.0).
    /// TryGetInt32 fails on a float JsonElement, so the implementation falls
    /// through to TryGetDouble and casts to int.  This test documents and
    /// guards that bug-fix path.
    /// </summary>
    [Fact]
    public void GetIntParam_JsonElementFloat_TruncatesToInt()
    {
        // 1.0 is a valid JSON Number but TryGetInt32 will fail for it on some
        // runtimes; TryGetDouble succeeds and the cast gives 1.
        var p = new Dictionary<string, object?> { ["k"] = JsonNumber("1.0") };
        Assert.Equal(1, TestableHandler.GetIntParam(p, "k"));
    }

    [Fact]
    public void GetIntParam_JsonElementLargeFloat_TruncatesDecimalPart()
    {
        // 7.9 → (int)7.9 == 7  (truncation, not rounding)
        var p = new Dictionary<string, object?> { ["k"] = JsonNumber("7.9") };
        Assert.Equal(7, TestableHandler.GetIntParam(p, "k"));
    }

    [Fact]
    public void GetIntParam_NonNumericString_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = "abc" };
        Assert.Equal(0, TestableHandler.GetIntParam(p, "k"));
    }

    [Fact]
    public void GetIntParam_NegativeInt_ReturnsNegativeValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = -3 };
        Assert.Equal(-3, TestableHandler.GetIntParam(p, "k"));
    }

    // ---------------------------------------------------------------------------
    // GetBoolParam — 8 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetBoolParam_MissingKey_ReturnsDefault()
    {
        var p = new Dictionary<string, object?>();
        Assert.True(TestableHandler.GetBoolParam(p, "missing", true));
    }

    [Fact]
    public void GetBoolParam_NullValue_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = null };
        Assert.False(TestableHandler.GetBoolParam(p, "k", false));
    }

    [Fact]
    public void GetBoolParam_BoolTrue_ReturnsTrue()
    {
        var p = new Dictionary<string, object?> { ["k"] = true };
        Assert.True(TestableHandler.GetBoolParam(p, "k"));
    }

    [Fact]
    public void GetBoolParam_BoolFalse_ReturnsFalse()
    {
        var p = new Dictionary<string, object?> { ["k"] = false };
        Assert.False(TestableHandler.GetBoolParam(p, "k", true));
    }

    [Fact]
    public void GetBoolParam_JsonElementTrue_ReturnsTrue()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonBool(true) };
        Assert.True(TestableHandler.GetBoolParam(p, "k"));
    }

    [Fact]
    public void GetBoolParam_JsonElementFalse_ReturnsFalse()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonBool(false) };
        Assert.False(TestableHandler.GetBoolParam(p, "k", true));
    }

    [Fact]
    public void GetBoolParam_NonBoolString_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = "yes" };
        Assert.False(TestableHandler.GetBoolParam(p, "k"));
    }

    [Fact]
    public void GetBoolParam_NumericValue_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = 1 };
        Assert.False(TestableHandler.GetBoolParam(p, "k"));
    }

    // ---------------------------------------------------------------------------
    // GetStringParam — 7 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetStringParam_MissingKey_ReturnsDefault()
    {
        var p = new Dictionary<string, object?>();
        Assert.Equal("fallback", TestableHandler.GetStringParam(p, "missing", "fallback"));
    }

    [Fact]
    public void GetStringParam_NullValue_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = null };
        Assert.Equal("default", TestableHandler.GetStringParam(p, "k", "default"));
    }

    [Fact]
    public void GetStringParam_StringValue_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = "hello" };
        Assert.Equal("hello", TestableHandler.GetStringParam(p, "k"));
    }

    [Fact]
    public void GetStringParam_JsonElementString_ReturnsValue()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonString("world") };
        Assert.Equal("world", TestableHandler.GetStringParam(p, "k"));
    }

    [Fact]
    public void GetStringParam_EmptyString_ReturnsEmptyString()
    {
        // An explicit empty string is a valid string value and should be returned.
        var p = new Dictionary<string, object?> { ["k"] = "" };
        Assert.Equal("", TestableHandler.GetStringParam(p, "k", "default"));
    }

    [Fact]
    public void GetStringParam_NonStringType_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = 42 };
        Assert.Equal("default", TestableHandler.GetStringParam(p, "k", "default"));
    }

    [Fact]
    public void GetStringParam_JsonElementNumber_ReturnsDefault()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonNumber("123") };
        Assert.Equal("fallback", TestableHandler.GetStringParam(p, "k", "fallback"));
    }

    // ---------------------------------------------------------------------------
    // GetStringParamNullable — 6 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetStringParamNullable_MissingKey_ReturnsNull()
    {
        var p = new Dictionary<string, object?>();
        Assert.Null(TestableHandler.GetStringParamNullable(p, "missing"));
    }

    [Fact]
    public void GetStringParamNullable_NullValue_ReturnsNull()
    {
        var p = new Dictionary<string, object?> { ["k"] = null };
        Assert.Null(TestableHandler.GetStringParamNullable(p, "k"));
    }

    [Fact]
    public void GetStringParamNullable_NonEmptyString_ReturnsString()
    {
        var p = new Dictionary<string, object?> { ["k"] = "SolidWorks" };
        Assert.Equal("SolidWorks", TestableHandler.GetStringParamNullable(p, "k"));
    }

    [Fact]
    public void GetStringParamNullable_EmptyString_ReturnsNull()
    {
        var p = new Dictionary<string, object?> { ["k"] = "" };
        Assert.Null(TestableHandler.GetStringParamNullable(p, "k"));
    }

    [Fact]
    public void GetStringParamNullable_JsonElementEmptyString_ReturnsNull()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonString("") };
        Assert.Null(TestableHandler.GetStringParamNullable(p, "k"));
    }

    [Fact]
    public void GetStringParamNullable_JsonElementNonEmptyString_ReturnsString()
    {
        var p = new Dictionary<string, object?> { ["k"] = JsonString("part.sldprt") };
        Assert.Equal("part.sldprt", TestableHandler.GetStringParamNullable(p, "k"));
    }

    // ---------------------------------------------------------------------------
    // MmToMeters — 4 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void MmToMeters_OneThousand_ReturnsOne()
    {
        Assert.Equal(1.0, TestableHandler.MmToMeters(1000.0));
    }

    [Fact]
    public void MmToMeters_Zero_ReturnsZero()
    {
        Assert.Equal(0.0, TestableHandler.MmToMeters(0.0));
    }

    [Fact]
    public void MmToMeters_NegativeValue_ReturnsNegativeMeters()
    {
        Assert.Equal(-0.5, TestableHandler.MmToMeters(-500.0));
    }

    [Fact]
    public void MmToMeters_Fractional_ReturnsCorrectMeters()
    {
        Assert.Equal(0.025, TestableHandler.MmToMeters(25.0), precision: 10);
    }

    // ---------------------------------------------------------------------------
    // MetersToMm — 4 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void MetersToMm_One_ReturnsOneThousand()
    {
        Assert.Equal(1000.0, TestableHandler.MetersToMm(1.0));
    }

    [Fact]
    public void MetersToMm_Zero_ReturnsZero()
    {
        Assert.Equal(0.0, TestableHandler.MetersToMm(0.0));
    }

    [Fact]
    public void MetersToMm_NegativeValue_ReturnsNegativeMm()
    {
        Assert.Equal(-250.0, TestableHandler.MetersToMm(-0.25));
    }

    [Fact]
    public void MetersToMm_Fractional_ReturnsCorrectMm()
    {
        Assert.Equal(12.5, TestableHandler.MetersToMm(0.0125), precision: 10);
    }

    // ---------------------------------------------------------------------------
    // DegreesToRadians — 4 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void DegreesToRadians_OneEighty_ReturnsPi()
    {
        Assert.Equal(Math.PI, TestableHandler.DegreesToRadians(180.0), precision: 12);
    }

    [Fact]
    public void DegreesToRadians_Zero_ReturnsZero()
    {
        Assert.Equal(0.0, TestableHandler.DegreesToRadians(0.0));
    }

    [Fact]
    public void DegreesToRadians_Ninety_ReturnsHalfPi()
    {
        Assert.Equal(Math.PI / 2.0, TestableHandler.DegreesToRadians(90.0), precision: 12);
    }

    [Fact]
    public void DegreesToRadians_ThreeSixty_ReturnsTwoPi()
    {
        Assert.Equal(2.0 * Math.PI, TestableHandler.DegreesToRadians(360.0), precision: 12);
    }

    // ---------------------------------------------------------------------------
    // RadiansToDegrees — 4 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void RadiansToDegrees_Pi_ReturnsOneEighty()
    {
        Assert.Equal(180.0, TestableHandler.RadiansToDegrees(Math.PI), precision: 10);
    }

    [Fact]
    public void RadiansToDegrees_Zero_ReturnsZero()
    {
        Assert.Equal(0.0, TestableHandler.RadiansToDegrees(0.0));
    }

    [Fact]
    public void RadiansToDegrees_HalfPi_ReturnsNinety()
    {
        Assert.Equal(90.0, TestableHandler.RadiansToDegrees(Math.PI / 2.0), precision: 10);
    }

    [Fact]
    public void RadiansToDegrees_TwoPi_ReturnsThreeSixty()
    {
        Assert.Equal(360.0, TestableHandler.RadiansToDegrees(2.0 * Math.PI), precision: 10);
    }

    // ---------------------------------------------------------------------------
    // Round-trip invariants — 4 test cases
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0.0)]
    [InlineData(250.0)]
    [InlineData(-1000.0)]
    [InlineData(0.001)]
    public void MmMeters_RoundTrip_IsIdentity(double originalMm)
    {
        var roundTripped = TestableHandler.MetersToMm(TestableHandler.MmToMeters(originalMm));
        Assert.Equal(originalMm, roundTripped, precision: 10);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(45.0)]
    [InlineData(90.0)]
    [InlineData(270.0)]
    public void DegreesRadians_RoundTrip_IsIdentity(double originalDegrees)
    {
        var roundTripped = TestableHandler.RadiansToDegrees(TestableHandler.DegreesToRadians(originalDegrees));
        Assert.Equal(originalDegrees, roundTripped, precision: 10);
    }

    // ---------------------------------------------------------------------------
    // IsPathSafe — 10 test cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsPathSafe_ValidAbsolutePath_ReturnsTrue()
    {
        var result = TestableHandler.IsPathSafe(@"C:\Users\Lenovo\output\model.sldprt", out var err);
        Assert.True(result);
        Assert.Null(err);
    }

    [Fact]
    public void IsPathSafe_EmptyString_ReturnsFalse()
    {
        var result = TestableHandler.IsPathSafe("", out var err);
        Assert.False(result);
        Assert.NotNull(err);
    }

    [Fact]
    public void IsPathSafe_NullString_ReturnsFalse()
    {
        // IsPathSafe accepts a non-nullable string parameter; passing null! exercises
        // the null-or-whitespace guard via the string.IsNullOrWhiteSpace branch.
        var result = TestableHandler.IsPathSafe(null!, out var err);
        Assert.False(result);
        Assert.NotNull(err);
    }

    [Fact]
    public void IsPathSafe_WhitespaceOnly_ReturnsFalse()
    {
        var result = TestableHandler.IsPathSafe("   ", out var err);
        Assert.False(result);
        Assert.NotNull(err);
    }

    [Fact]
    public void IsPathSafe_UncPath_ReturnsFalse()
    {
        var result = TestableHandler.IsPathSafe(@"\\server\share\file.sldprt", out var err);
        Assert.False(result);
        Assert.NotNull(err);
        Assert.Contains("UNC", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsPathSafe_DevicePathDotPrefix_ReturnsFalse()
    {
        // \\.\COM1 style device path — blocked after Path.GetFullPath
        var result = TestableHandler.IsPathSafe(@"\\.\COM1", out var err);
        Assert.False(result);
        Assert.NotNull(err);
    }

    [Fact]
    public void IsPathSafe_DevicePathQuestionPrefix_ReturnsFalse()
    {
        // \\?\ extended-length path prefix — should be blocked
        var result = TestableHandler.IsPathSafe(@"\\?\C:\file.sldprt", out var err);
        Assert.False(result);
        Assert.NotNull(err);
    }

    [Fact]
    public void IsPathSafe_PathWithSpaces_ReturnsTrue()
    {
        var result = TestableHandler.IsPathSafe(@"C:\My Documents\SolidWorks Parts\output.sldprt", out var err);
        Assert.True(result);
        Assert.Null(err);
    }

    [Fact]
    public void IsPathSafe_PathWithoutExtension_ReturnsTrue()
    {
        // Directory-like path that still has a valid parent — accepted
        var result = TestableHandler.IsPathSafe(@"C:\output\subfolder\modelfile", out var err);
        Assert.True(result);
        Assert.Null(err);
    }

    [Fact]
    public void IsPathSafe_ErrorMessage_IsNullOnSuccess()
    {
        TestableHandler.IsPathSafe(@"C:\valid\path\file.step", out var err);
        Assert.Null(err);
    }
}
