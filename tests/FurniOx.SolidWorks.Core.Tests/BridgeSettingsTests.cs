using System;
using System.IO;
using FurniOx.SolidWorks.Shared.Configuration;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class BridgeSettingsTests : IDisposable
{
    private readonly string _testDirectory;

    public BridgeSettingsTests()
    {
        _testDirectory = Path.Combine(AppContext.BaseDirectory, "bridge-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void ResolveAddInPath_ReturnsExplicitPath_WhenConfigured()
    {
        var settings = new BridgeSettings
        {
            AddInPath = @"C:\explicit\FurniOx.SolidWorks.Bridge.dll"
        };

        var resolvedPath = settings.ResolveAddInPath(_testDirectory);

        Assert.Equal(settings.AddInPath, resolvedPath);
    }

    [Fact]
    public void ResolveAddInPath_ReturnsBundledDll_WhenPresent()
    {
        var bundledPath = Path.Combine(_testDirectory, "FurniOx.SolidWorks.Bridge.dll");
        File.WriteAllText(bundledPath, "bridge");

        var settings = new BridgeSettings();
        var resolvedPath = settings.ResolveAddInPath(_testDirectory);

        Assert.Equal(bundledPath, resolvedPath);
    }

    [Fact]
    public void ResolveAddInPath_ReturnsNull_WhenNoExplicitOrBundledDll()
    {
        var settings = new BridgeSettings();

        var resolvedPath = settings.ResolveAddInPath(_testDirectory);

        Assert.Null(resolvedPath);
    }
}
