using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeManager.Services;

/// <summary>
/// Joins the active theme, its add-ons, the linked stylesheets, the branding CSS and the user's
/// own CSS into one sheet, then writes it into the server's branding config. That's what gets it
/// to clients, since the web client already fetches /Branding/Css on load.
/// </summary>
public partial class CssPublisher
{
    private const string StartMarker = "/* ==== jellyfin-theme-manager: managed block, edits here are overwritten ==== */";
    private const string EndMarker = "/* ==== end jellyfin-theme-manager ==== */";

    private readonly IServerConfigurationManager _serverConfiguration;
    private readonly ThemeInstaller _installer;
    private readonly ILogger<CssPublisher> _logger;

    public CssPublisher(
        IServerConfigurationManager serverConfiguration,
        ThemeInstaller installer,
        ILogger<CssPublisher> logger)
    {
        _serverConfiguration = serverConfiguration;
        _installer = installer;
        _logger = logger;
    }

    [GeneratedRegex(@"@import\s+(?:url\(\s*['""]?[^'""\)]+['""]?\s*\)|['""][^'""]+['""])[^;]*;", RegexOptions.IgnoreCase)]
    private static partial Regex ImportStatementRegex();

    [GeneratedRegex(@"@charset\s+[^;]+;", RegexOptions.IgnoreCase)]
    private static partial Regex CharsetRegex();

    private static ThemeManagerPlugin Plugin =>
        ThemeManagerPlugin.Instance ?? throw new InvalidOperationException("The Theme Manager plugin is not loaded.");

    public string Compose()
    {
        var config = Plugin.Configuration;
        var blocks = new List<(string Label, string Css)>();

        if (!string.IsNullOrWhiteSpace(config.ActiveThemeId))
        {
            var theme = ThemeInstaller.Find(config.ActiveThemeId);
            if (theme is not null)
            {
                blocks.Add(($"theme: {theme.Name}", _installer.ReadCss(theme)));
            }
            else
            {
                _logger.LogWarning("Active theme {Theme} is no longer installed", config.ActiveThemeId);
            }
        }

        // Add-ons go in the order they were enabled, last one wins.
        foreach (var addonId in config.EnabledAddonIds)
        {
            var addon = ThemeInstaller.Find(addonId);
            if (addon is not null)
            {
                blocks.Add(($"addon: {addon.Name}", _installer.ReadCss(addon)));
            }
        }

        var links = config.LinkedStylesheets
            .Where(l => l.Enabled && !string.IsNullOrWhiteSpace(l.Url))
            .ToArray();
        if (links.Length > 0)
        {
            var linkCss = new StringBuilder();
            foreach (var link in links)
            {
                // Left as a real @import so the browser fetches it and picks up upstream changes.
                linkCss.Append("@import url(\"").Append(EscapeUrl(link.Url)).AppendLine("\");");
            }

            blocks.Add(("linked stylesheets", linkCss.ToString()));
        }

        var branding = BrandingCssBuilder.Build(config.Branding);
        if (branding.Length > 0)
        {
            blocks.Add(("branding", branding));
        }

        if (!string.IsNullOrWhiteSpace(config.CustomCss))
        {
            blocks.Add(("custom css", config.CustomCss));
        }

        return Assemble(blocks);
    }

    public void Publish()
    {
        var config = Plugin.Configuration;
        var options = _serverConfiguration.GetConfiguration<BrandingOptions>("branding");

        var preserved = StripManagedBlock(options.CustomCss);
        var managed = config.ApplyToServerBranding ? Compose() : string.Empty;

        options.CustomCss = managed.Length == 0
            ? preserved
            : string.Concat(StartMarker, "\n", managed, "\n", EndMarker, preserved.Length > 0 ? "\n\n" + preserved : string.Empty);

        ApplySplashscreen(options);

        _serverConfiguration.SaveConfiguration("branding", options);

        config.LastAppliedAt = DateTime.UtcNow;
        Plugin.UpdateConfiguration(config);

        _logger.LogInformation("Published {Length} characters of theme CSS to the server branding configuration", managed.Length);
    }

    private void ApplySplashscreen(BrandingOptions options)
    {
        var branding = Plugin.Configuration.Branding;

        if (branding.Enabled && !string.IsNullOrWhiteSpace(branding.SplashFile))
        {
            var path = Path.Combine(Plugin.AssetsPath, branding.SplashFile);
            if (File.Exists(path))
            {
                options.SplashscreenLocation = path;
                options.SplashscreenEnabled = branding.SplashEnabled;
                return;
            }

            _logger.LogWarning("Splash screen image {Path} is missing", path);
        }

        // Only give the setting back if it was ours, don't clobber a splash set up elsewhere.
        if (options.SplashscreenLocation?.StartsWith(Plugin.AssetsPath, StringComparison.Ordinal) == true)
        {
            options.SplashscreenLocation = null;
            options.SplashscreenEnabled = false;
        }
    }

    // Careful here: CSS drops any @import that comes after a normal rule, and half the catalog
    // themes are nothing but @import lines. Without hoisting, enabling one add-on on top of an
    // Ultrachromic preset silently blanks the whole theme.
    internal static string Assemble(IReadOnlyList<(string Label, string Css)> blocks)
    {
        var imports = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var body = new StringBuilder();

        foreach (var (label, css) in blocks)
        {
            if (string.IsNullOrWhiteSpace(css))
            {
                continue;
            }

            var cleaned = CharsetRegex().Replace(css, string.Empty);

            cleaned = ImportStatementRegex().Replace(cleaned, match =>
            {
                var statement = match.Value.Trim();
                if (seen.Add(statement))
                {
                    imports.Add(statement);
                }

                return string.Empty;
            });

            body.Append("\n/* --- ").Append(label).AppendLine(" --- */")
                .AppendLine(cleaned.Trim());
        }

        var result = new StringBuilder();
        foreach (var import in imports)
        {
            result.AppendLine(import);
        }

        return result.Append(body).ToString().Trim();
    }

    internal static string StripManagedBlock(string? css)
    {
        if (string.IsNullOrEmpty(css))
        {
            return string.Empty;
        }

        var start = css.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return css.Trim();
        }

        var end = css.IndexOf(EndMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            // Someone deleted the end marker by hand. Drop everything from the start marker on
            // rather than leaving half a stylesheet behind.
            return css[..start].Trim();
        }

        return (css[..start] + css[(end + EndMarker.Length)..]).Trim();
    }

    private static string EscapeUrl(string url) => url.Replace("\"", "%22", StringComparison.Ordinal).Trim();
}
