---
name: verify-phase
description: Check a build-plan phase against its scope and definition of done, run the relevant checks, and report what is done, missing or unverifiable. Use when the user asks whether a phase is finished, what is left in a phase, or wants to close a phase.
---

# Verify phase

Checks a phase from `docs/build-plan.md` against the actual repository. Report in English.

## Inputs
The phase number. If it is not given, infer the current phase from the `Status` line in `README.md` and confirm it.

## Steps

1. Read the phase section in `docs/build-plan.md`. Split it into individual items: every **Scope** bullet and every **Definition of done** bullet.

2. For each item, find evidence in the repository:
   - Code, configuration or files that implement it (give paths).
   - Tests that prove it (give test names).
   - ADRs that the phase requires.
   - CI workflow jobs that run those tests.

3. Run the checks that are cheap and safe to run locally, always in containers through `./dev.ps1 run <tool> <dir> <command...>` (ADR-0005):
   - Build and unit tests of the services touched by the phase.
   - `docker compose config` for Compose files; `helm lint` and `terraform fmt -check` / `terraform validate` when the phase includes them.
   - Do not run long load tests or destroy local data without asking first.

4. Classify each item:
   - **Done**: implemented and proven by a test or a command you ran.
   - **Partial**: implemented but not tested, or tested only in part.
   - **Missing**: no implementation found.
   - **Manual check**: needs a human (for example, a demo, a video or a timing on a clean machine). Explain how to check it.

5. Apply the global rule: a phase is not done unless it runs with `./dev.ps1 up` or `make up` on a clean machine that has only Docker, and its tests run in CI. Check the CI workflows for the latter.

## Output
A table with the columns `Item`, `Status`, `Evidence` and `Next action`, followed by:
- Overall verdict: `Phase N complete`, or `Phase N not complete: X items left`.
- An ordered list of the smallest set of tasks needed to finish the phase.

Do not mark an item as done without evidence. Do not update `README.md` or the build plan unless the user asks; when a phase is complete, offer to update the `Status` line.
