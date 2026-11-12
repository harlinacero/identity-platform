# [001] Tenant-aware sign-in foundation — Business Specification

> Status: Approved (2026-11-03)
> Owning service: `services/identity`
> Author / requested by: Harlin Acero

## 1. Summary
End users sign in with a local account through an application that belongs to their tenant, and that application receives proof of who they are and which tenant they belong to. The data behind sign-in stays isolated between tenants, even if application code makes a mistake.

## 2. Business goal / problem
Nothing can be signed in to yet, and the tenant model must exist before the identity data takes shape (ADR-0006). Adding tenants later would mean migrating every client, user and sign-in record. The largest open risk is whether the second, independent isolation layer works with the sign-in records. This feature answers that early, before more is built on top.

## 3. Actors
| Actor | Description | Role / permission |
|-------|-------------|-------------------|
| End user | A person with a local account in one tenant | Signs in and views their own identity |
| Client application | An application registered to exactly one tenant, running where it cannot keep a secret (for example, in a browser) | Requests sign-ins and receives their result |
| Developer | Someone running the platform locally or in automated tests | Starts the environment with its test data |

## 4. User stories
- **US-01** — As a developer, I want a local and test environment with two tenants, their applications and their users, so that I can exercise and test sign-in and isolation without an administration tool.
- **US-02** — As an end user, I want to sign in through my tenant's application, so that I can use it with my account.
- **US-03** — As a tenant, I want a stolen one-time sign-in code to be useless, so that intercepting a redirect does not give access to my users' accounts.
- **US-04** — As a client application, I want to find out who the signed-in user is and which tenant they belong to, so that I can serve the right data.
- **US-05** — As the platform owner, I want isolation to hold even if application code forgets a tenant check, so that a single bug cannot expose another tenant's data.

## 5. Business rules
- **BR-01** — An application belongs to exactly one tenant. During sign-in, the tenant is decided only by the application. Any tenant value supplied by the requester is ignored.
- **BR-02** — Credentials are checked only against users of the application's tenant. The same username in two tenants means two separate accounts with independent passwords.
- **BR-03** — A failed sign-in does not reveal whether the username exists, in this or in any other tenant.
- **BR-04** — Passwords are never stored in a readable or reversible form.
- **BR-05** — Every sign-in request must include a one-time possession proof. A request without one, or with a weak form of one, is rejected.
- **BR-06** — A sign-in code works once, expires after 5 minutes, and can be exchanged only by the application that requested it, presenting the matching proof.
- **BR-07** — After sign-in, the user only returns to an address registered exactly for that application.
- **BR-08** — Access credentials expire after 15 minutes and always state the tenant. A credential without a tenant is rejected.
- **BR-09** — A sign-in session is valid only for the tenant in which it started. It never signs the user in to another tenant. It lasts 15 minutes by default, and that duration can be changed through configuration, without changing code.
- **BR-10** — Tenant-owned sign-in data is protected by two independent layers: the application's filtering and the data store's own enforcement. If no tenant has been established, nothing is returned.
- **BR-11** — The service's normal data access cannot bypass the data store's enforcement. Changing the data structure uses a separate, more privileged access.
- **BR-12** — Test data exists only in local and test environments, and never in production. Its secrets come from local configuration and are never stored in the repository.
- **BR-13** — If the second isolation layer cannot protect the sign-in records, work stops and the owner receives the evidence before any alternative is chosen (see UC-05).

## 6. Use cases

### UC-01 — Prepare the test environment (US-01)
- **Actor:** Developer
- **Preconditions:** The local configuration provides the test secrets.
- **Main flow:**
  1. The developer starts the service in a local or test environment.
  2. The system creates two tenants, one application for each, and users for each, including the same username in both tenants with different passwords.
  3. The service becomes available.
- **Alternative flows:**
  - **A1** (at step 2) — The test data already exists → nothing is duplicated or changed, and the flow continues at step 3.
- **Exception flows:**
  - **E1** (at step 2) — A required secret is missing → the service does not start and reports which setting is missing, without showing any secret value.
  - **E2** (at step 1) — The service starts in production → no test data is created.
- **Postconditions:** Exactly two test tenants exist, with their applications and users. On failure, the service is not available.

### UC-02 — Sign in through a tenant's application (US-02, US-03)
- **Actor:** End user, through a client application
- **Preconditions:** The application is registered and the user has an account in its tenant.
- **Main flow:**
  1. The application sends the user to the platform with its identity, its return address and a one-time possession proof.
  2. The system identifies the application and, from it, the tenant.
  3. The system asks the user for their username and password.
  4. The user submits their credentials.
  5. The system checks them against that tenant's users only.
  6. The system starts a session for that tenant and returns the user to the application with a one-time sign-in code.
  7. The application exchanges the code, presenting its proof.
  8. The system returns an access credential that states the user and the tenant.
- **Alternative flows:**
  - **A1** (at step 3) — The user already has a session in this tenant → the system skips the credentials and continues at step 6.
  - **A2** (at step 3) — The user has a session in another tenant → that session is ignored and the system asks for credentials.
  - **A3** (at step 4) — The request also states a tenant → that value is ignored and the application's tenant applies.
  - **A4** (at step 4) — The user has entered a wrong password → the system shows the generic failure and lets them try again from step 3.
  - **A5** (at step 3) — The user's session has passed its configured duration → the session is ignored and the system asks for credentials.
- **Exception flows:**
  - **E1** (at step 2) — The application is unknown → the request is rejected and the user is not redirected.
  - **E2** (at step 2) — The return address is not registered exactly for the application → the request is rejected and the user is not redirected.
  - **E3** (at step 2) — The possession proof is missing or weak → the request is rejected as invalid.
  - **E4** (at step 5) — The username exists only in another tenant → the same generic failure as a wrong password, and the other tenant's credentials are never checked.
  - **E5** (at step 5) — The username does not exist in any tenant → the same generic failure.
  - **E6** (at step 7) — The presented proof does not match → the exchange is rejected and no access credential is issued.
  - **E7** (at step 7) — The code is expired → the exchange is rejected.
  - **E8** (at step 7) — The code has already been used → the exchange is rejected.
  - **E9** (at step 7) — A different application presents the code, including one from another tenant → the exchange is rejected.
  - **E10** (any step) — The data store is unavailable → the sign-in fails with a temporary error, and no session, code or credential is created.
- **Postconditions:** On success, the application holds an access credential for this user and tenant. On failure, no credential exists and no session is started.

### UC-03 — Find out who the signed-in user is (US-04)
- **Actor:** Client application
- **Preconditions:** The application holds an access credential.
- **Main flow:**
  1. The application asks for the identity of the signed-in user, presenting the credential.
  2. The system validates the credential.
  3. The system returns the user's identity and tenant.
- **Alternative flows:** None.
- **Exception flows:**
  - **E1** (at step 2) — The credential has no tenant → rejected as not authorized.
  - **E2** (at step 2) — The credential is expired or has been altered → rejected as not authorized.
  - **E3** (at step 2) — No credential is presented → rejected as not authorized.
- **Postconditions:** No data changes.

### UC-04 — Keep tenants isolated when code makes a mistake (US-05)
- **Actor:** Data store, on behalf of the platform owner
- **Preconditions:** Sign-in data exists for both tenants.
- **Main flow:**
  1. The service reads or changes sign-in data while acting for tenant A.
  2. The data store limits the operation to tenant A's data, even if the application's own filter was left out.
- **Alternative flows:** None.
- **Exception flows:**
  - **E1** (at step 1) — No tenant has been established → reads return nothing, and writes are rejected.
  - **E2** (at step 1) — Code acting for tenant A tries to write data for tenant B → the write is rejected.
  - **E3** (at step 1) — A tenant from an earlier operation could carry over to the next one → it does not; each operation establishes its own tenant.
- **Postconditions:** Tenant B's data is unchanged and has not been exposed.

### UC-05 — Prove isolation covers the sign-in records (US-05)
- **Actor:** Developer, through automated verification
- **Preconditions:** The complete sign-in flow of UC-02 works.
- **Main flow:**
  1. The verification runs complete sign-ins for both tenants with the data store's enforcement turned on.
  2. Sign-ins succeed, and each tenant's sign-in records are unreachable from the other tenant.
  3. The verification confirms that the service's normal access cannot bypass the enforcement.
- **Alternative flows:** None.
- **Exception flows:**
  - **E1** (at step 2) — Enforcement breaks sign-in, or leaves any sign-in record unprotected → work stops, and the owner receives the evidence before any alternative is chosen.
- **Postconditions:** Either the risk recorded in ADR-0006 is closed, or the owner decides how to proceed.

## 7. Acceptance criteria
- **AC-01** (UC-01 main, BR-02) — *Given* an empty test environment with its secrets configured, *when* the service starts, *then* two tenants exist with one application each, and both have a user with the same username and different passwords.
- **AC-02** (UC-01 A1) — *Given* the test data already exists, *when* the service starts again, *then* nothing is duplicated or changed.
- **AC-03** (UC-01 E1, BR-12) — *Given* a required secret is missing, *when* the service starts, *then* it does not start, and it names the setting without showing any secret value.
- **AC-04** (UC-01 E2, BR-12) — *Given* a production environment, *when* the service starts, *then* no test data is created.
- **AC-05** (UC-02 main, BR-01, BR-08) — *Given* a user of tenant A, *when* they complete a sign-in through tenant A's application, *then* the application receives an access credential that expires after 15 minutes and states that user and tenant A.
- **AC-06** (UC-02 A1, BR-09) — *Given* a user with a session in tenant A, *when* tenant A's application starts another sign-in, *then* they are not asked for credentials and the application receives a code.
- **AC-07** (UC-02 A2, BR-09) — *Given* a user with a session in tenant A, *when* tenant B's application starts a sign-in, *then* they are asked for credentials and no code is issued from that session.
- **AC-08** (UC-02 A3, BR-01) — *Given* a sign-in through tenant A's application that also states tenant B, *when* the user submits tenant A credentials, *then* the result is for tenant A; and when they submit only tenant B credentials, the sign-in fails.
- **AC-09** (UC-02 A4, BR-03) — *Given* a wrong password, *when* it is submitted, *then* the user sees the generic failure, can try again, and can succeed with the right password.
- **AC-10** (UC-02 E1) — *Given* an unknown application, *when* it starts a sign-in, *then* it is rejected and the user is not redirected.
- **AC-11** (UC-02 E2, BR-07) — *Given* a return address that differs in any way from the registered one, *when* a sign-in starts, *then* it is rejected and the user is not redirected.
- **AC-12** (UC-02 E3, BR-05) — *Given* a sign-in request with a missing or weak possession proof, *when* it is received, *then* it is rejected as invalid.
- **AC-13** (UC-02 E4, BR-02, BR-03) — *Given* the same username in both tenants, *when* the user enters tenant B's password through tenant A's application, *then* they see the generic failure and tenant B's credentials are never checked.
- **AC-14** (UC-02 E5, BR-03) — *Given* a username that does not exist, *when* it is submitted, *then* the failure is identical to that of a wrong password.
- **AC-15** (UC-02 E6, BR-06) — *Given* a valid code, *when* it is exchanged with a proof that does not match, *then* no access credential is issued.
- **AC-16** (UC-02 E7, BR-06) — *Given* a code issued more than 5 minutes ago, *when* it is exchanged, *then* it is rejected.
- **AC-17** (UC-02 E8, BR-06) — *Given* a code that was already exchanged, *when* it is exchanged again, *then* it is rejected.
- **AC-18** (UC-02 E9, BR-06) — *Given* a code issued to tenant A's application, *when* tenant B's application presents it, *then* it is rejected.
- **AC-19** (UC-02 E10) — *Given* the data store is unavailable, *when* a sign-in is attempted, *then* it fails with a temporary error, and no session, code or credential is created.
- **AC-20** (BR-04) — *Given* a stored user, *when* their stored record is inspected, *then* the password is not readable or reversible.
- **AC-21** (UC-03 main) — *Given* a valid access credential, *when* the application asks for the user's identity, *then* it receives that user and their tenant.
- **AC-22** (UC-03 E1, BR-08) — *Given* a validly signed credential without a tenant, *when* it is presented, *then* it is rejected as not authorized.
- **AC-23** (UC-03 E2) — *Given* an expired or altered credential, *when* it is presented, *then* it is rejected as not authorized.
- **AC-24** (UC-03 E3) — *Given* no credential, *when* identity is requested, *then* the request is rejected as not authorized.
- **AC-25** (UC-04 main, BR-10) — *Given* data for both tenants, *when* a read or change for tenant A leaves out the application's filter, *then* it reaches only tenant A's data.
- **AC-26** (UC-04 E1, BR-10) — *Given* no established tenant, *when* sign-in data is read or written, *then* reads return nothing and writes are rejected.
- **AC-27** (UC-04 E2, BR-10) — *Given* an operation for tenant A, *when* it tries to write data for tenant B, *then* the write is rejected.
- **AC-28** (UC-04 E3, BR-10) — *Given* an operation for tenant A has finished, *when* the next operation runs on the same reused connection without establishing a tenant, *then* it sees no tenant A data.
- **AC-29** (UC-05 main, BR-10) — *Given* the enforcement is on, *when* complete sign-ins run for both tenants, *then* they succeed and each tenant's sign-in records are unreachable from the other.
- **AC-30** (UC-05 main, BR-11) — *Given* the service's normal data access, *when* its privileges are inspected, *then* it cannot bypass the enforcement or change the data structure.
- **AC-31** (UC-05 E1, BR-13) — *Given* the enforcement breaks sign-in or leaves a sign-in record unprotected, *when* the verification runs, *then* it fails and names the affected records, and the evidence goes to the owner before work continues.
- **AC-32** (UC-02 A5, BR-09) — *Given* no session duration is configured, *when* a session is older than 15 minutes, *then* the user is asked for credentials; and *given* a different configured duration, the session ends after that duration instead.

### Flow coverage
| Flow | AC |
|------|----|
| UC-01 main | AC-01 |
| UC-01 A1 | AC-02 |
| UC-01 E1 | AC-03 |
| UC-01 E2 | AC-04 |
| UC-02 main | AC-05, AC-20 |
| UC-02 A1 | AC-06 |
| UC-02 A2 | AC-07 |
| UC-02 A3 | AC-08 |
| UC-02 A4 | AC-09 |
| UC-02 A5 | AC-32 |
| UC-02 E1 | AC-10 |
| UC-02 E2 | AC-11 |
| UC-02 E3 | AC-12 |
| UC-02 E4 | AC-13 |
| UC-02 E5 | AC-14 |
| UC-02 E6 | AC-15 |
| UC-02 E7 | AC-16 |
| UC-02 E8 | AC-17 |
| UC-02 E9 | AC-18 |
| UC-02 E10 | AC-19 |
| UC-03 main | AC-21 |
| UC-03 E1 | AC-22 |
| UC-03 E2 | AC-23 |
| UC-03 E3 | AC-24 |
| UC-04 main | AC-25 |
| UC-04 E1 | AC-26 |
| UC-04 E2 | AC-27 |
| UC-04 E3 | AC-28 |
| UC-05 main | AC-29, AC-30 |
| UC-05 E1 | AC-31 |

## 8. Out of scope
- **Pending: inactive tenants.** Whether an inactive tenant's users can receive an access credential. The state arrives with Tenants & Users in Phase 2, and it is not simulated here.
- Application-to-application access with no user (client credentials), renewing access credentials, and token rotation: later Phase 1 features.
- Signing out, the Gateway and the final React sign-in experience: later Phase 1 features.
- Tenant, user and application administration: Phase 2 onwards.
- Lockout, rate limiting, risk scoring and MFA: Phase 4.
- Sign-in events and auditing: Phase 3.
- Corporate directory sign-in: Phase 5.
- Applications that can keep a secret.

## 9. Glossary (new terms only)
- **Possession proof** — A one-time secret that the application creates for each sign-in. It sends a derived form at the start and reveals the original when exchanging the code, which shows it is the application that began the sign-in.
- **Sign-in code** — A short-lived, single-use value returned to the application after sign-in, which it exchanges for an access credential.
- **Access credential** — A short-lived, signed statement of who the user is and which tenant they belong to.

## 10. Assumptions & open questions
- [x] Q-01 — What should the time limits be? (answer: 15 minutes for an access credential, 5 minutes for a sign-in code, and 15 minutes by default for a session, changeable through configuration)
- [x] Q-02 — Where does the sign-in form live until React is ready? (answer: a minimal form served by this service, to be replaced by React in a later feature)
- [x] Q-03 — What happens if the second isolation layer does not work with the sign-in records? (answer: stop and bring the evidence to the owner, BR-13)
- [x] Q-04 — How does test data get created? (answer: a seeder for local and test environments only, with secrets from local configuration, BR-12)
- [x] Q-05 — Can an inactive tenant's users receive a credential? (answer: out of this feature and recorded as pending, without simulating the state)
