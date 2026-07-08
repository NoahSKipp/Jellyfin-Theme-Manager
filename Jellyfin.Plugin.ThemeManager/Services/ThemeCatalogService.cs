using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.ThemeManager.Models;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeManager.Services;

/// <summary>
/// Supplies the list of installable themes. The bundled catalog is always there so the browse
/// tab still works with no outbound access; a remote catalog is merged over it if one is set.
/// </summary>
public class ThemeCatalogService
{
    private const string EmbeddedCatalogResource = "Jellyfin.Plugin.ThemeManager.Resources.themes.json";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ThemeCatalogService> _logger;

    public ThemeCatalogService(IHttpClientFactory httpClientFactory, ILogger<ThemeCatalogService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ThemeCatalog> GetCatalogAsync(bool refreshRemote, CancellationToken cancellationToken)
    {
        var catalog = ReadEmbeddedCatalog();

        var plugin = ThemeManagerPlugin.Instance;
        var remoteUrl = plugin?.Configuration.CatalogUrl;
        if (plugin is null || string.IsNullOrWhiteSpace(remoteUrl))
        {
            return catalog;
        }

        var remote = await GetRemoteCatalogAsync(plugin, remoteUrl, refreshRemote, cancellationToken).ConfigureAwait(false);
        return remote is null ? catalog : Merge(catalog, remote);
    }

    public async Task<ThemeCatalogEntry?> FindAsync(string id, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(false, cancellationToken).ConfigureAwait(false);
        return catalog.Themes.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static ThemeCatalog ReadEmbeddedCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedCatalogResource);
        if (stream is null)
        {
            return new ThemeCatalog();
        }

        return JsonSerializer.Deserialize<ThemeCatalog>(stream, _serializerOptions) ?? new ThemeCatalog();
    }

    private async Task<ThemeCatalog?> GetRemoteCatalogAsync(
        ThemeManagerPlugin plugin,
        string url,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var cachePath = plugin.RemoteCatalogPath;

        if (!refresh && File.Exists(cachePath))
        {
            try
            {
                await using var cached = File.OpenRead(cachePath);
                return await JsonSerializer.DeserializeAsync<ThemeCatalog>(cached, _serializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _logger.LogWarning(ex, "Cached theme catalog is unreadable, fetching a fresh copy");
            }
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsHttp(uri))
        {
            _logger.LogWarning("Catalog URL {Url} is not a valid http(s) address", url);
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            var json = await client.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<ThemeCatalog>(json, _serializerOptions);
            if (parsed is null)
            {
                return null;
            }

            await File.WriteAllTextAsync(cachePath, json, cancellationToken).ConfigureAwait(false);
            return parsed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            // Don't let a broken remote catalog take the browse tab down with it.
            _logger.LogWarning(ex, "Could not load the remote theme catalog from {Url}", url);
            return null;
        }
    }

    private static bool IsHttp(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static ThemeCatalog Merge(ThemeCatalog bundled, ThemeCatalog remote)
    {
        var merged = bundled.Themes.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in remote.Themes)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Url))
            {
                continue;
            }

            merged[entry.Id] = entry;
        }

        return new ThemeCatalog
        {
            Version = remote.Version,
            Updated = remote.Updated ?? bundled.Updated,
            Themes = merged.Values.ToList()
        };
    }
}
