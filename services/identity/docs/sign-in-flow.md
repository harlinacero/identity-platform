# Sign-in flow

How a client application signs a user in through Identity with Authorization Code + PKCE (feature [001](../features/001-tenant-aware-identity-foundation/)), and where the tenant is resolved and enforced on each step.

## Participants
- **Browser**: the user agent, which carries the session cookie.
- **Client app**: a public OIDC client registered to exactly one tenant (for example `tenant-a-web`).
- **Request pipeline**: `UseRequestTimeouts` (10-second budget), `TenantResolutionMiddleware`, and the cookie and OpenIddict authentication handlers.
- **OpenIddict**: validates protocol requests (client, `redirect_uri`, PKCE, codes and tokens) before the endpoints run, in passthrough mode.
- **Endpoints**: the Minimal API handlers in `Identity.Api/Endpoints`.
- **PostgreSQL**: reached as `identity_app`, a role that cannot bypass Row-Level Security. `TenantConnectionInterceptor` sets `app.tenant_id` on every connection that opens.

## Sequence
```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Browser
    participant App as Client app<br/>(public client)
    participant Pipe as Request pipeline<br/>(timeout + TenantResolutionMiddleware)
    participant OI as OpenIddict server
    participant EP as Endpoints<br/>(Minimal APIs)
    participant DB as PostgreSQL<br/>(identity_app, RLS)

    User->>App: Open the app
    App->>App: Create code_verifier, state and<br/>code_challenge = BASE64URL(SHA256(verifier))
    App-->>Browser: 302 to /connect/authorize?client_id&redirect_uri<br/>&scope&state&code_challenge&method=S256

    rect rgba(127, 127, 127, 0.08)
    note over Browser,DB: 1. Authorization request (no session yet)
    Browser->>Pipe: GET /connect/authorize
    Pipe->>DB: client_directory: tenant of client_id
    DB-->>Pipe: tenant_id
    Pipe->>Pipe: TenantContext.Set(tenant_id), once per request
    Pipe->>OI: Validate the request
    OI->>DB: Application, redirect_uri (filtered by app.tenant_id)
    alt Unknown client, wrong redirect_uri, or missing or plain PKCE
        OI-->>Browser: 400, never redirected to the client
    end
    OI->>EP: AuthorizationEndpoints.AuthorizeAsync
    EP->>EP: No session cookie, or its tenant_id differs from the client's
    EP-->>Browser: Challenge: 302 to /account/login?returnUrl=/connect/authorize?...
    end

    rect rgba(127, 127, 127, 0.08)
    note over Browser,DB: 2. Temporary login form
    Browser->>Pipe: GET /account/login?returnUrl=...
    Pipe->>Pipe: Only a local /connect/authorize returnUrl is accepted;<br/>its client_id resolves the tenant
    Pipe->>EP: LoginEndpoints.ShowForm
    EP-->>Browser: HTML form + antiforgery token
    User->>Browser: Username and password
    Browser->>Pipe: POST /account/login (form, antiforgery, returnUrl)
    Pipe->>DB: client_directory: tenant of the returnUrl's client_id
    Pipe->>EP: LoginEndpoints.SubmitAsync
    EP->>EP: Validate antiforgery and returnUrl
    EP->>DB: ValidateCredentialsHandler: user by username<br/>(EF filter + RLS limit it to the tenant)
    alt Unknown user or wrong password
        EP->>EP: Unknown user still runs a dummy PBKDF2 hash
        EP-->>Browser: Form again with one generic error
    else Valid credentials
        EP->>EP: PBKDF2-SHA512 verification
        EP-->>Browser: Set-Cookie identity.session (sub, tenant_id, preferred_username)<br/>302 to returnUrl
    end
    end

    rect rgba(127, 127, 127, 0.08)
    note over Browser,DB: 3. Authorization request (with a session)
    Browser->>Pipe: GET /connect/authorize (+ session cookie)
    Pipe->>DB: client_directory: tenant of client_id
    Pipe->>OI: Validate the request
    OI->>EP: AuthorizationEndpoints.AuthorizeAsync
    EP->>EP: Session tenant_id == client tenant_id<br/>Claims: sub, tenant_id, preferred_username only with "profile"
    EP->>OI: Results.SignIn(principal)
    OI->>DB: Store authorization and code (tenant_id, 5-minute lifetime)
    OI-->>Browser: 302 to redirect_uri?code&state
    Browser->>App: code, state
    end

    rect rgba(127, 127, 127, 0.08)
    note over App,DB: 4. Code exchange
    App->>App: Check state
    App->>Pipe: POST /connect/token (code, code_verifier, client_id, redirect_uri)
    Pipe->>DB: client_directory: tenant of client_id
    Pipe->>OI: Validate the request
    OI->>OI: TenantTokenValidationHandler: the code's tenant_id<br/>must match the client's tenant
    OI->>DB: Code entry (RLS), redeem it once
    OI->>OI: Check SHA256(code_verifier) == code_challenge
    alt Bad verifier, reused or expired code, or code from another tenant
        OI-->>App: 400 invalid_grant
    end
    OI->>EP: TokenEndpoints.ExchangeAsync
    EP->>OI: Results.SignIn(principal from the code)
    OI-->>App: access_token (signed JWT, 15 minutes, aud=identity-platform) + id_token
    end

    rect rgba(127, 127, 127, 0.08)
    note over App,DB: 5. UserInfo
    App->>Pipe: GET /connect/userinfo (Authorization: Bearer access_token)
    Pipe->>OI: Validate the token (signature, iss, aud, exp)
    OI->>OI: TenantTokenValidationHandler: tenant_id is required<br/>and becomes the request's tenant
    alt Missing, expired or altered token, or no valid tenant_id
        OI-->>App: 401
    end
    OI->>EP: UserInfoEndpoints.GetAsync
    EP-->>App: { sub, tenant_id, preferred_username? }
    end

    note over Pipe,DB: Any step: a data store outage returns 503 (DataStoreExceptionHandler),<br/>and so does a request that exceeds its 10-second budget.
```

## Where the tenant comes from
| Step | Source | Set by |
| --- | --- | --- |
| `/connect/authorize`, `/connect/token` | `client_id` → `client_directory` | `TenantResolutionMiddleware` |
| `/account/login` | `client_id` inside the local `returnUrl` → `client_directory` | `TenantResolutionMiddleware` |
| `/connect/token`, `/connect/userinfo` | `tenant_id` claim of the validated code or token | `TenantTokenValidationHandler` |

The tenant is set at most once per request; a second, different value throws. No body field, query value or header other than the OIDC `client_id` is ever treated as the tenant (ADR-0006).
