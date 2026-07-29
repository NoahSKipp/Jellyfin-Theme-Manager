using Jellyfin.Plugin.ThemeManager.Configuration;
using Jellyfin.Plugin.ThemeManager.Models;

namespace Jellyfin.Plugin.ThemeManager.Api;

// Everything the config page renders from, in one round trip.
public class ThemeManagerState
{
    public IReadOnlyList<ThemeCatalogEntry> Catalog { get; set; } = Array.Empty<ThemeCatalogEntry>();

    public InstalledTheme[] Installed { get; set; } = Array.Empty<InstalledTheme>();

    public string? ActiveThemeId { get; set; }

    public string? ActiveLinkId { get; set; }

    public string[] EnabledAddonIds { get; set; } = Array.Empty<string>();

    public LinkedStylesheet[] Links { get; set; } = Array.Empty<LinkedStylesheet>();

    public string CustomCss { get; set; } = string.Empty;

    public bool ApplyToServerBranding { get; set; }

    public bool InlineImportsOnInstall { get; set; }

    public string? CatalogUrl { get; set; }

    public BrandingSettings Branding { get; set; } = new BrandingSettings();

    public DateTime? LastAppliedAt { get; set; }
}

public class InstallRequest
{
    public string Id { get; set; } = string.Empty;
}

public class InstallUrlRequest
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public ThemeKind Kind { get; set; } = ThemeKind.Theme;
}

public class SettingsRequest
{
    public string? ActiveThemeId { get; set; }

    public string? ActiveLinkId { get; set; }

    public string[]? EnabledAddonIds { get; set; }

    public LinkedStylesheet[]? Links { get; set; }

    public string? CustomCss { get; set; }

    public bool ApplyToServerBranding { get; set; } = true;

    public bool InlineImportsOnInstall { get; set; }

    public string? CatalogUrl { get; set; }

    // Toggles only. The uploaded image fields belong to the upload endpoints.
    public BrandingSettings? Branding { get; set; }
}
