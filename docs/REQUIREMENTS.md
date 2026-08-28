# MVP requirements

## Users and roles

- Reporter: creates incidents and views incidents they reported.
- Responder: triages, assigns, comments on, and resolves incidents.
- Administrator: manages users, teams, categories, and reporting access.

## Incident lifecycle

`New -> Triaged -> InProgress -> Resolved -> Closed`

An incident may also become `Cancelled`. Every status change must eventually produce an immutable audit entry.

## MVP user stories

1. As a reporter, I can create an incident with a title, description, category, and priority.
2. As a responder, I can filter the incident queue and open an incident detail view.
3. As a responder, I can assign an incident and move it through allowed lifecycle states.
4. As a user, I can see a chronological activity history.
5. As an administrator, I can view dashboard metrics without accessing hidden secrets or credentials.

## Non-functional requirements

- API requests must be validated and return Problem Details responses.
- Authorization must be enforced server-side.
- Secrets must not be committed to source control.
- Important actions must be auditable.
- Automated tests must cover domain rules and critical API paths.
- Logs and traces must use correlation identifiers and must not contain sensitive content.
