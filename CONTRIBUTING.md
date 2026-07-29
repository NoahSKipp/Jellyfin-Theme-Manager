# Contributing a theme to the catalog

The catalog is the list of themes and add-ons that ship inside the plugin itself and show up
in the Themes / Add-ons grid without anyone typing a URL. It lives as one JSON file per entry
under [`Jellyfin.Plugin.ThemeManager/Resources/catalog/`](Jellyfin.Plugin.ThemeManager/Resources/catalog),
named after the entry's own id, e.g. `scyfin-oled.json`. One file per entry so two people adding
different themes in parallel don't collide on the same file.

This is for getting a theme into the bundled, ships-with-the-plugin list. If you just want your
own server to see a theme nobody else has, you don't need any of this, just paste the stylesheet URL
directly into the plugin's dashboard, or point **Extra catalog URL** at your own JSON (see the
[README](README.md#adding-your-own-catalog)).

## Before you open a PR

- **The stylesheet has to be yours, or you need to be sure the author is fine with it being
  linked from here.** The catalog only ever links to the theme's own hosting (jsDelivr off
  GitHub, in every existing entry), nothing is copied into this repo, but that's still
  pointing other people's Jellyfin servers at someone else's project, so don't add a theme you
  found without checking it's actually meant to be used this way (an MIT/Apache/BSD-licensed
  repo with the CSS as a normal tracked file is the easy case).
- **The `url` has to be a direct, stable link to raw CSS**, not a GitHub blob page or a URL that
  redirects. jsDelivr's `cdn.jsdelivr.net/gh/<owner>/<repo>/<path>` pattern is what every current
  entry uses and is a safe default; it's a CDN in front of the repo, not a copy, so it moves as
  the source moves.
- **Test it actually downloads and composes.** `dotnet test` runs the checks below, but it can't
  tell you the CSS is well-formed or that it looks right. For that, install the plugin locally
  against a Jellyfin server, add your entry, and use the **View CSS** button to see what actually
  gets published.

## The schema

```json
{
  "id": "my-theme",
  "name": "My Theme",
  "author": "their-github-username",
  "description": "One sentence, what it looks like and what's distinctive about it.",
  "homepage": "https://github.com/their-username/their-theme",
  "license": "MIT",
  "url": "https://cdn.jsdelivr.net/gh/their-username/their-theme/theme.css",
  "kind": "Theme",
  "family": "My Theme",
  "requires": [],
  "tags": ["dark", "minimal"],
  "swatch": ["#101010", "#2b2b2b", "#00a4dc"]
}
```

| Field | Required | Notes |
| --- | --- | --- |
| `id` | yes | Lowercase, hyphenated, matches the file name exactly (`my-theme.json` → `"id": "my-theme"`). Stable forever once merged, changing it later is a breaking change for anyone with it installed. |
| `name` | yes | Shown on the card. |
| `author` | yes | Credited on the card. Their GitHub username is fine. |
| `description` | no | One sentence. Shows on the card if set. |
| `homepage` | no | Linked as "Source" on the card. Almost always worth setting. |
| `license` | no | The upstream project's actual licence, not a guess. |
| `url` | yes | Direct https CSS link. See above. |
| `kind` | yes | `Theme` or `Addon`, see below. |
| `family` | no | Groups related entries together in the UI (e.g. every Ultrachromic preset shares `"family": "Ultrachromic"`). |
| `requires` | no | Ids of other catalog entries that must be installed for this one to make sense. See below. |
| `tags` | no | Free text, used by the filter box. |
| `swatch` | no | Two or three hex colours, used to draw a gradient card when there's no screenshot. Pick colours actually present in the stylesheet: a background shade and an accent works well. |

## Theme or Addon: how to decide

This isn't detected from the CSS. The plugin doesn't parse or analyse the stylesheet at all,
it downloads it, stores it, and concatenates it with whatever else is enabled. `kind` is a
judgement call you make after actually reading the CSS:

- **`Theme`** replaces the whole look. Only one theme is active at a time; picking a new one
  swaps out the old one.
- **`Addon`** is a small layer on top of whatever theme is active. Any number can be stacked
  together, in the order they were enabled. If it's mostly `@import` lines pulling in a larger
  stylesheet, it's a theme. If it's a focused set of rules doing one thing (rounding corners, an
  OLED colour swap, disabling backdrops) it's an addon.

### `requires`

Some add-ons are only a handful of CSS custom-property overrides and look broken without their
base theme applied first. The Scyfin colour variants are a good example, each one is a dozen
lines of `--primary-accent-color` and nothing else. If your addon is like that, set
`"requires": ["the-base-theme-id"]`. Installing your entry will pull in the required one
automatically. If your addon works standalone against any theme (like the Ultrachromic effects,
glassy panels, rounded corners), leave `requires` empty.

## What CI checks, and what it doesn't

Every push and PR runs `dotnet test`, which includes checks over the whole catalog:

- every entry has an id, a name, an author and an https `url`
- ids are unique
- `requires` points at ids that actually exist in the catalog
- a file's name matches the `id` inside it
- the config page is still embedded where the plugin expects it

None of that verifies the stylesheet renders correctly, that the licence claim is accurate, or
that `kind`/`requires` were judged correctly. That's a human review, same as any other PR.

## One thing worth knowing before you open the PR

**Merging doesn't make your theme available to anyone's server immediately.** The catalog is
compiled into the plugin DLL at build time, not fetched live. It only reaches installed servers
when the next version is tagged and released (see the [release workflow](.github/workflows/release.yml)).
If it's urgent, say so in the PR; otherwise it'll go out with whatever's next.
