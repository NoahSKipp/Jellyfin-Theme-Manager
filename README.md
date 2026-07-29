# Jellyfin Theme Manager

A Jellyfin plugin for installing community CSS themes and rebranding your server, without editing
files inside the Jellyfin installation.

Built for **Jellyfin 10.11** (`net9.0`).

## What it does

- **A bundled theme catalog.** Browse a curated list of community themes and add-ons from the
  dashboard, with author, licence and a link back to the source project. The catalog ships inside
  the plugin, so it works on servers with no outbound access to a package index.
- **Downloads, not just links.** Installing a theme stores a copy in the plugin's data folder and
  rewrites relative image and font URLs to absolute ones, so the theme keeps working when it is
  served from your server instead of the CDN it was written for.
- **Link any stylesheet.** Paste a URL to add it as an `@import` instead. Viewers fetch it from the
  original host and always get the current version.
- **Stackable add-ons.** Pick one theme, then layer optional tweaks on top of it: rounded corners,
  OLED blacks, hover effects, a TV layout. Add-ons that need a particular base theme pull it in
  automatically.
- **Branding.** Replace the logo, the browser tab icon, the installed app icon and the login splash
  screen, and set an accent colour.
- **Your own CSS.** A plain text box, applied last so it wins over everything else.

Everything enabled is composed into a single stylesheet and published through the server's branding
CSS, which is what the web client already fetches on load.

## Installing

### From the plugin catalog

Add this repository in **Dashboard → Plugins → Repositories**:

```
https://raw.githubusercontent.com/NoahSKipp/Jellyfin-Theme-Manager/main/manifest.json
```

Then install **Theme Manager** from the catalog and restart Jellyfin.

### Manually

Download the zip from [Releases](https://github.com/NoahSKipp/Jellyfin-Theme-Manager/releases),
extract it into a `Theme Manager` folder under your Jellyfin `plugins` directory, and restart.

### From source

```bash
dotnet publish Jellyfin.Plugin.ThemeManager/Jellyfin.Plugin.ThemeManager.csproj -c Release -o ./publish
```

Copy `publish/Jellyfin.Plugin.ThemeManager.dll` into a `plugins/Theme Manager/` folder and restart.

## Using it

Open **Dashboard → Plugins → Theme Manager**.

1. **Install** a theme from the Themes grid, then press **Apply** on it.
2. Optionally enable a few **add-ons**.
3. Press **Save**.
4. Reload your clients. Browsers cache the branding stylesheet, so a hard refresh may be needed the
   first time.

The **View CSS** button opens exactly what the plugin is publishing, which is the fastest way to see
why something is not applying.

## How the CSS is applied

The composed stylesheet is written into the server's branding configuration (the same setting behind
**Dashboard → General → Custom CSS**), wrapped in marker comments:

```css
/* ==== jellyfin-theme-manager: managed block, edits here are overwritten ==== */
...
/* ==== end jellyfin-theme-manager ==== */
```

Only that span is ever rewritten. Anything you typed into the dashboard's Custom CSS box yourself
sits after the block, untouched, and still overrides it.

Two details the plugin handles that a copy-and-paste of the same CSS would not:

- **`@import` hoisting.** Several popular themes, every Ultrachromic preset for instance, consist
  of nothing but `@import` lines. CSS silently discards any `@import` that appears after a normal
  rule, so stacking an add-on on top of one of those themes would blank it. The plugin lifts every
  import to the top of the composed sheet and de-duplicates them.
- **URL rebasing.** Relative `url()` targets in a downloaded theme are rewritten against the address
  the theme came from, so its fonts and images still resolve.

If you would rather wire the stylesheet up yourself, turn off **Publish to the server's branding CSS**
in Advanced. The composed sheet stays available at `/ThemeManager/Css`.

### Offline installs

**Inline imported stylesheets when downloading** (Advanced) follows `@import` targets at download
time and splices their contents in, up to four levels deep. Turn it on when your clients cannot
reach jsDelivr or GitHub. Downloads take longer, and picking up upstream changes then needs a
re-download rather than just a browser refresh.

## Branding

| What | How it is replaced |
| --- | --- |
| Logo (top bar, dashboard drawer, loading screen) | CSS, overriding `.pageTitleWithLogo`, `.pageTitleWithDefaultLogo`, `.adminDrawerLogo img` and `.splashLogo` |
| Browser tab icon | Served by the plugin when the browser asks for `favicon*.ico` |
| App icon (home screen / PWA) | Served by the plugin when the browser asks for `touchicon*.png` |
| Splash screen | Written to the server's `SplashscreenLocation` branding setting |
| Accent colour | CSS custom properties, on themes that expose one |

Two caveats worth knowing:

- The **accent colour** is only read by themes that expose it as a CSS variable. Of the catalog
  themes that means Scyfin (`--primary-accent-color`) and Finimalism (`--accent`); ElegantFin has
  no accent variable, and Jellyfin's own built-in themes write their colours as literals, so on a
  stock install it does nothing.
- The **loading screen logo** is drawn before the branding stylesheet has loaded, so the stock logo
  can flash briefly before yours replaces it.

Uploaded images are identified by their leading bytes rather than by file name, and are served with
`X-Content-Type-Options: nosniff` and a restrictive `Content-Security-Policy`.

## Themes in the catalog

All of them are third-party projects, MIT licensed, and are downloaded from their own repositories.
Credit and bug reports belong upstream:

- [Ultrachromic](https://github.com/CTalvio/Ultrachromic) by CTalvio, with the Monochromic, Novachromic and
  Kaleidochromic presets, plus most of the add-ons
- [ElegantFin](https://github.com/lscambo13/ElegantFin) by lscambo13
- [Finimalism](https://github.com/tedhinklater/finimalism) by tedhinklater
- [Scyfin](https://github.com/loof2736/scyfin) by loof2736, with its colour variants

### Adding your own catalog

Point **Extra catalog URL** at a JSON file you host yourself, shaped like:

```json
{ "themes": [ { "id": "...", "name": "...", "url": "https://...", "kind": "Theme" } ] }
```

Same fields as a [bundled entry](CONTRIBUTING.md#the-schema). Entries are merged over the bundled
list, and an entry that reuses a bundled id replaces it. This is the fast path for a theme only
you use, or for testing one before proposing it upstream. Nothing here needs a PR or a release.

### Getting a theme into the bundled catalog

Want your theme (or someone else's, with their say-so) to ship with the plugin itself, so it
shows up for everyone without them typing a URL? See [CONTRIBUTING.md](CONTRIBUTING.md).

## API

Everything requires an administrator token except where noted.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/ThemeManager/State` | Catalog, installed stylesheets and settings |
| `POST` | `/ThemeManager/Install` | Install a catalog entry |
| `POST` | `/ThemeManager/InstallUrl` | Download a stylesheet from a URL |
| `POST` | `/ThemeManager/Themes/{id}/Update` | Re-download an installed stylesheet |
| `DELETE` | `/ThemeManager/Themes/{id}` | Uninstall |
| `POST` | `/ThemeManager/Settings` | Save the selection and settings |
| `POST` | `/ThemeManager/Apply` | Recompose and republish |
| `GET` | `/ThemeManager/Css` | The composed stylesheet (anonymous) |
| `GET` | `/ThemeManager/Asset/{kind}` | An uploaded branding image (anonymous) |
| `POST` | `/ThemeManager/Asset/{kind}` | Upload a branding image, raw body |
| `DELETE` | `/ThemeManager/Asset/{kind}` | Remove a branding image |

`{kind}` is one of `logo`, `favicon`, `appicon` or `splash`.

## Development

```bash
dotnet build
dotnet test
```

The test suite covers the parts that fail quietly rather than loudly: `@import` hoisting, preserving
the admin's own CSS around the managed block, URL rebasing, the branding CSS selectors, and the
integrity of the bundled catalog.

## Uninstalling

Remove the plugin, then clear the managed block from **Dashboard → General → Custom CSS** if it is
still there. Downloaded themes and uploaded images live in the plugin's data folder and go with it.
