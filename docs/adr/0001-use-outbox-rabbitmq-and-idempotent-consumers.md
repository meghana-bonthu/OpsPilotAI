# ADR 0001: Use Transactional Outbox, RabbitMQ, and Idempotent Consumers

## Status

Accepted

## Context

OpsPilotAI needs to publish incident-related events without creating inconsistencies between database updates and message delivery. Directly publishing a message during the same request that changes application data can fail after the database transaction succeeds, leaving the system in an inconsistent state. Message brokers can also redeliver messages, so consumers must tolerate duplicate delivery.

## Decision

OpsPilotAI uses a transactional outbox pattern for incident events.

- Application changes and outbox records are persisted together in the application database.
- A background processor publishes pending outbox messages to RabbitMQ.
- Notification consumers persist processed message identifiers and handle duplicate deliveries idempotently.
- Redis caching remains independent from message processing and uses explicit invalidation after incident mutations.
- Messaging can be disabled through configuration for environments where RabbitMQ is not available.

## Consequences

Positive consequences:

- Database changes and event publication intent are committed atomically.
- Temporary broker outages do not require application requests to fail after the database transaction succeeds.
- Duplicate message delivery does not produce duplicate notification side effects.
- The design can evolve toward other brokers without changing incident domain transactions.

Tradeoffs:

- Event publication is eventually consistent rather than immediate.
- The outbox requires background processing, retry handling, and operational monitoring.
- Processed-message records add storage and cleanup considerations.
- The initial Azure deployment keeps RabbitMQ disabled, so distributed messaging is demonstrated primarily in the local environment.
