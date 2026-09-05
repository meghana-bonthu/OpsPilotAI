# ADR 0002: Isolate AI Gateways and Require Human Approval

## Status

Accepted

## Context

OpsPilotAI includes AI-assisted incident summaries, suggested actions, and semantic search. AI behavior may change over time, external providers may introduce privacy concerns, and generated recommendations may be inaccurate or unsafe. The core incident workflow must therefore remain independent from any specific AI provider and must not allow generated suggestions to mutate operational data automatically.

## Decision

OpsPilotAI isolates AI-assisted capabilities behind gateway interfaces and treats generated output as advisory.

- Incident summaries are generated through an incident summary gateway.
- Suggested actions are generated through a separate suggested-action gateway.
- Semantic search is isolated behind a semantic-search gateway with deterministic fallback behavior.
- Sensitive incident content is redacted before it is provided to AI gateways.
- Suggested actions are persisted as Pending and require explicit human approval or rejection.
- Approving an AI suggestion does not automatically change incident state.
- Prompt and behavior versions are tracked for AI-assisted capabilities.
- An evaluation dataset provides repeatable checks for AI-related behavior.

## Consequences

Positive consequences:

- Application business rules are not coupled to a specific AI provider.
- External AI integrations can be introduced or replaced behind stable interfaces.
- Sensitive information receives an explicit redaction step before AI processing.
- Human review reduces the risk of unsafe automated actions.
- Version tracking improves reproducibility and evaluation.
- Semantic search can fall back to conventional SQL search when AI search is unavailable.

Tradeoffs:

- Human approval adds an additional workflow step.
- Gateway abstractions and evaluation assets add implementation complexity.
- The current implementation uses local deterministic AI gateway behavior rather than an external generative model or vector-search service.
- Any future external AI provider will require an additional privacy, security, cost, and reliability review.
