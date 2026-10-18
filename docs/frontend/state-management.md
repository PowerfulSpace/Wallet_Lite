# Frontend State Management

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Scope:** Out of MVP; future phase.

## 1. Purpose

This document defines the proposed state-management strategy for the Wallet API frontend.

The objective is to keep application state predictable, minimize duplicated data, and handle asynchronous financial operations safely.

The specific state-management library has not been selected. The principles in this document apply regardless of the chosen frontend framework.

## 2. State Categories

Frontend state SHOULD be separated into the following categories.

### 2.1 Server State

Server state represents information obtained from the Wallet API.

Examples include:

- Current wallet balance.
- Transaction history.
- Transfer details.
- Data returned by API requests.

The backend is the source of truth for financial information.

The frontend MUST NOT treat cached data as guaranteed to reflect the latest server state.

### 2.2 Form State

Form state contains temporary user input that has not yet been submitted.

Examples include:

- Deposit amount.
- Recipient username.
- Transfer amount.
- Field-level validation errors.
- Unsaved form values.

Form state SHOULD remain local to the relevant feature unless multiple components genuinely need to share it.

### 2.3 Request State

Request state represents the lifecycle of an asynchronous operation.

It SHOULD distinguish relevant states such as:

- `idle`
- `loading`
- `success`
- `error`

Financial write operations require additional treatment for an uncertain outcome, as described below.

### 2.4 Navigation State

Navigation state includes information required to move between pages or preserve the user's place in a workflow.

Examples include:

- Transaction history cursor.
- Selected transfer details.
- Optional page-level filters, if introduced later.

Navigation state SHOULD NOT duplicate sensitive financial information unnecessarily.

### 2.5 Authentication State

Authentication state indicates whether the frontend has an authenticated identity and whether authentication status is still being resolved.

It MUST NOT be used as the sole basis for authorization decisions. The backend independently validates every protected request.

## 3. State Ownership

Each piece of state SHOULD have a clear owner.

| State | Recommended owner |
|---|---|
| Current wallet balance | Server-state layer |
| Transaction history pages | Server-state layer |
| Transfer details | Server-state layer |
| Deposit form input | Deposit feature |
| Transfer form input | Transfer feature |
| Field validation errors | Relevant form |
| Request lifecycle | Relevant feature or API-state layer |
| Idempotency key for an active logical operation | Financial-operation workflow |
| Current route | Routing layer |
| Authentication status | Authentication integration |

The implementation SHOULD avoid maintaining the same server data independently in multiple unrelated stores.

## 4. Server Data Fetching

Server data SHOULD be loaded through the shared API client.

For each request, the frontend SHOULD handle:

1. Initial loading.
2. Successful response.
3. Empty result, where applicable.
4. API error.
5. Network failure.
6. Cancellation or replacement by a newer request, where supported.

A page SHOULD NOT display stale data as if it were a newly confirmed result after a failed refresh without making the state clear.

If cached data remains visible during a refresh, the UI SHOULD distinguish the existing snapshot from the current request status.

## 5. Wallet Balance State

The wallet balance is authoritative only as returned by the backend.

The frontend MAY update or invalidate its cached wallet data after a confirmed deposit or transfer.

It SHOULD NOT independently decrement or increment the balance and present the calculated result as confirmed.

Recommended behavior after a successful financial operation:

1. Display the confirmed operation result.
2. Refresh or invalidate the wallet balance data.
3. Refresh transaction history when appropriate.
4. Handle refresh failures separately from the result of the completed operation.

A balance-refresh failure MUST NOT cause the frontend to report that an already confirmed deposit or transfer failed.

The frontend should preserve the distinction between the result of the financial operation and the result of subsequent read requests.

## 6. Transaction History State

Transaction history SHOULD be managed as paginated server data.

The frontend SHOULD:

- Store the currently displayed entries and the cursor needed for the next page.
- Treat the cursor as an opaque value.
- Append additional entries only when the corresponding page has loaded successfully.
- Avoid requesting the same page repeatedly because of unintended duplicate events.
- Handle an expired or invalid cursor according to the API contract.
- Reset pagination when the user explicitly starts a new history query.

The frontend SHOULD NOT assume that transactions remain in a fixed position while new operations are occurring.

If the API uses cursor pagination based on `(CreatedAtUtc, Id)`, the frontend should use the cursor returned by the API rather than recreating it locally.

## 7. Form State and Validation

Forms SHOULD separate input values, validation results, and submission state.

Client-side validation MAY provide immediate feedback for:

- Required fields.
- Basic username format.
- Positive monetary amounts.
- Maximum two fractional decimal digits.
- Maximum operation amount, where known from the API contract.

The backend remains responsible for authoritative validation, including available funds, recipient limits, authorization, and concurrent changes.

The frontend MUST NOT silently round an entered amount to two decimal places.

Validation errors returned by the API SHOULD be mapped to the relevant form fields when possible. Errors that cannot be mapped to a specific field SHOULD be presented as a form-level message.

## 8. Financial Operation Lifecycle

Deposits and transfers require explicit operation state because a network error does not necessarily mean that the server failed to commit the request.

The frontend SHOULD model the following conceptual states:

| State | Meaning |
|---|---|
| `idle` | No active submission |
| `submitting` | The request is being sent or processed |
| `succeeded` | The API confirmed the operation succeeded |
| `failed` | The API returned a definitive failure |
| `outcomeUnknown` | The client cannot determine whether the operation committed |

Transitions SHOULD be explicit and tested.

A typical successful flow is:

`idle → submitting → succeeded`

A definitive business rejection may follow:

`idle → submitting → failed`

A timeout or connection loss after submission may require:

`idle → submitting → outcomeUnknown`

The frontend MUST NOT automatically classify every network failure as a definitive financial failure.

## 9. Idempotency Key Management

Every deposit and transfer request MUST include the required `Idempotency-Key` header.

The key identifies a logical financial operation, not an individual HTTP attempt.

The frontend SHOULD follow these rules:

1. Generate a key when a new logical deposit or transfer is initiated.
2. Associate the key with the submitted request payload.
3. Reuse the same key when retrying the same logical operation.
4. Do not reuse the key for a different amount or recipient.
5. Create a new key only when the user deliberately starts a new logical operation.
6. Do not generate a new key automatically after a timeout merely to make the request succeed.

If the user changes the request payload after a submission, the application MUST distinguish the modified request from the previously submitted operation.

The backend remains responsible for enforcing idempotency and detecting key reuse with a different payload.

### 9.1 Uncertain Outcomes

When a request times out or the connection is interrupted after submission:

- Preserve the operation's idempotency key and payload association.
- Do not report a confirmed failure without sufficient evidence.
- Offer a safe retry using the same key, subject to the API contract.
- If a dedicated operation-status or recovery endpoint is introduced later, follow its documented behavior.

The frontend MUST NOT assume that a read of the wallet balance alone proves whether a specific operation succeeded.

### 9.2 Persistence Across Reloads

The storage mechanism for active operation metadata remains undecided.

If recovery after a browser refresh is required, the implementation should evaluate how to retain the key and request association without unnecessarily storing sensitive information.

The frontend MUST NOT persist credentials or server secrets as part of this recovery mechanism.

The final design must account for browser storage security, privacy, and the possibility that an operation has already committed.

## 10. Duplicate Submission Prevention

The frontend SHOULD reduce accidental duplicate submissions by:

- Disabling or otherwise guarding the submit action while a request is in progress.
- Preventing repeated activation from creating multiple logical operations unintentionally.
- Retaining the same idempotency key for retries of an existing operation.
- Avoiding automatic resubmission when a page remounts or refreshes.

These protections improve usability but do not guarantee financial correctness. Backend idempotency and database constraints remain mandatory.

## 11. Concurrent UI Requests

Multiple read requests may occur concurrently, for example when navigating quickly between pages.

The implementation SHOULD prevent an older response from overwriting newer state when requests target the same data and the older result is no longer relevant.

Request cancellation MAY be used where supported.

Cancellation of a client-side request MUST NOT be interpreted as proof that the server cancelled a financial operation.

Once a financial write request has been submitted, its final outcome must be handled according to the API response and idempotency contract.

## 12. Error State Management

The frontend SHOULD distinguish at least these categories:

- Input validation errors.
- Authentication errors.
- Authorization or resource-access errors.
- Business conflicts, including insufficient funds.
- Idempotency conflicts.
- Network failures.
- Unexpected server errors.

Errors SHOULD be normalized through the API client or a shared error-handling layer.

The UI SHOULD preserve user input after recoverable errors where safe and useful.

Sensitive technical details and raw server exceptions MUST NOT be shown to users.

## 13. State Reset and User Changes

When the authenticated identity changes or the user signs out, the frontend SHOULD clear user-specific cached data and transient state that must not cross identity boundaries.

This includes wallet data, transaction history, transfer details, and relevant authentication-dependent state.

The application MUST NOT display one user's cached wallet information to another authenticated user.

Any operation that remains in an uncertain state requires deliberate handling before its metadata is discarded, subject to the final authentication and recovery design.

## 14. Testing Requirements

State-management tests SHOULD cover:

- Initial loading and successful data retrieval.
- API errors and network failures.
- Balance refresh after confirmed operations.
- Preservation of a successful operation result when a subsequent refresh fails.
- Form input and validation behavior.
- Pagination and cursor handling.
- Idempotency-key reuse for retries.
- Distinguishing definitive failures from uncertain outcomes.
- Prevention of accidental duplicate submissions.
- Clearing user-specific state when identity changes.
- Handling out-of-order read responses.

Backend integration tests remain responsible for database atomicity, concurrency control, and financial invariants.

## 15. Technology Decisions

The following decisions remain open:

1. Whether to use framework-native state or a dedicated state-management library.
2. Whether to use a separate server-state caching library.
3. How to persist active idempotency metadata, if persistence is required.
4. Whether financial operation recovery needs a dedicated backend endpoint.
5. How cache invalidation will be implemented.
6. Which error normalization model will be shared across features.

The project SHOULD avoid introducing a global state store solely for data that can be owned locally by a page or feature.

## 16. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/api-integration.md`
- `docs/frontend/forms-and-validation.md`
- `docs/frontend/authentication.md`
- `docs/frontend/testing.md`
- `docs/api/api-guidelines.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/architecture/concurrency.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/project/open-questions.md`