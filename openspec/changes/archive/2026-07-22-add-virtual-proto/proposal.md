## Why

nginx-proxy's `VIRTUAL_PROTO` selects the backend protocol (http, https, and others). DockYarp always
targets `http://`, so it cannot proxy to HTTPS backends. This adds the backend scheme to the model and
label parsing (http/https now; grpc/fastcgi/uwsgi deferred).

## What Changes

- Add a **backend scheme** to the cluster endpoint / routing model (default `http`).
- Parse `VIRTUAL_PROTO` (values `http`, `https`) from container labels; build the endpoint address with the
  chosen scheme; unknown values are logged and fall back to `http`.
- The YARP mapping uses the endpoint scheme for the destination address.

## Capabilities

### Modified Capabilities
- `proxy-routing`: cluster endpoints carry a backend scheme.
- `docker-discovery`: `VIRTUAL_PROTO` selects the backend scheme.

## Impact

- **Code**: `src/DockYarp.Core` (endpoint scheme), `src/DockYarp.Docker` (parse `VIRTUAL_PROTO`, build
  address), `src/DockYarp.App/ReverseProxy` (destination uses the scheme).
- **Deferred**: `grpc`, `grpcs`, `fastcgi`, `uwsgi` protocols.
- **Owning agent**: AG-DD / AG-RP.
