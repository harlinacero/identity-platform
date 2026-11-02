# ADR-0004 · Local AI-assisted development with Claude Code

- **Status:** accepted
- **Date:** 2026-11-01

## Context
The project is built by one person across five languages and many cross-cutting patterns. AI-assisted development speeds this up and is itself a skill worth demonstrating. The project must not depend on paid AI APIs, either in the product or in CI.

## Decision
- Use Claude Code locally as a development tool, never as a runtime dependency.
- Version the AI configuration in the repository: `CLAUDE.md` for shared context, `.claude/agents/` for review subagents and `.claude/skills/` for repeatable workflows.
- Review subagents are advisory. Every change still goes through tests, CI and human review.
- No AI step runs in GitHub Actions, because that would require an API key.

## Alternatives considered
- Claude Code GitHub Action for automated PR review: useful, but needs an API key and adds cost.
- Keeping prompts private: hides a workflow that is part of what the project demonstrates.

## Consequences
- Anyone cloning the repo with Claude Code gets the same context, reviewers and skills.
- Agent files must be kept in sync with the architecture as it evolves.
- The quality bar does not depend on AI: CI, tests and ADRs remain the source of truth.
