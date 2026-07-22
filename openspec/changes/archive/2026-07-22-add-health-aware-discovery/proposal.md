## Why

nginx-proxy excludes containers that declare a health check but are not `healthy` (so traffic never lands
on a starting or failing backend). DockYarp ignores container health entirely: it lists running
containers and routes to all of them, and it never reacts to Docker `health_status` events — so an
unhealthy replica keeps receiving traffic and a recovered one is not picked up until an unrelated event.

## What Changes

- Carry a **health status** on the discovered container model (`None`, `Starting`, `Healthy`, `Unhealthy`),
  parsed from the Docker container status.
- **Exclude** containers that are `Unhealthy` or `Starting` from routing (their endpoint is not added);
  containers that are `Healthy` or have **no** health check are routed as before. Healthy replicas of a
  host still form the cluster when a sibling is unhealthy.
- **React to health transitions**: map Docker `health_status` events to a reconcile so a container that
  becomes healthy is picked up and one that turns unhealthy is dropped.

## Capabilities

### Modified Capabilities
- `docker-discovery`: routing is health-aware — only healthy (or health-check-less) containers are served,
  and health transitions trigger re-evaluation.

## Impact

- **Code**: `src/DockYarp.Docker` (`ContainerInfo.Health` + `ContainerHealth`, status/event parsing in
  `DockerContainerSource`, exclusion in `ContainerMapper`).
- **Owning agent**: AG-DD.
