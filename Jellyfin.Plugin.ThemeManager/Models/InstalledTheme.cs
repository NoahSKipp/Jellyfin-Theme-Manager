namespace Jellyfin.Plugin.ThemeManager.Models;

/// <summary>
/// A stylesheet we've downloaded into the plugin data folder.
/// </summary>
public class InstalledTheme
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Author { get; set; }

    public string? Homepage { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public ThemeKind Kind { get; set; } = ThemeKind.Theme;

    public bool FromCatalog { get; set; }

    public bool ImportsInlined { get; set; }

    public long SizeBytes { get; set; }

    // So we can tell when upstream has changed.
    public string? Sha256 { get; set; }

    public DateTime InstalledAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// A stylesheet we link with @import instead of downloading.
/// </summary>
public class LinkedStylesheet
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
