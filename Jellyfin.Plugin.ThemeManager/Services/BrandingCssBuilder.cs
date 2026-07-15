using System.Text;
using Jellyfin.Plugin.ThemeManager.Configuration;

namespace Jellyfin.Plugin.ThemeManager.Services;

/// <summary>
/// Builds the CSS that puts the uploaded branding images on screen. Jellyfin's themes point the
/// logo selectors at bundled images with plain background-image rules, so overriding the same
/// selectors is enough.
/// </summary>
public static class BrandingCssBuilder
{
    public static string Build(BrandingSettings branding)
    {
        if (!branding.Enabled)
        {
            return string.Empty;
        }

        var css = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(branding.LogoFile))
        {
            var logo = AssetUrl("logo", branding.AssetRevision);

            css.AppendLine("/* Logo */")
                .AppendLine(".pageTitleWithLogo,")
                .AppendLine(".pageTitleWithDefaultLogo,")
                .AppendLine(".layout-tv .pageTitleWithDefaultLogo {")
                .Append("    background-image: url(\"").Append(logo).AppendLine("\") !important;")
                .AppendLine("    background-position: left center;")
                .AppendLine("    background-repeat: no-repeat;")
                .AppendLine("    background-size: contain;")
                .Append("    height: ").Append(CssLength(branding.LogoMaxHeight)).AppendLine(";")
                .AppendLine("}")
                .AppendLine();

            // Drawer logo is a real <img>, only content: swaps that.
            css.AppendLine(".adminDrawerLogo img {")
                .Append("    content: url(\"").Append(logo).AppendLine("\") !important;")
                .Append("    max-height: ").Append(CssLength(branding.LogoMaxHeight)).AppendLine(";")
                .AppendLine("    width: auto;")
                .AppendLine("}")
                .AppendLine();

            if (branding.ApplyLogoToLogin)
            {
                css.AppendLine(".splashLogo {")
                    .Append("    background-image: url(\"").Append(logo).AppendLine("\") !important;")
                    .AppendLine("}")
                    .AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(branding.AccentColor))
        {
            var accent = CssColor(branding.AccentColor);
            if (accent is not null)
            {
                // Three spellings because the catalog themes don't agree. Scyfin uses
                // --primary-accent-color, Finimalism uses --accent.
                css.AppendLine("/* Accent */")
                    .AppendLine(":root {")
                    .Append("    --accent: ").Append(accent).AppendLine(";")
                    .Append("    --accent-color: ").Append(accent).AppendLine(";")
                    .Append("    --primary-accent-color: ").Append(accent).AppendLine(";")
                    .AppendLine("}")
                    .AppendLine();
            }
        }

        return css.ToString();
    }

    private static string AssetUrl(string kind, int revision) =>
        $"/ThemeManager/Asset/{kind}?v={revision}";

    // Both of these are typed by hand into a text box and end up inside a declaration, so keep
    // them boring or throw them away.
    private static string CssLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "2.4em";
        }

        var trimmed = value.Trim();
        var valid = trimmed.Length <= 16
                    && trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '%');

        return valid ? trimmed : "2.4em";
    }

    private static string? CssColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var valid = trimmed.Length is > 0 and <= 32
                    && trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c is '#' or '(' or ')' or ',' or '.' or '%' or ' ');

        return valid ? trimmed : null;
    }
}
