# Bundled fonts

The DockYARP documentation site self-hosts these fonts (no external font CDN). Both are licensed under the
**SIL Open Font License, Version 1.1** — the full license text for each is bundled alongside this file.

| Font | Weights (woff2, latin + latin-ext) | Copyright | Source | License |
|------|-------------------------------------|-----------|--------|---------|
| **Space Grotesk** | 400, 500, 600, 700 | © 2020 The Space Grotesk Project Authors | <https://github.com/floriankarsten/space-grotesk> | [OFL-SpaceGrotesk.txt](OFL-SpaceGrotesk.txt) |
| **JetBrains Mono** | 400, 500, 700 | © 2020 The JetBrains Mono Project Authors | <https://github.com/JetBrains/JetBrainsMono> | [OFL-JetBrainsMono.txt](OFL-JetBrainsMono.txt) |

The `woff2` files were generated from the Google Fonts `css2` endpoint (latin and latin-ext subsets). Regenerate
with the same request and the fetch used in the `add-doc-brand-theme` change if the fonts need updating.
