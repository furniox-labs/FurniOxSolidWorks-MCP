using FurniOx.SolidWorks.Shared.Configuration;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class PublicProfileSettingsTests
{
    [Fact]
    public void PublicProfile_InitializesContactSettings()
    {
        var settings = new SolidWorksSettings();

        Assert.NotNull(settings.PublicProfile);
        Assert.NotNull(settings.PublicProfile.Contact);
        Assert.True(settings.PublicProfile.Enabled);
    }

    [Fact]
    public void GetProgIdVersionHint_PrefersExplicitProgIdVersion()
    {
        var settings = new SolidWorksSettings
        {
            Version = "30",
            ProgIdVersion = "31"
        };

        Assert.Equal("31", settings.GetProgIdVersionHint());
    }

    [Fact]
    public void GetPartTemplatePath_UsesTemplateVersionFallback()
    {
        var settings = new SolidWorksSettings
        {
            TemplateVersion = "2024"
        };

        Assert.Equal(@"C:\ProgramData\SolidWorks\SOLIDWORKS 2024\templates\Part.prtdot", settings.GetPartTemplatePath());
    }
}
