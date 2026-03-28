using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Adapters;
using SolidWorks.Interop.swconst;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class CustomPropertySupportTests
{
    [Fact]
    public void ResolveRequestedType_DefaultsUnknownTypeToText()
    {
        var type = CustomPropertySupport.ResolveRequestedType("bogus", "value");

        Assert.Equal(swCustomInfoType_e.swCustomInfoText, type);
    }

    [Fact]
    public void ResolveRequestedType_UpgradesNumberWithDecimalToDouble()
    {
        var type = CustomPropertySupport.ResolveRequestedType("number", "12.5");

        Assert.Equal(swCustomInfoType_e.swCustomInfoDouble, type);
    }

    [Fact]
    public void ResolveRequestedType_PreservesYesOrNo()
    {
        var type = CustomPropertySupport.ResolveRequestedType("yesorno", "Yes");

        Assert.Equal(swCustomInfoType_e.swCustomInfoYesOrNo, type);
    }

    [Fact]
    public void FormatConfigurationLabel_UsesFileLevelForEmpty()
    {
        Assert.Equal("File-level", CustomPropertySupport.FormatConfigurationLabel(string.Empty));
    }

    [Fact]
    public void TryGetRequiredString_RejectsWhitespaceWhenNotAllowed()
    {
        var parameters = new Dictionary<string, object?> { ["Name"] = "   " };

        var success = CustomPropertySupport.TryGetRequiredString(parameters, "Name", allowEmpty: false, out _, out var failure);

        Assert.False(success);
        Assert.Contains("Missing or invalid", failure.Message, System.StringComparison.Ordinal);
    }
}
