# ADR-0006 · Multi-tenancy strategy

- **Status:** accepted
- **Date:** 2026-11-03

## Context
One tenant reading or changing another tenant's data is the most serious failure this platform can have. The build plan first addressed isolation in Phase 2, but Phase 1 already fixes the identity schema, so retrofitting tenancy later would mean migrating clients, tokens and users.

A tenant isolation audit of the design also found questions the documents left open: how the tenant is known before a token exists, whether internal services can trust a caller's tenant, how uniqueness works across tenants, and what a super-administrator is. This ADR settles them before Phase 1 starts.

## Decision
Shared databases with a `tenant_id` column, enforced in two layers, with the tenant always derived from a server-side source.

**Tenant resolution**
- Tenant ids are UUIDs and are validated before use in any key, index name or query.
- During sign-in, the tenant comes from the OIDC `client_id`. Each client belongs to exactly one tenant, recorded server-side. Credentials are only checked against that tenant's users, and a tenant value sent in a sign-in form is ignored.
- After sign-in, it comes from the mandatory `tenant_id` claim. A token without the claim gets 401. SCIM requests use the tenant of the authenticated SCIM client.
- It never comes from a request body, query string, route value or header. If a route contains a tenant id, it must match the token claim unless a super-administrator check passes.
- The platform has one issuer. First-party frontends may choose a tenant's `client_id` from a slug in the URL, because selecting a client grants nothing: credentials are still checked only within that tenant.

**Trust between services**
- Every service validates the JWT itself (`iss`, `aud`, `exp`, `tenant_id`). The Gateway is an additional layer, not the only one.
- The Gateway removes any caller-supplied tenant header.
- Internal calls with no user token, such as Identity asking Tenants & Users for credentials, use a client credentials token that identifies the calling service. Only these internal endpoints accept an explicit tenant parameter, only from an allow-list of named services, and the Gateway does not route them.

**Data layer**
- `tenant_id` is on every tenant-owned table from its first migration, including clients, tokens and local users in Phase 1. `identity-db` stores it without a foreign key, because tenants live in another service's database.
- Layer 1: EF Core global query filters. Layer 2: PostgreSQL Row-Level Security on all tenant-owned tables, with `FORCE ROW LEVEL SECURITY`.
- Services connect with a per-service role that is neither a superuser nor `BYPASSRLS`, and that can reach only its own database. Migrations use a separate role.
- The tenant is set whenever a connection opens and is reset when the connection returns to the pool, so pooled connections cannot carry it over. Without tenant context, queries return nothing or throw. See the 2026-11-04 amendment.
- Uniqueness is scoped by tenant: `(tenant_id, email)`, `(tenant_id, username)`, `(tenant_id, external_id)` and `(tenant_id, idempotency_key)`.
- Relationships between tenant-owned rows, such as role assignments and group members, use composite foreign keys that include `tenant_id`.

**Other stores and messages**
- Redis keys start with `{tenantId}:`, built by one shared helper per language.
- OpenSearch uses one audit index per tenant, `audit-{tenantId}`. Search and live streams take the tenant from the token, and tenant users get no wildcard patterns.
- Commands get an envelope with a required `tenantId`, as events already have. Consumers send messages without one to the DLQ.
- Outbox rows store `tenant_id` and copy it to the envelope. Relays, workers and seeders set tenant context per unit of work.
- Tenants & Users owns per-tenant LDAP configuration. The LDAP Connector receives the tenant on an authenticated internal call and never shares pooled connections across tenants.

**Super-administrators**
- A reserved platform tenant holds super-administrators, who sign in through its own client. Only that tenant's users can receive the `platform_role=super_admin` claim.
- The server checks the claim through a named authorization policy. Cross-tenant access exists only behind that policy, and each use emits an audit event.
- Tenant administrators get 404 for other tenants' resources and 403 on super-administrator endpoints.

## Alternatives considered
- Database per tenant: the strongest isolation and simple per-tenant restores, but migrations and connection pools grow with every tenant, which is heavy for this project.
- Schema per tenant: stronger separation than a shared schema, but migrations still multiply and EF Core's model caching gets harder.
- `tenant_id` with EF Core filters only: the simplest option, but one raw SQL query or one `IgnoreQueryFilters()` is enough to leak data.
- Tenant from a subdomain or per-tenant issuer: clear for users, but it adds DNS, TLS and per-issuer key handling. The slug-to-client mapping leaves room to add it later.
- Trusting the Gateway and network policies inside the cluster: less work per service, but a single compromised or misrouted caller could claim any tenant.

## Consequences
- A bug in application code no longer reaches other tenants' rows on its own, because the database also enforces the boundary.
- Each service does more work: tenant context on every transaction, JWT validation and more roles to manage. Local Compose and Kubernetes need per-service database roles from Phase 1.
- The same user can exist in two tenants as two separate accounts; there is no cross-tenant single account.
- **Resolved on 2026-11-04:** OpenIddict's EF Core stores are not tenant-aware, and they look up tokens and authorization codes before a user is known. Identity feature 001 proved that RLS works with them, taking the tenant from `client_id` or from the validated token, with a test that fails when RLS is removed.
- **To watch:** failed logins for an unknown client have no tenant. Audit needs a defined destination for these events, decided in Phase 3.
- Not covered here: per-service OpenSearch roles and Redis ACLs, which remain a later hardening step.

## Amendments

### 2026-11-04 · Tenant set per connection instead of `SET LOCAL`
The original decision set the tenant per transaction with `SET LOCAL`. OpenIddict's stores read outside transactions, where `SET LOCAL` has no effect, and one transaction per request would hold a connection during password hashing.

Services now apply `set_config('app.tenant_id', <tenant or ''>, false)` when a connection opens, and rely on Npgsql's reset when it returns to the pool. The goal is unchanged: a pooled connection must never carry a tenant into another operation.

This risk is accepted for now and must be validated by a test in each service that uses this approach (AC-28 of Identity feature 001). It is incompatible with PgBouncer in transaction mode, so the cloud phase must use session mode or direct connections, or revisit this decision.
