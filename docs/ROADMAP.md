# Implementation roadmap

## Phase 1 — Foundation

- Repository and application structure
- Incident entity and SQL persistence
- Create/list API endpoints
- Angular incident list
- OpenAPI documentation

## Phase 2 — Secure workflow

- ASP.NET Core Identity or external OIDC provider
- JWT authentication and role-based policies
- Team assignment and lifecycle rules
- Immutable audit history
- Global Problem Details and structured logging

## Phase 3 — Quality

- Domain unit tests with xUnit
- API integration tests using WebApplicationFactory and Testcontainers
- Angular component and service tests
- GitHub Actions build, test, and security scanning

## Phase 4 — Distributed capabilities

- Outbox pattern
- Azure Service Bus or RabbitMQ events
- Background notification worker
- Redis caching with explicit invalidation
- Idempotent message consumers

## Phase 5 — AI and search

- Generate incident summaries through an isolated AI gateway
- Require human approval for suggested actions
- Redact sensitive content before external requests
- Semantic incident search with clear fallback behavior
- Prompt/version tracking and evaluation dataset

## Phase 6 — Cloud operations

- Azure App Service, Azure SQL, Key Vault, and Application Insights
- Bicep infrastructure definitions
- OpenTelemetry traces, logs, and metrics
- Health checks, alerts, deployment slots, and rollback notes
- Threat model, architecture decision records, and demo video
