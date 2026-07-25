using Jellyfin.Plugin.ThemeManager.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeManager.Tests;

public class CssCompositionTests
{
    [Fact]
    public void Assemble_LiftsImportsAboveEveryRule()
    {
        // The Ultrachromic presets are nothing but @import lines. Stack an add-on on one and
        // those imports end up below a rule, where the browser drops the lot.
        var result = CssPublisher.Assemble(new[]
        {
            ("theme", "@import url('https://cdn.example/base.css');\n.card { color: red; }"),
            ("addon", "@import url('https://cdn.example/oled.css');\nbody { background: #000; }")
        });

        var firstRule = result.IndexOf(".card", StringComparison.Ordinal);
        var lastImport = result.LastIndexOf("@import", StringComparison.Ordinal);

        Assert.True(lastImport < firstRule, $"An @import survived below a rule:\n{result}");
        Assert.Contains("base.css", result, StringComparison.Ordinal);
        Assert.Contains("oled.css", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_KeepsEachImportOnlyOnce()
    {
        var result = CssPublisher.Assemble(new[]
        {
            ("a", "@import url('https://cdn.example/base.css');"),
            ("b", "@import url('https://cdn.example/base.css');")
        });

        var occurrences = result.Split("base.css").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Assemble_DropsCharsetDeclarations()
    {
        // @charset is only legal as the first bytes of a file, so it can't survive concatenation.
        var result = CssPublisher.Assemble(new[] { ("theme", "@charset \"utf-8\";\n.a { color: red; }") });

        Assert.DoesNotContain("@charset", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".a", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_SkipsEmptyBlocks()
    {
        var result = CssPublisher.Assemble(new[]
        {
            ("empty", string.Empty),
            ("whitespace", "   \n  "),
            ("real", ".a { color: red; }")
        });

        Assert.DoesNotContain("empty", result, StringComparison.Ordinal);
        Assert.DoesNotContain("whitespace", result, StringComparison.Ordinal);
        Assert.Contains(".a", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StripManagedBlock_LeavesTheUsersOwnCssAlone()
    {
        var published = CssPublisher.Assemble(new[] { ("theme", ".a { color: red; }") });
        var combined = "/* ==== jellyfin-theme-manager: managed block, edits here are overwritten ==== */\n"
                       + published
                       + "\n/* ==== end jellyfin-theme-manager ==== */\n\n.mine { color: blue; }";

        Assert.Equal(".mine { color: blue; }", CssPublisher.StripManagedBlock(combined));
    }

    [Fact]
    public void StripManagedBlock_PassesThroughCssWeNeverTouched()
    {
        Assert.Equal(".mine { color: blue; }", CssPublisher.StripManagedBlock(".mine { color: blue; }"));
        Assert.Equal(string.Empty, CssPublisher.StripManagedBlock(null));
    }

    [Fact]
    public void StripManagedBlock_DiscardsAHalfDeletedBlock()
    {
        var mangled = ".mine { color: blue; }\n"
                      + "/* ==== jellyfin-theme-manager: managed block, edits here are overwritten ==== */\n"
                      + ".theme { color: red; }";

        Assert.Equal(".mine { color: blue; }", CssPublisher.StripManagedBlock(mangled));
    }
}
