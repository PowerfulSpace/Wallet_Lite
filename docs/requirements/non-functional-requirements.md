# Non-Functional Requirements

**Document ID:** REQ-NFR  
**Status:** Draft  
**Version:** 0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the non-functional requirements (NFRs) for the Wallet API MVP.

These requirements establish measurable or verifiable expectations for system reliability, data integrity, security, performance, maintainability, testability, and operational readiness.

Non-functional requirements complement the functional requirements. They describe the quality attributes and constraints under which the system must operate.

## 2. Requirement Conventions

Each requirement has a unique identifier using the prefix `NFR`.

- **MUST:** Mandatory requirement.
- **SHOULD:** Recommended requirement that may be relaxed only with documented justification.
- **MAY:** Optional capability.

Where a requirement specifies a measurable target, the verification method and test conditions must be documented sufficiently to make the result reproducible.

## 3. Reliability and Data Integrity

### NFR-001 — Atomicity of Monetary Operations

A deposit or transfer MUST either commit all required financial effects or commit none of them.

Verification MUST include integration tests against the selected relational database.

### NFR-002 — Balance Invariants

The system MUST preserve the following invariants under normal execution, errors, retries, and concurrent requests:

- Wallet balances MUST NOT become negative.
- Wallet balances MUST NOT exceed the configured maximum.
- A successful transfer MUST debit and credit equal amounts.
- A successful transfer's two ledger entries MUST sum to zero.
- A successful deposit MUST have a corresponding positive ledger entry.

### NFR-003 — Concurrency Safety

Concurrent monetary operations MUST NOT violate financial invariants.

Verification MUST include integration tests using independent concurrent requests against a real SQL Server instance rather than relying exclusively on mocks or an in-memory database.

### NFR-004 — Persistence Consistency

The persisted wallet balance, operation records, ledger entries, and successful idempotency result MUST remain mutually consistent after a committed operation.

### NFR-005 — Failure Recovery

When a monetary operation fails before commit, the system MUST leave no partial committed financial effects.

The behavior of failures during transaction execution and commit MUST be tested where it can be reproduced reliably.

### NFR-006 — Idempotent Execution

Repeated equivalent requests using the same idempotency key MUST NOT produce duplicate financial effects, including when requests arrive concurrently.

## 4. Security and Privacy

### NFR-007 — Authorization Enforcement

Authorization MUST be enforced on the server side for every protected resource and operation.

The system MUST NOT rely on client-side checks to prevent access to another user's data.

### NFR-008 — Development Authentication Isolation

Development-only authentication MUST be unavailable in Production. The application MUST fail safely when its configuration would permit development authentication in a Production environment.

### NFR-009 — Sensitive Data Protection

Secrets, credentials, authentication tokens, and sensitive configuration values MUST NOT be exposed through API responses or ordinary application logs.

### NFR-010 — Safe Error Responses

Unexpected internal failures MUST produce safe client-facing error responses without exposing stack traces, database details, or implementation internals.

### NFR-011 — Input Handling

External input MUST be validated before it can affect monetary state or database integrity.

Validation MUST include monetary precision, amount boundaries, required identifiers, and idempotency-key requirements.

## 5. Performance and Resource Usage

### NFR-012 — Performance Targets

**Status:** Deferred beyond the MVP.

The MVP does not define numeric latency targets. A meaningful performance target requires a concrete workload, an evaluation environment, and a measurement method, none of which are defined for the MVP.

The following are recorded as **deferred**, not as passing requirements:

- Response-time percentiles for `POST /api/v1/wallet/deposits`, `POST /api/v1/transfers`, `GET /api/v1/wallet`, and `GET /api/v1/wallet/transactions`.
- Throughput targets.
- Concurrency targets beyond correctness (correctness is covered by `NFR-003` and `NFR-024`).

When performance targets are introduced, they MUST identify the operation, percentile, workload, environment, and measurement method. See `OQ-024` in `docs/project/open-questions.md`.

### NFR-013 — Database Efficiency

Wallet balance retrieval and paginated transaction-history retrieval SHOULD use database access patterns appropriate to their query shapes and data volume.

The implementation SHOULD avoid unnecessary full-table reads and unbounded history responses.

### NFR-014 — Bounded Requests

API requests that return collections MUST enforce a maximum page size.

The maximum page size and default value MUST be defined in the API contract.

### NFR-015 — Resource Limits

The application SHOULD define appropriate limits for request sizes, database command execution, and other resource-intensive operations.

Exact limits MUST be selected according to the chosen hosting and development environment.

## 6. Maintainability and Architecture

### NFR-016 — Separation of Concerns

The implementation MUST maintain clear boundaries between domain logic, application use cases, infrastructure concerns, and HTTP delivery.

The architecture documentation defines the intended dependency direction and permitted interactions.

### NFR-017 — Explicit Business Rules

Critical monetary rules MUST be expressed in a form that can be reviewed and tested independently of the HTTP transport layer.

### NFR-018 — Documented Architectural Decisions

Significant architectural decisions MUST be recorded as Architecture Decision Records (ADRs), including the context, decision, alternatives considered where relevant, and consequences.

### NFR-019 — Reproducible Database Evolution

Database schema changes MUST be version-controlled and applied through documented migration procedures.

### NFR-020 — Documentation Consistency

Changes to requirements, API contracts, database models, security behavior, or architectural boundaries MUST trigger a review of the affected documentation.

## 7. Testability and Verification

### NFR-021 — Automated Verification

Critical domain behavior and monetary invariants MUST be covered by automated tests.

### NFR-022 — Unit Tests

Domain rules and application behavior that can be tested independently of external infrastructure SHOULD have unit tests.

### NFR-023 — Integration Tests

Database-dependent behavior MUST be verified through integration tests against the selected database engine.

### NFR-024 — Concurrency Tests

Concurrency tests MUST cover scenarios in which simultaneous operations compete for the same wallet balance or use the same idempotency key.

### NFR-025 — Repeatable Test Environment

The project MUST document how to configure and run its automated test suites, including any required database dependencies.

### NFR-026 — Regression Protection

A change to critical monetary behavior MUST include appropriate regression tests or demonstrate why existing tests sufficiently cover the affected behavior.

## 8. Observability and Diagnostics

### NFR-027 — Structured Logging

The application SHOULD use structured logs to support diagnosis of failed requests and database operations.

### NFR-028 — Request Correlation

The system SHOULD support correlating logs associated with a single request or operation.

### NFR-029 — Sensitive Information in Logs

Logs MUST NOT expose secrets or authentication credentials. Logging of monetary operations MUST avoid unnecessary personal or sensitive information.

### NFR-030 — Health Reporting

The application SHOULD expose health information suitable for identifying whether the service is running and whether required dependencies are available.

Health reporting MUST NOT expose sensitive configuration or internal diagnostic details to unauthorized callers.

## 9. Portability and Configuration

### NFR-031 — Environment-Specific Configuration

Environment-specific settings MUST be configurable without changing application source code.

Secrets MUST be supplied through an appropriate secret-management or configuration mechanism rather than committed to source control.

### NFR-032 — Supported Runtime

The project MUST document its supported .NET runtime, database engine, and required development dependencies.

### NFR-033 — Local Development

A developer SHOULD be able to reproduce the documented development and test environment using the project's setup instructions.

## 10. Operational Readiness

### NFR-034 — Failure Visibility

Operationally relevant failures MUST be diagnosable through appropriate error reporting and logs without exposing sensitive information.

### NFR-035 — Database Recovery Documentation

The project MUST document the assumptions and basic procedures for recovering the local development database and rebuilding it from the version-controlled schema and migrations.

This requirement does not imply a production disaster-recovery service or a defined production recovery-time objective.

### NFR-036 — Production Readiness Boundary

The project MUST clearly distinguish production-oriented engineering practices from actual production readiness.

The MVP MUST NOT be represented as suitable for real-money operations without a separate assessment of security, operational controls, regulatory obligations, and other applicable requirements.

## 11. Acceptance and Verification

Each mandatory NFR MUST have an identifiable verification method appropriate to the requirement.

Verification methods may include:

- Automated unit tests.
- Database integration tests.
- Concurrent execution tests.
- API contract tests.
- Configuration and security checks.
- Code and architecture reviews.
- Reproducible manual verification procedures.
- Performance measurements, where quantitative targets have been agreed.

Requirements with unresolved numeric targets MUST remain explicitly marked as TBD and MUST NOT be treated as having passed quantitative acceptance.

`NFR-012` is explicitly deferred beyond the MVP. Its absence from acceptance verification is intentional and must not be interpreted as a passing performance claim.

## 12. Related Documents

- `overview.md` — project goals, scope, and success criteria.
- `functional-requirements.md` — functional behavior.
- `domain-rules.md` — business invariants and monetary constraints.
- `../architecture/architecture.md` — architectural boundaries and dependency rules.
- `../architecture/data-model.md` — persistence model.
- `../architecture/concurrency.md` — concurrency and consistency strategies.
- `../api/endpoints.md` — endpoint contracts and request limits.
- `../security/security-model.md` — security controls.
- `../security/threat-model.md` — threat analysis.
- `../development/testing.md` — verification strategy.
- `../operations/observability.md` — logging and diagnostics.
- `../operations/recovery.md` — recovery procedures.
- `../adr/` — significant architectural decisions.

---

**End of document**