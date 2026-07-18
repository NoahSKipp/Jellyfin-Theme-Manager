using Jellyfin.Plugin.ThemeManager.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeManager.Services;

public enum BrandingAssetKind
{
    Logo,
    Favicon,
    AppIcon,
    Splash
}

public class BrandingAssetService
{
    private readonly ILogger<BrandingAssetService> _logger;

    public BrandingAssetService(ILogger<BrandingAssetService> logger)
    {
        _logger = logger;
    }

    private static ThemeManagerPlugin Plugin =>
        ThemeManagerPlugin.Instance ?? throw new InvalidOperationException("The Theme Manager plugin is not loaded.");

    public static bool TryParseKind(string? value, out BrandingAssetKind kind) =>
        Enum.TryParse(value, ignoreCase: true, out kind);

    public string Save(BrandingAssetKind kind, byte[] content)
    {
        if (content.Length == 0)
        {
            throw new InvalidOperationException("The uploaded image is empty.");
        }

        var (extension, contentType) = Sniff(content)
            ?? throw new InvalidOperationException("Only PNG, JPEG, GIF, WebP, SVG and ICO images are supported.");

        if (kind == BrandingAssetKind.Favicon && extension == ".svg")
        {
            // Safari and most TV browsers ignore SVG favicons, which just looks like the upload
            // didn't work. Better to say so up front.
            throw new InvalidOperationException("SVG favicons are not supported by every browser. Upload a PNG or ICO instead.");
        }

        Directory.CreateDirectory(Plugin.AssetsPath);

        var config = Plugin.Configuration;
        var fileName = kind.ToString().ToLowerInvariant() + extension;

        RemoveStaleFiles(kind, fileName);
        File.WriteAllBytes(Path.Combine(Plugin.AssetsPath, fileName), content);

        var branding = config.Branding;
        switch (kind)
        {
            case BrandingAssetKind.Logo:
                branding.LogoFile = fileName;
                branding.LogoContentType = contentType;
                break;
            case BrandingAssetKind.Favicon:
                branding.FaviconFile = fileName;
                branding.FaviconContentType = contentType;
                break;
            case BrandingAssetKind.AppIcon:
                branding.AppIconFile = fileName;
                branding.AppIconContentType = contentType;
                break;
            case BrandingAssetKind.Splash:
                branding.SplashFile = fileName;
                break;
        }

        branding.AssetRevision++;

        Plugin.UpdateConfiguration(config);
        _logger.LogInformation("Stored branding asset {Kind} as {FileName}", kind, fileName);

        return fileName;
    }

    public void Clear(BrandingAssetKind kind)
    {
        var config = Plugin.Configuration;
        var branding = config.Branding;

        var current = FileNameFor(branding, kind);
        if (current is not null)
        {
            var path = Path.Combine(Plugin.AssetsPath, current);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        switch (kind)
        {
            case BrandingAssetKind.Logo:
                branding.LogoFile = null;
                branding.LogoContentType = null;
                break;
            case BrandingAssetKind.Favicon:
                branding.FaviconFile = null;
                branding.FaviconContentType = null;
                break;
            case BrandingAssetKind.AppIcon:
                branding.AppIconFile = null;
                branding.AppIconContentType = null;
                break;
            case BrandingAssetKind.Splash:
                branding.SplashFile = null;
                branding.SplashEnabled = false;
                break;
        }

        branding.AssetRevision++;
        Plugin.UpdateConfiguration(config);
    }

    public static (string Path, string ContentType)? Resolve(BrandingAssetKind kind)
    {
        var branding = Plugin.Configuration.Branding;
        var fileName = FileNameFor(branding, kind);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = Path.Combine(Plugin.AssetsPath, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var contentType = kind switch
        {
            BrandingAssetKind.Logo => branding.LogoContentType,
            BrandingAssetKind.Favicon => branding.FaviconContentType,
            BrandingAssetKind.AppIcon => branding.AppIconContentType,
            _ => null
        };

        return (path, contentType ?? ContentTypeFor(Path.GetExtension(path)));
    }

    private static string? FileNameFor(BrandingSettings branding, BrandingAssetKind kind) => kind switch
    {
        BrandingAssetKind.Logo => branding.LogoFile,
        BrandingAssetKind.Favicon => branding.FaviconFile,
        BrandingAssetKind.AppIcon => branding.AppIconFile,
        BrandingAssetKind.Splash => branding.SplashFile,
        _ => null
    };

    // Replacing a PNG with an ICO would otherwise leave the PNG sitting there.
    private static void RemoveStaleFiles(BrandingAssetKind kind, string keep)
    {
        var prefix = kind.ToString().ToLowerInvariant() + ".";
        foreach (var path in Directory.EnumerateFiles(Plugin.AssetsPath))
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && !name.Equals(keep, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
            }
        }
    }

    // Work out the type from the bytes, not the file name. We serve this content back out and
    // the name came from whoever uploaded it.
    private static (string Extension, string ContentType)? Sniff(byte[] content)
    {
        static bool Starts(byte[] data, params byte[] signature) =>
            data.Length >= signature.Length && data.Take(signature.Length).SequenceEqual(signature);

        if (Starts(content, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
        {
            return (".png", "image/png");
        }

        if (Starts(content, 0xFF, 0xD8, 0xFF))
        {
            return (".jpg", "image/jpeg");
        }

        if (Starts(content, 0x47, 0x49, 0x46, 0x38))
        {
            return (".gif", "image/gif");
        }

        if (Starts(content, 0x00, 0x00, 0x01, 0x00))
        {
            return (".ico", "image/x-icon");
        }

        if (content.Length >= 12
            && Starts(content, 0x52, 0x49, 0x46, 0x46)
            && content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50)
        {
            return (".webp", "image/webp");
        }

        var head = System.Text.Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 512)).TrimStart();
        if (head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        {
            return (".svg", "image/svg+xml");
        }

        return null;
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };
}
