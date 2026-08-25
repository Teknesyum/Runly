using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Guards the one switch <c>RunlySettings.exe</c> accepts; everything else has to stay ignored.</summary>
public sealed class SettingsCommandLineTests
{
    [Fact]
    public void NoArguments_YieldsNoExtension()
    {
        Assert.Null(SettingsCommandLine.ParseSelectedExtension([]));
        Assert.Null(SettingsCommandLine.ParseSelectedExtension(null));
    }

    [Fact]
    public void UnknownArguments_AreIgnored()
    {
        Assert.Null(SettingsCommandLine.ParseSelectedExtension(["/install", "--verbose", "C:\\tmp\\a.txt"]));
    }

    [Fact]
    public void SelectWithoutValue_YieldsNoExtension()
    {
        Assert.Null(SettingsCommandLine.ParseSelectedExtension(["--select"]));
    }

    [Fact]
    public void MissingDot_IsAdded()
    {
        Assert.Equal(".zzq", SettingsCommandLine.ParseSelectedExtension(["--select", "zzq"]));
    }

    [Fact]
    public void UpperCase_IsLowered()
    {
        Assert.Equal(".ps1", SettingsCommandLine.ParseSelectedExtension(["--select", ".PS1"]));
        Assert.Equal(".ps1", SettingsCommandLine.ParseSelectedExtension(["--SELECT", "PS1"]));
    }

    [Theory]
    [InlineData(".p s1")]
    [InlineData(".p/s1")]
    [InlineData("..\\..\\windows")]
    [InlineData(".p\"s")]
    [InlineData(".ürün")]
    public void InvalidCharacters_AreRejected(string value)
    {
        Assert.Null(SettingsCommandLine.ParseSelectedExtension(["--select", value]));
    }

    [Fact]
    public void EmptyValue_IsRejected()
    {
        Assert.Null(SettingsCommandLine.ParseSelectedExtension(["--select", "   "]));
        Assert.Null(SettingsCommandLine.ParseSelectedExtension(["--select", "."]));
    }

    [Fact]
    public void LengthLimit_IsEnforced()
    {
        var atLimit = new string('a', SettingsCommandLine.MaxExtensionLength - 1);
        Assert.Equal("." + atLimit, SettingsCommandLine.ParseSelectedExtension(["--select", atLimit]));

        var overLimit = new string('a', SettingsCommandLine.MaxExtensionLength);
        Assert.Null(SettingsCommandLine.ParseSelectedExtension(["--select", overLimit]));
    }

    [Fact]
    public void ValueAfterOtherArguments_IsStillFound()
    {
        Assert.Equal(".zzq", SettingsCommandLine.ParseSelectedExtension(["--noise", "--select", ".zzq", "--more"]));
    }
}
