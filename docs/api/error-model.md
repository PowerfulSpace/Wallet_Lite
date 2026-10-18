# API Error Model

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the standard error response format for the Wallet API MVP.

All API endpoints must return errors consistently, without exposing internal implementation details, database information, or sensitive data.

## 2. Error Response Format

The API uses **RFC 9457 Problem Details for HTTP APIs**, represented as JSON with the media type `application/problem+json`.

### Example

```json
{
  "type": "https://wallet-api.example.com/problems/insufficient-funds",
  "title": "Insufficient funds",
  "status": 409,
  "detail": "The wallet does not have sufficient funds to complete the transfer.",
  "instance": "/api/v1/transfers",
  "code": "INSUFFICIENT_FUNDS",
  "traceId": "00-example-trace-id"
}
```

The domain name is illustrative and must be replaced with a real project-specific URI or a stable URN before implementation.

### Standard Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `type` | string | Yes | URI identifying the problem type |
| `title` | string | Yes | Short, human-readable problem summary |
| `status` | integer | Yes | HTTP status code |
| `detail` | string | No | Safe explanation of the specific error |
| `instance` | string | No | URI identifying the request occurrence |
| `code` | string | Recommended | Stable machine-readable application error code |
| `traceId` | string | Recommended | Correlation identifier for troubleshooting |

The `status` field must match the HTTP response status.

Clients should use `code` for application logic rather than parsing `title` or `detail`.

## 3. HTTP Status Codes

| Status | Meaning | Typical Use |
|---|---|---|
| `400 Bad Request` | Invalid request | Malformed JSON, invalid amount, invalid cursor, or failed input validation |
| `401 Unauthorized` | Authentication required | Missing, invalid, or expired credentials |
| `403 Forbidden` | Authenticated but explicitly forbidden | Use when access is denied and revealing resource existence is acceptable |
| `404 Not Found` | Resource unavailable | Unknown recipient, unknown transfer, or inaccessible transfer when existence must be concealed |
| `409 Conflict` | Request conflicts with current state | Insufficient funds, balance limit exceeded, self-transfer, or idempotency key conflict |
| `415 Unsupported Media Type` | Unsupported request format | Unsupported `Content-Type` |
| `429 Too Many Requests` | Request rate limit exceeded | Only if rate limiting is implemented |
| `500 Internal Server Error` | Unexpected server failure | Unhandled application or infrastructure failure |
| `503 Service Unavailable` | Service temporarily unavailable | Temporary inability to serve requests, when deliberately handled |

Not every status code must be used in the MVP. For example, `429` should not be documented as an expected response until rate limiting exists.

## 4. Application Error Codes

Error codes are stable identifiers intended for client applications, automated tests, and diagnostics.

### Request Validation

| Code | HTTP Status | Meaning |
|---|---:|---|
| `VALIDATION_ERROR` | 400 | One or more input fields are invalid |
| `MALFORMED_REQUEST` | 400 | The request body cannot be parsed |
| `UNSUPPORTED_MEDIA_TYPE` | 415 | The request uses an unsupported media type |

### Authentication and Access

| Code | HTTP Status | Meaning |
|---|---:|---|
| `AUTHENTICATION_REQUIRED` | 401 | The request is not authenticated |
| `ACCESS_DENIED` | 403 | Access is forbidden |
| `RESOURCE_NOT_FOUND` | 404 | The requested resource does not exist or is not visible to the current user |

### Wallet and Financial Operations

| Code | HTTP Status | Meaning |
|---|---:|---|
| `INSUFFICIENT_FUNDS` | 409 | The sender's wallet cannot cover the transfer |
| `BALANCE_LIMIT_EXCEEDED` | 409 | The resulting wallet balance would exceed the configured maximum |
| `INVALID_TRANSFER` | 409 | The transfer violates a business rule, such as transferring to oneself |
| `IDEMPOTENCY_KEY_REUSED` | 409 | A previously used key was supplied with a different request |
| `OPERATION_CONFLICT` | 409 | The operation conflicts with the current persisted state |

### Infrastructure and Unexpected Failures

| Code | HTTP Status | Meaning |
|---|---:|---|
| `INTERNAL_ERROR` | 500 | An unexpected error occurred |
| `SERVICE_UNAVAILABLE` | 503 | The service cannot currently process the request |
| `IDEMPOTENCY_IN_PROGRESS` | 503 | A matching idempotent request is still being processed after the bounded wait; retry with the same key and request |

Error codes must be documented and tested. Implementations must not expose raw exception messages as application error codes or descriptions.

## 5. Validation Error Details

For field-level validation, the response may include an `errors` object whose keys identify request fields and whose values are arrays of messages.

### Example

```json
{
  "type": "https://wallet-api.example.com/problems/validation-error",
  "title": "Request validation failed",
  "status": 400,
  "detail": "One or more fields contain invalid values.",
  "code": "VALIDATION_ERROR",
  "errors": {
    "amount": [
      "Amount must be greater than zero.",
      "Amount must have no more than two fractional decimal digits."
    ],
    "recipientUsername": [
      "Recipient username is required."
    ]
  }
}
```

Rules:

- Field names must correspond to the public API contract.
- Messages must be safe for client display.
- Responses must not include stack traces, SQL statements, internal entity details, or sensitive values.
- Validation must not silently round an amount or change it to another value.
- The exact messages are not guaranteed to remain stable; clients should rely on the error code and field names.

## 6. Financial Error Semantics

### Insufficient Funds

A transfer that exceeds the sender's available balance must fail with `409 Conflict` and code `INSUFFICIENT_FUNDS`.

No debit, credit, transfer record, or ledger entry may be committed.

### Balance Limit Exceeded

If a deposit or transfer would make a wallet balance exceed `1,000,000.00`, the operation must fail with `409 Conflict` and code `BALANCE_LIMIT_EXCEEDED`.

The operation must have no partial financial effect.

### Idempotency Conflict

If an idempotency key is reused with a different request payload within its defined scope, return `409 Conflict` with code `IDEMPOTENCY_KEY_REUSED`.

The original operation and its persisted result must remain unchanged.

### Resource Not Found

If a user requests transfer details for a transfer they are not authorized to view, return the same public `404` response used for a nonexistent transfer. This avoids disclosing whether another user's transfer exists.

Whether unknown recipient usernames should also be concealed is an open security decision and must be consistent across the API.

## 7. Security Requirements

Error responses must not disclose:

- Stack traces or exception types.
- Database schema, SQL statements, or connection details.
- Authentication tokens, credentials, or secrets.
- Another user's balance, wallet identifier, or transaction details.
- Internal filesystem paths or deployment configuration.
- Idempotency records belonging to another user.

Public messages should be concise. Detailed diagnostics belong in server-side logs and must be access-controlled.

## 8. Logging and Correlation

Unexpected errors should be logged with a correlation or trace identifier so they can be investigated without returning internal details to the client.

Logs must not include secrets or unnecessarily duplicate financial payloads.

The `traceId` returned to the client should correspond to the identifier available in server-side diagnostics. Its precise source and format are implementation decisions.

Expected business conflicts, such as insufficient funds, should be observable through structured logging or metrics without being treated automatically as unhandled server errors.

## 9. Consistency Requirements

- Every error response must use the common Problem Details format.
- The HTTP status and body `status` must agree.
- Each documented application error code must map consistently to its documented HTTP status.
- Error responses must not imply that a failed financial operation partially succeeded.
- Error responses for repeated idempotent requests must follow the idempotency contract.
- Error responses must not expose sensitive information through differences in messages where resource concealment is required.

## 10. Open Decisions

1. Choose stable production values for Problem Details `type` URIs.
2. Confirm the final application error-code catalogue.
3. Decide whether unknown recipient usernames should return `404` or a generic response.
4. Define the trace identifier source and logging format.
5. Decide whether the API will implement rate limiting in the MVP.
6. Confirm whether the validation response uses ASP.NET Core's default validation extensions or a customized, standardized format.
7. Define retryable infrastructure failures and their client-facing behavior.
