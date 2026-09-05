# OpsPilotAI Threat Model

## Scope

This threat model covers the OpsPilotAI web API, Azure deployment architecture, authentication and authorization, database access, AI-assisted features, messaging, caching, observability, and deployment operations.

The primary assets to protect are:

- User identities and role assignments
- Incident data and activity history
- JWT signing material
- Azure SQL credentials and stored application data
- Key Vault secrets
- Application telemetry and logs
- AI prompt inputs and generated suggestions
- Infrastructure configuration and deployment credentials

## Trust Boundaries

Important trust boundaries include:

- Browser or API client to ASP.NET Core API
- ASP.NET Core API to Azure SQL
- ASP.NET Core API to Azure Key Vault through managed identity
- ASP.NET Core API to Application Insights and Azure Monitor
- API to RabbitMQ and Redis when distributed features are enabled
- API to AI gateway implementations
- GitHub Actions to source code, package registries, and deployment targets
- Staging App Service slot to production resources

## Threats and Mitigations

### 1. Authentication and Token Abuse

Threats:
- Stolen or forged JWTs could allow unauthorized API access.
- Weak signing material could make token forgery easier.

Mitigations:
- JWT authentication is enforced server-side.
- Role-based authorization policies protect privileged operations.
- JWT signing keys are not committed to source control.
- Azure deployments store the JWT signing key in Key Vault.
- HTTPS and TLS 1.2 or later protect tokens in transit.

### 2. Broken Authorization and Privilege Escalation

Threats:
- Reporters could attempt to access incidents owned by other users.
- Users could attempt responder or administrator operations without the required role.

Mitigations:
- Reporter ownership is enforced by server-side queries.
- Responder and Administrator operations use authorization policies.
- Public registration assigns the Reporter role rather than allowing users to choose privileged roles.
- Status changes and team assignments record actor identity for auditing.

### 3. Sensitive Data Exposure

Threats:
- Credentials, incident content, or personal information could leak through source control, logs, telemetry, or AI requests.

Mitigations:
- Deployment secrets are stored in Key Vault.
- Local secret files and deployment parameter files are excluded from Git.
- Structured HTTP logs record request metadata rather than request bodies.
- Sensitive-data redaction is applied before incident content is sent to AI gateways.
- Application code must not log passwords, JWT keys, connection strings, or sensitive request bodies.

### 4. SQL and Data Integrity Threats

Threats:
- Unauthorized database access could expose or modify incident data.
- Unsafe schema migrations could cause data loss.
- Staging operations could affect production data because both slots currently share a database.

Mitigations:
- Application database access uses Entity Framework Core.
- Azure SQL connections require encryption.
- Database readiness is monitored through `/health/ready`.
- EF Core migrations are version-controlled and must be reviewed before deployment.
- Deployment documentation requires migration compatibility and data-impact review.
- Shared staging/production database usage is explicitly documented as an MVP risk.

### 5. AI-Assisted Feature Risks

Threats:
- Incident content sent to an external AI provider could expose sensitive information.
- AI-generated suggestions could be incorrect, unsafe, or inappropriate.
- Changes to prompts or AI behavior could make results difficult to reproduce or evaluate.

Mitigations:
- AI functionality is isolated behind gateway interfaces.
- Sensitive-data redaction occurs before content is provided to AI gateways.
- Suggested actions require explicit human approval or rejection.
- AI suggestions do not automatically change incident state.
- Prompt versions are tracked for summaries, suggested actions, and semantic search.
- A synthetic AI evaluation dataset exercises deterministic gateway behavior and redaction.

### 6. Audit Tampering and Repudiation

Threats:
- Users could deny making important workflow changes.
- Mutable history could hide unauthorized or accidental actions.

Mitigations:
- Incident status changes are persisted as audit history.
- Team assignment changes are persisted as audit history.
- Actor identity is recorded for important workflow changes.
- Incident activity is exposed chronologically.
- Audit entries are treated as historical records rather than editable incident state.

### 7. Messaging, Caching, and Duplicate Processing

Threats:
- Message delivery retries could cause duplicate side effects.
- Database changes and published events could become inconsistent.
- Cached incident data could become stale after writes.

Mitigations:
- The transactional outbox pattern persists events with application changes.
- Notification message processing is idempotent.
- Processed message identifiers are persisted.
- Redis cache entries use explicit invalidation after incident mutations.
- Messaging and Redis can be disabled independently when their infrastructure is unavailable.

### 8. Logging and Observability Risks

Threats:
- Logs or traces could expose sensitive content.
- Insufficient telemetry could make attacks and failures difficult to investigate.

Mitigations:
- Structured logs use request metadata and correlation identifiers.
- Request bodies and credentials are excluded from intentional application logging.
- OpenTelemetry captures application traces and metrics.
- Application Insights and Log Analytics provide centralized Azure observability.
- HTTP 5xx responses are monitored by an Azure Monitor metric alert.

### 9. Availability and Dependency Failure

Threats:
- Database outages could make the API unable to serve valid requests.
- External dependencies could degrade application availability.

Mitigations:
- `/health` provides a lightweight application liveness endpoint.
- `/health/ready` verifies database connectivity.
- App Service Health Check uses the readiness endpoint.
- RabbitMQ, Redis, and Azure Monitor exporting are configuration-controlled where appropriate.
- A staging deployment slot supports validation before production slot swaps.

## Residual Risks and Future Hardening

The following risks are accepted for the current MVP and should be addressed before a production-hardened deployment:

- Azure SQL currently allows the Azure-services firewall rule instead of using private endpoints and network isolation.
- The production App Service and staging slot currently share the same Azure SQL database and JWT signing key.
- The HTTP 5xx Azure Monitor alert currently has no notification Action Group.
- RabbitMQ and Redis are disabled in the initial Azure deployment, so their distributed capabilities are demonstrated primarily in the local environment.
- Additional rate limiting and abuse protection should be considered for internet-facing endpoints.
- Key rotation procedures should be established for JWT signing material and database credentials.
- Production backup, restore, retention, and disaster-recovery requirements should be formally defined.
- Dependency and container vulnerability scanning should remain part of continuous security maintenance.

## Security Review Checklist

Before a production deployment:

1. Confirm no secrets are committed to Git.
2. Review role and authorization policies.
3. Validate Key Vault access assignments.
4. Review Azure SQL network exposure.
5. Review database migration impact.
6. Verify HTTPS and TLS configuration.
7. Verify health checks and monitoring.
8. Verify logs and traces contain no sensitive content.
9. Confirm AI redaction and human-approval controls remain enabled.
10. Review dependency and security scan results.

## Threat Model Review

This threat model should be reviewed whenever authentication, authorization, cloud networking, AI providers, persistent data stores, messaging infrastructure, or deployment architecture changes.
