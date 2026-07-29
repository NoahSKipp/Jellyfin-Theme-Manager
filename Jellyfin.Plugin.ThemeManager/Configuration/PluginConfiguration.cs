using Jellyfin.Plugin.ThemeManager.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeManager.Configuration;

/// <summary>
/// Saved plugin state. Jellyfin persists this with XmlSerializer, so everything here has to be
/// a concrete settable type with a parameterless constructor. No interfaces, no records.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    // Exactly one of ActiveThemeId (an installed theme) and ActiveLinkId (a Theme-kind
    // linked stylesheet) is ever set, never both. Settings save enforces that.
    public string? ActiveThemeId { get; set; }

    public string? ActiveLinkId { get; set; }

    public string[] EnabledAddonIds { get; set; } = Array.Empty<string>();

    public InstalledTheme[] InstalledThemes { get; set; } = Array.Empty<InstalledTheme>();

    public LinkedStylesheet[] LinkedStylesheets { get; set; } = Array.Empty<LinkedStylesheet>();

    // Appended last so it beats everything else.
    public string CustomCss { get; set; } = string.Empty;

    public bool ApplyToServerBranding { get; set; } = true;

    public bool InlineImportsOnInstall { get; set; }

    public int MaxDownloadSizeBytes { get; set; } = 8 * 1024 * 1024;

    public string? CatalogUrl { get; set; }

    public BrandingSettings Branding { get; set; } = new BrandingSettings();

    public DateTime? LastAppliedAt { get; set; }
}
