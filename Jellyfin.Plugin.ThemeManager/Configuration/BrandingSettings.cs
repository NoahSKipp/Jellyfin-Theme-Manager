namespace Jellyfin.Plugin.ThemeManager.Configuration;

/// <summary>
/// Images and colours that replace Jellyfin's own branding. Uploads live in the plugin's
/// assets folder and are served from /ThemeManager/Asset/{kind}. Only the file name is stored
/// so moving the Jellyfin data directory doesn't break anything.
/// </summary>
public class BrandingSettings
{
    public bool Enabled { get; set; }

    public string? LogoFile { get; set; }

    public string? LogoContentType { get; set; }

    public string LogoMaxHeight { get; set; } = "2.4em";

    public bool ApplyLogoToLogin { get; set; } = true;

    public string? FaviconFile { get; set; }

    public string? FaviconContentType { get; set; }

    public string? AppIconFile { get; set; }

    public string? AppIconContentType { get; set; }

    // CSS can't touch the tab icon, so a middleware answers those requests instead.
    public bool ReplaceWebIcons { get; set; } = true;

    public string? SplashFile { get; set; }

    public bool SplashEnabled { get; set; }

    // Only does anything on themes that expose an accent variable. Scyfin and Finimalism do,
    // Jellyfin's own themes hardcode their colours.
    public string? AccentColor { get; set; }

    // Bumped on every upload. Without a changing URL the browser keeps the old favicon forever.
    public int AssetRevision { get; set; } = 1;
}
