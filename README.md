# OpsPilot AI

OpsPilot AI is a production-style incident and workflow management platform built as a portfolio project for a .NET Full Stack Developer. The system demonstrates secure API development, a modern Angular client, relational data modeling, automated testing, event-driven processing, cloud deployment, observability, and practical AI integration.

## Current milestone: Foundation

This repository begins with a deliberately small vertical slice:

- Create and list operational incidents
- Track priority and lifecycle status
- Validate requests at the API boundary
- Persist data with EF Core and SQL Server
- Display incidents in an Angular feature component
- Publish an OpenAPI contract for frontend and integration work

Authentication, audit history, tests, messaging, AI summaries, infrastructure-as-code, and monitoring are planned in the roadmap rather than represented as completed work.

## Architecture

```text
Angular client -> ASP.NET Core API -> Application/domain rules -> EF Core -> SQL Server
```

The first milestone uses a modular monolith. This keeps local development and deployment understandable while preserving boundaries that can later support background workers and event-driven integrations.

## Repository structure

```text
src/OpsPilot.Api       ASP.NET Core Web API and domain model
src/opspilot-web       Angular client foundation
docs                   Architecture decisions, requirements, and roadmap
```

## Prerequisites

- .NET 8 SDK
- Node.js 20 or later
- Angular CLI 18 or later
- Docker Desktop, or a local SQL Server instance

## Local setup

1. Set `OPSPILOT_SQL_PASSWORD` to a strong local password and start SQL Server with `docker compose up -d sqlserver`.
2. Set `ConnectionStrings__OpsPilot` to a local connection string containing that password. From `src/OpsPilot.Api`, run `dotnet restore` and `dotnet ef database update`.
3. Start the API with `dotnet run`.
4. From `src/opspilot-web`, run `npm install` and `npm start`.

The Angular development server expects the API at `https://localhost:7043`.

## Portfolio evidence to add before publishing

- Screenshots and a short demo video
- Unit and integration test results
- CI/CD workflow status
- Azure deployment URL
- Architecture and threat-model diagrams
- Measured performance and accessibility results

See [docs/ROADMAP.md](docs/ROADMAP.md) for the implementation sequence.
