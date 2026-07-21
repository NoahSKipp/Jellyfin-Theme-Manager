using Jellyfin.Plugin.ThemeManager.Configuration;
using Jellyfin.Plugin.ThemeManager.Models;
using Jellyfin.Plugin.ThemeManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeManager.Api;

[ApiController]
[Route("ThemeManager")]
[Authorize(Policy = "RequiresElevation")]
public class ThemeManagerController : ControllerBase
{
    private const int MaxUploadBytes = 10 * 1024 * 1024;

    private readonly ThemeCatalogService _catalog;
    private readonly ThemeInstaller _installer;
    private readonly CssPublisher _publisher;
    private readonly BrandingAssetService _assets;
    private readonly ILogger<ThemeManagerController> _logger;

    public ThemeManagerController(
        ThemeCatalogService catalog,
        ThemeInstaller installer,
        CssPublisher publisher,
        BrandingAssetService assets,
        ILogger<ThemeManagerController> logger)
    {
        _catalog = catalog;
        _installer = installer;
        _publisher = publisher;
        _assets = assets;
        _logger = logger;
    }

    private static PluginConfiguration Config =>
        (ThemeManagerPlugin.Instance ?? throw new InvalidOperationException("Plugin not loaded")).Configuration;

    private static ThemeManagerPlugin Plugin =>
        ThemeManagerPlugin.Instance ?? throw new InvalidOperationException("Plugin not loaded");

    [HttpGet("State")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ThemeManagerState>> GetState(
        [FromQuery] bool refresh,
        CancellationToken cancellationToken)
    {
        var catalog = await _catalog.GetCatalogAsync(refresh, cancellationToken).ConfigureAwait(false);
        var config = Config;

        return new ThemeManagerState
        {
            Catalog = catalog.Themes,
            CatalogUpdated = catalog.Updated,
            Installed = config.InstalledThemes,
            ActiveThemeId = config.ActiveThemeId,
            EnabledAddonIds = config.EnabledAddonIds,
            Links = config.LinkedStylesheets,
            CustomCss = config.CustomCss,
            ApplyToServerBranding = config.ApplyToServerBranding,
            InlineImportsOnInstall = config.InlineImportsOnInstall,
            CatalogUrl = config.CatalogUrl,
            Branding = config.Branding,
            LastAppliedAt = config.LastAppliedAt
        };
    }

    [HttpPost("Install")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InstalledTheme>> Install(
        [FromBody] InstallRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _installer.InstallFromCatalogAsync(request.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Failed to install {Theme}", request.Id);
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("InstallUrl")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InstalledTheme>> InstallUrl(
        [FromBody] InstallUrlRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _installer
                .InstallFromUrlAsync(request.Name, request.Url, request.Kind, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Failed to install the stylesheet at {Url}", request.Url);
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("Themes/{id}/Update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InstalledTheme>> UpdateTheme(string id, CancellationToken cancellationToken)
    {
        try
        {
            var theme = await _installer.UpdateAsync(id, cancellationToken).ConfigureAwait(false);
            _publisher.Publish();
            return theme;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Failed to update {Theme}", id);
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("Themes/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteTheme(string id)
    {
        if (!_installer.Uninstall(id))
        {
            return NotFound();
        }

        _publisher.Publish();
        return NoContent();
    }

    [HttpPost("Settings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult SaveSettings([FromBody] SettingsRequest request)
    {
        var config = Config;

        // Drop ids that aren't installed, otherwise a stale one from the page quietly produces
        // an empty stylesheet later.
        var installedIds = config.InstalledThemes.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        config.ActiveThemeId = !string.IsNullOrWhiteSpace(request.ActiveThemeId) && installedIds.Contains(request.ActiveThemeId)
            ? request.ActiveThemeId
            : null;
        config.EnabledAddonIds = (request.EnabledAddonIds ?? Array.Empty<string>())
            .Where(installedIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        config.LinkedStylesheets = (request.Links ?? Array.Empty<LinkedStylesheet>())
            .Where(l => !string.IsNullOrWhiteSpace(l.Url))
            .Select(l => new LinkedStylesheet
            {
                Id = string.IsNullOrWhiteSpace(l.Id) ? Guid.NewGuid().ToString("N") : l.Id,
                Name = string.IsNullOrWhiteSpace(l.Name) ? l.Url : l.Name,
                Url = l.Url.Trim(),
                Enabled = l.Enabled
            })
            .ToArray();

        config.CustomCss = request.CustomCss ?? string.Empty;
        config.ApplyToServerBranding = request.ApplyToServerBranding;
        config.InlineImportsOnInstall = request.InlineImportsOnInstall;
        config.CatalogUrl = string.IsNullOrWhiteSpace(request.CatalogUrl) ? null : request.CatalogUrl.Trim();

        if (request.Branding is not null)
        {
            var branding = config.Branding;

            branding.Enabled = request.Branding.Enabled;
            branding.LogoMaxHeight = request.Branding.LogoMaxHeight;
            branding.ApplyLogoToLogin = request.Branding.ApplyLogoToLogin;
            branding.ReplaceWebIcons = request.Branding.ReplaceWebIcons;
            branding.SplashEnabled = request.Branding.SplashEnabled;
            branding.AccentColor = request.Branding.AccentColor;
        }

        Plugin.UpdateConfiguration(config);
        _publisher.Publish();

        return NoContent();
    }

    [HttpPost("Apply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Apply()
    {
        _publisher.Publish();
        return NoContent();
    }

    // Anonymous: it's a stylesheet, and the login page needs it before anyone has signed in.
    [HttpGet("Css")]
    [AllowAnonymous]
    [Produces("text/css")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetCss() => Content(_publisher.Compose(), "text/css");

    [HttpGet("Asset/{kind}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetAsset(string kind)
    {
        if (!BrandingAssetService.TryParseKind(kind, out var parsed))
        {
            return NotFound();
        }

        var asset = BrandingAssetService.Resolve(parsed);
        if (asset is null)
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";

        // Uploaded SVGs are only ever meant to be painted, serve them inert.
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

        return PhysicalFile(asset.Value.Path, asset.Value.ContentType);
    }

    // Body is the raw image.
    [HttpPost("Asset/{kind}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UploadAsset(string kind, CancellationToken cancellationToken)
    {
        if (!BrandingAssetService.TryParseKind(kind, out var parsed))
        {
            return Problem($"Unknown branding asset '{kind}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxUploadBytes)
            {
                return Problem("The image is larger than 10 MB.", statusCode: StatusCodes.Status400BadRequest);
            }

            buffer.Write(chunk, 0, read);
        }

        try
        {
            _assets.Save(parsed, buffer.ToArray());
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        _publisher.Publish();
        return NoContent();
    }

    [HttpDelete("Asset/{kind}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult DeleteAsset(string kind)
    {
        if (!BrandingAssetService.TryParseKind(kind, out var parsed))
        {
            return Problem($"Unknown branding asset '{kind}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        _assets.Clear(parsed);
        _publisher.Publish();
        return NoContent();
    }
}
