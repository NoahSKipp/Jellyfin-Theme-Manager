using Jellyfin.Plugin.ThemeManager.Api;
using Xunit;

namespace Jellyfin.Plugin.ThemeManager.Tests;

/// <summary>
/// The active theme can come from an installed theme or a Theme-kind link, never both. This is
/// the one place that decides which wins when settings are saved.
/// </summary>
public class ActiveThemeResolutionTests
{
    private static readonly IReadOnlySet<string> _installed = new HashSet<string> { "scyfin" };
    private static readonly IReadOnlySet<string> _themeLinks = new HashSet<string> { "link-1" };

    [Fact]
    public void PicksTheInstalledThemeWhenOnlyThatIsRequested()
    {
        var result = ThemeManagerController.ResolveActiveTheme("scyfin", null, _installed, _themeLinks);

        Assert.Equal("scyfin", result.ThemeId);
        Assert.Null(result.LinkId);
    }

    [Fact]
    public void PicksTheLinkedThemeWhenOnlyThatIsRequested()
    {
        var result = ThemeManagerController.ResolveActiveTheme(null, "link-1", _installed, _themeLinks);

        Assert.Null(result.ThemeId);
        Assert.Equal("link-1", result.LinkId);
    }

    [Fact]
    public void InstalledThemeWinsWhenBothAreRequested()
    {
        // Shouldn't happen from the page, it clears one whenever you apply the other, but the
        // API is reachable directly so this has to resolve to something rather than both.
        var result = ThemeManagerController.ResolveActiveTheme("scyfin", "link-1", _installed, _themeLinks);

        Assert.Equal("scyfin", result.ThemeId);
        Assert.Null(result.LinkId);
    }

    [Fact]
    public void DropsAThemeIdThatIsNotActuallyInstalled()
    {
        var result = ThemeManagerController.ResolveActiveTheme("not-installed", null, _installed, _themeLinks);

        Assert.Null(result.ThemeId);
        Assert.Null(result.LinkId);
    }

    [Fact]
    public void DropsALinkIdThatIsNotAThemeKindLink()
    {
        // Covers an addon-kind link's id being sent by mistake, or one that was since removed.
        var result = ThemeManagerController.ResolveActiveTheme(null, "not-a-theme-link", _installed, _themeLinks);

        Assert.Null(result.ThemeId);
        Assert.Null(result.LinkId);
    }

    [Fact]
    public void BothNullWhenNeitherIsRequested()
    {
        var result = ThemeManagerController.ResolveActiveTheme(null, null, _installed, _themeLinks);

        Assert.Null(result.ThemeId);
        Assert.Null(result.LinkId);
    }
}
