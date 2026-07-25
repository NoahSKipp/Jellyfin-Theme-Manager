using Jellyfin.Plugin.ThemeManager.Configuration;
using Jellyfin.Plugin.ThemeManager.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeManager.Tests;

public class BrandingCssBuilderTests
{
    [Fact]
    public void ProducesNothingWhenBrandingIsOff()
    {
        var css = BrandingCssBuilder.Build(new BrandingSettings { Enabled = false, LogoFile = "logo.png" });

        Assert.Equal(string.Empty, css);
    }

    [Fact]
    public void OverridesTheSelectorsJellyfinActuallyUsesForTheLogo()
    {
        var css = BrandingCssBuilder.Build(new BrandingSettings
        {
            Enabled = true,
            LogoFile = "logo.png",
            ApplyLogoToLogin = true
        });

        Assert.Contains(".pageTitleWithDefaultLogo", css, StringComparison.Ordinal);
        Assert.Contains(".layout-tv .pageTitleWithDefaultLogo", css, StringComparison.Ordinal);
        Assert.Contains(".adminDrawerLogo img", css, StringComparison.Ordinal);
        Assert.Contains(".splashLogo", css, StringComparison.Ordinal);
        Assert.Contains("/ThemeManager/Asset/logo", css, StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesTheSplashLogoAloneWhenTheLoginOverrideIsOff()
    {
        var css = BrandingCssBuilder.Build(new BrandingSettings
        {
            Enabled = true,
            LogoFile = "logo.png",
            ApplyLogoToLogin = false
        });

        Assert.DoesNotContain(".splashLogo", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AssetUrlCarriesTheRevisionSoBrowsersDropTheOldImage()
    {
        var css = BrandingCssBuilder.Build(new BrandingSettings
        {
            Enabled = true,
            LogoFile = "logo.png",
            AssetRevision = 7
        });

        Assert.Contains("/ThemeManager/Asset/logo?v=7", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("3em", "3em")]
    [InlineData("40px", "40px")]
    [InlineData("", "2.4em")]
    [InlineData("2em; } body { display: none; } .x {", "2.4em")]
    public void LogoHeightFallsBackWhenItIsNotAPlainLength(string input, string expected)
    {
        var css = BrandingCssBuilder.Build(new BrandingSettings
        {
            Enabled = true,
            LogoFile = "logo.png",
            LogoMaxHeight = input
        });

        Assert.Contains($"height: {expected};", css, StringComparison.Ordinal);
        Assert.DoesNotContain("display: none", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AccentColourIsDroppedWhenItCouldCarryExtraDeclarations()
    {
        var css = BrandingCssBuilder.Build(new BrandingSettings
        {
            Enabled = true,
            AccentColor = "red; } html { display: none; } :root {"
        });

        Assert.Equal(string.Empty, css);
    }

    [Fact]
    public void AccentColourIsWrittenToTheVariablesTheCatalogThemesRead()
    {
        var css = BrandingCssBuilder.Build(new BrandingSettings { Enabled = true, AccentColor = "#ff6f61" });

        Assert.Contains("--accent: #ff6f61;", css, StringComparison.Ordinal);
        Assert.Contains("--primary-accent-color: #ff6f61;", css, StringComparison.Ordinal);
    }
}
