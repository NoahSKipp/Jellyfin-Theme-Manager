using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeManager.Services;

/// <summary>
/// Downloads a stylesheet and fixes it up so it still works served from our own origin.
/// </summary>
public partial class StylesheetFetcher
{
    private const int MaxImportDepth = 4;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StylesheetFetcher> _logger;

    public StylesheetFetcher(IHttpClientFactory httpClientFactory, ILogger<StylesheetFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [GeneratedRegex(@"url\(\s*(?<q>['""]?)(?<target>[^'""\)]+)\k<q>\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlTokenRegex();

    [GeneratedRegex(
        @"@import\s+(?:url\(\s*(?<q1>['""]?)(?<t1>[^'""\)]+)\k<q1>\s*\)|(?<q2>['""])(?<t2>[^'""]+)\k<q2>)(?<media>[^;]*);",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImportRegex();

    [GeneratedRegex(@"^\s*@charset\s+[^;]+;", RegexOptions.IgnoreCase)]
    private static partial Regex CharsetRegex();

    public async Task<string> FetchAsync(Uri url, bool inlineImports, int maxBytes, CancellationToken cancellationToken)
    {
        var css = await DownloadAsync(url, maxBytes, cancellationToken).ConfigureAwait(false);
        css = RewriteRelativeUrls(css, url);

        if (inlineImports)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { url.AbsoluteUri };
            css = await InlineImportsAsync(css, maxBytes, visited, 0, cancellationToken).ConfigureAwait(false);
        }

        return css;
    }

    private async Task<string> DownloadAsync(Uri url, int maxBytes, CancellationToken cancellationToken)
    {
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Only http and https stylesheet URLs are supported, got '{url.Scheme}'.");
        }

        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        using var response = await client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw new InvalidOperationException(
                $"The stylesheet at {url} is {response.Content.Headers.ContentLength} bytes, over the {maxBytes} byte limit.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();

        // Content-Length can lie or be missing, so cap it as we read too.
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidOperationException($"The stylesheet at {url} is over the {maxBytes} byte limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return new UTF8Encoding(false).GetString(buffer.ToArray()).TrimStart('﻿');
    }

    /// <summary>
    /// Makes relative url() and @import targets absolute. These themes are written to be loaded
    /// off a CDN, so their fonts and images break the moment we serve the file ourselves.
    /// </summary>
    public static string RewriteRelativeUrls(string css, Uri baseUri)
    {
        return UrlTokenRegex().Replace(css, match =>
        {
            var target = match.Groups["target"].Value.Trim();
            if (!NeedsRebasing(target) || !Uri.TryCreate(baseUri, target, out var absolute))
            {
                return match.Value;
            }

            return $"url(\"{absolute.AbsoluteUri}\")";
        });
    }

    private static bool NeedsRebasing(string target)
    {
        if (target.Length == 0)
        {
            return false;
        }

        // Absolute, protocol relative, data and fragment targets already resolve fine.
        return !target.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
               && !target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               && !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               && !target.StartsWith("//", StringComparison.Ordinal)
               && !target.StartsWith('#');
    }

    private async Task<string> InlineImportsAsync(
        string css,
        int maxBytes,
        HashSet<string> visited,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth >= MaxImportDepth)
        {
            return css;
        }

        var matches = ImportRegex().Matches(css);
        if (matches.Count == 0)
        {
            return css;
        }

        var result = new StringBuilder(css.Length);
        var position = 0;

        foreach (Match match in matches)
        {
            result.Append(css, position, match.Index - position);
            position = match.Index + match.Length;

            var target = match.Groups["t1"].Success ? match.Groups["t1"].Value : match.Groups["t2"].Value;
            var media = match.Groups["media"].Value.Trim();

            if (!Uri.TryCreate(target.Trim(), UriKind.Absolute, out var importUri)
                || (importUri.Scheme != Uri.UriSchemeHttp && importUri.Scheme != Uri.UriSchemeHttps)
                || !visited.Add(importUri.AbsoluteUri))
            {
                // Can't resolve it, or already pulled it in. Leave the line rather than drop a
                // rule the theme might need.
                result.Append(match.Value);
                continue;
            }

            string imported;
            try
            {
                imported = await DownloadAsync(importUri, maxBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Could not inline the imported stylesheet {Url}, leaving it as a link", importUri);
                result.Append(match.Value);
                continue;
            }

            imported = CharsetRegex().Replace(imported, string.Empty);
            imported = RewriteRelativeUrls(imported, importUri);
            imported = await InlineImportsAsync(imported, maxBytes, visited, depth + 1, cancellationToken)
                .ConfigureAwait(false);

            result.Append("\n/* inlined from ").Append(importUri.AbsoluteUri).Append(" */\n");

            if (media.Length > 0)
            {
                result.Append("@media ").Append(media).Append(" {\n").Append(imported).Append("\n}\n");
            }
            else
            {
                result.Append(imported).Append('\n');
            }
        }

        result.Append(css, position, css.Length - position);
        return result.ToString();
    }
}
