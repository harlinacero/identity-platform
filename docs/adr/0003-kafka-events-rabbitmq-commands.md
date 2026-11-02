# ADR-0003 · Kafka for events, RabbitMQ for commands

- **Status:** accepted
- **Date:** 2026-11-01

## Context
The platform produces two kinds of messages. **Events** about security and users are immutable facts that several consumers (Audit, Risk) read at their own pace and that should be replayable. **Commands**, such as sending an MFA code, have a single recipient, must be executed once, and need retries and a dead-letter queue.

## Decision
- Kafka for events (`security.*`, `user.*`), partitioned by `tenantId`, with retention that allows replay.
- RabbitMQ for commands (`notifications.*`), with retries with backoff, a DLQ and idempotent consumers.
- Both behind abstractions (`IEventPublisher`, `ICommandBus`) so they can be switched to Event Hubs or MSK, and to Service Bus or SQS, through configuration.

## Alternatives considered
- Kafka only: commands work, but DLQs and per-message retries are more manual.
- RabbitMQ only: loses the retention and replay that auditing needs.

## Consequences
- Two brokers to operate locally and in the cloud.
- Each broker is used where it is strongest, and the project demonstrates both models.
- Event and command contracts live separately in `contracts/`.
