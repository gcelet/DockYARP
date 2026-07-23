## 1. Reference stack (AG-DEP)

- [x] 1.1 `docker-compose.yml`: add a `dockerproxy` (`tecnativa/docker-socket-proxy`, `CONTAINERS=1`) service
- [x] 1.2 `docker-compose.yml`: DockYarp drops the socket mount, sets `Docker__DockerEndpoint=tcp://dockerproxy:2375`, `depends_on` the proxy
- [x] 1.3 Add `examples/docker-compose.group-add.yml`: direct socket mount + `group_add: ["${DOCKER_GID}"]`
- [x] 1.4 `Smoke` Nuke target logs a clear OK/KO verdict for the probe

## 2. Aspire e2e harness (AG-DEP)

- [x] 2.1 AppHost: add a `dockerproxy` container mounting the socket; DockYarp uses `tcp://dockerproxy:2375`, no socket mount, `WaitFor(dockerproxy)`

## 3. Docs

- [x] 3.1 `docs/deployment.md`: document both modes, recommend the socket proxy, explain obtaining `DOCKER_GID`

## 4. Build & validation

- [x] 4.1 `./build.ps1 Test` green; `openspec validate fix-docker-socket-access --strict`
- [x] 4.2 Runtime: `./build.ps1 Smoke` reaches the sample service — validated in WSL (Smoke Succeeded)
