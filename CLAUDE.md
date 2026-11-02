# CLAUDE.md

Context for Claude Code when working in this repository.

## Project
Multi-tenant OAuth2/OIDC identity platform built as a public portfolio project. Polyglot microservices, asynchronous messaging, resilience, observability and autoscaling. Everything runs locally (Docker Compose, kind/k3d) and is ready for Azure and AWS through Terraform.

- Architecture: `docs/architecture/c4-context.md`, `docs/architecture/c4-containers.md`
- Build plan and definitions of done: `docs/build-plan.md`
- Layout and conventions: `docs/repository-structure.md`
- Decisions: `docs/adr/`

## Language rule
**Everything committed to this repository is written in English**: code, comments, identifiers, commit messages, PR descriptions, docs, ADRs, specs and agent files. The developer may talk to you in Spanish; answer in the conversation language, but write all files in English.

## Services
| Service | Path | Stack |
| --- | --- | --- |
| Gateway | `services/gateway` | .NET 10, YARP |
| Identity | `services/identity` | .NET 10, OpenIddict, EF Core, PostgreSQL |
| Tenants & Users | `services/tenants-users` | .NET 10, EF Core, PostgreSQL, SCIM 2.0 |
| LDAP Connector | `services/ldap-connector` | Java 21, Spring Boot 3, Spring LDAP |
| Audit | `services/audit` | Go, OpenSearch |
| Risk | `services/risk` | Python 3.12, FastAPI |
| Notifications | `services/notifications` | Node.js 22, TypeScript |
| Sign-in and self-service | `frontends/account-web` | React, Vite, TypeScript |
| Admin console | `frontends/admin-console` | Angular |

## Commands
Docker is the only host requirement (ADR-0005). On Windows, use `./dev.ps1`; with Make, the same names work as `make <command>`.
```powershell
./dev.ps1 infra       # start PostgreSQL, Redis, Kafka, RabbitMQ, OpenSearch, OpenLDAP, Mailpit
./dev.ps1 up          # start infrastructure and services
./dev.ps1 down        # stop everything
./dev.ps1 k8s-up      # local Kubernetes cluster
./dev.ps1 load-test   # k6 sign-in load test
```
Per service, in toolchain containers (`./dev.ps1 run <tool> <dir> <command...>`):
```powershell
./dev.ps1 run dotnet services/identity dotnet test
./dev.ps1 run java services/ldap-connector ./gradlew test
./dev.ps1 run go services/audit go test ./...
./dev.ps1 run python services/risk pytest
./dev.ps1 run node services/notifications npm test
./dev.ps1 run terraform infra/terraform/azure terraform validate
./dev.ps1 run k8s . helm lint deploy/helm/platform
```

## Non-negotiable rules
1. **Tenant isolation.** Every query on tenant-owned data filters by `tenant_id`. The tenant comes from the validated token or SCIM credentials, never from a request body or query string.
2. **Database per service.** A service never reads or writes another service's database.
3. **Outbox for events.** State changes and their events are saved in the same transaction; a relay publishes to Kafka. Never publish directly from a request handler.
4. **Idempotent consumers.** Every consumer deduplicates by `eventId` or message id. SCIM writes honour `Idempotency-Key` and `externalId`.
5. **Resilience on every outbound call.** Explicit timeout, retries with exponential backoff and jitter, and a circuit breaker for external dependencies (LDAP, Risk).
6. **Kafka for events, RabbitMQ for commands** (ADR-0003). Messaging goes through `IEventPublisher` and `ICommandBus`, never a broker client in business code.
7. **No secrets in code or config files.** Use `.env` locally (never committed) and External Secrets in Kubernetes.
8. **Trace context everywhere.** Propagate `traceparent` over HTTP and in message headers.
9. **Security defaults.** PKCE for public clients, short-lived access tokens, refresh token rotation with reuse detection, validated `iss`/`aud`/`exp`, no tokens or credentials in logs.
10. **Docker only.** Build, test, lint and run everything in containers, even if an SDK happens to be on the host. Never install anything on the developer's machine: if something truly cannot run in Docker, say what it is and why, and ask first. New toolchains go in `deploy/compose/docker-compose.tools.yml`; service Dockerfiles are multi-stage and compile inside the image.

## Contracts
- Event and command schemas live in `contracts/` and are changed there first.
- Every event uses the common envelope: `eventId`, `eventType`, `version`, `tenantId`, `occurredAt`, `traceparent`, `data`.
- Breaking changes require a new version, not an edit.
- Use the `new-event` skill to add an event end to end.

## Workflow
- Trunk-based, short PRs, Conventional Commits (`feat(identity): ...`).
- Before opening a PR on security-sensitive code, run the `security-reviewer`, `tenant-isolation-auditor` and `resilience-reviewer` subagents.
- Record cross-cutting decisions as ADRs (`docs/adr/0000-template.md`).
- Use the `verify-phase` skill to check a phase against its definition of done before closing it.
