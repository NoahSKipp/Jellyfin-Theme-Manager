using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.ThemeManager.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeManager.Services;

public class ThemeInstaller
{
    private readonly ThemeCatalogService _catalog;
    private readonly StylesheetFetcher _fetcher;
    private readonly ILogger<ThemeInstaller> _logger;

    public ThemeInstaller(ThemeCatalogService catalog, StylesheetFetcher fetcher, ILogger<ThemeInstaller> logger)
    {
        _catalog = catalog;
        _fetcher = fetcher;
        _logger = logger;
    }

    private static ThemeManagerPlugin Plugin =>
        ThemeManagerPlugin.Instance ?? throw new InvalidOperationException("The Theme Manager plugin is not loaded.");

    public async Task<InstalledTheme> InstallFromCatalogAsync(string id, CancellationToken cancellationToken)
    {
        var entry = await _catalog.FindAsync(id, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"No catalog entry with id '{id}'.");

        foreach (var requiredId in entry.Requires)
        {
            if (!IsInstalled(requiredId))
            {
                _logger.LogInformation("Installing {Required}, required by {Theme}", requiredId, entry.Id);
                await InstallFromCatalogAsync(requiredId, cancellationToken).ConfigureAwait(false);
            }
        }

        var config = Plugin.Configuration;
        var css = await _fetcher.FetchAsync(
            ParseUrl(entry.Url),
            config.InlineImportsOnInstall,
            config.MaxDownloadSizeBytes,
            cancellationToken).ConfigureAwait(false);

        var theme = new InstalledTheme
        {
            Id = entry.Id,
            Name = entry.Name,
            Author = entry.Author,
            Homepage = entry.Homepage,
            SourceUrl = entry.Url,
            Kind = entry.Kind,
            FromCatalog = true,
            ImportsInlined = config.InlineImportsOnInstall
        };

        return Save(theme, css);
    }

    public async Task<InstalledTheme> InstallFromUrlAsync(
        string name,
        string url,
        ThemeKind kind,
        CancellationToken cancellationToken)
    {
        var uri = ParseUrl(url);
        var config = Plugin.Configuration;

        var css = await _fetcher.FetchAsync(
            uri,
            config.InlineImportsOnInstall,
            config.MaxDownloadSizeBytes,
            cancellationToken).ConfigureAwait(false);

        var theme = new InstalledTheme
        {
            Id = "custom-" + ShortHash(uri.AbsoluteUri),
            Name = string.IsNullOrWhiteSpace(name) ? DeriveName(uri) : name.Trim(),
            SourceUrl = uri.AbsoluteUri,
            Kind = kind,
            FromCatalog = false,
            ImportsInlined = config.InlineImportsOnInstall
        };

        return Save(theme, css);
    }

    public async Task<InstalledTheme> UpdateAsync(string id, CancellationToken cancellationToken)
    {
        var existing = Find(id) ?? throw new InvalidOperationException($"'{id}' is not installed.");
        var config = Plugin.Configuration;

        var css = await _fetcher.FetchAsync(
            ParseUrl(existing.SourceUrl),
            config.InlineImportsOnInstall,
            config.MaxDownloadSizeBytes,
            cancellationToken).ConfigureAwait(false);

        existing.ImportsInlined = config.InlineImportsOnInstall;
        return Save(existing, css);
    }

    public bool Uninstall(string id)
    {
        var config = Plugin.Configuration;
        var existing = Find(id);
        if (existing is null)
        {
            return false;
        }

        var path = Path.Combine(Plugin.ThemesPath, existing.FileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        config.InstalledThemes = config.InstalledThemes.Where(s => s.Id != id).ToArray();
        config.EnabledAddonIds = config.EnabledAddonIds.Where(a => a != id).ToArray();

        if (string.Equals(config.ActiveThemeId, id, StringComparison.Ordinal))
        {
            config.ActiveThemeId = null;
        }

        Plugin.UpdateConfiguration(config);
        return true;
    }

    public string ReadCss(InstalledTheme theme)
    {
        var path = Path.Combine(Plugin.ThemesPath, theme.FileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning("Cached stylesheet for {Theme} is missing from {Path}", theme.Id, path);
            return string.Empty;
        }

        return File.ReadAllText(path);
    }

    public static InstalledTheme? Find(string id) =>
        Plugin.Configuration.InstalledThemes.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    private static bool IsInstalled(string id) => Find(id) is not null;

    private InstalledTheme Save(InstalledTheme theme, string css)
    {
        var config = Plugin.Configuration;
        var bytes = Encoding.UTF8.GetBytes(css);

        theme.FileName = SafeFileName(theme.Id);
        theme.SizeBytes = bytes.LongLength;
        theme.Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var existing = config.InstalledThemes.FirstOrDefault(s => s.Id == theme.Id);
        if (existing is null)
        {
            theme.InstalledAt = DateTime.UtcNow;
            config.InstalledThemes = config.InstalledThemes.Append(theme).ToArray();
        }
        else
        {
            theme.InstalledAt = existing.InstalledAt;
            theme.UpdatedAt = DateTime.UtcNow;
            config.InstalledThemes = config.InstalledThemes.Select(s => s.Id == theme.Id ? theme : s).ToArray();
        }

        Directory.CreateDirectory(Plugin.ThemesPath);
        File.WriteAllBytes(Path.Combine(Plugin.ThemesPath, theme.FileName), bytes);

        Plugin.UpdateConfiguration(config);
        _logger.LogInformation("Installed stylesheet {Theme} ({Size} bytes)", theme.Id, theme.SizeBytes);

        return theme;
    }

    private static Uri ParseUrl(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"'{url}' is not a valid http or https URL.");
        }

        return uri;
    }

    private static string DeriveName(Uri uri)
    {
        var name = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(name) ? uri.Host : name;
    }

    // Ids can come from a remote catalog, so strip anything that isn't a word character before
    // this goes near the file system.
    private static string SafeFileName(string id)
    {
        var cleaned = new string(id.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        return (cleaned.Length == 0 ? ShortHash(id) : cleaned) + ".css";
    }

    private static string ShortHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12].ToLowerInvariant();
}
