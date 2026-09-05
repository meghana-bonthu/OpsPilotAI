# ADR 0003: Use Azure App Service, Azure SQL, Key Vault, and OpenTelemetry

## Status

Accepted

## Context

OpsPilotAI requires a cloud deployment architecture that supports managed hosting, relational persistence, secure secret storage, observability, health monitoring, and low-risk application deployments. The infrastructure should also be reproducible through infrastructure as code.

## Decision

OpsPilotAI uses Azure services provisioned through Bicep for the initial cloud architecture.

- Azure App Service hosts the ASP.NET Core API on Linux with .NET 8.
- The App Service plan uses the Standard tier to support deployment slots.
- A staging slot is used to validate releases before production slot swaps.
- Azure SQL Database provides relational application persistence.
- Azure Key Vault stores the SQL connection string and JWT signing key.
- App Service system-assigned managed identities receive access to required Key Vault secrets.
- Application Insights and Log Analytics provide centralized telemetry.
- OpenTelemetry instruments ASP.NET Core requests, outgoing HTTP calls, metrics, and logs.
- `/health` provides application liveness while `/health/ready` checks SQL readiness.
- Azure Monitor tracks repeated HTTP 5xx responses.
- Infrastructure definitions are maintained in Bicep and version-controlled with the application.

## Consequences

Positive consequences:

- Infrastructure can be reviewed and reproduced from source control.
- Application secrets do not need to be stored in committed configuration files.
- Managed identity reduces the need for application-managed Key Vault credentials.
- Deployment slots provide a controlled validation and rollback mechanism.
- Centralized telemetry improves diagnosis of production failures.
- Separate liveness and readiness endpoints distinguish application availability from database readiness.

Tradeoffs:

- The Standard App Service plan and Azure SQL introduce ongoing cloud cost.
- The current Azure SQL firewall permits Azure services and should be tightened for a production-hardened environment.
- Key Vault currently permits public network access and should be reviewed for stronger network isolation.
- Production and staging currently share the same database and JWT signing key.
- The HTTP 5xx alert currently records alert state without an Action Group notification.
- A SQL outage can cause `/health/ready` to fail and influence App Service health decisions.
