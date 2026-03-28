using System;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class OperationHandlerPathSafetyTests
{
    [Fact]
    public void IsPathSafe_AcceptsNormalAbsolutePaths()
    {
        Assert.True(TestableOperationHandler.IsPathSafe(@"C:\Users\Lenovo\output\model.sldprt", out var error));
        Assert.Null(error);
        Assert.True(TestableOperationHandler.IsPathSafe(@"C:\My Documents\SolidWorks Parts\output.sldprt", out error));
        Assert.Null(error);
        Assert.True(TestableOperationHandler.IsPathSafe(@"C:\output\subfolder\modelfile", out error));
        Assert.Null(error);
    }

    [Fact]
    public void IsPathSafe_RejectsEmptyNullAndWhitespace()
    {
        Assert.False(TestableOperationHandler.IsPathSafe("", out var emptyError));
        Assert.NotNull(emptyError);
        Assert.False(TestableOperationHandler.IsPathSafe(null!, out var nullError));
        Assert.NotNull(nullError);
        Assert.False(TestableOperationHandler.IsPathSafe("   ", out var whitespaceError));
        Assert.NotNull(whitespaceError);
    }

    [Fact]
    public void IsPathSafe_RejectsUncAndDevicePaths()
    {
        Assert.False(TestableOperationHandler.IsPathSafe(@"\\server\share\file.sldprt", out var uncError));
        Assert.Contains("UNC", uncError, StringComparison.OrdinalIgnoreCase);
        Assert.False(TestableOperationHandler.IsPathSafe(@"\\.\COM1", out var dotDeviceError));
        Assert.NotNull(dotDeviceError);
        Assert.False(TestableOperationHandler.IsPathSafe(@"\\?\C:\file.sldprt", out var questionDeviceError));
        Assert.NotNull(questionDeviceError);
    }

    [Fact]
    public void IsPathSafe_ErrorIsNullOnSuccess()
    {
        TestableOperationHandler.IsPathSafe(@"C:\valid\path\file.step", out var error);
        Assert.Null(error);
    }
}
