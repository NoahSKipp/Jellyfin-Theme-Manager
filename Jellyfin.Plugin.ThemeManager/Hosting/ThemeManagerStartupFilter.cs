using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.ThemeManager.Hosting;

// Jellyfin gives plugins no hook into its pipeline, but startup filters resolved from the
// container wrap whatever the host configures. That puts us ahead of the static file handler
// that would otherwise serve the stock icons.
public class ThemeManagerStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<BrandingAssetMiddleware>();
        next(app);
    };
}
