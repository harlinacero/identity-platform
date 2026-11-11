# [001] Tenant-aware sign-in foundation — Technical Specification

> Status: Approved (2026-11-04)
> Based on: `01-business-spec.md` (Approved 2026-11-03)
> Reference implementation followed: none; this is the first code in the repository. It follows `docs/repository-structure.md`, `CLAUDE.md` and ADR-0006.

## 1. Overview
A new .NET 10 service, built on OpenIddict 7 and EF Core with PostgreSQL, implements Authorization Code with PKCE for public clients. The tenant is resolved from `client_id` through a small lookup table, then held in a scoped `ITenantContext`.

Isolation has two layers:
- EF Core global query filters on every tenant-owned entity.
- PostgreSQL Row-Level Security (RLS) with `FORCE`, using a session setting applied by a connection interceptor. The service connects with a role that cannot bypass it.

The RLS proof (UC-05) comes first in the task list. If it fails, work stops as BR-13 requires.

## 2. Components by layer
All paths are under `services/identity/`.

### API (`src/Identity.Api`)
- (+) `Program.cs` — composition root.
- (+) `Endpoints/AuthorizationEndpoints.cs` — `/connect/authorize` passthrough, session check and token issuance.
- (+) `Endpoints/TokenEndpoints.cs` — `/connect/token` passthrough for the code exchange.
- (+) `Endpoints/UserInfoEndpoints.cs` — `/connect/userinfo` passthrough.
- (+) `Endpoints/LoginEndpoints.cs` — `GET` and `POST /account/login`: the minimal HTML form, with antiforgery.
- (+) `Middleware/TenantResolutionMiddleware.cs` — takes `client_id` from the OIDC request, or from the local `returnUrl` on `/account/login`, and sets the tenant context. It reads no other tenant input.
- (+) `OpenIddict/TenantTokenValidationHandler.cs` — OpenIddict handler ordered just before `RestoreTokenEntryProperties`, the first read of a token entry. It reads `tenant_id` from the validated token, rejects tokens without it, rejects a mismatch with the tenant already resolved, and sets the context when none is set (`/connect/userinfo`).
- (+) `Options/IdentitySessionOptions.cs` — `Identity:Session:Duration`.
- (+) `Errors/DataStoreExceptionHandler.cs` — maps data store failures to 503.
- (+) `Dockerfile` at the service root — multi-stage; includes a migrations bundle target.

### Application (`src/Identity.Application`)
- (+) `Tenancy/ITenantContext.cs` — read-only `TenantId`.
- (+) `Tenancy/ITenantContextSetter.cs` — set once per scope; changing it throws.
- (+) `Tenancy/IClientDirectory.cs` — `FindTenantIdAsync(clientId, ct)`.
- (+) `Users/ILocalUserRepository.cs`.
- (+) `Users/ValidateCredentialsHandler.cs` — looks up the user within the current tenant and verifies the password. It hashes against a dummy value when the user doesn't exist, so timing doesn't reveal it.
- (+) `Users/IPasswordVerifier.cs`.

### Domain (`src/Identity.Domain`)
- (+) `Tenancy/ITenantOwned.cs` — `Guid TenantId`.
- (+) `Users/LocalUser.cs` — temporary here; moves to Tenants & Users in Phase 2.

### Infrastructure (`src/Identity.Infrastructure`)
- (+) `Persistence/IdentityDbContext.cs` — global query filters for `ITenantOwned`.
- (+) `Persistence/Entities/TenantApplication.cs`, `TenantAuthorization.cs`, `TenantToken.cs`, `PlatformScope.cs` — OpenIddict entities; the first three add `TenantId`.
- (+) `Persistence/ClientDirectoryEntry.cs`, `ClientDirectory.cs`.
- (+) `Persistence/Interceptors/TenantConnectionInterceptor.cs` — applies `set_config('app.tenant_id', <tenant or ''>, false)` whenever a connection opens.
- (+) `Persistence/Interceptors/TenantWriteInterceptor.cs` — stamps `TenantId` on added entities and throws on a mismatch or when no tenant is set.
- (+) `Persistence/Migrations/` — schema, RLS policies and composite foreign keys.
- (+) `Security/Pbkdf2PasswordVerifier.cs` — wraps `PasswordHasher<T>` from the ASP.NET Core shared framework, with 210,000 iterations.
- (+) `Seeding/DevelopmentSeeder.cs`, `Seeding/SeedOptions.cs` — idempotent; one scope per tenant.

### Tests
- (+) `tests/Identity.UnitTests/`
- (+) `tests/Identity.IntegrationTests/` — `WebApplicationFactory` with Testcontainers PostgreSQL, connecting as the real `identity_app` role.

### Solution and deployment
- (+) `Identity.slnx`, `Directory.Packages.props`, `Directory.Build.props`.
- (+) `deploy/compose/seed/postgres/02-identity-roles.sh`.
- (~) `deploy/compose/docker-compose.services.yml`, `.env.example`.

## 3. API contract
| Method | Route | Auth | Request | Response | Errors |
|--------|-------|------|---------|----------|--------|
| GET | `/connect/authorize` | Session cookie, if any | OIDC parameters; `code_challenge` with `S256` required | 302 to `redirect_uri` with `code`, or 302 to `/account/login` | 400 and no redirect for an unknown client, an unregistered `redirect_uri`, or a missing or `plain` challenge (`invalid_request`) |
| POST | `/connect/token` | None (public client) | `grant_type=authorization_code`, `code`, `client_id`, `redirect_uri`, `code_verifier` | 200 with `access_token`, `token_type`, `expires_in` | 400 `invalid_grant` for a bad verifier, or an expired, used or foreign code; 503 |
| GET, POST | `/connect/userinfo` | Bearer token | — | 200 with `sub`, `tenant_id`, `preferred_username` | 401 |
| GET | `/account/login` | None | `returnUrl`, local and under `/connect/authorize` | 200 HTML form | 400 for any other `returnUrl` |
| POST | `/account/login` | Antiforgery | `username`, `password`, `returnUrl` | 302 to `returnUrl` with the cookie | 200 with the form and one generic error; 503 |
| GET | `/.well-known/openid-configuration`, `/.well-known/jwks` | None | — | OpenIddict metadata and keys | — |
| GET | `/health` | None | — | 200 | 503 when PostgreSQL is unreachable |

Access token claims: `iss`, `aud`, `exp`, `sub`, `tenant_id`, `preferred_username`, `client_id`. Access tokens are signed but not encrypted, so that the Gateway can validate them through JWKS later.

## 4. Data model & persistence
Database: `identity`.

| Table | Tenant-owned | RLS | Notes |
|-------|--------------|-----|-------|
| `local_users` | Yes | Yes | `id`, `tenant_id`, `username`, `normalized_username`, `password_hash`, `created_at`; unique `(tenant_id, normalized_username)` |
| `openiddict_applications` | Yes | Yes | Adds `tenant_id`; `client_id` stays globally unique |
| `openiddict_authorizations` | Yes | Yes | Adds `tenant_id` |
| `openiddict_tokens` | Yes | Yes | Adds `tenant_id` |
| `openiddict_scopes` | No | No | Required by OpenIddict and stays empty; scopes are registered in code |
| `client_directory` | No | No | `client_id` primary key, `tenant_id`; holds only public data |

`tenant_id` has no foreign key to a tenants table, because tenants belong to another service (ADR-0006).

Each RLS table gets:
```sql
ALTER TABLE <t> ENABLE ROW LEVEL SECURITY;
ALTER TABLE <t> FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON <t>
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
```
`NULLIF` is needed because a setting that was reset reads as an empty string, which would otherwise fail the cast.

Tokens and authorizations reference their parents with composite foreign keys that include `tenant_id`, such as `(tenant_id, application_id)`. They are added with SQL in the migration, because OpenIddict's mappings use single-column keys.

Roles, created by `02-identity-roles.sh`:
- `identity_migrator` owns the schema and runs migrations.
- `identity_app` is what the service uses at runtime: `NOSUPERUSER NOBYPASSRLS`, DML on these tables only, `SELECT, INSERT` without `UPDATE` or `DELETE` on `client_directory`, no access to `__EFMigrationsHistory`, and no connection to other services' databases.

## 5. Inter-service impact
| Service | Change | Mechanism (event / HTTP) | Contract |
|---------|--------|--------------------------|----------|
| None | — | — | — |

There are no events yet; sign-in events and their outbox arrive in Phase 3. There are no outbound HTTP calls, so PostgreSQL is the only dependency.

## 6. Configuration & dependencies
| Key | Purpose | Default |
|-----|---------|---------|
| `ConnectionStrings:Identity` | `identity_app` connection | Required |
| `Identity:Issuer` | Token issuer | Required |
| `Identity:Session:Duration` | Session duration (BR-09) | `00:15:00` |
| `Identity:Signing:CertificateBase64`, `Identity:Encryption:CertificateBase64` | Keys | Required in Production; ephemeral keys elsewhere |
| `Seed:TenantA:UserPassword`, `Seed:TenantB:UserPassword` | Seeder secrets | Required when seeding |

The access token and code lifetimes (15 and 5 minutes) are fixed in code, as agreed.

`.env.example` gains these required variables, which also affect existing `.env` files: `IDENTITY_MIGRATOR_PASSWORD`, `IDENTITY_APP_PASSWORD`, `SEED_TENANT_A_USER_PASSWORD`, `SEED_TENANT_B_USER_PASSWORD`. It gains `IDENTITY_PORT` as optional.

Compose adds `identity-migrations` (one-shot bundle) and `identity`, which depends on the migrations finishing. The roles script only runs on an empty PostgreSQL volume, so existing volumes must be recreated with `docker compose down -v`.

**New packages, approved on 2026-11-04:**

| Package | Version | Why |
|---------|---------|-----|
| `OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore` | 7.8.0 | OIDC server and stores |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | PostgreSQL provider |
| `Microsoft.EntityFrameworkCore.Design` | 10.x | Migrations |
| `xunit.v3` | 4.0.2 | Tests |
| `Testcontainers.PostgreSql` | 4.16.0 | Real database in tests |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.x | In-process API tests |
| `NSubstitute` | 6.2.0 | Mocks at boundaries |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.x | Expiry tests without waiting |

## 7. Security
- **PKCE:** required for every client, with `S256` only. Clients are public and have no secret.
- **Tenant source:** only `client_id` during sign-in and the validated `tenant_id` claim afterwards. Any other tenant parameter is ignored.
- **Login `returnUrl`:** must be local and under `/connect/authorize`, which prevents open redirects. Tenant selection through it grants nothing, because credentials are checked only within that tenant (ADR-0006).
- **Session cookie:** `HttpOnly`, `SameSite=Lax`, and `Secure` outside Development and Testing; absolute expiry with no sliding. It carries `tenant_id`, and `/connect/authorize` treats a session from another tenant as absent.
- **OpenIddict entity cache:** disabled with `DisableEntityCaching()`. It is in-process and its keys don't include the tenant, so it would bypass both isolation layers.
- **Passwords:** PBKDF2-HMAC-SHA512 with 210,000 iterations, the current OWASP guidance.
- **HTTPS:** enforced by OpenIddict, and relaxed only in Development and Testing, where no TLS proxy exists yet.
- **Logs:** never include passwords, codes, verifiers or tokens. OpenIddict logs request parameters at Information level, so `appsettings.json` keeps OpenIddict, EF Core and ASP.NET Core at Warning; a test checks the logs of a complete sign-in.
- **Seeder:** runs only when the environment is Development or Testing. There is no flag that could turn it on in Production.

## 8. Error handling & logging
| Failure | Behaviour |
|---------|-----------|
| PostgreSQL unreachable or timing out | 5-second command timeout, up to 3 retries through EF Core's retrying strategy, then 503; no session, code or token is created |
| Unknown client | No tenant is set, so the lookup finds nothing and OpenIddict rejects it without redirecting |
| No tenant at write time | `TenantWriteInterceptor` throws, and RLS rejects it independently |
| Token without `tenant_id` | Rejected by `TenantTokenValidationHandler`; 401 at userinfo |
| Missing seed secret | Options validation fails at startup and names the key, not the value |
| Failed login | Information-level log with the tenant and client, without the username |

`traceparent` is handled by ASP.NET Core for inbound HTTP; there are no outbound calls or messages yet.

## 9. Traceability
All tests are integration tests against real PostgreSQL unless stated otherwise.

| AC | Components | Test level |
|----|------------|------------|
| AC-01, AC-02 | `DevelopmentSeeder` | Integration |
| AC-03 | `SeedOptions` validation | Integration (startup) |
| AC-04 | `DevelopmentSeeder` | Integration (Production environment) |
| AC-05 | Authorization, login and token endpoints | Integration, full flow |
| AC-06, AC-07, AC-32 | Session cookie, `AuthorizationEndpoints`, `IdentitySessionOptions` | Integration, with `FakeTimeProvider` for AC-32 |
| AC-08 | `TenantResolutionMiddleware` | Integration |
| AC-09, AC-13, AC-14 | `LoginEndpoints`, `ValidateCredentialsHandler` | Integration; unit for the dummy hash |
| AC-10, AC-11, AC-12 | OpenIddict server configuration | Integration |
| AC-15, AC-16, AC-17, AC-18 | OpenIddict, `TenantTokenValidationHandler` | Integration |
| AC-19 | `DataStoreExceptionHandler`, retry settings | Integration, with the container stopped |
| AC-20 | `Pbkdf2PasswordVerifier` | Unit and integration |
| AC-21, AC-22, AC-23, AC-24 | Userinfo endpoint, `TenantTokenValidationHandler` | Integration; AC-22 signs a test token with the test key |
| AC-25, AC-26, AC-27 | RLS policies, interceptors | Integration, using `IgnoreQueryFilters()` to leave out layer 1 |
| AC-28 | `TenantConnectionInterceptor`, connection pooling | Integration, on a single-connection pool |
| AC-29, AC-31 | Everything above | Integration; a catalog test lists every table with `tenant_id` and fails if RLS or `FORCE` is off |
| AC-30 | `02-identity-roles.sh` | Integration, querying `pg_roles` and table ownership |

## 10. Risks, trade-offs & decisions
- **D-01 — The tenant setting is applied per connection, not with `SET LOCAL`. Approved on 2026-11-04, with the risk accepted subject to the tests; recorded as an amendment to ADR-0006.** OpenIddict's stores read outside transactions, so `SET LOCAL` would have no effect on them. The alternative, one transaction per request, would hold a connection during password hashing. EF Core opens connections per operation, the interceptor always overwrites the value when one opens, and Npgsql resets connections when they return to the pool. The goal of the ADR is kept, and AC-28 tests it. **Limit:** this is incompatible with PgBouncer in transaction mode, which matters for the cloud phase.
- **D-02 — `client_directory` is a table without RLS.** The tenant must be known before tenant-protected data can be read, so client lookup can't sit behind RLS. Alternatives rejected: a `SECURITY DEFINER` function, which needs an owner that bypasses `FORCE`; and an exception on the applications table, which would expose its other columns. The table holds only public values, and the runtime role cannot change existing rows.
- **D-03 — Userinfo sets the tenant inside OpenIddict's pipeline.** OpenIddict looks up the token entry before any endpoint code runs (first in `RestoreTokenEntryProperties`), so the context comes from the signature-validated token in a handler ordered just before it.
- **D-04 — No token pruning in this feature.** OpenIddict's pruning runs across tenants and would need tenant context per unit of work. Expired rows accumulate until a later feature adds it.
- **R-01 — RLS with OpenIddict: closed on 2026-11-04.** Complete sign-ins pass with RLS enforced and through the runtime role, also with every EF Core filter removed. Removing RLS from a table makes the proof fail. See the T-28 notes in `03-tasks.md`.
- **R-02 — Retries and OpenIddict transactions: closed on 2026-11-04.** No conflict; code replay and outage tests pass with retries on.
- **R-03 — `up --wait` with a one-shot service: closed on 2026-11-05.** `./dev.ps1 up` exits 0 from empty volumes, with the migrations container exiting 0.
- **R-04 — Sessions and antiforgery tokens don't survive a restart** until data protection keys are persisted. This is acceptable with 15-minute sessions and a temporary form.
- **R-05 — The temporary `/account/login` form is replaced by React later.** Its endpoints are kept small for that reason.
