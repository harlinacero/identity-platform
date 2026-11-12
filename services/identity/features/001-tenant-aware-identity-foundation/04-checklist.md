# [001] Tenant-aware identity foundation — Implementation Checklist

> Last reviewed: 2026-11-11
> Overall: ✅ Complete. All 32 acceptance criteria and 79 tasks are done; deferred review findings are listed in section 5.

Test paths are relative to `services/identity/tests/`: `IT` is `Identity.IntegrationTests` and `UT` is `Identity.UnitTests`. Implementation paths are relative to `services/identity/src/`.

## 1. Acceptance criteria
| AC | Flow | Status | Proving test(s) | Implementation |
|----|------|--------|-----------------|----------------|
| AC-01 | UC-01 main | ✅ | `DevelopmentSeederTests.Seed_WhenEmpty_ShouldCreateTwoTenantsWithClientsAndUsers` | `Identity.Infrastructure/Seeding/DevelopmentSeeder.cs`, `SeedData.cs` |
| AC-02 | UC-01 A1 | ✅ | `DevelopmentSeederTests.Seed_WhenRunTwice_ShouldNotDuplicateOrChangeData` | `DevelopmentSeeder.cs` |
| AC-03 | UC-01 E1 | ✅ | `DevelopmentSeederTests.Startup_WhenSeedSecretMissing_ShouldFailAndNameSettingOnly` | `Seeding/SeedOptions.cs`, `DevelopmentSeeder.cs` |
| AC-04 | UC-01 E2 | ✅ | `DevelopmentSeederTests.Startup_WhenProduction_ShouldNotSeed` | `DevelopmentSeeder.cs` |
| AC-05 | UC-02 main | ✅ | `AuthorizationCodeFlowTests.CodeFlow_WhenCredentialsValid_ShouldIssueTokenWithUserAndTenant`, `AuthorizationRequestTests.Authorize_WhenRequestIsPost_ShouldCompleteSignIn` | `Identity.Api/Endpoints/AuthorizationEndpoints.cs`, `TokenEndpoints.cs`, `LoginEndpoints.cs`, `OpenIddict/OpenIddictServerServiceCollectionExtensions.cs` |
| AC-06 | UC-02 A1 | ✅ | `SessionTests.Authorize_WhenSessionInSameTenant_ShouldNotAskForCredentials` | `AuthorizationEndpoints.cs`, `Program.cs` (cookie) |
| AC-07 | UC-02 A2 | ✅ | `SessionTests.Authorize_WhenSessionInAnotherTenant_ShouldAskForCredentials` | `AuthorizationEndpoints.cs` |
| AC-08 | UC-02 A3 | ✅ | `TenantInputTests.SignIn_WhenRequestStatesAnotherTenant_ShouldUseClientTenant` | `Middleware/TenantResolutionMiddleware.cs`, `Persistence/ClientDirectory.cs` |
| AC-09 | UC-02 A4 | ✅ | `CredentialsTests.Login_WhenPasswordWrong_ShouldShowGenericErrorAndAllowRetry` | `LoginEndpoints.cs`, `Identity.Application/Users/ValidateCredentialsHandler.cs` |
| AC-10 | UC-02 E1 | ✅ | `AuthorizationRequestTests.Authorize_WhenClientUnknown_ShouldRejectWithoutRedirect` | OpenIddict configuration, `TenantResolutionMiddleware.cs` |
| AC-11 | UC-02 E2 | ✅ | `AuthorizationRequestTests.Authorize_WhenRedirectUriDiffers_ShouldRejectWithoutRedirect` (5 cases) | OpenIddict configuration |
| AC-12 | UC-02 E3 | ✅ | `AuthorizationRequestTests.Authorize_WhenChallengeMissing_ShouldReject`, `Authorize_WhenChallengeMethodPlain_ShouldReject` | `OpenIddictServerServiceCollectionExtensions.cs` |
| AC-13 | UC-02 E4 | ✅ | `CredentialsTests.Login_WhenPasswordBelongsToSameUsernameInOtherTenant_ShouldFail` | `ValidateCredentialsHandler.cs`, `Persistence/LocalUserRepository.cs`, RLS |
| AC-14 | UC-02 E5 | ✅ | `CredentialsTests.Login_WhenUsernameUnknown_ShouldReturnSameResponseAsWrongPassword`, `ValidateCredentialsHandlerTests.Handle_WhenUserMissing_ShouldStillRunPasswordVerification` | `ValidateCredentialsHandler.cs` (dummy hash) |
| AC-15 | UC-02 E6 | ✅ | `CodeExchangeTests.Exchange_WhenVerifierDoesNotMatch_ShouldIssueNoToken` | OpenIddict configuration |
| AC-16 | UC-02 E7 | ✅ | `CodeExchangeTests.Exchange_WhenCodeOlderThanFiveMinutes_ShouldBeRejected`, `Exchange_WhenCodeWithinFiveMinutes_ShouldSucceed` | `OpenIddictServerServiceCollectionExtensions.cs` |
| AC-17 | UC-02 E8 | ✅ | `CodeExchangeTests.Exchange_WhenCodeAlreadyUsed_ShouldBeRejected` | OpenIddict token storage |
| AC-18 | UC-02 E9 | ✅ | `CodeExchangeTests.Exchange_WhenPresentedByOtherTenantClient_ShouldBeRejected` | `OpenIddict/TenantTokenValidationHandler.cs`, RLS |
| AC-19 | UC-02 E10 | ✅ | `DataStoreOutageTests.Login_WhenDatabaseUnavailable_ShouldReturnTemporaryErrorAndCreateNothing`, `Exchange_WhenDatabaseUnavailable_ShouldReturnTemporaryError`, `SlowDataStoreTests.*_WhenDataStoreSlow_ShouldReturnTemporaryErrorWithinBudget`, `DataStoreFailuresTests.*` | `Errors/DataStoreExceptionHandler.cs`, `Persistence/DataStoreFailures.cs`, request timeouts in `Program.cs` |
| AC-20 | BR-04 | ✅ | `Pbkdf2PasswordVerifierTests.Hash_WhenCreated_ShouldNotContainPasswordAndUseConfiguredIterations` | `Security/Pbkdf2PasswordVerifier.cs` |
| AC-21 | UC-03 main | ✅ | `UserInfoTests.UserInfo_WhenTokenValid_ShouldReturnUserAndTenant` | `UserInfoEndpoints.cs` |
| AC-22 | UC-03 E1 | ✅ | `UserInfoTests.UserInfo_WhenTokenHasNoTenant_ShouldBeUnauthorized`, `UserInfo_WhenSignedTokenNamesAnotherTenant_ShouldBeUnauthorized` | `TenantTokenValidationHandler.cs` |
| AC-23 | UC-03 E2 | ✅ | `UserInfoTests.UserInfo_WhenTokenExpired_ShouldBeUnauthorized`, `UserInfo_WhenTokenAltered_ShouldBeUnauthorized` | OpenIddict validation |
| AC-24 | UC-03 E3 | ✅ | `UserInfoTests.UserInfo_WhenNoToken_ShouldBeUnauthorized` | OpenIddict validation |
| AC-25 | UC-04 main | ✅ | `RowLevelSecurityTests.Query_WhenFilterIgnored_ShouldReturnOnlyCurrentTenantRows`, `SignInRecordWriteTests.UpdateAndDelete_WhenRowBelongsToAnotherTenant_ShouldAffectNothing` | RLS migrations, `Migrations/RowLevelSecuritySql.cs` |
| AC-26 | UC-04 E1 | ✅ | `RowLevelSecurityTests.Query_WhenNoTenantEstablished_ShouldReturnNothing`, `Insert_WhenNoTenantEstablished_ShouldBeRejected`, `TenantWriteGuardTests.Apply_WhenTenantMissing_ShouldThrow` | RLS, `Interceptors/TenantWriteGuard.cs` |
| AC-27 | UC-04 E2 | ✅ | `RowLevelSecurityTests.Insert_WhenRowBelongsToAnotherTenant_ShouldBeRejected`, `SignInRecordWriteTests.Insert_WhenRowBelongsToAnotherTenant_ShouldBeRejected`, `Reference_WhenParentBelongsToAnotherTenant_ShouldBeRejected`, `TenantWriteGuardTests.Apply_WhenTenantDiffers_ShouldThrow` | RLS, composite foreign keys, `TenantWriteGuard.cs` |
| AC-28 | UC-04 E3 | ✅ | `RowLevelSecurityTests.NextOperation_WhenConnectionIsReused_ShouldNotSeePreviousTenant`, `TenantContextTests.Set_WhenChangedWithinScope_ShouldThrow` | `Interceptors/TenantConnectionInterceptor.cs`, `Identity.Application/Tenancy/TenantContext.cs` |
| AC-29 | UC-05 main | ✅ | `SignInRecordIsolationTests.SignIn_WhenRlsEnforced_ShouldSucceedForBothTenantsAndHideEachOthersRecords` | RLS on OpenIddict tables, `TenantTokenValidationHandler.cs` |
| AC-30 | UC-05 main | ✅ | `RuntimeRoleTests.RuntimeRole_WhenInspected_ShouldNotBypassRlsOrOwnTables`, `RuntimeRole_WhenConnectingToAnotherServiceDatabase_ShouldBeRejected`, `ClientDirectoryTests.ClientDirectory_WhenRuntimeRoleChangesEntry_ShouldBeRejected` | `deploy/compose/seed/postgres/02-identity-roles.sh`, `01-create-databases.sql`, migration grants |
| AC-31 | UC-05 E1 | ✅ | `RlsCatalogTests.Catalog_WhenInspected_ShouldHaveRlsAndForceOnEveryTenantOwnedTable`, `Catalog_WhenInspected_ShouldHaveOnlyTheTenantPolicy`, `Catalog_WhenInspected_ShouldProtectExpectedTable`; stop gate T-28 passed with two negative controls | RLS migrations |
| AC-32 | UC-02 A5 | ✅ | `SessionTests.Authorize_WhenSessionOlderThanDefault_ShouldAskForCredentials`, `Authorize_WhenSessionOlderThanConfiguredDuration_ShouldAskForCredentials` | `Options/IdentitySessionOptions.cs`, cookie options in `Program.cs` |

Every use-case flow has at least one passing test; there are no uncovered exception flows. Protections that passed on their first run were checked by temporarily disabling them: the two T-28 negative controls and mutations M1 to M6 in `03-tasks.md`.

## 2. Tasks
| Task | Status | Evidence / note |
|------|--------|-----------------|
| T-01 – T-04 | ✅ | Solution, roles script, `PostgresFixture`, `IdentityApiFactory`, `OidcTestClient` exist and every integration test uses them |
| T-05 – T-15 | ✅ | Isolation tests above pass; `TenantWriteGuard` unit-tested instead of the interceptor (deviation, see section 6) |
| T-16 – T-21 | ✅ | Seeder and password tests pass; OpenIddict entities moved into T-21 |
| T-22 – T-24 | ✅ | Full code flow and userinfo pass |
| T-25 – T-28 | ✅ | Stop gate passed on 2026-11-04 with negative controls; R-01 closed |
| T-29 – T-38 | ✅ | Session, tenant input and credential tests pass; dummy hash checked by mutation M1 |
| T-39 – T-43 | ✅ | Rejected-request tests pass; antiforgery 500 fixed to 400 |
| T-44 – T-48 | ✅ | Code exchange tests pass |
| T-49 – T-52 | ✅ | Userinfo tests pass; handler checked by mutation M5 |
| T-53 – T-54 | ✅ | Outage tests pass |
| T-55 – T-59 | ✅ | Dockerfile, Compose, README and spec updates in place; `./dev.ps1 up` from empty volumes verified on 2026-11-05 (R-03) |
| T-60 | ✅ | Lint, build and tests rerun on 2026-11-11 (section 4) |
| T-61 | ✅ | `security-reviewer`, `tenant-isolation-auditor` and `resilience-reviewer` run on 2026-11-11; findings handled in Group 12 or deferred (section 5) |
| T-62 – T-79 | ✅ | Review fixes; each `[TEST]` failed for the expected reason before its fix, except the regression tests noted in `03-tasks.md` |

## 3. Convention compliance
- [x] Layering: Domain has no dependencies; only Infrastructure references EF Core and Npgsql. `DataStoreFailures` and `DataStoreProbe` live in Infrastructure, so the Api uses them without database code of its own.
- [x] Minimal APIs, no controllers, no MediatR; plain handler classes (`ValidateCredentialsHandler`).
- [x] Nullable enabled, warnings as errors (build: 0 warnings), async with `CancellationToken`.
- [x] One type per file, and file-scoped namespaces everywhere except EF Core's generated migrations, which keep the generator's style.
- [x] Tests: xUnit with Testcontainers PostgreSQL, no in-memory provider; `Method_WhenCondition_ShouldResult` naming with Arrange / Act / Assert; NSubstitute only at boundaries.
- [x] Cross-tenant tests for every endpoint (`/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/account/login`) and every tenant-owned table (ADR-0006). The health endpoints hold no tenant data.
- [x] Package versions centralized in `Directory.Packages.props`; no packages added since the approval of 2026-11-04.
- [x] Everything committed is in English; no secrets in code or config (`.env.example` holds placeholders only).
- [ ] ⚠ Minor: `IdentitySessionOptions`, `IdentityRequestOptions` and `SeedOptions` are bound with `GetSection().Get<T>()` at startup instead of being injected as `IOptions<T>`. They configure middleware at build time, where binding directly is the usual pattern, but this is a literal departure from "configuration through `IOptions`". Suggested fix if the team prefers: register them with `AddOptions<T>().Bind(...)` and resolve them in the configuring delegates.
- [ ] ⚠ Minor: `TokenEndpoints` and `UserInfoEndpoints` take `CancellationToken _` without using it. OpenIddict already observes `RequestAborted`, so behaviour is unaffected.

## 4. Build & tests
```
./dev.ps1 lint dotnet    → dotnet format --verify-no-changes: no changes
./dev.ps1 build dotnet   → Build succeeded. 0 Warning(s), 0 Error(s)
./dev.ps1 test dotnet    → Passed! total: 97, failed: 0, succeeded: 97, skipped: 0 (1m 59s)
docker compose ... config --quiet → valid
```
Run on 2026-11-11 at commit `e2a9aa5`, and again before shipping with the same result.

Manual verification on 2026-11-11, with `./dev.ps1 up` from empty volumes:
- Every container became healthy, the migrations exited 0, and `identity` probes `/health/ready` and has `restart: unless-stopped`.
- `scripts/Try-SignIn.ps1 -Automatic` completed real sign-ins for both tenants: the right `tenant_id`, and `preferred_username` only with `profile`.
- A replayed code was rejected. A tenant A session was not reused for tenant B, and tenant A's password failed there.
- The container logs held no passwords, verifiers or tokens.
- The sign-in records created while verifying (40 tokens and 14 authorizations) were deleted afterwards; only the seed data remains.

## 5. Missing / pending
Nothing in this feature's scope is missing. The following review findings were deferred by the owner on 2026-11-11:

- **Security L1: `prompt`, `max_age`, `auth_time` and `id_token_hint`.** `prompt=none` does not return `login_required`, `prompt=login` and `max_age` don't force re-authentication, no `auth_time` is issued, and a hint that doesn't match the session is ignored. Proposed as its own feature before the React sign-in.
- **Security L3: signing key rotation.** Only one signing and one encryption certificate are loaded. Cloud phase.
- **Forwarded headers (`UseForwardedHeaders`).** Outside Development, OpenIddict would reject every request from a TLS-terminating proxy. It fails closed; it comes with the Gateway.
- **Resilience M2: retry logs and metrics.** EF Core retries are logged at Information, below the configured Warning level, and no metric counts them. Observability phase.
- **Resilience L1: code exchange is not atomic.** A failure between redeeming the code and storing the token consumes the code, and the user signs in again. Documented in the technical spec; clients must not retry `/connect/token`.
- **Resilience L2 and L3:** a startup guard against `No Reset On Close` or a transaction-mode pooler, and retries for the migrations bundle.
- **Isolation L2:** tests for `client_id` in both form and query, a duplicate `client_id`, and `/connect/authorize/` with a trailing slash. Every case reviewed fails closed today.
- **Isolation L5:** token pruning (D-04) must run per tenant with the context set, or it will silently do nothing.
- **Accepted risks already in the business spec:** no lockout or rate limiting until Phase 4, so `/account/login` is unauthenticated PBKDF2 load; refresh tokens and rotation come in a later Phase 1 feature; data protection keys are not persisted (R-04).

## 6. Deviations from spec
All of these are recorded in `03-tasks.md`, and the technical spec was updated where it applied.
- **T-21 / T-24:** OpenIddict entities and their migration moved earlier, because the seeder creates applications.
- **T-12 / T-13:** the write rule is unit-tested in `TenantWriteGuard` rather than through the interceptor, to avoid an in-memory provider.
- **T-27 (D-03):** `TenantTokenValidationHandler` runs before `RestoreTokenEntryProperties`, not `ValidateTokenEntry`.
- **Naming:** `SessionOptions` became `IdentitySessionOptions`.
- **AC-12:** OpenIddict rejects a missing or `plain` challenge with 400 and no redirect; the spec's API table was updated.
- **T-57 finding:** OpenIddict logged the `code_verifier`; its logs are now at Warning, covered by `Logs_WhenSignInCompletes_ShouldNotContainSecrets`.
- **T-67:** the client directory foreign key points from applications to the directory; spec D-02 updated.
- **T-69:** the cross-tenant `id_token_hint` test asserts that the hint has no effect, not that the request is rejected. Spec section 7 updated.
- **T-78:** `/health` was replaced by `/health/live` and `/health/ready`; spec, README and Compose updated.
- **Migrations:** EF Core writes migrations with a UTF-8 BOM, which `.editorconfig` forbids; the BOM is removed by hand on each new migration.

## 7. Risks & follow-ups
- **D-01 / R-07:** the tenant setting depends on Npgsql resetting pooled connections, which rules out PgBouncer in transaction mode. `Maximum Pool Size=50` is per replica and must be sized for the cloud.
- **R-06:** `IdentityDbContext` must not be pooled while the interceptors capture the scoped tenant context.
- **R-04:** sessions and antiforgery tokens don't survive a restart until data protection keys are persisted.
- **R-05:** the temporary `/account/login` form will be replaced by `frontends/account-web`.
- **Existing `.env` files** need the variables added in this feature; `.env.example` and the root README say so.
- **Next:** run `verify-phase` for Phase 1 of the build plan to choose the next feature.
