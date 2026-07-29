## 1. Options + filter builder (AG-DD)
- [x] 1.1 `DockerDiscoveryOptions`: add `ContainerFilters` (`IDictionary<string, IList<string>>`,
      default empty)
- [x] 1.2 New `DockerFilters.Build`: option map → Docker `IDictionary<string, IDictionary<string, bool>>`;
      drop empty keys/values; return `null` when nothing to filter

## 2. Wire into the source (AG-DD)
- [x] 2.1 `DockerContainerSource`: build the filter in the constructor and pass it to
      `ListContainersAsync` (`ContainersListParameters.Filters`); event stream stays unfiltered

## 3. Tests (AG-DD)
- [x] 3.1 `DockerFilters.Build`: empty → null; single key/value; single key multi-value (OR); multiple keys
      (AND shape); a value containing `=` is preserved verbatim

## 4. Verify (AG-DD)
- [x] 4.1 Nuke `Test` gate green
