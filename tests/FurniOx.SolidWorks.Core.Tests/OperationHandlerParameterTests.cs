using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class OperationHandlerParameterTests
{
    [Fact]
    public void GetDoubleParam_MissingKey_ReturnsDefault()
    {
        Assert.Equal(3.14, TestableOperationHandler.GetDoubleParam(new Dictionary<string, object?>(), "missing", 3.14));
    }

    [Fact]
    public void GetDoubleParam_NullValue_ReturnsDefault()
    {
        Assert.Equal(9.9, TestableOperationHandler.GetDoubleParam(new Dictionary<string, object?> { ["k"] = null }, "k", 9.9));
    }

    [Fact]
    public void GetDoubleParam_SupportsDoubleIntegerAndJsonValues()
    {
        Assert.Equal(2.718, TestableOperationHandler.GetDoubleParam(new Dictionary<string, object?> { ["k"] = 2.718 }, "k"));
        Assert.Equal(42.0, TestableOperationHandler.GetDoubleParam(new Dictionary<string, object?> { ["k"] = 42 }, "k"));
        Assert.Equal(7.0, TestableOperationHandler.GetDoubleParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.Number("7") }, "k"));
    }

    [Fact]
    public void GetDoubleParam_InvalidString_ReturnsDefault()
    {
        Assert.Equal(0.0, TestableOperationHandler.GetDoubleParam(new Dictionary<string, object?> { ["k"] = "not-a-number" }, "k"));
    }

    [Fact]
    public void GetIntParam_MissingOrNullKey_ReturnsDefault()
    {
        Assert.Equal(5, TestableOperationHandler.GetIntParam(new Dictionary<string, object?>(), "missing", 5));
        Assert.Equal(7, TestableOperationHandler.GetIntParam(new Dictionary<string, object?> { ["k"] = null }, "k", 7));
    }

    [Fact]
    public void GetIntParam_SupportsIntLongAndJsonValues()
    {
        Assert.Equal(100, TestableOperationHandler.GetIntParam(new Dictionary<string, object?> { ["k"] = 100 }, "k"));
        Assert.Equal(255, TestableOperationHandler.GetIntParam(new Dictionary<string, object?> { ["k"] = 255L }, "k"));
        Assert.Equal(42, TestableOperationHandler.GetIntParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.Number("42") }, "k"));
    }

    [Fact]
    public void GetIntParam_FloatJson_TruncatesToInt()
    {
        Assert.Equal(1, TestableOperationHandler.GetIntParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.Number("1.0") }, "k"));
        Assert.Equal(7, TestableOperationHandler.GetIntParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.Number("7.9") }, "k"));
    }

    [Fact]
    public void GetIntParam_InvalidString_ReturnsDefault()
    {
        Assert.Equal(0, TestableOperationHandler.GetIntParam(new Dictionary<string, object?> { ["k"] = "abc" }, "k"));
    }

    [Fact]
    public void GetBoolParam_MissingOrNullKey_ReturnsDefault()
    {
        Assert.True(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?>(), "missing", true));
        Assert.False(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?> { ["k"] = null }, "k", false));
    }

    [Fact]
    public void GetBoolParam_SupportsBoolAndJsonBool()
    {
        Assert.True(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?> { ["k"] = true }, "k"));
        Assert.False(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?> { ["k"] = false }, "k", true));
        Assert.True(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.Bool(true) }, "k"));
        Assert.False(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.Bool(false) }, "k", true));
    }

    [Fact]
    public void GetBoolParam_InvalidValues_ReturnDefault()
    {
        Assert.False(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?> { ["k"] = "yes" }, "k"));
        Assert.False(TestableOperationHandler.GetBoolParam(new Dictionary<string, object?> { ["k"] = 1 }, "k"));
    }

    [Fact]
    public void GetStringParam_SupportsStringAndJsonString()
    {
        Assert.Equal("hello", TestableOperationHandler.GetStringParam(new Dictionary<string, object?> { ["k"] = "hello" }, "k"));
        Assert.Equal("world", TestableOperationHandler.GetStringParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.String("world") }, "k"));
        Assert.Equal("", TestableOperationHandler.GetStringParam(new Dictionary<string, object?> { ["k"] = "" }, "k", "default"));
    }

    [Fact]
    public void GetStringParam_MissingNullOrWrongType_ReturnsDefault()
    {
        Assert.Equal("fallback", TestableOperationHandler.GetStringParam(new Dictionary<string, object?>(), "missing", "fallback"));
        Assert.Equal("default", TestableOperationHandler.GetStringParam(new Dictionary<string, object?> { ["k"] = null }, "k", "default"));
        Assert.Equal("default", TestableOperationHandler.GetStringParam(new Dictionary<string, object?> { ["k"] = 42 }, "k", "default"));
        Assert.Equal("fallback", TestableOperationHandler.GetStringParam(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.Number("123") }, "k", "fallback"));
    }

    [Fact]
    public void GetStringParamNullable_HandlesMissingEmptyAndNonEmptyValues()
    {
        Assert.Null(TestableOperationHandler.GetStringParamNullable(new Dictionary<string, object?>(), "missing"));
        Assert.Null(TestableOperationHandler.GetStringParamNullable(new Dictionary<string, object?> { ["k"] = null }, "k"));
        Assert.Equal("SolidWorks", TestableOperationHandler.GetStringParamNullable(new Dictionary<string, object?> { ["k"] = "SolidWorks" }, "k"));
        Assert.Null(TestableOperationHandler.GetStringParamNullable(new Dictionary<string, object?> { ["k"] = "" }, "k"));
        Assert.Null(TestableOperationHandler.GetStringParamNullable(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.String("") }, "k"));
        Assert.Equal("part.sldprt", TestableOperationHandler.GetStringParamNullable(new Dictionary<string, object?> { ["k"] = OperationHandlerJson.String("part.sldprt") }, "k"));
    }
}
