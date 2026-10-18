# Frontend API Integration

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Scope:** Out of MVP; future phase.

## 1. Purpose

This document defines how the frontend communicates with the Wallet API.

It establishes conventions for HTTP requests, response parsing, error handling, request cancellation, and financial-operation retries.

The frontend MUST use the documented API contract as the source of truth for endpoint paths, request schemas, response schemas, and status codes.

## 2. Integration Principles

The frontend API integration SHOULD follow these principles:

- Centralize HTTP communication in a shared API client.
- Keep transport details out of presentation components.
- Represent API request and response models with explicit types.
- Preserve monetary precision.
- Normalize API errors into a consistent application format.
- Distinguish definitive operation results from uncertain outcomes.
- Use idempotency keys for supported financial write operations.
- Avoid automatic retries that could create duplicate logical operations.
- Never expose server-side secrets in frontend code.
- Test the API client independently of individual pages.

## 3. API Base URL and Configuration

The API base URL MUST be configurable per environment.

Example development configuration:

```text
API_BASE_URL=https://localhost:7001
```

The value is illustrative and MUST match the actual API launch configuration.

The frontend SHOULD construct endpoint URLs relative to the configured base URL rather than duplicating host names throughout the application.

Configuration MUST NOT contain database credentials, private signing keys, or other server-side secrets.

The implementation SHOULD fail clearly when required configuration is missing or invalid.

## 4. API Versioning and Endpoints

The initial API version uses the `/api/v1` prefix.

The frontend SHOULD access endpoints through named API-client functions rather than embedding paths directly in UI components.

| Function | HTTP method and path |
|---|---|
| Get wallet | `GET /api/v1/wallet` |
| Get transaction history | `GET /api/v1/wallet/transactions` |
| Create deposit | `POST /api/v1/wallet/deposits` |
| Create transfer | `POST /api/v1/transfers` |
| Get transfer details | `GET /api/v1/transfers/{id}` |

The API client MUST follow the request and response contracts defined in `docs/api/endpoints.md`.

## 5. Request Construction

The API client SHOULD provide a consistent mechanism for:

- HTTP method and URL.
- Request headers.
- Request body serialization.
- Authentication integration.
- Response parsing.
- Cancellation signals where supported.
- Error normalization.

Requests SHOULD use the `Accept: application/json` header where appropriate.

Requests containing JSON bodies SHOULD use `Content-Type: application/json`.

The API client MUST NOT assume that every response contains a JSON body. It should handle empty responses and unexpected response content types according to the endpoint contract.

## 6. Request and Response Types

The frontend SHOULD define explicit request and response types based on the API contract.

Illustrative TypeScript types:

```typescript
type Money = string;

interface CreateDepositRequest {
  amount: Money;
}

interface CreateTransferRequest {
  recipientUsername: string;
  amount: Money;
}
```

These types are examples of a possible implementation, not a commitment to TypeScript.

The frontend SHOULD distinguish request DTOs from response DTOs rather than using one loosely defined object for both purposes.

Type definitions provide compile-time assistance but do not replace runtime handling of potentially malformed or unexpected server responses.

If generated API types are adopted, the generation process SHOULD be reproducible and based on the actual API specification.

## 7. Monetary Values

Monetary values MUST follow the representation defined by the API contract.

For the current proposal:

- Amounts are transmitted as decimal strings.
- The decimal separator is a period.
- At most two fractional decimal digits are permitted.
- Amounts MUST NOT be silently rounded.
- Binary floating-point values MUST NOT be used as the authoritative representation of money.
- Display formatting MUST NOT change the submitted monetary value.

Example request:

```json
{
  "amount": "50.00"
}
```

The API client SHOULD pass validated monetary strings to the server without converting them to a floating-point number and back.

The backend remains responsible for validating precision, positivity, operation limits, and available balances.

## 8. Authentication

The API client SHOULD integrate with the selected authentication mechanism through a dedicated abstraction.

The exact mechanism remains undecided.

Depending on the chosen authentication model, the client may need to attach an access token or use an appropriately configured browser session.

The client MUST NOT invent or accept a sender identity from ordinary transfer form input. The backend determines the sender from the authenticated principal.

If a request returns `401 Unauthorized`, the frontend SHOULD follow the documented authentication recovery flow.

The client SHOULD avoid automatic authentication retries that can create redirect loops or repeatedly submit a financial operation.

## 9. Idempotency for Financial Requests

The following operations require an idempotency key under the current API proposal:

- `POST /api/v1/wallet/deposits`
- `POST /api/v1/transfers`

The API client SHOULD expose these operations through explicit functions that require an idempotency key.

Illustrative function signatures:

```typescript
createDeposit(
  request: CreateDepositRequest,
  idempotencyKey: string
): Promise<DepositResponse>;

createTransfer(
  request: CreateTransferRequest,
  idempotencyKey: string
): Promise<TransferResponse>;
```

These signatures are illustrative.

The client MUST send the key using the `Idempotency-Key` request header.

The same logical operation MUST retain the same key across retries. A new key MUST be generated for a genuinely new logical operation, not for another attempt to deliver the same request.

If the same key is submitted with a different payload, the API may return an idempotency conflict according to its contract.

The frontend MUST NOT attempt to resolve such a conflict by automatically generating another key and resubmitting the changed payload.

## 10. Response Handling

The API client SHOULD distinguish successful HTTP responses from unsuccessful responses.

For successful responses, it SHOULD:

1. Check the HTTP status.
2. Parse the response body according to the endpoint contract.
3. Validate required fields where runtime validation is used.
4. Return a typed result to the calling feature.

The frontend MUST NOT treat a successful HTTP response as valid application data if required fields are missing or have incompatible types.

For unsuccessful responses, the client SHOULD parse the documented error format where possible and produce a normalized error.

## 11. Error Normalization

The backend is expected to use Problem Details for API errors.

The client SHOULD normalize errors into a common application representation.

Illustrative type:

```typescript
interface ApiError {
  status: number;
  code?: string;
  title?: string;
  detail?: string;
  traceId?: string;
  fieldErrors?: Record<string, string[]>;
}
```

The final structure MUST be aligned with `docs/api/error-model.md`.

The client SHOULD handle at least:

- `400 Bad Request`: invalid request or validation failure.
- `401 Unauthorized`: authentication is missing or invalid.
- `404 Not Found`: requested resource is unavailable or not accessible.
- `409 Conflict`: business conflict or idempotency conflict.
- `500 Internal Server Error`: unexpected server failure.

The UI SHOULD use documented application error codes where available instead of relying exclusively on human-readable error text.

Raw exception details MUST NOT be displayed to users.

## 12. Business Errors and Network Errors

The client MUST distinguish an explicit API response from a failure to obtain a response.

Examples of explicit business errors include insufficient funds and recipient balance-limit violations.

Examples of transport failures include:

- Connection loss.
- Request timeout.
- Browser network error.
- Interrupted connection while reading a response.

An explicit business rejection may establish that the operation did not succeed, subject to the documented API semantics.

A transport failure may leave the outcome unknown if the request reached the server and the response was lost.

The frontend MUST NOT convert all transport failures into a definitive financial failure.

## 13. Retry Policy

Retries MUST be chosen according to the operation type and the available evidence about its outcome.

### 13.1 Read Requests

Read requests MAY be retried for transient failures using a bounded retry policy.

Retries SHOULD use a reasonable delay and MUST avoid unbounded loops.

### 13.2 Financial Write Requests

Deposit and transfer requests MUST NOT be automatically retried with a new idempotency key.

A retry of the same logical operation MUST preserve the original key and payload.

If the request outcome is uncertain, the frontend SHOULD:

1. Preserve the operation metadata.
2. Explain that the result could not be confirmed.
3. Offer a safe retry or documented recovery path.
4. Display the result returned by the API when it becomes available.

The frontend SHOULD NOT automatically retry every `5xx` response. The API contract and operation outcome semantics must determine the correct behavior.

## 14. Request Cancellation and Timeouts

The API client MAY support request cancellation and timeouts.

Cancellation is useful when a user leaves a page or when an obsolete read request no longer needs to complete.

However, cancellation of a client request does not guarantee cancellation of server-side processing.

For financial writes, a timeout or cancellation MUST NOT be interpreted as proof that the server rolled back the operation.

The UI must continue to handle the possibility of an unknown outcome.

## 15. Pagination

The transaction history endpoint uses cursor-based pagination in the current proposal.

The client SHOULD:

- Send the cursor returned by the API.
- Treat the cursor as opaque.
- Use the documented page-size parameter.
- Append entries only after a successful response.
- Avoid fabricating cursors or assuming a fixed page size.
- Handle missing cursors according to the API contract.

The exact page-size limits, response schema, and cursor encoding remain open decisions.

## 16. Logging and Diagnostics

The frontend SHOULD record only the information necessary to diagnose API integration issues.

It MAY record:

- Endpoint category.
- HTTP status.
- Request duration.
- Correlation or trace identifier.
- Sanitized error category.

The frontend MUST NOT log credentials, access tokens, or sensitive authentication material.

Financial request bodies and idempotency keys SHOULD NOT be logged by default. If operational diagnostics require additional metadata, the project must define an explicit data-minimization policy.

## 17. Testing Requirements

API-client tests SHOULD verify:

- Correct HTTP method and endpoint.
- Request body serialization.
- Required headers.
- Idempotency-key transmission.
- Monetary strings remain unchanged.
- Successful response parsing.
- Problem Details parsing.
- Handling of empty or malformed responses.
- Network failure behavior.
- Read-request retry limits.
- Preservation of the original key during financial-operation retries.
- Handling of uncertain operation outcomes.
- Request cancellation behavior.

Integration tests SHOULD verify compatibility with the actual backend contract.

## 18. Open Decisions

The following items are tracked. Their labels indicate status:

1. Which HTTP client library will be used?
2. Will request and response types be generated from an API specification?
3. What runtime response-validation mechanism is required?
4. Which authentication mechanism will the API client support?
5. What are the exact timeout and retry policies?
6. Will the backend provide an operation-status or recovery endpoint?
7. What is the final transaction-history response schema?
8. Which correlation identifier will be returned by the API?
9. How will cross-origin requests be configured if the frontend and API use different origins?

## 19. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/state-management.md`
- `docs/frontend/forms-and-validation.md`
- `docs/frontend/authentication.md`
- `docs/api/api-guidelines.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/project/open-questions.md`