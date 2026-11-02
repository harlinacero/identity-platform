# ADR-0002 · Polyglot monorepo

- **Status:** accepted
- **Date:** 2026-11-01

## Context
Seven services in five languages, two frontends, shared contracts, Helm and Terraform. It is a one-person project that must be easy to clone and run within minutes.

## Decision
A single repository with `services/`, `frontends/`, `contracts/`, `deploy/` and `infra/` folders. CI uses path filters to build and test only what changed.

## Alternatives considered
- One repository per service: more realistic for large organizations, but it multiplies CI configuration and makes changes that touch contracts and several services at once harder.

## Consequences
- Atomic changes across contracts and services in a single PR.
- Services remain independent: each has its own image, version and chart, and none imports code from another.
- CI path filters must be maintained carefully so the pipeline stays fast.
