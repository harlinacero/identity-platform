# Identity

OAuth2/OIDC sign-in for the platform, built with .NET 10, OpenIddict and PostgreSQL. This covers feature [001](features/001-tenant-aware-identity-foundation/): local users and Authorization Code + PKCE, isolated by tenant ([ADR-0006](../../docs/adr/0006-multi-tenancy-strategy.md)).

Refresh tokens, client credentials, sign-out and the React sign-in form come in later features. Local users move to Tenants & Users in Phase 2.

## Run it
```powershell
./dev.ps1 up                                            # PostgreSQL, migrations and the service
./dev.ps1 run dotnet services/identity dotnet test      # all tests
```
The service listens on `http://localhost:5001` (`IDENTITY_PORT`).

The database roles are created only when the PostgreSQL volume is empty. If you started PostgreSQL before this service existed, recreate the volumes with `docker compose --project-directory . -f deploy/compose/docker-compose.infra.yml down -v`, which **deletes all local infrastructure data**.

## Try a sign-in
The local environment creates two tenants, each with one public client and a user called `demo.user`:

| Tenant | Client | Password setting |
| --- | --- | --- |
| A | `tenant-a-web` | `SEED_TENANT_A_USER_PASSWORD` |
| B | `tenant-b-web` | `SEED_TENANT_B_USER_PASSWORD` |

Open this URL, replacing `<challenge>` with the Base64URL-encoded SHA-256 of a random verifier that you keep:
```text
http://localhost:5001/connect/authorize?client_id=tenant-a-web&response_type=code&scope=openid%20profile&redirect_uri=http%3A%2F%2Flocalhost%3A5173%2Fcallback&state=demo&code_challenge=<challenge>&code_challenge_method=S256
```
After you sign in, the browser goes to `localhost:5173/callback?code=...`. Nothing listens there until React exists, so copy the code from the address bar and exchange it within 5 minutes:
```powershell
curl.exe -X POST http://localhost:5001/connect/token -d grant_type=authorization_code -d client_id=tenant-a-web -d code=<code> -d code_verifier=<verifier> -d redirect_uri=http://localhost:5173/callback
curl.exe http://localhost:5001/connect/userinfo -H "Authorization: Bearer <access_token>"
```

## Endpoints
| Method | Route | Purpose |
| --- | --- | --- |
| GET, POST | `/connect/authorize` | Starts sign-in; requires PKCE with `S256` |
| POST | `/connect/token` | Exchanges a code for tokens |
| GET, POST | `/connect/userinfo` | Returns `sub`, `tenant_id` and `preferred_username` |
| GET, POST | `/account/login` | Temporary sign-in form |
| GET | `/.well-known/openid-configuration`, `/.well-known/jwks` | Discovery and signing keys |
| GET | `/health` | 200 when PostgreSQL is reachable, otherwise 503 |

## Configuration
| Setting | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings:Identity` | Connection as `identity_app` | Required |
| `Identity:Issuer` | Token issuer | Required |
| `Identity:Session:Duration` | Sign-in session duration | `00:15:00` |
| `Identity:Signing:CertificateBase64`, `Identity:Encryption:CertificateBase64` | PFX keys, Base64-encoded | Required in Production; ephemeral in Development and Testing |
| `Seed:TenantA:UserPassword`, `Seed:TenantB:UserPassword` | Test user passwords | Required in Development and Testing |

Access tokens last 15 minutes and sign-in codes 5 minutes; both are fixed in code.

## How isolation works
- **Tenant source:** the OIDC client during sign-in, through `client_directory`, and the validated `tenant_id` claim afterwards. Any other tenant input is ignored.
- **Two layers:** EF Core global query filters, and PostgreSQL Row-Level Security with `FORCE`. `TenantConnectionInterceptor` sets `app.tenant_id` on every connection that opens.
- **Roles:** `identity_app` runs the service and cannot bypass RLS or change the schema. `identity_migrator` owns the schema and runs migrations.
- **Limit:** this approach is incompatible with PgBouncer in transaction mode (ADR-0006).

A new tenant-owned table needs `ITenantOwned`, a query filter and `RowLevelSecuritySql.Enable` in its migration. `RlsCatalogTests` fails if the migration leaves RLS out.

## Add a migration
```powershell
./dev.ps1 run dotnet services/identity sh -c "dotnet tool restore && dotnet ef migrations add <Name> -p src/Identity.Infrastructure -s src/Identity.Infrastructure -o Persistence/Migrations"
```
