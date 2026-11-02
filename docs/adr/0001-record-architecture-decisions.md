# ADR-0001 · Record architecture decisions

- **Status:** accepted
- **Date:** 2026-11-01

## Context
The project combines five languages, two brokers and two clouds. Without a record, the reasoning behind each choice is lost and the repository shows only the result, not the judgment behind it.

## Decision
Every decision that affects more than one service, security or deployment is recorded as an ADR in `docs/adr/`, using `0000-template.md`, and reviewed in the same PR that implements it. ADRs are written in English.

## Alternatives considered
- GitHub wiki: lives outside the code's version control.
- Code comments: do not cover cross-cutting decisions.

## Consequences
More work per PR, in exchange for a history that explains the decisions to anyone reviewing the project.
