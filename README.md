# Identity Platform · Multi-tenant identity platform

A multi-tenant OAuth2/OIDC identity platform built with polyglot microservices (.NET, Java, Go, Python and Node.js) and React and Angular frontends. It demonstrates asynchronous communication, resilience, idempotency, observability and autoscaling, runs entirely on a local machine, and has a deployment path to Azure and AWS through Terraform.

> Status: **Phase 1 · Minimal identity.** Phase 0 is complete. See the [build plan](docs/build-plan.md).

## What it solves
- Centralized sign-in (OIDC with Authorization Code + PKCE) for applications across many tenants.
- Per-tenant users and roles, with automatic provisioning through SCIM 2.0.
- Authentication against corporate LDAP directories, with fallback to local credentials.
- Sign-in risk detection, MFA and brute-force lockout.
- Security auditing searchable in near real time.

## Architecture

```mermaid
flowchart LR
    U[Users and admins] --> FE[React · Angular]
    FE --> GW[Gateway · YARP]
    APPS[OIDC client apps] --> GW
    HR[HR · SCIM] --> GW
    GW --> ID[Identity · OpenIddict]
    GW --> TU[Tenants & Users]
    GW --> AU[Audit · Go]
    ID --> TU
    ID --> LC[LDAP Connector · Spring]
    ID --> RK[Risk · Python]
    LC --> LDAP[(OpenLDAP)]
    ID -. events .-> K{{Kafka}}
    TU -. events .-> K
    K -. .-> AU
    K -. .-> RK
    ID -. commands .-> R{{RabbitMQ}}
    R -. .-> NT[Notifications · Node]
    AU --> OS[(OpenSearch)]
```

Details: [C4 context](docs/architecture/c4-context.md) · [C4 containers](docs/architecture/c4-containers.md) · [repository structure](docs/repository-structure.md).

## Services

| Service | Language | Responsibility |
| --- | --- | --- |
| Gateway | .NET (YARP) | Routing, JWT validation, rate limiting |
| Identity | .NET (OpenIddict) + PostgreSQL | OAuth2/OIDC, token issuance, refresh token rotation |
| Tenants & Users | .NET + PostgreSQL | Organizations, users, RBAC and SCIM |
| LDAP Connector | Java (Spring Boot) | Authentication against OpenLDAP |
| Audit | Go + OpenSearch | Indexes and exposes security events |
| Risk | Python | Per-login risk score |
| Notifications | Node.js | MFA codes and alerts |
| Sign-in and self-service | React | Sign-in, MFA, account management |
| Admin console | Angular | Tenants, users, roles and audit |

## Implemented patterns

| Pattern | Where | Phase |
| --- | --- | --- |
| Rate limiting and progressive lockout | Gateway, Identity | 4 |
| Distributed sessions and cache | Redis | 1–4 |
| Circuit breaker with fallback | Identity → LDAP Connector | 5 |
| Idempotency (SCIM and consumers) | Tenants & Users, Audit, Notifications | 2–4 |
| Transactional outbox | Identity, Tenants & Users | 3 |
| Retries with backoff and jitter, timeouts, DLQ | All clients and consumers | 3–5 |
| Secrets kept out of code | External Secrets, Key Vault, Secrets Manager | 0, 9 |

## Quick start

The only requirement is [Docker](https://docs.docker.com/get-docker/). SDKs, test runners, kind, kubectl, Helm, Terraform and k6 all run in containers ([ADR-0005](docs/adr/0005-docker-only-development.md)).

On Windows, in PowerShell:

```powershell
./dev.ps1 infra     # PostgreSQL, Redis, Kafka, RabbitMQ, OpenSearch, OpenLDAP, Mailpit
./dev.ps1 up        # services and frontends (from phase 1)
./dev.ps1 k8s-up    # local cluster with Helm, HPA and KEDA (phase 8)
./dev.ps1           # list every command
```

On Linux or macOS, the same commands work with Make: `make infra`, `make up`, `make k8s-up`. The first run creates `.env` from `.env.example`.

If you already have a `.env`, compare it with `.env.example` after pulling changes: Compose stops and names any required variable that is missing.

If PowerShell blocks the script, run it with `powershell -ExecutionPolicy Bypass -File .\dev.ps1 infra`.

Run a service's tools without installing them:

```powershell
./dev.ps1 run dotnet services/identity dotnet test
./dev.ps1 run node services/notifications npm test
./dev.ps1 shell go    # interactive shell in a toolchain container
```

| Local URL | Service |
| --- | --- |
| http://localhost:5001 | Identity ([how to try a sign-in](services/identity/README.md)) |
| http://localhost:8025 | Mailpit inbox |
| http://localhost:15672 | RabbitMQ management |
| https://localhost:9200 | OpenSearch API (self-signed certificate) |

## Local and cloud

| Local | Azure | AWS |
| --- | --- | --- |
| Kubernetes (kind) | AKS | EKS |
| Kafka | Event Hubs (Kafka API) | MSK |
| RabbitMQ | Service Bus | SQS + SNS |
| PostgreSQL | Azure Database for PostgreSQL | RDS |
| Redis | Azure Cache for Redis | ElastiCache |
| OpenSearch | Elastic on Azure or self-managed | OpenSearch Service |
| Secrets | Key Vault | Secrets Manager |
| Image registry | ACR | ECR |

## AI-assisted development
The project is built with Claude Code running locally: shared project context in `CLAUDE.md`, review subagents and reusable skills in `.claude/`. No AI APIs are called at runtime or in CI. See [AI-assisted development](docs/ai-assisted-development.md).

## Architecture decision records (ADRs)
- [ADR-0001 · Record architecture decisions](docs/adr/0001-record-architecture-decisions.md)
- [ADR-0002 · Polyglot monorepo](docs/adr/0002-polyglot-monorepo.md)
- [ADR-0003 · Kafka for events, RabbitMQ for commands](docs/adr/0003-kafka-events-rabbitmq-commands.md)
- [ADR-0004 · Local AI-assisted development with Claude Code](docs/adr/0004-local-ai-assisted-development.md)
- [ADR-0005 · Docker as the only host requirement](docs/adr/0005-docker-only-development.md)
- [ADR-0006 · Multi-tenancy strategy](docs/adr/0006-multi-tenancy-strategy.md)

Upcoming: OpenIddict vs. Keycloak or Duende, outbox, lockout and risk policy, local-credential fallback, HPA vs. KEDA, and multi-cloud strategy.

## Demo
A k6 sign-in load test scales Identity on Kubernetes, and a brute-force attempt is blocked live in the audit console. *(Video and screenshots in phase 10.)*

## Author
Harlin Acero · [LinkedIn](https://linkedin.com/in/harlinacero) · [harlinacero.github.io](https://harlinacero.github.io)
