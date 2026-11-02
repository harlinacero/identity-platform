# C4 · Level 2: Containers

Deployable services, data stores and brokers. Solid arrows are synchronous calls; dotted arrows are asynchronous messaging.

```mermaid
flowchart TB
    user["<b>End user</b><br/>[Person]"]
    admin["<b>Tenant administrator</b><br/>[Person]"]
    rp["<b>Client applications</b><br/>[External system · OIDC]"]
    hr["<b>HR / corporate IdP</b><br/>[External system · SCIM]"]
    ldap["<b>OpenLDAP</b><br/>[External system]"]
    mail["<b>Mailpit / SMTP</b><br/>[External system]"]

    subgraph platform["Multi-tenant identity platform"]
        web["<b>Sign-in and self-service</b><br/>[React + Vite]<br/>Sign-in, MFA, own account"]
        console["<b>Admin console</b><br/>[Angular]<br/>Tenants, users, roles, audit"]
        gw["<b>Gateway</b><br/>[.NET · YARP]<br/>Routing, JWT validation,<br/>rate limiting, correlation id"]

        idp["<b>Identity</b><br/>[.NET · OpenIddict]<br/>OAuth2/OIDC, token issuance,<br/>refresh token rotation, progressive lockout"]
        tu["<b>Tenants & Users</b><br/>[.NET]<br/>Tenants, users, RBAC, SCIM 2.0"]
        lc["<b>LDAP Connector</b><br/>[Java · Spring Boot]<br/>LDAP bind and search"]
        risk["<b>Risk</b><br/>[Python · FastAPI]<br/>Per-login risk score"]
        notif["<b>Notifications</b><br/>[Node.js]<br/>MFA codes and alerts"]
        audit["<b>Audit</b><br/>[Go]<br/>Event ingestion and search"]

        idpdb[("<b>identity-db</b><br/>[PostgreSQL]<br/>Clients, tokens, outbox")]
        tudb[("<b>tenants-db</b><br/>[PostgreSQL]<br/>Tenants, users, roles, outbox")]
        redis[("<b>Redis</b><br/>Sessions, cache,<br/>rate-limit counters")]
        os[("<b>OpenSearch</b><br/>Per-tenant<br/>audit indices")]
        kafka{{"<b>Kafka</b><br/>Events: security.*, user.*"}}
        rabbit{{"<b>RabbitMQ</b><br/>Commands: notifications.*<br/>+ DLQ"}}
    end

    user --> web
    admin --> console
    web -- "HTTPS" --> gw
    console -- "HTTPS" --> gw
    rp -- "OIDC" --> gw
    hr -- "SCIM 2.0" --> gw

    gw -- "/connect/*, /account/*" --> idp
    gw -- "/api/tenants/*, /scim/v2/*" --> tu
    gw -- "/api/audit/*" --> audit
    gw -. "counters" .-> redis

    idp -- "Local credentials,<br/>roles [REST/gRPC]" --> tu
    idp -- "Corporate authentication<br/>[REST · circuit breaker,<br/>local fallback]" --> lc
    idp -- "Risk score<br/>[REST · timeout]" --> risk
    lc -- "LDAP" --> ldap

    idp --> idpdb
    tu --> tudb
    idp -- "Sessions, lockouts" --> redis

    idp -. "security.login.* (outbox)" .-> kafka
    tu -. "user.* (outbox)" .-> kafka
    kafka -. "consume (idempotent)" .-> audit
    kafka -. "consume history" .-> risk
    audit --> os

    idp -. "SendMfaCode" .-> rabbit
    audit -. "SendSecurityAlert" .-> rabbit
    rabbit -. "consume (idempotent,<br/>retry + DLQ)" .-> notif
    notif -- "SMTP" --> mail

    classDef person fill:#08427b,stroke:#052e56,color:#fff
    classDef ext fill:#999999,stroke:#6b6b6b,color:#fff
    classDef svc fill:#1168bd,stroke:#0b4884,color:#fff
    classDef front fill:#438dd5,stroke:#2e6295,color:#fff
    classDef data fill:#2f4f4f,stroke:#1f3535,color:#fff
    class user,admin person
    class rp,hr,ldap,mail ext
    class gw,idp,tu,lc,risk,notif,audit svc
    class web,console front
    class idpdb,tudb,redis,os,kafka,rabbit data
```

## Containers

| Container | Technology | Exposes | Owned data | Publishes | Consumes |
| --- | --- | --- | --- | --- | --- |
| Gateway | .NET 10, YARP | Single HTTPS entry point | — (counters in Redis) | — | — |
| Identity | .NET 10, OpenIddict, EF Core | `/connect/*`, `/.well-known/*`, `/account/*` | identity-db | `security.login.succeeded/failed`, `security.account.locked`, `security.token.refreshed` (Kafka); `SendMfaCode` (RabbitMQ) | — |
| Tenants & Users | .NET 10, EF Core | `/api/tenants`, `/api/users`, `/api/roles`, `/scim/v2` | tenants-db | `user.created/updated/deactivated`, `role.assigned` (Kafka) | — |
| LDAP Connector | Java 21, Spring Boot 3, Spring LDAP | `/ldap/authenticate`, `/ldap/users/{dn}` | — | — | — |
| Risk | Python 3.12, FastAPI | `/risk/score` | Redis (per-user history) | — | `security.login.*` |
| Notifications | Node.js 22, TypeScript | `/health` | — | — | `notifications.send-mfa-code`, `notifications.send-alert` |
| Audit | Go 1.23 | `/api/audit/events` (search) | OpenSearch | `SendSecurityAlert` (RabbitMQ) | `security.*`, `user.*` |
| Sign-in and self-service | React 19, Vite, TypeScript | SPA | — | — | — |
| Admin console | Angular 20 | SPA | — | — | — |

## Key flows

**Sign-in with risk and MFA.** The user sends credentials to Identity through the Gateway. Identity checks the progressive lockout state in Redis, validates against Tenants & Users (or the LDAP Connector if the tenant uses a directory), and asks Risk for a score with a short timeout. If Risk does not answer, the risk is treated as medium and MFA is required. When the score requires it, Identity publishes `SendMfaCode` to RabbitMQ. The outcome is written to the outbox table in the same transaction, and a relay publishes it to Kafka.

**Brute force (demo moment).** Each failure increments per-user and per-IP counters in Redis; the Gateway applies rate limiting and Identity applies progressive lockout. Audit indexes `security.login.failed` and `security.account.locked`, the console shows them in near real time, and Audit sends an alert through RabbitMQ.

**SCIM provisioning.** The HR system creates or deactivates users at `/scim/v2/Users`. The operation is idempotent by `externalId` and by the `Idempotency-Key` header; the change is emitted as a `user.*` event through the outbox.

## Decisions reflected
- Database per service; no service reads another service's database.
- Kafka for facts (immutable, replayable events); RabbitMQ for commands (single effective delivery, DLQ).
- Every internal synchronous call has a timeout, retries with backoff and jitter, and a circuit breaker where the dependency is external.
- W3C trace context (`traceparent`) is propagated over HTTP and through message headers on both brokers.
