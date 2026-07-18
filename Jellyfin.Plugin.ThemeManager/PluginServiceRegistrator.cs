using Jellyfin.Plugin.ThemeManager.Hosting;
using Jellyfin.Plugin.ThemeManager.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ThemeManager;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ThemeCatalogService>();
        serviceCollection.AddSingleton<StylesheetFetcher>();
        serviceCollection.AddSingleton<ThemeInstaller>();
        serviceCollection.AddSingleton<CssPublisher>();
        serviceCollection.AddSingleton<BrandingAssetService>();

        serviceCollection.AddSingleton<IStartupFilter, ThemeManagerStartupFilter>();
    }
}
