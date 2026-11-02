# Phased Build Plan

Each phase ends with something demonstrable and a green CI. General rule: **a phase is not done unless it runs with `./dev.ps1 up` or `make up` on a clean machine that has only Docker installed (ADR-0005), and has automated tests in the pipeline.**

Estimates assume part-time work (about 15–20 hours per week).

| Phase | Name | Visible outcome | Estimate |
| --- | --- | --- | --- |
| 0 | Foundations | Repo, CI and local environment with infrastructure | 1 week |
| 1 | Minimal identity | End-to-end OIDC sign-in with React | 2 weeks |
| 2 | Tenants, RBAC and SCIM | Real multi-tenancy and idempotent provisioning | 2 weeks |
| 3 | Events and auditing | Outbox → Kafka → Audit → OpenSearch | 2 weeks |
| 4 | Sign-in security | Rate limiting, progressive lockout, Risk and MFA | 2 weeks |
| 5 | LDAP and resilience | Corporate sign-in with circuit breaker and fallback | 1–2 weeks |
| 6 | Admin console | Angular with users, roles and live audit | 2 weeks |
| 7 | Observability | Correlated traces, metrics and logs | 1 week |
| 8 | Kubernetes and autoscaling | kind/k3d, Helm, HPA, KEDA and E2E in CI | 2 weeks |
| 9 | Cloud with Terraform | Azure and AWS modules validated in CI | 1–2 weeks |
| 10 | Portfolio polish | Recorded demo, final README, complete ADRs | 1 week |

---

## Phase 0 · Foundations

**Scope**
- Repository structure (see `docs/repository-structure.md`), license, `.editorconfig`, `CODEOWNERS`, PR and issue templates.
- `deploy/compose/docker-compose.infra.yml` with PostgreSQL, Redis, Kafka (KRaft), RabbitMQ, OpenSearch, OpenLDAP and Mailpit.
- `Makefile` with `up`, `down`, `infra`, `test`, `lint`.
- CI workflow that detects which folder changed (`paths-filter`) and runs lint and build per language.
- Conventional Commits and ADR-0001 (record decisions with ADRs).
- `.claude/` folder with project context, review subagents and skills (see ADR-0004).

**Definition of done**
- `make infra` brings up all infrastructure with green healthchecks in under 3 minutes.
- CI runs on every PR and blocks the merge on failure.
- README with the context diagram and getting-started steps.

---

## Phase 1 · Minimal identity

**Scope**
- Identity with OpenIddict: Authorization Code + PKCE, client credentials, refresh token rotation with reuse detection, discovery and JWKS.
- Local users hashed with Argon2id or PBKDF2 (temporarily in Identity; they move in phase 2).
- Gateway with YARP: routing, JWT validation against the JWKS, correlation id header.
- Minimal React frontend: sign-in, callback, profile and sign-out with `oidc-client-ts`.
- Unit tests (xUnit) and integration tests with Testcontainers (PostgreSQL).

**Definition of done**
- A user signs in from React, the Gateway accepts their token and rejects a tampered or expired one.
- Reusing an already-rotated refresh token revokes the whole token family (integration test).
- Coverage ≥ 70% in Identity; ADR on OpenIddict vs. Duende or Keycloak.

---

## Phase 2 · Tenants, RBAC and SCIM

**Scope**
- Tenants & Users service: tenants, users, roles, permissions and assignments.
- Isolation by `tenant_id` with EF Core global query filters and, optionally, PostgreSQL Row-Level Security.
- SCIM 2.0 endpoint (`/Users`, `/Groups`, basic filters, `PATCH`).
- Idempotency: `Idempotency-Key` header with a stored-responses table, and uniqueness by `externalId`.
- Identity resolves credentials and roles through this service; roles travel as claims.

**Definition of done**
- A tenant A user cannot see tenant B data (dedicated integration tests).
- Sending the same SCIM request 10 times leaves one user and returns the same response.
- Postman collection or `.http` file with the full flow; ADR on the multi-tenancy strategy.

---

## Phase 3 · Events and auditing

**Scope**
- Transactional outbox in Identity and Tenants & Users with a relay to Kafka (background worker).
- Versioned event contracts in `contracts/events` (JSON Schema or Avro) with a common envelope: `eventId`, `tenantId`, `occurredAt`, `traceparent`.
- Audit service in Go: idempotent consumer (deduplication by `eventId`), per-tenant index in OpenSearch and a search API.
- Retries with backoff and jitter, and an error topic (DLQ) for unprocessable events.

**Definition of done**
- Stop Kafka, perform sign-ins and bring it back: no event is lost (automated test).
- Delivering the same event twice produces a single document in OpenSearch.
- Pact contract tests between producers and Audit; ADR on the outbox pattern.

---

## Phase 4 · Sign-in security

**Scope**
- Rate limiting in the Gateway per IP and per client, with counters in Redis.
- Progressive lockout in Identity (for example, 5 failures → 1 min, 10 → 15 min) and unlock by an administrator.
- Risk service in Python: score based on new IP, unusual hours and recent failures; Identity calls it with a timeout and fallback.
- Notifications service in Node.js: consumes `SendMfaCode` from RabbitMQ, sends through Mailpit, idempotent consumer, retries and DLQ.
- Command messaging abstraction (`ICommandBus`) with a RabbitMQ implementation and Azure Service Bus and SQS adapters selectable by configuration.
- MFA by email (TOTP as an optional extension).

**Definition of done**
- Brute-force script: past the threshold, responses become 429 or account-locked, and the events show up in Audit.
- With Risk down, sign-in still works and requires MFA.
- A poison message ends up in the DLQ after N retries without blocking the queue.
- ADR on the lockout and risk policy.

---

## Phase 5 · LDAP and resilience

**Scope**
- LDAP Connector in Spring Boot with Spring LDAP against OpenLDAP seeded with test users.
- Per-tenant configuration: `local`, `ldap` or `ldap-with-fallback` mode.
- Circuit breaker and timeouts with Polly in Identity (and Resilience4j inside the connector).
- Optional sync of LDAP attributes into Tenants & Users.

**Definition of done**
- An LDAP user signs in through the same OIDC flow.
- When OpenLDAP is stopped, the circuit opens within seconds and, if the tenant allows it, local credentials are used; the transition is visible in metrics and in Audit.
- JUnit integration tests with Testcontainers (OpenLDAP); ADR on local-credential fallback and its risks.

---

## Phase 6 · Admin console

**Scope**
- Angular app with OIDC sign-in as a client of the platform.
- Management of tenants, users, roles and assignments; account unlock.
- Audit viewer with filters and live updates (SSE or WebSocket from Audit).
- React self-service: password change, MFA and active sessions with revocation.

**Definition of done**
- A tenant administrator manages their tenant without seeing others; a super-administrator sees all of them.
- A brute-force lockout appears in the console in under 5 seconds.
- Unit tests (Jasmine or Vitest) and Playwright E2E tests for the main flow.

---

## Phase 7 · Observability

**Scope**
- OpenTelemetry in all seven services: traces, metrics and logs exported to an OTel Collector.
- Prometheus, Grafana, Tempo (or Jaeger) and Loki in Compose.
- Versioned dashboards: sign-ins per minute, failure rate, p95 latency, consumer lag, circuit breaker state.
- Configurable exporters for Application Insights and CloudWatch.

**Definition of done**
- A sign-in trace crosses Gateway → Identity → Risk → Kafka → Audit and is complete in Tempo.
- You can jump from a log line to its trace and back.
- At least two alerts defined as code (for example, high failure rate and messages in a DLQ).

---

## Phase 8 · Kubernetes and autoscaling

**Scope**
- One Helm chart per service and an umbrella chart; values for `local`, `azure` and `aws`.
- Local cluster with kind or k3d through `make k8s-up`.
- HPA on Identity by CPU and a request metric; KEDA on Notifications and Audit by queue length and Kafka lag.
- Probes, `PodDisruptionBudget`, resource limits, `NetworkPolicy`, and PostgreSQL and Redis replicas where applicable.
- CI: images to GitHub Container Registry, Trivy scanning, deployment to kind inside the pipeline, and E2E tests.
- Load test with k6.

**Definition of done**
- The k6 test scales Identity from 2 to N replicas and back down; this is captured on a dashboard.
- The pipeline deploys to kind and runs E2E on every merge to `main`.
- Trivy reports no critical vulnerabilities; ADR on HPA vs. KEDA.

---

## Phase 9 · Cloud with Terraform

**Scope**
- `infra/terraform/azure`: AKS, Azure Database for PostgreSQL, Azure Cache for Redis, Service Bus, Event Hubs (Kafka API), Key Vault and ACR.
- `infra/terraform/aws`: EKS, RDS, ElastiCache, SQS and SNS, MSK, Secrets Manager and ECR.
- Reusable modules, commented-out remote backends and documented variables.
- Secrets through External Secrets Operator (Key Vault or Secrets Manager) instead of Helm values.
- CI: `terraform fmt -check`, `terraform validate`, `tflint` and `checkov`. Tests against LocalStack and Azurite where possible.

**Definition of done**
- Both stacks pass `validate` and the linters in CI without real credentials.
- `terraform plan` works against LocalStack for the supported AWS resources.
- Estimated cost document per cloud for a demo deployment; ADR on the multi-cloud strategy.

---

## Phase 10 · Portfolio polish

**Scope**
- Final README with diagrams, screenshots, a demo GIF, and a table of patterns linking to the code that implements them.
- Short video (3–5 minutes) of the demo moment: k6 load, autoscaling and live brute-force lockout.
- ADR review, CI and coverage badges, and a section on rejected alternatives.
- Publication on LinkedIn and on harlinacero.github.io.

**Definition of done**
- Someone outside the project clones the repo and sees the demo running in under 15 minutes by following the README.
- Every required pattern in scope links to its code, test and ADR.

---

## Ordering and shortcuts

If time is tight, the **demonstrable core** is phases 0 to 4 plus phase 8: that already covers multi-tenant OIDC, idempotent SCIM, outbox, auditing, brute-force lockout and Kubernetes autoscaling. Phases 5, 6, 7 and 9 add breadth and can be reordered to match the roles being targeted (for example, bring phase 5 forward when a role asks for LDAP or Active Directory).
