## 1. Label parsing (AG-DD)

- [x] 1.1 Add `AuthUser`/`AuthPassword`/`AuthRealm` constants to `DockerLabels`
- [x] 1.2 Add `Auth` (`BasicAuthCredentials?`) to `ContainerLabelConfig`
- [x] 1.3 In `LabelParser`, set `Auth` only when both user and password are present (realm optional); add `HasIncompleteAuth(labels)`

## 2. Mapping (AG-DD)

- [x] 2.1 `HostGroup.BuildRoute` sets `Auth = first.Auth`
- [x] 2.2 Warn (and leave unprotected) when auth labels are incomplete

## 3. Tests & docs (AG-DD)

- [x] 3.1 Parser tests: complete auth → credentials; partial → none
- [x] 3.2 Mapper tests: complete auth → route protected; partial → route unprotected + warning
- [x] 3.3 Document the auth labels in `docs/labels-reference.md`
- [x] 3.4 Build + full test suite green via the Nuke CLI
