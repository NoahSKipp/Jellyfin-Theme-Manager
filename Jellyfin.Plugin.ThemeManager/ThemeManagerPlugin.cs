using Jellyfin.Plugin.ThemeManager.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ThemeManager;

public class ThemeManagerPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string PluginGuid = "0c47f62a-8d40-4ec2-84e0-a305f01da83d";

    public ThemeManagerPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        Directory.CreateDirectory(ThemesPath);
        Directory.CreateDirectory(AssetsPath);
    }

    // Jellyfin news up plugins itself, so the services and the controller get at the config
    // through here rather than the container.
    public static ThemeManagerPlugin? Instance { get; private set; }

    public override string Name => "Theme Manager";

    public override Guid Id => Guid.Parse(PluginGuid);

    public override string Description =>
        "Browse and install community CSS themes, link your own stylesheets, and replace the logo, icons and splash screen.";

    public string ThemesPath => Path.Combine(DataFolderPath, "themes");

    public string AssetsPath => Path.Combine(DataFolderPath, "assets");

    public string RemoteCatalogPath => Path.Combine(DataFolderPath, "catalog.cache.json");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            // The dashboard builds the page URL out of this, keep it free of spaces.
            Name = "ThemeManager",
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }
}
