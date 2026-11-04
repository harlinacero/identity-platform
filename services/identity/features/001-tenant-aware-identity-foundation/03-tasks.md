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
- [ ] **T-29** `[TEST]` `IT/SignIn/SessionTests.cs` → `Authorize_WhenSessionInSameTenant_ShouldNotAskForCredentials` — redirects to the client with a code. (AC-06)
- [ ] **T-30** `[TEST]` Same file → `Authorize_WhenSessionInAnotherTenant_ShouldAskForCredentials` — redirects to the login form and issues no code. (AC-07)
- [ ] **T-31** `[TEST]` Same file → `Authorize_WhenSessionOlderThanDefault_ShouldAskForCredentials` and `Authorize_WhenSessionOlderThanConfiguredDuration_ShouldAskForCredentials` — using `FakeTimeProvider`. (AC-32)
- [ ] **T-32** `[IMPL]` Session cookie options, `SessionOptions` and the tenant comparison in `AuthorizationEndpoints`. (T-29 to T-31)
- [ ] **T-33** `[TEST]` `IT/SignIn/TenantInputTests.cs` → `SignIn_WhenRequestStatesAnotherTenant_ShouldUseClientTenant` — tenant A credentials give a tenant A result; tenant B credentials fail. (AC-08)
- [ ] **T-34** `[TEST]` `IT/SignIn/CredentialsTests.cs` → `Login_WhenPasswordWrong_ShouldShowGenericErrorAndAllowRetry`. (AC-09)
- [ ] **T-35** `[TEST]` Same file → `Login_WhenPasswordBelongsToSameUsernameInOtherTenant_ShouldFail`. (AC-13)
- [ ] **T-36** `[TEST]` Same file → `Login_WhenUsernameUnknown_ShouldReturnSameResponseAsWrongPassword` — same status and same body, apart from the antiforgery token. (AC-14)
- [ ] **T-37** `[TEST]` `UT/Users/ValidateCredentialsHandlerTests.cs` → `Handle_WhenUserMissing_ShouldStillRunPasswordVerification`. (AC-14)
- [ ] **T-38** `[IMPL]` Any corrections to the login endpoints and handler, including the dummy hash. (T-33 to T-37)

## Group 7 — Rejected sign-in requests (AC-10, AC-11, AC-12)
- [ ] **T-39** `[TEST]` `IT/SignIn/AuthorizationRequestTests.cs` → `Authorize_WhenClientUnknown_ShouldRejectWithoutRedirect`. (AC-10)
- [ ] **T-40** `[TEST]` Same file → `Authorize_WhenRedirectUriDiffers_ShouldRejectWithoutRedirect` — as a theory: different path, added query, different case, different port. (AC-11)
- [ ] **T-41** `[TEST]` Same file → `Authorize_WhenChallengeMissing_ShouldReject` and `Authorize_WhenChallengeMethodPlain_ShouldReject`. (AC-12)
- [ ] **T-42** `[TEST]` `IT/SignIn/LoginReturnUrlTests.cs` → `Login_WhenReturnUrlIsNotLocalAuthorize_ShouldReject` — covers external and other local URLs. (Security section of the technical spec)
- [ ] **T-43** `[IMPL]` Any corrections to the server configuration and `returnUrl` validation. (T-39 to T-42)

## Group 8 — Code exchange (AC-15, AC-16, AC-17, AC-18)
- [ ] **T-44** `[TEST]` `IT/SignIn/CodeExchangeTests.cs` → `Exchange_WhenVerifierDoesNotMatch_ShouldIssueNoToken`. (AC-15)
- [ ] **T-45** `[TEST]` Same file → `Exchange_WhenCodeOlderThanFiveMinutes_ShouldBeRejected` — using `FakeTimeProvider`. (AC-16)
- [ ] **T-46** `[TEST]` Same file → `Exchange_WhenCodeAlreadyUsed_ShouldBeRejected`. (AC-17; also exercises R-02)
- [ ] **T-47** `[TEST]` Same file → `Exchange_WhenPresentedByOtherTenantClient_ShouldBeRejected`. (AC-18)
- [ ] **T-48** `[IMPL]` Any corrections to the handler and server configuration. (T-44 to T-47)

## Group 9 — Identity requests (AC-22, AC-23, AC-24)
- [ ] **T-49** `[TEST]` `IT/SignIn/UserInfoTests.cs` → `UserInfo_WhenTokenHasNoTenant_ShouldBeUnauthorized` — a token validly signed with the test key but without `tenant_id`. (AC-22)
- [ ] **T-50** `[TEST]` Same file → `UserInfo_WhenTokenExpired_ShouldBeUnauthorized` and `UserInfo_WhenTokenAltered_ShouldBeUnauthorized`. (AC-23)
- [ ] **T-51** `[TEST]` Same file → `UserInfo_WhenNoToken_ShouldBeUnauthorized`. (AC-24)
- [ ] **T-52** `[IMPL]` Any corrections to `TenantTokenValidationHandler`. (T-49 to T-51)

## Group 10 — Data store outage (AC-19)
- [ ] **T-53** `[TEST]` `IT/Resilience/DataStoreOutageTests.cs` → `Login_WhenDatabaseUnavailable_ShouldReturnTemporaryErrorAndCreateNothing` and `Exchange_WhenDatabaseUnavailable_ShouldReturnTemporaryError` — stops the container, then restarts it and checks that no session cookie, code or token exists. (AC-19; also exercises R-02)
- [ ] **T-54** `[IMPL]` `DataStoreExceptionHandler`, the command timeout and retry settings. (T-53)

## Group 11 — Running it and documentation
- [ ] **T-55** `[SETUP]` Multi-stage `Dockerfile` with an image target for the service and one for the migrations bundle.
- [ ] **T-56** `[SETUP]` Repository root `.env.example` and `deploy/compose/docker-compose.services.yml` — add `identity-migrations` and `identity`, with `/health` as the healthcheck.
- [ ] **T-57** `[SETUP]` Run `./dev.ps1 up` from an empty volume and resolve R-03. Update the technical spec with the result.
- [ ] **T-58** `[SETUP]` `services/identity/README.md` — endpoints, configuration, roles, how to run it and how to try a sign-in. Update the repository root `README.md` where it describes Quick start or configuration, and add the `features/` folder to `docs/repository-structure.md`.
- [ ] **T-59** `[SETUP]` Update `02-technical-spec.md` and ADR-0006 with anything that changed during implementation, including R-01 and R-02.

## Final verification
- [ ] **T-60** Run lint, build and the full test suite: `./dev.ps1 lint dotnet`, `./dev.ps1 build dotnet`, `./dev.ps1 test dotnet`.
- [ ] **T-61** Before the PR, run `security-reviewer`, `tenant-isolation-auditor` and `resilience-reviewer`, as `CLAUDE.md` requires for security-sensitive code, and record the findings and fixes in the checklist.

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
