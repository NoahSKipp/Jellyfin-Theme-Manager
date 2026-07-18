using Jellyfin.Plugin.ThemeManager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeManager.Hosting;

/// <summary>
/// Answers the web client's icon requests with the uploaded images. The tab and app icons are
/// plain static files inside jellyfin-web and no stylesheet can reach them, so the only way to
/// swap them without editing the install is to get in front of the static file handler.
/// </summary>
public class BrandingAssetMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BrandingAssetMiddleware> _logger;

    public BrandingAssetMiddleware(RequestDelegate next, ILogger<BrandingAssetMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        (string Path, string ContentType)? asset = null;

        try
        {
            var kind = Match(context.Request.Path.Value);
            if (kind is not null)
            {
                var branding = ThemeManagerPlugin.Instance?.Configuration.Branding;
                if (branding is { Enabled: true, ReplaceWebIcons: true })
                {
                    asset = BrandingAssetService.Resolve(kind.Value);
                }
            }
        }
        catch (Exception ex)
        {
            // This sits in front of every request. If it breaks, fall through to the real file,
            // don't take the web client down.
            _logger.LogError(ex, "Failed while checking for a branding icon override");
            asset = null;
        }

        if (asset is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Response.ContentType = asset.Value.ContentType;
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "public, max-age=3600";
        await context.Response.SendFileAsync(asset.Value.Path, context.RequestAborted).ConfigureAwait(false);
    }

    // Webpack puts a content hash in the middle of favicon.ico, so match on prefix and extension
    // rather than the whole name.
    private static BrandingAssetKind? Match(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return null;
        }

        var directory = path[..lastSlash];
        if (!directory.Equals(string.Empty, StringComparison.Ordinal)
            && !directory.Equals("/web", StringComparison.OrdinalIgnoreCase)
            && !directory.Equals("/web/favicons", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var file = path[(lastSlash + 1)..];

        if (file.StartsWith("favicon", StringComparison.OrdinalIgnoreCase)
            && file.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            return BrandingAssetKind.Favicon;
        }

        if (file.StartsWith("touchicon", StringComparison.OrdinalIgnoreCase)
            && file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return BrandingAssetKind.AppIcon;
        }

        return null;
    }
}
