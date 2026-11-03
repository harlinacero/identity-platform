## Summary
What changes and why. Link the issue, ADR or build-plan phase.

## Testing
Commands you ran and their results, and anything left unverified.

## Checklist
- [ ] The title follows Conventional Commits (`feat(identity): ...`).
- [ ] New behaviour has tests, and they run in containers (`./dev.ps1 test <stack>`).
- [ ] Queries on tenant-owned data filter by `tenant_id`, and new endpoints or consumers have a cross-tenant test.
- [ ] Events and commands changed in `contracts/` first; breaking changes use a new version.
- [ ] Outbound calls have a timeout, retries and, for external dependencies, a circuit breaker.
- [ ] No secrets, tokens or credentials in code, config or logs.
- [ ] Cross-cutting decisions are recorded as an ADR.
- [ ] Security-sensitive changes went through `security-reviewer`, `tenant-isolation-auditor` and `resilience-reviewer`.
