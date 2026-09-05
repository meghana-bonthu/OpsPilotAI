# OpsPilotAI Deployment and Rollback Runbook

## Overview

OpsPilotAI is designed for deployment to Microsoft Azure using the Bicep infrastructure definition in `infra/main.bicep`.

The Azure architecture includes:

- Azure App Service for the ASP.NET Core API
- A staging deployment slot
- Azure SQL Database for persistent application data
- Azure Key Vault for the SQL connection string and JWT signing key
- Application Insights and Log Analytics for observability
- OpenTelemetry traces, metrics, and structured logs
- App Service health monitoring through `/health/ready`
- Azure Monitor alerting for repeated HTTP 5xx responses

RabbitMQ and Redis are implemented for local/distributed operation but are disabled in the initial Azure deployment configuration to avoid requiring additional hosted dependencies.

## Prerequisites

Before deployment:

1. Install the Azure CLI and Bicep CLI.
2. Authenticate to the intended Azure subscription.
3. Confirm the target subscription and resource group.
4. Review expected Azure costs before creating resources.
5. Provide deployment-time values for the SQL administrator password and JWT signing key.
6. Confirm the deploying identity has permission to create resources and Key Vault role assignments.
7. Build and test the application before publishing.
8. Validate the Bicep template.

Bicep validation command:

```powershell
az bicep build --file .\infra\main.bicep
```

Secrets must never be committed to source control.

## Health Endpoints

The API exposes two health endpoints:

- `/health` - application liveness
- `/health/ready` - readiness, including Azure SQL connectivity

Azure App Service Health Check uses `/health/ready`.

## Deployment Strategy

OpsPilotAI uses a staging-slot deployment strategy.

The intended deployment flow is:

1. Provision or update Azure infrastructure using `infra/main.bicep`.
2. Apply required Entity Framework Core database migrations.
3. Build and publish the ASP.NET Core API.
4. Deploy the new API version to the `staging` App Service slot.
5. Verify the staging application starts successfully.
6. Verify `/health` returns a successful response.
7. Verify `/health/ready` returns a successful response.
8. Perform critical API smoke tests.
9. Review Application Insights telemetry for startup or dependency failures.
10. Swap the validated staging slot into production.
11. Monitor production health, HTTP errors, logs, traces, and metrics after the swap.

The staging slot and production application currently use the same Azure SQL database and JWT signing key. This simplifies the MVP deployment but means staging activity must avoid destructive production-data changes.

## Database Migration Safety

Database schema migrations must be reviewed before production deployment.

Before applying a migration:

1. Review the generated EF Core migration.
2. Determine whether the change is backward compatible with the currently deployed application.
3. Back up or otherwise protect important production data when appropriate.
4. Apply the migration before performing a slot swap when the new application requires the updated schema.
5. Verify database readiness after migration.

From the repository root, apply reviewed migrations with:

```powershell
dotnet ef database update --project .\src\OpsPilot.Api\OpsPilot.Api.csproj
```

The target database connection string must be supplied through secure environment configuration before running the command. Do not place Azure SQL passwords or complete production connection strings in source control, deployment documentation, or command-line history.

The API does not run EF Core migrations automatically during application startup. Database migration remains an explicit deployment operation so schema changes can be reviewed and coordinated with the staging-slot deployment.

Destructive migrations should not be automatically rolled back without evaluating their data impact.

## Post-Deployment Verification

After deployment or slot swap:

1. Check `/health`.
2. Check `/health/ready`.
3. Confirm authentication works.
4. Confirm authorized incident creation and retrieval work.
5. Confirm the application can read and write Azure SQL data.
6. Confirm Application Insights receives telemetry.
7. Review HTTP 5xx metrics and the configured Azure Monitor alert.
8. Confirm secrets are resolved through Key Vault references.
9. Verify no sensitive values appear in logs.

## Rollback Strategy

If the new production version causes application failures:

1. Review App Service health and Application Insights telemetry.
2. Determine whether the failure is application-only or database-related.
3. If the previous slot version remains compatible with the current database schema, swap the App Service slots back.
4. Verify `/health` and `/health/ready`.
5. Repeat critical smoke tests.
6. Continue monitoring telemetry after rollback.

A slot swap rollback restores the previous application version but does not automatically reverse database migrations.

If a database migration caused the incident, evaluate the migration and data impact before applying any corrective migration. Prefer a forward-fix migration when reversing the schema could cause data loss.

## Infrastructure Rollback

Infrastructure changes are defined declaratively in Bicep and stored in Git.

If an infrastructure change causes a problem:

1. Identify the last known-good Git commit.
2. Review the Bicep differences between the failing and known-good versions.
3. Reapply a corrected Bicep definition rather than manually changing resources when possible.
4. Validate application health after the infrastructure correction.

Resource deletion must be reviewed carefully because removing Azure SQL, Key Vault, or monitoring resources can cause permanent data or configuration loss.

## Monitoring During Deployment and Rollback

During deployment and rollback, monitor:

- App Service health
- HTTP 5xx responses
- Application Insights requests and failures
- OpenTelemetry traces
- Structured application logs
- Azure SQL connectivity
- `/health`
- `/health/ready`

## Security Notes

- SQL administrator passwords and JWT signing keys are deployment-time secrets.
- Secrets are stored in Azure Key Vault and are not committed to Git.
- App Service and the staging slot use system-assigned managed identities for Key Vault access.
- HTTPS is required.
- TLS 1.2 or later is required.
- FTPS is disabled.
- Application logs must not contain request bodies, credentials, JWT keys, SQL passwords, or other sensitive content.

## Known MVP Deployment Tradeoffs

The initial cloud architecture intentionally makes several MVP tradeoffs:

- Azure SQL permits the Azure-services firewall rule rather than using private networking.
- Production and staging share the same database.
- RabbitMQ messaging is disabled in the initial Azure environment.
- Redis caching is disabled in the initial Azure environment.
- The HTTP 5xx alert currently records alert state but has no notification Action Group configured.

These tradeoffs should be reconsidered before treating the environment as a production-hardened deployment.
