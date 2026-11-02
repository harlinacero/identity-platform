---
name: new-event
description: Add a new domain event end to end - versioned schema in contracts/, outbox-based producer, idempotent consumer, contract test and docs. Use when the user asks to add, publish or consume a new event (security.*, user.*, role.*).
---

# New event

Adds an event following ADR-0003 (Kafka for events) and the rules in `CLAUDE.md`. All files are written in English.

## Inputs
Confirm these before writing code. Ask only for what cannot be inferred:
1. Event type, in `<domain>.<entity>.<past-tense-verb>` form (for example `security.mfa.challenged`).
2. Producing service and the state change that triggers it.
3. Consuming services and what each one does with it.
4. Payload fields, marking any personal data.

## Steps

1. **Schema.** Create `contracts/events/<event-type>/v1.schema.json` (JSON Schema draft 2020-12) using the common envelope:
   `eventId` (uuid), `eventType`, `version`, `tenantId`, `occurredAt` (date-time), `traceparent`, `data`.
   Put the payload under `data`. Never include passwords, tokens, MFA codes or secrets. Keep personal data to the minimum needed.
   Add one valid example in `contracts/events/<event-type>/v1.example.json`.

2. **Topic.** Use the domain topic (`security.events` or `user.events`) unless the volume or retention needs differ; if a new topic is needed, add it to the Compose topic init and the Helm values.

3. **Producer** (in the producing service):
   - Add the event class or record mapped to the schema.
   - Write it to the outbox in the same transaction as the state change. Do not publish directly.
   - Key the message by `tenantId`, or by aggregate id when per-aggregate ordering matters.
   - Copy the current `traceparent` into the envelope.
   - Unit test: the state change and the outbox row are saved together, and neither is saved when the transaction fails.

4. **Consumer** (in each consuming service):
   - Deserialize and validate against the schema version.
   - Deduplicate by `eventId` (processed-events table or store-level unique key).
   - Use `tenantId` from the envelope for every write.
   - Commit the offset only after successful processing; after bounded retries with backoff and jitter, send the message to the DLQ.
   - Continue the trace from `traceparent`.
   - Tests: processing the same event twice has a single effect; an invalid message ends up in the DLQ.

5. **Contract test.** Add a Pact message pact: consumer expectation in the consumer service, provider verification in the producer service.

6. **Docs.** Update the "Publishes" and "Consumes" columns in `docs/architecture/c4-containers.md`. If the event introduces a new cross-service dependency, propose an ADR.

7. **Verify.** Run the producer's and consumers' tests and report the results.

## Output
Finish with a short summary: files created or changed per service, tests added and their results, and any open questions. Suggest running the `resilience-reviewer` and `tenant-isolation-auditor` subagents on the change.
