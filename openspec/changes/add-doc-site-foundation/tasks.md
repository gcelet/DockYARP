## 1. Scaffold (AG-DOC)
- [x] 1.1 Create `docs-site/` from the adopted draft (Hugo config, brand SCSS, partials, favicons/images,
      `package.json`) with a `docs-site/.gitignore` (public/, resources/, .hugo_build.lock, node_modules/)
- [x] 1.2 `hugo.toml`: repo `gcelet/DockYARP`, `github_subdir = docs-site`, swappable GitHub Pages `baseURL`,
      drop the duplicate menu entry, fix `disableKinds`, Docsy via Git submodule (`theme = docsy/theme`, no Go)

## 2. Theme (AG-DOC)
- [x] 2.1 First-class light **and** dark themes: explicit dark overrides in the brand SCSS (surfaces, content,
      sidebar, code, borders); `showLightDarkModeMenu` enabled

## 3. Content (AG-DOC)
- [x] 3.1 Information architecture sections: Getting Started, Configuration, Architecture, Deployment,
      Contributing
- [x] 3.2 Rewrite starter content with the real labels (`VIRTUAL_HOST`/`VIRTUAL_PORT`/`LETSENCRYPT_HOST`/
      `DOCKYARP_*`); remove the admin-portal page/link
- [x] 3.3 `docs-site/README` with the install + build steps (Hugo Extended, Docsy submodule, npm-via-fnm)

## 4. Verify (AG-DOC)
- [x] 4.1 .NET `Nuke Test` gate green (unchanged — no code touched)
- [x] 4.2 Site build verified locally: Docsy submodule + Hugo Extended 0.164 → `hugo serve` builds 12 pages,
      served at `/DockYARP/`; light/dark navbar corrected. (CI publish is `add-doc-ci-publish`.)
