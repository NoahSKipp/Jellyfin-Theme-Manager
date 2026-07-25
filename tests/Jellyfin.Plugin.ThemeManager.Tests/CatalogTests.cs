using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.ThemeManager.Models;
using Xunit;

namespace Jellyfin.Plugin.ThemeManager.Tests;

public class CatalogTests
{
    private static readonly Assembly _pluginAssembly = typeof(ThemeManagerPlugin).Assembly;

    private static ThemeCatalog LoadCatalog()
    {
        using var stream = _pluginAssembly.GetManifestResourceStream("Jellyfin.Plugin.ThemeManager.Resources.themes.json");
        Assert.NotNull(stream);

        var catalog = JsonSerializer.Deserialize<ThemeCatalog>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(catalog);
        return catalog;
    }

    [Fact]
    public void ConfigPageIsEmbeddedUnderTheNameThePluginAdvertises()
    {
        // Getting this wrong doesn't fail the build, it just serves a blank settings page.
        var expected = $"{typeof(ThemeManagerPlugin).Namespace}.Configuration.configPage.html";

        Assert.Contains(expected, _pluginAssembly.GetManifestResourceNames());
    }

    [Fact]
    public void CatalogParsesAndIsNotEmpty()
    {
        Assert.NotEmpty(LoadCatalog().Themes);
    }

    [Fact]
    public void EveryEntryHasTheFieldsTheUiAndInstallerNeed()
    {
        foreach (var entry in LoadCatalog().Themes)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Id), "an entry is missing its id");
            Assert.False(string.IsNullOrWhiteSpace(entry.Name), $"{entry.Id} is missing its name");
            Assert.False(string.IsNullOrWhiteSpace(entry.Author), $"{entry.Id} is missing its author");

            Assert.True(
                Uri.TryCreate(entry.Url, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps,
                $"{entry.Id} does not point at an https stylesheet");
        }
    }

    [Fact]
    public void EntryIdsAreUnique()
    {
        var ids = LoadCatalog().Themes.Select(s => s.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void PrerequisitesPointAtEntriesThatExist()
    {
        var catalog = LoadCatalog();
        var ids = catalog.Themes.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in catalog.Themes)
        {
            foreach (var required in entry.Requires)
            {
                Assert.True(ids.Contains(required), $"{entry.Id} requires '{required}', which is not in the catalog");
            }
        }
    }

    [Fact]
    public void ScyfinVariantsDeclareTheirBaseTheme()
    {
        // These are a handful of custom properties. Apply one on its own and the client just
        // looks broken.
        var variants = LoadCatalog().Themes.Where(s => s.Id.StartsWith("scyfin-", StringComparison.Ordinal));

        foreach (var variant in variants)
        {
            Assert.Contains("scyfin", variant.Requires);
        }
    }
}
