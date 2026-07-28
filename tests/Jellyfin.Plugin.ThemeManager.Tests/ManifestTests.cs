using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.ThemeManager.Tests;

/// <summary>
/// The repository manifest is what Jellyfin reads to offer the plugin. Nothing here fails the
/// build when it's wrong, the plugin just quietly never shows up in the catalog.
/// </summary>
public class ManifestTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "manifest.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }

    private static JsonElement Manifest()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "manifest.json")));
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ManifestIsAnArrayWithOnePackage()
    {
        var manifest = Manifest();

        Assert.Equal(JsonValueKind.Array, manifest.ValueKind);
        Assert.Equal(1, manifest.GetArrayLength());
    }

    [Fact]
    public void ManifestCarriesTheFieldsJellyfinReads()
    {
        var package = Manifest()[0];

        foreach (var field in new[] { "guid", "name", "description", "overview", "owner", "category", "versions" })
        {
            Assert.True(package.TryGetProperty(field, out _), $"manifest.json is missing '{field}'");
        }
    }

    [Fact]
    public void GuidMatchesEverywhereItIsWritten()
    {
        // Three copies of the same id: the plugin, the packaging metadata and the manifest. If
        // they drift, Jellyfin treats the installed plugin and the catalog entry as different
        // plugins and updates silently stop working.
        var root = RepoRoot();
        var manifestGuid = Manifest()[0].GetProperty("guid").GetString();

        var buildYaml = File.ReadAllText(Path.Combine(root, "build.yaml"));
        var buildGuid = Regex.Match(buildYaml, @"guid:\s*""?([0-9a-fA-F-]{36})""?").Groups[1].Value;

        Assert.Equal(ThemeManagerPlugin.PluginGuid, manifestGuid);
        Assert.Equal(ThemeManagerPlugin.PluginGuid, buildGuid);
    }

    [Fact]
    public void EveryPublishedVersionLooksInstallable()
    {
        foreach (var version in Manifest()[0].GetProperty("versions").EnumerateArray())
        {
            var number = version.GetProperty("version").GetString();
            Assert.True(Version.TryParse(number, out _), $"'{number}' is not a version number");

            var abi = version.GetProperty("targetAbi").GetString();
            Assert.True(Version.TryParse(abi, out _), $"'{abi}' is not a target ABI");

            var source = version.GetProperty("sourceUrl").GetString();
            Assert.True(
                Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                $"sourceUrl '{source}' is not an https URL");

            // Jellyfin checks the md5 of the download against this and refuses to install on a
            // mismatch, so an empty or short one means a broken release.
            var checksum = version.GetProperty("checksum").GetString();
            Assert.True(checksum?.Length == 32, $"checksum '{checksum}' is not an md5");
        }
    }
}
