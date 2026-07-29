using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ThemeManager.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemeKind
{
    Theme = 0,
    Addon = 1
}

/// <summary>
/// The merged catalog. The bundled one is assembled from Resources/catalog/*.json, one file
/// per entry; the extra catalog URL setting still expects this whole shape as one JSON object.
/// </summary>
public class ThemeCatalog
{
    public IReadOnlyList<ThemeCatalogEntry> Themes { get; set; } = Array.Empty<ThemeCatalogEntry>();
}

public class ThemeCatalogEntry
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Author { get; set; }

    public string? Description { get; set; }

    public string? Homepage { get; set; }

    public string? License { get; set; }

    public string Url { get; set; } = string.Empty;

    public ThemeKind Kind { get; set; } = ThemeKind.Theme;

    // Groups related entries in the UI, e.g. all the Ultrachromic presets.
    public string? Family { get; set; }

    // Colour variants are only a few variables, so they need their base theme too.
    public IReadOnlyList<string> Requires { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    public string? PreviewImage { get; set; }

    // Draws a gradient tile when there's no screenshot. Beats a broken image.
    public IReadOnlyList<string> Swatch { get; set; } = Array.Empty<string>();
}
