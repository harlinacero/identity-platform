# [001] Tenant-aware sign-in foundation — Tasks (TDD)

> Status: Approved (2026-11-04)
> Based on: `02-technical-spec.md` (Approved 2026-11-04)

Legend: `[TEST]` write a failing test (red) · `[IMPL]` make it pass (green) · `[REFACTOR]` · `[SETUP]` config/infra/docs

Paths are relative to `services/identity/`, unless they start at the repository root. `IT` means `tests/Identity.IntegrationTests` and `UT` means `tests/Identity.UnitTests`. All commands run in containers (`./dev.ps1 run dotnet services/identity dotnet test`).

## Group 1 — Scaffolding
- [x] **T-01** `[SETUP]` `Identity.slnx`, `Directory.Build.props` (nullable, warnings as errors), `Directory.Packages.props` with the approved packages, the four `src/` projects and the two test projects.
- [x] **T-02** `[SETUP]` Repository root `deploy/compose/seed/postgres/02-identity-roles.sh` — creates `identity_migrator` and `identity_app` from environment variables.
- [x] **T-03** `[SETUP]` `IT/Infrastructure/PostgresFixture.cs` — starts PostgreSQL with the roles script, migrates as `identity_migrator`, and exposes an `identity_app` connection string.
- [x] **T-04** `[SETUP]` `IT/Infrastructure/IdentityApiFactory.cs` and `IT/Infrastructure/OidcTestClient.cs` — in-process API in the Testing environment; helper that runs the code flow with a generated verifier and challenge.

## Group 2 — Data store isolation (AC-25, AC-26, AC-27, AC-28, AC-30)
- [x] **T-05** `[TEST]` `IT/Isolation/RuntimeRoleTests.cs` → `RuntimeRole_WhenInspected_ShouldNotBypassRlsOrOwnTables` — `identity_app` has `rolsuper` and `rolbypassrls` false, owns no table and cannot run DDL. (AC-30)
- [x] **T-06** `[IMPL]` Finish `02-identity-roles.sh` and the grants in the initial migration. (T-05)
- [x] **T-07** `[TEST]` `IT/Isolation/RowLevelSecurityTests.cs` → `Query_WhenFilterIgnored_ShouldReturnOnlyCurrentTenantRows` — with `IgnoreQueryFilters()`, a read and an update for tenant A reach only tenant A's `local_users`. (AC-25)
- [x] **T-08** `[TEST]` Same file → `Query_WhenNoTenantEstablished_ShouldReturnNothing` and `Insert_WhenNoTenantEstablished_ShouldBeRejected` — using raw SQL, so that only the data store is under test. (AC-26)
- [x] **T-09** `[TEST]` Same file → `Insert_WhenRowBelongsToAnotherTenant_ShouldBeRejected` — raw SQL as tenant A, with tenant B's id, fails the policy. (AC-27)
- [x] **T-10** `[TEST]` Same file → `NextOperation_WhenConnectionIsReused_ShouldNotSeePreviousTenant` — on a pool of size 1, an operation for tenant A followed by one with no tenant returns no rows. (AC-28)
- [x] **T-11** `[IMPL]` `LocalUser`, `ITenantOwned`, `ITenantContext`, `ITenantContextSetter`, `IdentityDbContext` with its global filter, `TenantConnectionInterceptor`, and the initial migration with `local_users`, the policy and `FORCE`. (T-07 to T-10)
- [x] **T-12** `[TEST]` `UT/Tenancy/TenantWriteGuardTests.cs` → `Apply_WhenTenantMissing_ShouldThrow`, `Apply_WhenTenantDiffers_ShouldThrow`, `Apply_WhenEntityHasNoTenant_ShouldStampCurrentTenant`. (AC-26, AC-27)
- [x] **T-13** `[IMPL]` `TenantWriteGuard`, used by `TenantWriteInterceptor`. (T-12)
- [x] **T-14** `[TEST]` `UT/Tenancy/TenantContextTests.cs` → `Set_WhenChangedWithinScope_ShouldThrow`. (AC-28)
- [x] **T-15** `[IMPL]` Set-once behaviour in the tenant context. (T-14)

## Group 3 — Test data (AC-01, AC-02, AC-03, AC-04, AC-20)
- [x] **T-16** `[TEST]` `IT/Seeding/DevelopmentSeederTests.cs` → `Seed_WhenEmpty_ShouldCreateTwoTenantsWithClientsAndUsers` — two directory entries, one application per tenant, the same username in both, and password hashes that differ. (AC-01)
- [x] **T-17** `[TEST]` Same file → `Seed_WhenRunTwice_ShouldNotDuplicateOrChangeData` — counts and hashes unchanged. (AC-02)
- [x] **T-18** `[TEST]` Same file → `Startup_WhenSeedSecretMissing_ShouldFailAndNameSettingOnly` — the startup error contains the key and no configured secret value. (AC-03)
- [x] **T-19** `[TEST]` Same file → `Startup_WhenProduction_ShouldNotSeed` — with seed secrets present, no rows are created. (AC-04)
- [x] **T-20** `[TEST]` `UT/Security/Pbkdf2PasswordVerifierTests.cs` → `Hash_WhenCreated_ShouldNotContainPasswordAndUseConfiguredIterations`, `Verify_WhenPasswordWrong_ShouldFail`. (AC-20)
- [x] **T-21** `[IMPL]` `Pbkdf2PasswordVerifier`, `ClientDirectoryEntry`, `ClientDirectory`, the `client_directory` migration, `SeedOptions` with startup validation, and `DevelopmentSeeder` with one scope per tenant. (T-16 to T-20)

## Group 4 — Complete sign-in (AC-05, AC-21)
- [x] **T-22** `[TEST]` `IT/SignIn/AuthorizationCodeFlowTests.cs` → `CodeFlow_WhenCredentialsValid_ShouldIssueTokenWithUserAndTenant` — `sub` and `tenant_id` are tenant A's, and `expires_in` is 900 seconds. (AC-05)
- [x] **T-23** `[TEST]` `IT/SignIn/UserInfoTests.cs` → `UserInfo_WhenTokenValid_ShouldReturnUserAndTenant`. (AC-21)
- [x] **T-24** `[IMPL]` OpenIddict entities and migration with `tenant_id`, RLS and composite foreign keys; server configuration with PKCE `S256`, lifetimes and `DisableEntityCaching()`; `TenantResolutionMiddleware`; `TenantTokenValidationHandler`; `ValidateCredentialsHandler`; authorization, token, userinfo and login endpoints. (T-22, T-23)

## Group 5 — RLS proof: stop gate (AC-29, AC-31)
- [x] **T-25** `[TEST]` `IT/Isolation/SignInRecordIsolationTests.cs` → `SignIn_WhenRlsEnforced_ShouldSucceedForBothTenantsAndHideEachOthersRecords` — complete sign-ins for both tenants; with filters ignored, tenant A's context sees no tenant B applications, authorizations or tokens, and vice versa. (AC-29)
- [x] **T-26** `[TEST]` `IT/Isolation/RlsCatalogTests.cs` → `Catalog_WhenInspected_ShouldHaveRlsAndForceOnEveryTenantOwnedTable` — queries the catalog for every table with `tenant_id`, and on failure names the unprotected tables. (AC-31)
- [x] **T-27** `[IMPL]` Only the corrections that stay within the approved design. (T-25, T-26)
- [x] **T-28** **STOP GATE (BR-13).** If T-25 or T-26 can't pass within the approved design, stop all further tasks, record the evidence below in Implementation notes, and bring it to the owner. If they pass, record the results and close R-01 in the technical spec and ADR-0006. (AC-31)

## Group 6 — Tenant boundaries at sign-in (AC-06, AC-07, AC-08, AC-09, AC-13, AC-14, AC-32)
- [x] **T-29** `[TEST]` `IT/SignIn/SessionTests.cs` → `Authorize_WhenSessionInSameTenant_ShouldNotAskForCredentials` — redirects to the client with a code. (AC-06)
- [x] **T-30** `[TEST]` Same file → `Authorize_WhenSessionInAnotherTenant_ShouldAskForCredentials` — redirects to the login form and issues no code. (AC-07)
- [x] **T-31** `[TEST]` Same file → `Authorize_WhenSessionOlderThanDefault_ShouldAskForCredentials` and `Authorize_WhenSessionOlderThanConfiguredDuration_ShouldAskForCredentials` — using `FakeTimeProvider`. (AC-32)
- [x] **T-32** `[IMPL]` Session cookie options, `SessionOptions` and the tenant comparison in `AuthorizationEndpoints`. (T-29 to T-31)
- [x] **T-33** `[TEST]` `IT/SignIn/TenantInputTests.cs` → `SignIn_WhenRequestStatesAnotherTenant_ShouldUseClientTenant` — tenant A credentials give a tenant A result; tenant B credentials fail. (AC-08)
- [x] **T-34** `[TEST]` `IT/SignIn/CredentialsTests.cs` → `Login_WhenPasswordWrong_ShouldShowGenericErrorAndAllowRetry`. (AC-09)
- [x] **T-35** `[TEST]` Same file → `Login_WhenPasswordBelongsToSameUsernameInOtherTenant_ShouldFail`. (AC-13)
- [x] **T-36** `[TEST]` Same file → `Login_WhenUsernameUnknown_ShouldReturnSameResponseAsWrongPassword` — same status and same body, apart from the antiforgery token. (AC-14)
- [x] **T-37** `[TEST]` `UT/Users/ValidateCredentialsHandlerTests.cs` → `Handle_WhenUserMissing_ShouldStillRunPasswordVerification`. (AC-14)
- [x] **T-38** `[IMPL]` Any corrections to the login endpoints and handler, including the dummy hash. (T-33 to T-37)

## Group 7 — Rejected sign-in requests (AC-10, AC-11, AC-12)
- [x] **T-39** `[TEST]` `IT/SignIn/AuthorizationRequestTests.cs` → `Authorize_WhenClientUnknown_ShouldRejectWithoutRedirect`. (AC-10)
- [x] **T-40** `[TEST]` Same file → `Authorize_WhenRedirectUriDiffers_ShouldRejectWithoutRedirect` — as a theory: different path, added query, different case, different port. (AC-11)
- [x] **T-41** `[TEST]` Same file → `Authorize_WhenChallengeMissing_ShouldReject` and `Authorize_WhenChallengeMethodPlain_ShouldReject`. (AC-12)
- [x] **T-42** `[TEST]` `IT/SignIn/LoginReturnUrlTests.cs` → `Login_WhenReturnUrlIsNotLocalAuthorize_ShouldReject` — covers external and other local URLs. (Security section of the technical spec)
- [x] **T-43** `[IMPL]` Any corrections to the server configuration and `returnUrl` validation. (T-39 to T-42)

## Group 8 — Code exchange (AC-15, AC-16, AC-17, AC-18)
- [x] **T-44** `[TEST]` `IT/SignIn/CodeExchangeTests.cs` → `Exchange_WhenVerifierDoesNotMatch_ShouldIssueNoToken`. (AC-15)
- [x] **T-45** `[TEST]` Same file → `Exchange_WhenCodeOlderThanFiveMinutes_ShouldBeRejected` — using `FakeTimeProvider`. (AC-16)
- [x] **T-46** `[TEST]` Same file → `Exchange_WhenCodeAlreadyUsed_ShouldBeRejected`. (AC-17; also exercises R-02)
- [x] **T-47** `[TEST]` Same file → `Exchange_WhenPresentedByOtherTenantClient_ShouldBeRejected`. (AC-18)
- [x] **T-48** `[IMPL]` Any corrections to the handler and server configuration. (T-44 to T-47)

## Group 9 — Identity requests (AC-22, AC-23, AC-24)
- [x] **T-49** `[TEST]` `IT/SignIn/UserInfoTests.cs` → `UserInfo_WhenTokenHasNoTenant_ShouldBeUnauthorized` — a token validly signed with the test key but without `tenant_id`. (AC-22)
- [x] **T-50** `[TEST]` Same file → `UserInfo_WhenTokenExpired_ShouldBeUnauthorized` and `UserInfo_WhenTokenAltered_ShouldBeUnauthorized`. (AC-23)
- [x] **T-51** `[TEST]` Same file → `UserInfo_WhenNoToken_ShouldBeUnauthorized`. (AC-24)
- [x] **T-52** `[IMPL]` Any corrections to `TenantTokenValidationHandler`. (T-49 to T-51)

## Group 10 — Data store outage (AC-19)
- [x] **T-53** `[TEST]` `IT/Resilience/DataStoreOutageTests.cs` → `Login_WhenDatabaseUnavailable_ShouldReturnTemporaryErrorAndCreateNothing` and `Exchange_WhenDatabaseUnavailable_ShouldReturnTemporaryError` — stops the container, then restarts it and checks that no session cookie, code or token exists. (AC-19; also exercises R-02)
- [x] **T-54** `[IMPL]` `DataStoreExceptionHandler`, the command timeout and retry settings. (T-53)

## Group 11 — Running it and documentation
- [x] **T-55** `[SETUP]` Multi-stage `Dockerfile` with an image target for the service and one for the migrations bundle.
- [x] **T-56** `[SETUP]` Repository root `.env.example` and `deploy/compose/docker-compose.services.yml` — add `identity-migrations` and `identity`, with `/health` as the healthcheck.
- [x] **T-57** `[SETUP]` Run `./dev.ps1 up` from an empty volume and resolve R-03. Update the technical spec with the result.
- [x] **T-58** `[SETUP]` `services/identity/README.md` — endpoints, configuration, roles, how to run it and how to try a sign-in. Update the repository root `README.md` where it describes Quick start or configuration, and add the `features/` folder to `docs/repository-structure.md`.
- [x] **T-59** `[SETUP]` Update `02-technical-spec.md` and ADR-0006 with anything that changed during implementation, including R-01 and R-02.

## Group 12 — Review findings (T-61, approved 2026-11-11)
Fixes for the findings of `security-reviewer`, `tenant-isolation-auditor` and `resilience-reviewer` that the owner chose to close in this feature. The deferred findings are listed in the checklist.

- [x] **T-62** `[TEST]` `UT/Persistence/DataStoreFailuresTests.cs` → `IsTransient_WhenPermanentDatabaseError_ShouldBeFalse` (theory: unique violation, insufficient privilege, undefined table; direct and wrapped in `DbUpdateException`) and `IsTransient_WhenOutageOrTimeout_ShouldBeTrue` (theory: `RetryLimitExceededException`, `TimeoutException`, transient `NpgsqlException`). (Resilience H1)
- [x] **T-63** `[IMPL]` `Identity.Infrastructure/Persistence/DataStoreFailures.cs`, used by `DataStoreExceptionHandler`, so that only outages and timeouts become 503 and every other database error is a 500. (T-62)
- [x] **T-64** `[TEST]` `IT/Resilience/SlowDataStoreTests.cs` → `Login_WhenDataStoreSlow_ShouldReturnTemporaryErrorWithinBudget` and `Exchange_WhenDataStoreSlow_ShouldReturnTemporaryErrorWithinBudget` — a test interceptor delays every command; the response is 503 within the configured request timeout plus a margin. (Resilience H2)
- [x] **T-65** `[IMPL]` Request timeouts (`Identity:Requests:Timeout`, default 10 s, 503 on expiry), 2 retries instead of 3, and an explicit connect timeout and pool size in the Compose connection string. (T-64)
- [x] **T-66** `[TEST]` `IT/Isolation/ClientDirectoryTests.cs` → `ClientDirectory_WhenRuntimeRoleChangesEntry_ShouldBeRejected` (theory: UPDATE, DELETE) and `Application_WhenDirectoryNamesAnotherTenant_ShouldBeRejected`. (Isolation M1)
- [x] **T-67** `[IMPL]` Migration `ClientDirectoryIntegrity`: `UNIQUE (tenant_id, client_id)` on `client_directory` and a foreign key from `openiddict_applications (tenant_id, client_id)` to it, so an application can only exist in the tenant its directory entry names. (T-66)
- [x] **T-68** `[TEST]` `IT/Isolation/SignInRecordWriteTests.cs` → `Insert_WhenRowBelongsToAnotherTenant_ShouldBeRejected`, `UpdateAndDelete_WhenRowBelongsToAnotherTenant_ShouldAffectNothing` (theories over the three OpenIddict tables) and `Reference_WhenParentBelongsToAnotherTenant_ShouldBeRejected` (authorization → application, token → authorization). (Isolation M2)
- [x] **T-69** `[TEST]` `IT/SignIn/UserInfoTests.cs` → `UserInfo_WhenSignedTokenNamesAnotherTenant_ShouldBeUnauthorized`; `IT/SignIn/AuthorizationRequestTests.cs` → `Authorize_WhenIdTokenHintFromAnotherTenant_ShouldNotAffectTenant`. (Isolation M3)
- [x] **T-70** `[TEST]` `IT/Isolation/RlsCatalogTests.cs` → `Catalog_WhenInspected_ShouldHaveOnlyTheTenantPolicy` — exactly one permissive policy for all commands, with the expected `USING` and `WITH CHECK` expressions, on every tenant-owned table. (Isolation M4)
- [x] **T-71** `[TEST]` `IT/SignIn/ClaimReleaseTests.cs` → `SignIn_WhenProfileScopeNotRequested_ShouldNotReleaseUsername` and `SignIn_WhenProfileScopeRequested_ShouldReleaseUsername` — ID token, access token and userinfo. (Security L2)
- [x] **T-72** `[IMPL]` `AuthorizationEndpoints` adds `preferred_username` only with the `profile` scope; `UserInfoEndpoints` leaves out claims the token does not carry. (T-71)
- [x] **T-73** `[TEST]` `IT/SignIn/SessionCookieTests.cs` → `SessionCookie_WhenProduction_ShouldUseHostPrefix` and `SessionCookie_WhenTesting_ShouldUsePlainName`. (Security L4)
- [x] **T-74** `[IMPL]` Cookie name `__Host-identity.session` outside Development and Testing. (T-73)
- [x] **T-75** `[TEST]` `IT/SignIn/AuthorizationRequestTests.cs` → `Authorize_WhenRequestIsPost_ShouldCompleteSignIn`. (Security, functional bug)
- [x] **T-76** `[IMPL]` `AuthorizationEndpoints` rebuilds the login `returnUrl` from the form for POST requests. (T-75)
- [x] **T-77** `[TEST]` `IT/Resilience/DataStoreOutageTests.cs` → `Health_WhenDatabaseUnavailable_ShouldBeLiveButNotReady` — live 200 and ready 503 within 3 s; the existing health tests move to `/health/ready`. (Resilience M1)
- [x] **T-78** `[IMPL]` `/health/live` with no dependencies and `/health/ready` through `DataStoreProbe` (direct connection, `SELECT 1`, 2 s, no retries); Compose uses `/health/ready` and the `identity` service gets `restart: unless-stopped`. (T-77, resilience M3)
- [x] **T-79** `[SETUP]` Update `02-technical-spec.md`, `services/identity/README.md` and the checklist with these changes and the deferred findings.

## Final verification
- [x] **T-60** Run lint, build and the full test suite: `./dev.ps1 lint dotnet`, `./dev.ps1 build dotnet`, `./dev.ps1 test dotnet`.
- [x] **T-61** Before the PR, run `security-reviewer`, `tenant-isolation-auditor` and `resilience-reviewer`, as `CLAUDE.md` requires for security-sensitive code, and record the findings and fixes in the checklist.

## AC coverage
| AC | Test tasks |
|----|------------|
| AC-01 | T-16 |
| AC-02 | T-17 |
| AC-03 | T-18 |
| AC-04 | T-19 |
| AC-05 | T-22 |
| AC-06 | T-29 |
| AC-07 | T-30 |
| AC-08 | T-33 |
| AC-09 | T-34 |
| AC-10 | T-39 |
| AC-11 | T-40 |
| AC-12 | T-41 |
| AC-13 | T-35 |
| AC-14 | T-36, T-37 |
| AC-15 | T-44 |
| AC-16 | T-45 |
| AC-17 | T-46 |
| AC-18 | T-47 |
| AC-19 | T-53 |
| AC-20 | T-20 |
| AC-21 | T-23 |
| AC-22 | T-49 |
| AC-23 | T-50 |
| AC-24 | T-51 |
| AC-25 | T-07 |
| AC-26 | T-08, T-12 |
| AC-27 | T-09, T-12 |
| AC-28 | T-10, T-14 |
| AC-29 | T-25 |
| AC-30 | T-05 |
| AC-31 | T-26 |
| AC-32 | T-31 |

## Implementation notes
### T-28 stop gate: passed (2026-11-04)
RLS works with OpenIddict's stores within the approved design. All 26 tests pass (`./dev.ps1 run dotnet services/identity dotnet test`), with the service connecting as `identity_app`. Two temporary negative controls confirmed the proof can fail and does not depend on the EF Core filters:

| Control | Expected | Result |
|---------|----------|--------|
| A: remove RLS from `openiddict_tokens` | The proof fails | 3 failed, 23 passed: the two catalog tests for that table and `SignIn_WhenRlsEnforced_...` |
| B: remove every EF Core query filter | Sign-in works on RLS alone | 26 passed |

### Deviations
- **T-21 and T-24:** OpenIddict's entities and migration moved from T-24 to T-21, because the seeder creates applications. The migrations are `InitialLocalUsers` and `OpenIddictAndClientDirectory`.
- **T-12 and T-13:** the rule lives in `TenantWriteGuard` and is unit tested there, because testing the interceptor directly would have needed a database or an in-memory provider. The interceptor only calls it, and the integration tests cover the path.
- **T-27 (D-03):** the first run failed at `/connect/userinfo` with 401. OpenIddict's `RestoreTokenEntryProperties` reads the token entry before `ValidateTokenEntry`, so RLS hid the row. `TenantTokenValidationHandler` now runs just before `RestoreTokenEntryProperties`.
- **Packages:** `Microsoft.Extensions.Identity.Core` was removed, because the ASP.NET Core shared framework already provides it. `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` aren't used either: xUnit v3 runs on Microsoft.Testing.Platform, selected in `global.json`.
- **Naming:** `SessionOptions` became `IdentitySessionOptions`, because the original name clashes with an ASP.NET Core type.
- **Additions:** `Verify_WhenPasswordRight_ShouldSucceed` and `Set_WhenSameTenantSetAgain_ShouldKeepIt`; and the migrations revoke `identity_app`'s access to `__EFMigrationsHistory`, for AC-30.
- **`ValidateCredentialsHandler`:** written with its dummy hash in T-24, so T-37 must be checked by disabling the dummy hash.

### T-29 to T-59 (2026-11-04 and 2026-11-05)
Most of these tests passed on their first run, because OpenIddict already provides the behaviour. Each such protection was checked by disabling it temporarily (mutation) and confirming the expected test failed. All six were restored afterwards:

| Mutation | Failed as expected |
|----------|--------------------|
| M1: no dummy hash for unknown users | `Handle_WhenUserMissing_ShouldStillRunPasswordVerification` |
| M2: session not tied to its tenant | `Authorize_WhenSessionInAnotherTenant_ShouldAskForCredentials` |
| M3: configured session duration ignored | Both `Authorize_WhenSessionOlderThan...` tests |
| M4: `plain` PKCE method allowed | `Authorize_WhenChallengeMethodPlain_ShouldReject` |
| M5: no `TenantTokenValidationHandler` | `UserInfo_WhenTokenHasNoTenant_...`, `UserInfo_WhenTokenValid_...` and the RLS proof |
| M6: tenant taken from request input | `SignIn_WhenRequestStatesAnotherTenant_ShouldUseClientTenant` |

Without the handler (M5), `Exchange_WhenPresentedByOtherTenantClient_ShouldBeRejected` still passes, because OpenIddict's own client check and RLS also reject it. The handler is one of three layers there.

Deviations and findings:
- **AC-12 (T-41):** OpenIddict rejects a missing or `plain` challenge with 400 and no redirect, not with an error redirect to the client. Both are a rejection as invalid, and the test asserts what OpenIddict does.
- **T-38:** a missing antiforgery token returned 500; it now returns 400, covered by the new `Login_WhenAntiforgeryTokenMissing_ShouldReject`.
- **T-54:** data store failures return 503 through `DataStoreExceptionHandler`, with a 5-second command timeout and up to 3 retries. `/health` was added here, with `Health_WhenDatabaseAvailable_ShouldBeHealthy`.
- **R-02 closed:** the retrying strategy did not conflict with OpenIddict. Code replay (T-46) and the outage tests (T-53) pass with retries on.
- **R-03 closed:** `./dev.ps1 up` from empty volumes exits 0 in 104 seconds, with `identity-migrations` exiting 0 and `identity` healthy. The runtime image has no HTTP client, so the healthcheck uses bash.
- **Finding from the real run (T-57):** OpenIddict logged the `code_verifier` in plain text at Information level, which breaks rule 9 of `CLAUDE.md`. `appsettings.json` now sets OpenIddict, EF Core and ASP.NET Core logs to Warning, and `Logs_WhenSignInCompletes_ShouldNotContainSecrets` covers the verifier, code, access token and password. A repeated real sign-in found none of them in the container logs.
- **Gap closed:** `identity_app` could connect to `tenants_users`, because PostgreSQL allows any role to connect by default. `01-create-databases.sql` now revokes it, covered by `RuntimeRole_WhenConnectingToAnotherServiceDatabase_ShouldBeRejected`.
- **Logs:** a failed sign-in logs only the tenant id, not the client id, because the client id comes from the request.
- **Existing `.env` files:** the new required variables must be added by hand; `.env.example` and the root README say so.
- **T-60:** `./dev.ps1 build dotnet` succeeded with 0 warnings, and `./dev.ps1 test dotnet` gave 64 passed and 0 failed. `./dev.ps1 lint` first failed on `dotnet format`'s charset rule: EF Core writes migrations with a UTF-8 BOM, and `.editorconfig` requires UTF-8 without one. The BOM was removed from the five generated files, and `./dev.ps1 lint` then passed. New migrations need the same fix.

### T-61 to T-79: review findings (2026-11-11)
The three reviewers found nothing critical and no cross-tenant path. The owner chose to fix eight findings in this feature (Group 12); the rest are deferred in the checklist. `./dev.ps1 lint dotnet` passed, `./dev.ps1 build dotnet` gave 0 warnings, and `./dev.ps1 test dotnet` gave 97 passed and 0 failed.

- **Red first, as planned:** T-62, T-64, T-66 (foreign key), T-71, T-73, T-75 and T-77 failed for the expected reason before their fix. Without request timeouts, the slow data store tests took 1 minute (login) and 3 minutes (code exchange).
- **Already protected, now tested:** the `UPDATE` and `DELETE` revoke on `client_directory`, cross-tenant writes and composite keys on the OpenIddict tables, a re-signed token naming another tenant, and the exact RLS policy expression all passed on their first run. They are regression tests for layers that had no direct test.
- **T-69 deviation:** the planned `..._ShouldIssueNoCode` was wrong. OpenIddict validates an `id_token_hint` but discards it instead of rejecting the request when it fails, and the endpoint does not use hints. A tenant A hint therefore has no effect on a tenant B request; the test now asserts that the issued token is entirely tenant B. Answering `login_required` when a hint does not match the session is deferred with `prompt` and `max_age`.
- **T-67:** the foreign key goes from `openiddict_applications (tenant_id, client_id)` to `client_directory`, not the other way round, so the seeder's order (entry first) still works and an application can never exist outside its entry's tenant. EF Core wrote the new migration with a UTF-8 BOM again; it was removed.
- **T-74:** the cookie path is now set to `/` explicitly; with a `PathBase` the default would break the `__Host-` prefix.
- **T-78:** `/health` is replaced by `/health/live` and `/health/ready`. The readiness probe uses its own unpooled connection with a 2-second budget and no retries, so it no longer logs an error through the exception handler on every probe during an outage.
