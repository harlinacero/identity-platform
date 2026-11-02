# AI-Assisted Development

This project is built with [Claude Code](https://claude.com/claude-code) running locally. The AI configuration is versioned in the repository so the workflow is reproducible and reviewable. No AI APIs are called by the product or by CI (see ADR-0004).

## What is in the repository

| Path | Purpose |
| --- | --- |
| `CLAUDE.md` | Shared project context: services, commands, non-negotiable rules, contracts and workflow. |
| `.claude/agents/security-reviewer.md` | Reviews OAuth2/OIDC, credential, session and OWASP issues. |
| `.claude/agents/tenant-isolation-auditor.md` | Hunts for cross-tenant data leaks along every data path. |
| `.claude/agents/resilience-reviewer.md` | Reviews timeouts, retries, circuit breakers, outbox, idempotency and DLQs. |
| `.claude/skills/new-event/` | Adds an event end to end: schema, outbox producer, idempotent consumer, Pact test, docs. |
| `.claude/skills/verify-phase/` | Checks a build-plan phase against its definition of done with evidence. |

## How it is used

1. **Plan.** Each feature starts from the build plan and, for larger features, a short spec.
2. **Implement.** Claude Code works with the context in `CLAUDE.md`; skills handle repeatable work such as adding events.
3. **Review.** Before a PR on sensitive code, the three subagents review the diff. Their findings are advisory.
4. **Verify.** Tests and CI are the source of truth. `verify-phase` checks a phase against its definition of done before it is closed.
5. **Decide.** Cross-cutting decisions are recorded as ADRs, whether or not AI suggested them.

## Running the reviewers

In Claude Code, from the repository root:

```text
Use the security-reviewer, tenant-isolation-auditor and resilience-reviewer subagents to review my changes against main.
```

## Guardrails
- All files are written in English.
- AI output is reviewed like any other contribution: tests, CI and human review.
- Secrets never go into prompts, agent files or skills.
