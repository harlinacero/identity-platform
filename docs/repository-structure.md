# Repository Structure

Monorepo: one place for services, contracts, deployment and infrastructure, with CI that only builds what changed.

```text
identity-platform/
├── README.md
├── CLAUDE.md                        # project context for Claude Code
├── LICENSE
├── dev.ps1                          # project commands on Windows, needs only Docker
├── Makefile                         # the same commands for Linux, macOS and CI
├── .editorconfig
├── .gitattributes
├── .gitignore
├── .env.example                     # local variables, no real secrets
├── .claude/
│   ├── agents/                      # review subagents
│   │   ├── security-reviewer.md
│   │   ├── tenant-isolation-auditor.md
│   │   └── resilience-reviewer.md
│   └── skills/
│       ├── new-event/SKILL.md
│       └── verify-phase/SKILL.md
├── .github/
│   ├── CODEOWNERS
│   ├── pull_request_template.md
│   ├── ISSUE_TEMPLATE/
│   └── workflows/
│       ├── ci.yml                   # detects changes and calls per-language workflows
│       ├── dotnet.yml               # build, xUnit, Testcontainers
│       ├── java.yml                 # Gradle, JUnit, Testcontainers
│       ├── go.yml                   # go test, golangci-lint
│       ├── python.yml               # pytest, ruff, mypy
│       ├── node.yml                 # vitest, eslint
│       ├── frontends.yml            # React and Angular
│       ├── contracts.yml            # Pact and event schema validation
│       ├── security.yml             # Trivy, CodeQL, SonarCloud
│       ├── images.yml               # build and push to GHCR
│       ├── e2e-kind.yml             # deploy to kind + E2E tests
│       └── terraform.yml            # fmt, validate, tflint, checkov
│
├── services/
│   ├── gateway/                     # .NET 10 · YARP
│   │   ├── src/Gateway/
│   │   ├── tests/Gateway.Tests/
│   │   └── Dockerfile
│   ├── identity/                    # .NET 10 · OpenIddict
│   │   ├── src/
│   │   │   ├── Identity.Api/
│   │   │   ├── Identity.Application/
│   │   │   ├── Identity.Domain/
│   │   │   └── Identity.Infrastructure/   # EF Core, outbox, Redis, HTTP clients
│   │   ├── tests/
│   │   │   ├── Identity.UnitTests/
│   │   │   └── Identity.IntegrationTests/
│   │   └── Dockerfile
│   ├── tenants-users/               # .NET 10 · RBAC + SCIM (same clean structure)
│   ├── ldap-connector/              # Java 21 · Spring Boot · Spring LDAP
│   │   ├── src/main/java/...
│   │   ├── src/test/java/...
│   │   ├── build.gradle.kts
│   │   └── Dockerfile
│   ├── audit/                       # Go
│   │   ├── cmd/audit/
│   │   ├── internal/{consumer,store,api}/
│   │   ├── go.mod
│   │   └── Dockerfile
│   ├── risk/                        # Python · FastAPI
│   │   ├── src/risk/
│   │   ├── tests/
│   │   ├── pyproject.toml
│   │   └── Dockerfile
│   └── notifications/               # Node.js · TypeScript
│       ├── src/
│       ├── test/
│       ├── package.json
│       └── Dockerfile
│
├── frontends/
│   ├── account-web/                 # React + Vite: sign-in, MFA, self-service
│   └── admin-console/               # Angular: tenants, users, roles, audit
│
├── building-blocks/
│   └── dotnet/                      # shared internal NuGet packages
│       ├── Platform.Messaging/      # ICommandBus, IEventPublisher, RabbitMQ/Service Bus/SQS/Kafka adapters
│       ├── Platform.Outbox/
│       ├── Platform.Observability/  # shared OpenTelemetry setup
│       └── Platform.Resilience/     # standard Polly policies
│
├── contracts/
│   ├── events/                      # versioned schemas (security.*, user.*)
│   ├── commands/                    # SendMfaCode, SendSecurityAlert
│   ├── openapi/                     # one specification per service
│   └── pacts/                       # generated contracts (not versioned if published to a broker)
│
├── deploy/
│   ├── compose/
│   │   ├── docker-compose.infra.yml         # Postgres, Redis, Kafka, RabbitMQ, OpenSearch, OpenLDAP, Mailpit
│   │   ├── docker-compose.services.yml
│   │   ├── docker-compose.tools.yml         # on-demand SDKs and CLIs (ADR-0005)
│   │   ├── docker-compose.observability.yml # OTel Collector, Prometheus, Grafana, Tempo, Loki
│   │   ├── docker-compose.emulators.yml     # LocalStack, Azurite, Service Bus emulator
│   │   └── seed/                            # PostgreSQL databases, OpenLDAP LDIF, test tenant data
│   ├── tools/
│   │   └── k8s/                             # image with kind, kubectl and Helm
│   ├── k8s/
│   │   ├── kind-config.yaml
│   │   ├── k3d-config.yaml
│   │   └── keda/
│   └── helm/
│       ├── platform/                # umbrella chart
│       ├── gateway/
│       ├── identity/
│       ├── tenants-users/
│       ├── ldap-connector/
│       ├── audit/
│       ├── risk/
│       ├── notifications/
│       └── values/{local,azure,aws}.yaml
│
├── infra/
│   └── terraform/
│       ├── modules/
│       ├── azure/                   # AKS, PostgreSQL, Redis, Service Bus, Event Hubs, Key Vault, ACR
│       └── aws/                     # EKS, RDS, ElastiCache, SQS/SNS, MSK, Secrets Manager, ECR
│
├── observability/
│   ├── otel-collector/
│   ├── grafana/dashboards/
│   ├── prometheus/rules/
│   └── loki/
│
├── tests/
│   ├── e2e/                         # Playwright: sign-in, MFA and console flows
│   └── load/                        # k6: sign-in load and brute force
│
├── scripts/                         # utilities (seed data, dev certificates)
│
└── docs/
    ├── architecture/
    │   ├── c4-context.md
    │   └── c4-containers.md
    ├── adr/
    ├── ai-assisted-development.md
    ├── build-plan.md
    └── repository-structure.md
```

## Conventions
- **English only:** code, comments, commits, docs, ADRs and agent files are written in English.
- **One self-contained folder per service:** its own `Dockerfile`, tests and Helm chart. No service imports code from another; shared code lives in `building-blocks` or `contracts`.
- **Docker only:** builds, tests and tools run in containers, and only Docker is required on the host (ADR-0005).
- **Contracts first:** a change to an event or API starts in `contracts/`, and CI checks backward compatibility.
- **Secrets:** only `.env.example` is committed; in Kubernetes, External Secrets points to Key Vault or Secrets Manager.
- **Branches and commits:** trunk-based with short PRs and Conventional Commits; images are tagged with the commit SHA.
