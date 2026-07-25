using Jellyfin.Plugin.ThemeManager.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeManager.Tests;

public class StylesheetFetcherTests
{
    private static readonly Uri _source = new("https://cdn.example/gh/someone/theme/theme.css");

    [Theory]
    [InlineData("url(fonts/a.woff2)", "https://cdn.example/gh/someone/theme/fonts/a.woff2")]
    [InlineData("url('../img/bg.png')", "https://cdn.example/gh/someone/img/bg.png")]
    [InlineData("url(\"/root.png\")", "https://cdn.example/root.png")]
    public void RewriteRelativeUrls_RebasesAgainstTheSource(string declaration, string expected)
    {
        var result = StylesheetFetcher.RewriteRelativeUrls($".a {{ background: {declaration}; }}", _source);

        Assert.Contains(expected, result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("url(https://other.example/a.png)")]
    [InlineData("url(//other.example/a.png)")]
    [InlineData("url(data:image/png;base64,iVBORw0KGgo=)")]
    [InlineData("url(#gradient)")]
    public void RewriteRelativeUrls_LeavesResolvableTargetsAlone(string declaration)
    {
        var css = $".a {{ background: {declaration}; }}";

        Assert.Equal(css, StylesheetFetcher.RewriteRelativeUrls(css, _source));
    }

    [Fact]
    public void RewriteRelativeUrls_HandlesImportsToo()
    {
        var result = StylesheetFetcher.RewriteRelativeUrls("@import url(parts/base.css);", _source);

        Assert.Contains("https://cdn.example/gh/someone/theme/parts/base.css", result, StringComparison.Ordinal);
    }
}
