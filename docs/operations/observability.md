# Observability

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the observability requirements for the Wallet API MVP.

Observability enables developers and operators to understand application health, investigate failures, identify performance problems, and verify the behavior of financial operations.

The observability strategy covers:

- Structured logging.
- Application metrics.
- Distributed tracing and correlation.
- Health checks.
- Financial operation diagnostics.
- Sensitive-data protection.
- Operational troubleshooting.

The MVP should use a simple approach that can be extended as deployment and monitoring requirements become clearer.

## 2. Observability Principles

The application MUST follow these principles:

- Operational events MUST be distinguishable from authoritative financial records.
- Logs MUST NOT be used as a substitute for the transaction ledger.
- Logs and traces MUST NOT expose credentials, authentication tokens, or unnecessary personal data.
- Financial failures MUST be diagnosable using structured error information and correlation identifiers.
- Observability MUST NOT change the outcome or atomicity of a financial operation.
- Monitoring failures MUST NOT cause duplicate deposits or transfers.
- Metrics SHOULD use bounded-cardinality labels.
- Production diagnostics MUST NOT expose stack traces or internal implementation details to API clients.

## 3. Structured Logging

The API SHOULD use structured logging rather than relying exclusively on free-form text messages.

Log events SHOULD include relevant fields where available:

- Timestamp in UTC.
- Log level.
- Application name and version.
- Environment.
- Trace or correlation identifier.
- Request method and route template.
- HTTP response status.
- Request duration.
- Financial operation type.
- Operation identifier.
- Stable application error code.
- Idempotency outcome, such as new request, replay, or key conflict.

Fields MUST be included only when applicable and safe to record.

### 3.1 Log Levels

| Level | Intended use |
|---|---|
| Trace/Debug | Detailed development diagnostics |
| Information | Application lifecycle and significant business-operation outcomes |
| Warning | Expected failures requiring attention, unusual conditions, or retryable infrastructure issues |
| Error | Unexpected failures or operations that could not complete because of an internal error |
| Critical | Severe failures affecting the application's ability to operate safely |

Expected business failures, such as insufficient funds, SHOULD be logged at a level appropriate to their operational significance. They MUST NOT automatically be treated as internal application errors.

### 3.2 Request Logging

Request logs SHOULD capture the HTTP method, route template, status code, and duration.

The application MUST NOT log complete request or response bodies by default for financial endpoints.

URLs and query strings SHOULD be reviewed before logging to ensure they do not contain credentials or other sensitive values.

## 4. Financial Operation Diagnostics

The application SHOULD emit structured diagnostic events for deposits and transfers.

Useful event attributes include:

- Operation type: deposit or transfer.
- Operation identifier, when available.
- Authenticated user identifier, using an internal identifier where appropriate.
- Outcome: succeeded or failed.
- Stable error code for a failure.
- Trace identifier.
- Idempotency outcome.
- Duration.
- Retry or concurrency-conflict information, where applicable.

The application SHOULD avoid logging monetary payloads unless there is a clear operational need and the logging policy explicitly permits them.

The ledger and persisted operation records remain the authoritative sources for financial history and reconciliation.

### 4.1 Successful Operations

A successful operation SHOULD produce enough diagnostic information to correlate the HTTP request with the persisted operation record.

The success log MUST be emitted only after the application knows the transaction has committed. If logging is asynchronous, its failure MUST NOT reverse or duplicate the financial operation.

### 4.2 Failed Operations

Failure diagnostics SHOULD distinguish among:

- Invalid input.
- Authentication or authorization failure.
- Insufficient funds.
- Recipient wallet balance limit exceeded.
- Idempotency key conflict.
- Concurrency conflict.
- Database or infrastructure failure.
- Unexpected application error.

Diagnostic messages MUST NOT disclose internal SQL statements, credentials, stack traces, or database details to API clients.

## 5. Metrics

The application SHOULD expose metrics that help identify reliability and performance problems.

Candidate metrics include:

| Metric | Purpose |
|---|---|
| HTTP request count | Track request volume and response outcomes |
| HTTP request duration | Identify latency changes |
| HTTP error count | Detect elevated failure rates |
| Deposit outcomes | Observe successful and failed deposits |
| Transfer outcomes | Observe successful and failed transfers |
| Idempotency replays | Understand repeated client submissions |
| Idempotency conflicts | Detect reuse of keys with different requests |
| Database operation duration | Identify database-related latency |
| Database connection failures | Detect connectivity problems |
| Concurrency conflicts and deadlocks | Identify contention and retry patterns |

Metric names and instrumentation technology are implementation decisions.

Metrics SHOULD use bounded labels such as operation type, outcome, and status category.

Metrics MUST NOT use user IDs, wallet IDs, idempotency keys, or other high-cardinality identifiers as labels.

Financial totals SHOULD NOT be derived from transient request counters as an authoritative accounting source.

## 6. Distributed Tracing and Correlation

The API SHOULD support request tracing using the .NET diagnostic infrastructure and, if needed, OpenTelemetry.

Each request SHOULD have a trace identifier that can connect relevant API, application, and database diagnostic events.

Where practical, the trace identifier SHOULD be available in:

- Application logs.
- Error responses through a documented correlation field or header.
- Trace data.
- Operational dashboards.

A trace identifier is diagnostic metadata. It MUST NOT be used as an authorization credential, an idempotency key, or a financial operation identifier.

Sensitive request data MUST NOT be copied into trace attributes.

## 7. Health Checks

Health checks SHOULD distinguish application availability from dependency readiness.

### 7.1 Liveness

A liveness check indicates whether the application process is running and able to respond.

It SHOULD NOT depend on every external dependency being healthy. A temporary database outage should not automatically create an endless restart loop.

### 7.2 Readiness

A readiness check indicates whether the instance is ready to receive requests that depend on required services.

For this MVP, SQL Server connectivity is a relevant readiness dependency.

The readiness check SHOULD perform a lightweight database connectivity check and MUST NOT modify wallet balances or create financial records.

### 7.3 Health-Check Security

Health-check endpoints MUST NOT expose:

- Connection strings.
- Credentials.
- Detailed SQL errors.
- Internal configuration values.
- Stack traces.

Detailed diagnostic information SHOULD be restricted to authorized operators or internal monitoring systems.

The exact health-check routes and deployment probes remain to be defined.

## 8. Alerting and Operational Thresholds

Alerting thresholds depend on the deployment environment and expected workload.

Potential alert conditions include:

- Sustained increase in server errors.
- Elevated request latency.
- Database connectivity failures.
- Repeated database deadlocks.
- Unusual increases in idempotency conflicts.
- Failure of readiness checks.
- Unexpected process termination.

Thresholds, notification channels, and escalation procedures are not configured. Tracked as `KI-009`.

A business failure such as insufficient funds is not, by itself, evidence of a system outage.

## 9. Privacy and Data Protection

Observability data MUST follow the same security principles as the API itself.

The application MUST NOT record:

- Passwords or authentication tokens.
- Secret configuration values.
- Full database connection strings.
- Unnecessary personal information.
- Sensitive payloads without an explicitly approved reason.

Access to production logs, traces, and dashboards SHOULD be restricted by role.

Retention periods SHOULD be defined according to operational needs, applicable privacy requirements, and storage constraints.

The MVP's exact retention and access policies remain open decisions.

## 10. Failure Isolation

Observability infrastructure is not part of the financial transaction's correctness boundary.

The application MUST NOT:

- Repeat a deposit because writing a log failed.
- Repeat a transfer because a metric could not be exported.
- Roll back a committed transfer because trace export failed.
- Expose internal exception details to make diagnosis easier for API clients.

Failures in telemetry export SHOULD be handled independently from the result of the financial operation.

If an operational requirement later demands durable audit events beyond the financial ledger, that requirement must be designed separately with explicit delivery and consistency guarantees.

## 11. Testing Requirements

Observability behavior SHOULD be verified through tests where practical.

Tests SHOULD confirm that:

- Requests receive correlation identifiers.
- Errors contain safe, stable diagnostic information.
- Sensitive values are not included in logs.
- Successful financial operations can be correlated with persisted operation identifiers.
- Health checks do not modify financial data.
- Telemetry failures do not cause repeated financial operations.
- Metric labels do not contain user-specific identifiers.
- Expected business failures are distinguishable from unexpected infrastructure errors.

## 12. Open Decisions

The following items are tracked. Their labels indicate status:

1. Which structured logging provider will be used? (resolved in OQ-029)
2. Will OpenTelemetry be included in the MVP? (resolved in OQ-029)
3. Which metrics exporter and monitoring platform will be used? (resolved in OQ-029)
4. What are the health-check routes and access restrictions? (unique to this document)
5. Which latency and error-rate thresholds should trigger alerts? (tracked in OQ-024)
6. What are the retention periods for logs and traces? (unique to this document)
7. Should successful financial operation events be logged at Information level by default? (unique to this document)
8. What correlation header or response field will the API standardize on? (unique to this document)

## 13. Related Documents

- `docs/operations/configuration.md`
- `docs/operations/recovery.md`
- `docs/api/error-model.md`
- `docs/security/security-model.md`
- `docs/architecture/concurrency.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/adr/ADR-005-idempotency.md`