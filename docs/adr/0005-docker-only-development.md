# ADR-0005 · Docker as the only host requirement

- **Status:** accepted
- **Date:** 2026-11-02

## Context
The project spans five language toolchains plus Kubernetes, Terraform and load-testing CLIs. Installing and keeping them at the right versions on every machine is slow and error-prone, and it clutters the developer's computer. The Makefile also assumed Make, which Windows does not ship.

## Decision
Docker is the only program required on the host. Everything else runs in containers:

| Need | How it runs |
| --- | --- |
| Infrastructure | `deploy/compose/docker-compose.infra.yml` |
| Services and frontends | `deploy/compose/docker-compose.services.yml`, built from multi-stage Dockerfiles that compile inside the image |
| SDKs, test runners, k6, Terraform | On-demand containers in `deploy/compose/docker-compose.tools.yml` |
| kind, kubectl, Helm | The `k8s` tool container, built from `deploy/tools/k8s` |

`dev.ps1` (Windows, no installation needed) and the `Makefile` (Linux, macOS, CI) expose the same commands. Both only delegate to Docker Compose, so they hold no logic of their own.

If something cannot run in a container, it is documented with its reason here and the developer is asked before anything is installed.

## Alternatives considered
- Dev Containers: a good editor experience, but it ties the workflow to specific editors. It can be added later on top of these images.
- A single toolbox image with every SDK: one command to learn, but a very large image that rebuilds on any version change.
- Running Make inside a container: it would require translating bind-mount paths between the container and a Windows host, which is fragile. A small PowerShell script is simpler.
- Host-installed SDKs: the fastest builds, but they are what this decision avoids.

## Consequences
- A clean machine needs only Docker and a clone of the repository, and toolchain versions are pinned in one file.
- Bind mounts are slower on Windows, so caches and `node_modules` live in named volumes. File watchers use polling, because file change events do not cross the mount.
- The `dotnet`, `java` and `k8s` tool containers mount the Docker socket for Testcontainers and kind, which gives them full control of the local Docker engine. They only run trusted project code, and the socket is never mounted in application services or in production.
- `dotnet` and `java` set `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` so that tests reach the sibling containers they start.
- Editors without a local SDK lose some language features unless they attach to a container, which is an accepted trade-off for now.
- On Linux hosts, files created by tool containers are owned by root.
