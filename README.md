# OpsPilotAI

OpsPilotAI is a production-style incident and workflow management platform built with ASP.NET Core, Angular, SQL Server, distributed messaging, caching, observability, infrastructure as code, and AI-assisted workflows.

The project demonstrates full-stack .NET engineering practices including authentication and authorization, incident lifecycle management, application-level audit history, automated testing, transactional messaging, idempotent consumers, Redis caching, human-reviewed AI assistance, OpenTelemetry observability, CI validation, and Azure deployment architecture.

## Project Status

The planned implementation roadmap is complete through the core application, distributed capabilities, AI features, automated quality controls, and Azure deployment design.

Current validation includes:

- 59 passing .NET backend and API integration tests
- 6 passing Angular component and service tests
- Successful Angular production build
- GitHub Actions validation for backend, frontend, dependency auditing, and Bicep infrastructure
- Bicep definitions for Azure App Service, Azure SQL, Key Vault, Application Insights, Log Analytics, deployment slots, and Azure Monitor alerting
- Deployment and rollback documentation
- Threat model and architecture decision records

The Azure infrastructure is defined and validated but has not yet been provisioned as a live paid deployment.

## Core Capabilities

- Secure JWT authentication with Reporter, Responder, and Administrator roles
- Server-side incident ownership and role-based authorization
- Incident creation, queue retrieval, assignment, and lifecycle transitions
- Chronological status and team-assignment audit history
- Global Problem Details responses and structured request logging
- Transactional outbox processing with RabbitMQ
- Idempotent notification consumption
- Redis caching with explicit invalidation
- AI-assisted incident summaries and suggested actions
- Mandatory human approval or rejection of AI-suggested actions
- Sensitive-data redaction before AI gateway processing
- Semantic incident search with deterministic fallback behavior
- AI prompt/version tracking and evaluation data
- Liveness and SQL readiness health checks
- OpenTelemetry tracing and metrics with optional Azure Monitor export

## Architecture

```text
Angular Client
      |
      v
ASP.NET Core API
      |
      +--> Identity / JWT / Authorization
      +--> Incident Domain + Audit History
      +--> EF Core --> SQL Server
      +--> Transactional Outbox --> RabbitMQ --> Notification Consumer
      +--> Redis Cache
      +--> AI Gateway Layer --> Redaction / Human Approval / Search Fallback
      +--> OpenTelemetry --> Application Insights / Azure Monitor
```

The application uses a modular-monolith approach for the core API while demonstrating distributed processing through an outbox, RabbitMQ, Redis, and background workers.

## Technology Stack

**Backend**
- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- ASP.NET Core Identity
- JWT authentication
- SQL Server
- xUnit and WebApplicationFactory
- Testcontainers

**Frontend**
- Angular
- TypeScript
- Vitest

**Distributed Capabilities**
- RabbitMQ
- Transactional outbox pattern
- Idempotent message consumers
- Redis

**AI and Search**
- Isolated AI gateway interfaces
- Sensitive-data redaction
- Human-approved suggested actions
- Deterministic semantic-search gateway with SQL fallback
- Prompt/version tracking and evaluation dataset

**Cloud and DevOps**
- Azure App Service
- Azure SQL Database
- Azure Key Vault
- Application Insights
- Log Analytics
- Azure Monitor
- OpenTelemetry
- Bicep
- GitHub Actions
- Docker Compose

## Repository Structure

```text
src/OpsPilot.Api          ASP.NET Core API, domain logic, persistence, AI gateways, and workers
src/opspilot-web          Angular frontend
tests/OpsPilot.Api.Tests  Domain and API integration tests
infra/main.bicep          Azure infrastructure definition
docs/REQUIREMENTS.md      MVP requirements and lifecycle rules
docs/ROADMAP.md           Implementation roadmap
docs/DEPLOYMENT.md        Deployment, migration, verification, and rollback runbook
docs/THREAT_MODEL.md      Application and cloud threat model
docs/adr                  Architecture Decision Records
```

## Prerequisites

- .NET 8 SDK
- Node.js 20 or later
- Docker Desktop
- EF Core CLI (`dotnet ef`)
- Azure CLI with Bicep support only when validating or deploying Azure infrastructure

SQL Server, RabbitMQ, and Redis can be run locally through Docker Compose.

## Local Development

### 1. Start infrastructure

Start the local SQL Server, RabbitMQ, and Redis services required for the full distributed environment:

```powershell
docker compose up -d
```

Configure the local SQL connection string and JWT settings through environment variables or another secure local configuration mechanism. Do not commit passwords, JWT signing keys, or production connection strings.

### 2. Apply database migrations

From the repository root:

```powershell
dotnet ef database update --project .\src\OpsPilot.Api\OpsPilot.Api.csproj
```

### 3. Run the API

```powershell
dotnet run --project .\src\OpsPilot.Api\OpsPilot.Api.csproj
```

### 4. Run the Angular client

```powershell
cd .\src\opspilot-web
npm ci
npm start
```

## Testing

Run the backend test suite from the repository root:

```powershell
dotnet test --configuration Release
```

The backend suite includes domain tests and API integration tests using WebApplicationFactory and Testcontainers.

Run the Angular tests and production build with:

```powershell
cd .\src\opspilot-web
npm test
npm run build
```

Current verified results are 59 passing backend tests and 6 passing frontend tests.

## Continuous Integration

GitHub Actions validates the repository on pushes and pull requests to `main`.

The CI workflow performs:

- .NET dependency restore, Release build, and backend tests
- Angular dependency installation, tests, and production build
- Production npm dependency auditing with retry handling for transient registry failures
- Bicep compilation to validate the Azure infrastructure definition

## Azure Infrastructure

Azure infrastructure is defined in `infra/main.bicep`.

Validate the Bicep definition locally with:

```powershell
az bicep build --file .\infra\main.bicep
```

Compiling the Bicep file does not deploy Azure resources. Review `docs/DEPLOYMENT.md` before an actual deployment because the defined App Service, Azure SQL, monitoring, and related resources can incur Azure charges.

## Security and Reliability Design

Key design controls include:

- Server-side authorization and reporter ownership enforcement
- JWT signing material and database credentials excluded from source control
- Azure Key Vault references for cloud secrets
- Managed identity access from App Service to Key Vault
- HTTPS and TLS 1.2 or later in the Azure architecture
- Sensitive-data redaction before AI gateway processing
- Human approval before accepting AI-suggested actions
- Application-level audit history for important workflow changes
- Transactional outbox and idempotent message consumption
- Separate liveness and database-readiness health checks
- Structured logging without intentional request-body or credential logging

The application audit history is immutable through normal application workflow semantics; it is not intended to be tamper-proof against a privileged database administrator.

Known MVP risks and production-hardening recommendations are documented in `docs/THREAT_MODEL.md` and `docs/DEPLOYMENT.md`.

## AI Implementation Note

The current AI gateways are local and deterministic. They demonstrate provider isolation, redaction, human approval, prompt/version tracking, evaluation, and fallback behavior without requiring an external AI service.

Semantic search uses deterministic concept matching rather than embeddings or a vector database. The gateway architecture allows a future external model or semantic-search provider to be introduced after appropriate privacy, security, reliability, and cost review.

## Documentation

- [Requirements](docs/REQUIREMENTS.md)
- [Implementation Roadmap](docs/ROADMAP.md)
- [Deployment and Rollback Runbook](docs/DEPLOYMENT.md)
- [Threat Model](docs/THREAT_MODEL.md)
- [ADR 0001 - Transactional Outbox, RabbitMQ, and Idempotent Consumers](docs/adr/0001-use-outbox-rabbitmq-and-idempotent-consumers.md)
- [ADR 0002 - Isolated AI Gateways and Human Approval](docs/adr/0002-isolate-ai-gateways-and-require-human-approval.md)
- [ADR 0003 - Azure App Service, SQL, Key Vault, and OpenTelemetry](docs/adr/0003-use-azure-app-service-sql-key-vault-and-opentelemetry.md)

## Deployment Status

The application, automated tests, CI workflow, observability configuration, and Azure Bicep architecture are implemented and validated.

A live Azure environment has intentionally not been provisioned yet. The deployment runbook documents the staging-slot, database-migration, verification, monitoring, and rollback procedure to use when a deployment is performed.
