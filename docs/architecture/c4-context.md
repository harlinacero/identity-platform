# C4 · Level 1: System Context

Who uses the platform and which external systems it talks to. Locally, external systems are simulated with containers (OpenLDAP, Mailpit and a test SCIM client).

```mermaid
flowchart TB
    user["<b>End user</b><br/>[Person]<br/>Signs in, completes MFA<br/>and manages their own account"]
    admin["<b>Tenant administrator</b><br/>[Person]<br/>Manages users and roles,<br/>reviews the audit trail"]

    platform["<b>Multi-tenant identity platform</b><br/>[Software system]<br/>OAuth2/OIDC, per-tenant users and RBAC,<br/>MFA, risk scoring and security auditing"]

    rp["<b>Client applications</b><br/>[External system]<br/>Tenant apps that delegate<br/>sign-in through OIDC"]
    hr["<b>HR system or corporate IdP</b><br/>[External system]<br/>Provisions and deactivates users"]
    ldap["<b>Corporate LDAP directory</b><br/>[External system]<br/>OpenLDAP locally"]
    mail["<b>Email / SMS provider</b><br/>[External system]<br/>Mailpit locally"]

    user -- "Sign-in, MFA and self-service<br/>[HTTPS]" --> platform
    admin -- "Manages tenants, users,<br/>roles and audit [HTTPS]" --> platform
    rp -- "Authorization Code + PKCE,<br/>client credentials [OIDC]" --> platform
    hr -- "Provisioning<br/>[SCIM 2.0 / HTTPS]" --> platform
    platform -- "Authenticates corporate users<br/>[LDAP / LDAPS]" --> ldap
    platform -- "MFA codes and alerts<br/>[SMTP / API]" --> mail

    classDef person fill:#08427b,stroke:#052e56,color:#fff
    classDef system fill:#1168bd,stroke:#0b4884,color:#fff
    classDef external fill:#999999,stroke:#6b6b6b,color:#fff
    class user,admin person
    class platform system
    class rp,hr,ldap,mail external
```

## Notes
- **Multi-tenant:** each tenant has its own users, roles, OIDC clients and, optionally, its own LDAP directory.
- **Trust boundary:** all external traffic enters through a single entry point (the Gateway, see level 2).
- **Cloud equivalents:** see the table in the README; the diagram is the same for local, Azure and AWS.
