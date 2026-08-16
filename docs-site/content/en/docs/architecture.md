---
title: Architecture
weight: 6
description: How discovery, routing, and TLS fit together.
---

DockYARP is a .NET application built on **YARP** (reverse proxy) and **Kestrel** (web server), using
**Docker.DotNet** for discovery and **ACME** for certificates.

## Flow

1. **Discovery** watches the Docker daemon (initial listing + event stream) and produces a normalized view of
   the running containers.
2. **Mapping** turns labelled containers into a routing model (routes + clusters), applying precedence, health
   awareness, and reachability.
3. **YARP** is driven from that model: routes and clusters are updated live as containers change.
4. **TLS/ACME** provisions and renews certificates for hosts that declare them; Kestrel selects the right
   certificate per SNI host.
5. **Security middleware** applies HTTPS enforcement, HSTS, headers, access control, and auth.

## Modules

| Project | Responsibility |
|---------|----------------|
| `DockYarp.Core` | Domain models, interfaces, stores (leaf project). |
| `DockYarp.Docker` | Docker discovery + label mapping. |
| `DockYarp.Tls` | ACME + certificates. |
| `DockYarp.Security` | HTTPS enforcement, auth, access control. |
| `DockYarp.AdminApi` | Admin / observability endpoints. |
| `DockYarp.App` | ASP.NET host: YARP, DI, pipeline. |

## Design principle

Reverse-proxy behavior is built on **YARP extension points** (match policies, transforms, dynamic config) and
web-server behavior on **Kestrel / ASP.NET Core** — DockYARP does not reimplement a proxy layer on top of them.
