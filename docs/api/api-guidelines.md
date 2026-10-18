# API Guidelines

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `docs/api/api-guidelines.md`

## 1. Purpose

This document defines the conventions for the Wallet API's HTTP interface.

It establishes common rules for route naming, versioning, request and response formats, monetary values, authentication, idempotency, pagination, status codes, and error handling.

Endpoint-specific request and response schemas are defined in `docs/api/endpoints.md`. The canonical error response contract is defined in `docs/api/error-model.md`.

## 2. API Design Principles

The API must be:

- **Consistent:** equivalent operations follow the same conventions.
- **Explicit:** requests and responses have documented semantics.
- **Predictable:** status codes and error representations do not vary arbitrarily.
- **Secure by default:** protected resources require authentication and authorization.
- **Safe to retry:** financial write operations support the agreed idempotency contract.
- **Strict about money:** monetary input is validated without silent rounding.
- **Backward-conscious:** changes to published contracts must not unintentionally break clients.

The API is synchronous. A successful financial write means the operation has committed before the success response is returned.

## 3. Protocol and Base Path

The API uses HTTP over TLS in deployed environments.

The versioned route prefix is:

`/api/v1`

Local development may use HTTP on loopback or within an appropriately isolated development environment. Production traffic must be protected in transit.

JSON is the default request and response representation unless an endpoint explicitly documents otherwise.

## 4. Resource Naming and HTTP Methods

Route paths use lowercase, plural resource names where appropriate. Words are separated with hyphens only when needed for readability.

Use HTTP methods according to their standard semantics:

| Method | Intended use |
|---|---|
| `GET` | Retrieve a resource or collection without changing financial state. |
| `POST` | Create a resource or execute a financial operation. |

The MVP does not need general-purpose update or delete endpoints for financial records.

Successful deposits and transfers are represented as created operation resources. Financial records and ledger entries must not be edited or deleted through ordinary client-facing endpoints.

## 5. JSON Conventions

Requests and responses use JSON with UTF-8 encoding.

Unless a specific contract states otherwise:

- Property names use `camelCase`.
- Resource identifiers use a documented, consistent representation.
- Timestamps use ISO 8601 with UTC indicated explicitly.
- Nullable fields are documented rather than inferred.
- Clients must not depend on JSON property ordering.
- Undocumented fields are not guaranteed to be stable.

For example:

```json
{
  "recipientUsername": "bob",
  "amount": "25.00"
}
```

## 6. Monetary Representation

### 6.1 Request Format

Monetary amounts are represented as JSON strings using a decimal point.

Examples:

- `"50.00"`
- `"0.01"`
- `"1000000.00"`

This avoids ambiguity introduced by binary floating-point representations and allows strict validation of the original decimal scale.

### 6.2 Validation Rules

- The amount must parse as a valid decimal value under the documented input grammar.
- The amount must be greater than zero for deposits and transfers.
- More than two fractional decimal digits must be rejected.
- The API must not silently round an input amount.
- The individual operation amount must not exceed `$1,000,000.00`.
- The resulting wallet balance must not exceed `$1,000,000.00`.
- Currency is USD only in the MVP.

The precise accepted string grammar, including signs, leading zeros, whitespace, and exponent notation, must be specified by the endpoint contract and implemented consistently.

### 6.3 Response Format

Amounts in API responses are also represented as decimal strings with exactly two fractional digits.

Examples:

- `"0.00"`
- `"25.00"`
- `"1000000.00"`

The API must not serialize monetary values as floating-point JSON numbers.

## 7. Authentication and Resource Access

Protected endpoints require an authenticated request.

The server determines the current user from the verified authentication context. Clients must not select the acting user by supplying an arbitrary user or sender identifier.

The API must enforce resource ownership on every applicable read:

- A user can retrieve their own wallet.
- A user can retrieve their own transaction history.
- A user can retrieve transfer details only when they are the sender or recipient.

The API must not rely on frontend checks to enforce access restrictions.

The concrete authentication mechanism and development-authentication safeguards are defined in `docs/security/security-model.md`.

## 8. Idempotency

Deposit and transfer creation endpoints require the `Idempotency-Key` request header.

The key is scoped by authenticated user and operation type.

The API follows these rules:

1. The same scoped key and equivalent normalized request must replay the original successful response without repeating the financial operation.
2. Reuse of the same scoped key with a different request must return a documented conflict.
3. Concurrent requests using the same scoped key must not create duplicate financial effects.
4. The financial operation and its successful idempotency result must be committed atomically.
5. Failure handling must allow a valid retry according to the documented idempotency policy.

The header is required. The accepted format is `^[A-Za-z0-9_-]{8,128}$` (`CONST-005`). See `ADR-005 §4` for validation rules and `ADR-005 §5` for the request fingerprint.

Idempotency is not a replacement for database transaction guarantees or concurrency control.

## 9. HTTP Status Codes

The API uses status codes consistently with the operation outcome.

| Status | Meaning |
|---|---|
| `200 OK` | A read request succeeded, or an existing successful result is replayed where the endpoint contract permits it. |
| `201 Created` | A new deposit or transfer was successfully committed. |
| `400 Bad Request` | The request is malformed or fails request-contract validation. |
| `401 Unauthorized` | Authentication is missing or invalid. |
| `403 Forbidden` | The caller is authenticated but lacks permission for an operation, where the API chooses to distinguish this case. |
| `404 Not Found` | The requested resource does not exist or is not accessible under the documented resource-visibility policy. |
| `409 Conflict` | A business conflict prevents the requested operation, such as insufficient funds, a recipient balance limit violation, or reuse of an idempotency key with a different request. |
| `429 Too Many Requests` | The request is rejected by an applicable rate limit, if rate limiting is implemented. |
| `500 Internal Server Error` | An unexpected server-side failure occurred. |

Endpoint contracts must specify the applicable status codes. The API must not return success for an operation that has not committed.

A `403` versus `404` policy for inaccessible resources must be applied consistently and finalized in the security and endpoint contracts.

## 10. Error Responses

Errors use the Problem Details format unless a documented protocol-level constraint prevents it.

The error contract must:

- Provide a stable machine-readable error identifier where applicable.
- Include a human-readable summary safe for clients.
- Avoid exposing stack traces, SQL statements, database schema details, credentials, or internal exception messages.
- Include validation details when they can be disclosed safely.
- Support correlation between client-visible failures and server-side diagnostics.

The exact response fields and error-code catalogue are defined in `docs/api/error-model.md`.

Clients should branch on documented status codes and stable error identifiers, not on human-readable message text.

## 11. Pagination

Transaction history uses cursor-based pagination.

The default order is newest first. The ordering must be deterministic, using `(CreatedAtUtc, Id)` as the ordering tuple.

The API must:

- Return a bounded number of records per response.
- Apply the same ownership restriction to every page.
- Reject invalid pagination parameters according to the documented contract.
- Provide a continuation cursor when more records are available.
- Avoid requiring clients to construct database-specific ordering keys.

The default page size, maximum page size, cursor encoding, and behavior for invalid or stale cursors must be defined in `docs/api/endpoints.md`.

The default page size is `CONST-007` (20). The maximum page size is `CONST-008` (100). Values above the maximum are rejected with `400 VALIDATION_ERROR`.

## 12. Timestamps and Ordering

Timestamps are expressed in UTC using ISO 8601 with explicit UTC (`CONST-026`). The application uses .NET's round-trip `"O"` format for serialization, which produces fractional seconds when present (e.g. `2026-10-18T12:30:45.1234567Z`).

Persisted timestamps use SQL Server `datetime2(7)` (`CONST-009`), preserving the same precision.

The server is responsible for assigning authoritative operation timestamps. Client-provided timestamps must not determine the order or creation time of financial records.

When multiple records share the same timestamp, a stable secondary identifier must preserve deterministic pagination.

The API contract must not imply that timestamps alone provide a globally unique order.

## 13. API Versioning and Compatibility

The MVP uses an explicit version in the URL path: `/api/v1`.

Changes that alter the meaning of existing fields, remove documented fields, change status-code semantics, or invalidate previously valid requests must be treated as potentially breaking changes.

Additive changes must still be evaluated for compatibility with strict clients and generated SDKs.

A new API version should be introduced when a breaking change cannot be avoided within the existing contract. The project does not require a formal multi-version support policy for the MVP, but compatibility decisions must be documented.

## 14. Request Size, Timeouts, and Cancellation

The API should impose reasonable request-size and execution-time limits appropriate to its synchronous operations.

Request cancellation should be propagated to application and database operations where supported.

Cancellation does not prove that a financial transaction failed to commit. If a client disconnects during a write, the result may be uncertain from the client's perspective; retries must therefore use the same idempotency key.

Timeouts and cancellation must not be implemented in a way that returns success before the database commit.

Exact limits remain a configuration decision and must be documented in the operational configuration.

## 15. Logging and Sensitive Data

The API should support correlation identifiers or equivalent request tracing for diagnostics.

Logs must not include:

- Authentication credentials or tokens.
- Raw secrets.
- Full request headers containing sensitive values.
- Unnecessary personal information.
- Full idempotency keys when a safer identifier or redacted representation is sufficient.

Financial amounts and operation identifiers may be logged only where justified by operational needs and consistent with the project's data-handling policy.

Client-facing error messages must not expose internal implementation details.

## 16. Open Decisions

The following items must be finalized in the relevant contracts:

- Accepted monetary string grammar. (unique to this document)
- Identifier format for each resource. (unique to this document)
- Exact success response bodies and `Location` header behavior. (tracked in OQ-018)
- Idempotency key syntax, length, retention, and replay representation. (partially resolved: syntax/length in CONST-005/006; retention/replay in OQ-010)
- Default and maximum transaction-history page sizes. (tracked in OQ-013)
- Cursor encoding and invalid-cursor behavior. (tracked in OQ-014)
- Exact `403` versus `404` resource-visibility policy. (tracked in OQ-015)
- Rate-limiting requirements. (tracked in OQ-023)
- Request-size and execution-time limits. (unique to this document)
- Exact error-code catalogue and validation response shape. (tracked in OQ-020)

Until those decisions are documented, implementations must not independently invent incompatible conventions.

## 17. Related Documents

- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/requirements/functional-requirements.md`
- `docs/requirements/domain-rules.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/security/security-model.md`
- `docs/operations/configuration.md`
- `docs/operations/observability.md`
