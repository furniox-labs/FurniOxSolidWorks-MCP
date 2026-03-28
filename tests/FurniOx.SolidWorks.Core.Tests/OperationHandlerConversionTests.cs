using System;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class OperationHandlerConversionTests
{
    [Fact]
    public void MmToMeters_ConvertsExpectedValues()
    {
        Assert.Equal(1.0, TestableOperationHandler.MmToMeters(1000.0));
        Assert.Equal(0.0, TestableOperationHandler.MmToMeters(0.0));
        Assert.Equal(-0.5, TestableOperationHandler.MmToMeters(-500.0));
        Assert.Equal(0.025, TestableOperationHandler.MmToMeters(25.0), precision: 10);
    }

    [Fact]
    public void MetersToMm_ConvertsExpectedValues()
    {
        Assert.Equal(1000.0, TestableOperationHandler.MetersToMm(1.0));
        Assert.Equal(0.0, TestableOperationHandler.MetersToMm(0.0));
        Assert.Equal(-250.0, TestableOperationHandler.MetersToMm(-0.25));
        Assert.Equal(12.5, TestableOperationHandler.MetersToMm(0.0125), precision: 10);
    }

    [Fact]
    public void DegreesToRadians_ConvertsExpectedValues()
    {
        Assert.Equal(Math.PI, TestableOperationHandler.DegreesToRadians(180.0), precision: 12);
        Assert.Equal(0.0, TestableOperationHandler.DegreesToRadians(0.0));
        Assert.Equal(Math.PI / 2.0, TestableOperationHandler.DegreesToRadians(90.0), precision: 12);
        Assert.Equal(2.0 * Math.PI, TestableOperationHandler.DegreesToRadians(360.0), precision: 12);
    }

    [Fact]
    public void RadiansToDegrees_ConvertsExpectedValues()
    {
        Assert.Equal(180.0, TestableOperationHandler.RadiansToDegrees(Math.PI), precision: 10);
        Assert.Equal(0.0, TestableOperationHandler.RadiansToDegrees(0.0));
        Assert.Equal(90.0, TestableOperationHandler.RadiansToDegrees(Math.PI / 2.0), precision: 10);
        Assert.Equal(360.0, TestableOperationHandler.RadiansToDegrees(2.0 * Math.PI), precision: 10);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(250.0)]
    [InlineData(-1000.0)]
    [InlineData(0.001)]
    public void MmMeters_RoundTrip_IsIdentity(double originalMillimeters)
    {
        var roundTripped = TestableOperationHandler.MetersToMm(TestableOperationHandler.MmToMeters(originalMillimeters));
        Assert.Equal(originalMillimeters, roundTripped, precision: 10);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(45.0)]
    [InlineData(90.0)]
    [InlineData(270.0)]
    public void DegreesRadians_RoundTrip_IsIdentity(double originalDegrees)
    {
        var roundTripped = TestableOperationHandler.RadiansToDegrees(TestableOperationHandler.DegreesToRadians(originalDegrees));
        Assert.Equal(originalDegrees, roundTripped, precision: 10);
    }
}
