# Frontend Testing

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the frontend testing strategy for the Wallet API project.

The goal is to verify that the frontend behaves correctly, communicates with the backend reliably, protects user-specific information, and presents financial operation outcomes accurately.

Testing must cover both normal user flows and failure scenarios. In particular, the frontend must not misrepresent the outcome of a deposit or transfer when a request fails or its outcome is uncertain.

## 2. Testing Principles

The frontend testing strategy SHOULD follow these principles:

- Test observable behavior rather than implementation details.
- Prioritize financial correctness and user-facing behavior.
- Use deterministic tests wherever practical.
- Keep unit tests independent of the backend and external services.
- Test API integration through controlled responses and dedicated integration environments.
- Test critical workflows against the actual backend API.
- Verify loading, success, validation, and error states.
- Avoid relying exclusively on snapshots or mocked dependencies.
- Keep tests maintainable and suitable for continuous integration.

Frontend tests complement backend tests. They MUST NOT replace server-side tests for authorization, transaction atomicity, idempotency, or concurrency control.

## 3. Testing Levels

### 3.1 Unit Tests

Unit tests SHOULD cover isolated functions, hooks, utilities, and state transitions.

Examples include:

- Monetary input parsing and validation.
- Formatting USD amounts for display.
- Mapping API errors to user-facing messages.
- Authentication-state transitions.
- Transaction type presentation.
- Cursor pagination logic.
- Idempotency-key lifecycle logic.
- Handling of unknown operation outcomes.

Unit tests SHOULD avoid unnecessary rendering complexity and external dependencies.

### 3.2 Component Tests

Component tests SHOULD verify individual UI components and their interaction behavior.

Examples include:

- Balance display.
- Transaction history rows.
- Deposit forms.
- Transfer forms.
- Validation messages.
- Loading indicators.
- Confirmation dialogs, if implemented.
- Error and success notifications.
- Pagination controls.

Tests SHOULD verify accessibility-relevant behavior, including labels, roles, keyboard interaction, and focus management where applicable.

### 3.3 Integration Tests

Frontend integration tests SHOULD verify interactions between related frontend modules.

Examples include:

- A transfer form submitting through the API client.
- API validation errors appearing on the correct fields.
- A successful deposit refreshing the wallet balance.
- Authentication expiration invalidating user-specific state.
- Transaction pagination loading the next page.
- An identity change clearing previously displayed wallet data.

These tests may use mocked HTTP responses or a controlled test server, depending on the scenario.

### 3.4 End-to-End Tests

End-to-end (E2E) tests SHOULD exercise critical workflows through the running frontend and backend.

Priority scenarios include:

- Viewing the current wallet balance.
- Depositing funds.
- Transferring funds between Alice and Bob.
- Viewing transaction history.
- Rejecting a transfer when funds are insufficient.
- Preventing duplicate financial effects when a request is retried.
- Handling authentication failures.
- Recovering from network failures without incorrectly reporting success or failure.

E2E tests MUST use an isolated test environment and test data that cannot affect production users or financial data.

## 4. Monetary Value Testing

Money-related behavior requires explicit coverage.

The frontend MUST preserve the API's monetary representation and validation rules.

Tests SHOULD cover:

- Valid positive amounts.
- Zero and negative amounts.
- Amounts with one or two fractional decimal digits.
- Amounts with more than two fractional decimal digits.
- Amounts exceeding the maximum operation amount.
- Invalid numeric formats.
- Empty and whitespace-only input.
- Large values near the configured limits.
- Formatting values for display without changing the underlying amount.

The frontend MUST NOT silently round invalid monetary input.

Tests SHOULD distinguish between the string entered by the user, the validated monetary value, and the representation sent to the API.

For monetary values, JavaScript floating-point arithmetic MUST NOT be used where it could alter the exact financial amount. Prefer validated decimal strings or an explicitly selected decimal-safe representation.

## 5. Financial Operation Testing

### 5.1 Deposit Flow

Tests SHOULD verify that:

1. The user enters a valid amount.
2. Client-side validation runs.
3. The request includes the required idempotency key.
4. The UI indicates that submission is in progress.
5. The successful response is presented accurately.
6. The wallet balance and transaction history are refreshed or updated consistently.
7. Validation and server errors are presented clearly.
8. Duplicate interaction does not cause an unintended second operation.

### 5.2 Transfer Flow

Tests SHOULD verify that:

1. The recipient username is validated.
2. The amount is validated.
3. The request includes the required idempotency key.
4. The sender identity is not taken from an arbitrary client-supplied user identifier.
5. The submission state prevents accidental repeated activation.
6. Success displays the correct recipient and amount.
7. Insufficient funds are presented as a business error.
8. A recipient or balance-limit rejection is handled correctly.
9. The UI does not report success before the backend confirms it.
10. Relevant wallet and transaction data are refreshed after success.

Backend tests remain responsible for proving that concurrent transfers preserve financial invariants.

## 6. Idempotency and Retry Testing

The frontend MUST preserve the relationship between a logical financial operation and its idempotency key.

Tests SHOULD cover the following cases:

| Scenario | Expected behavior |
|---|---|
| First submission succeeds | Display the confirmed result |
| User activates submit repeatedly while a request is in progress | Prevent unintended duplicate submission |
| Same operation is retried after a transient failure | Reuse the original key when retrying the same logical operation |
| User changes the amount or recipient | Treat the changed request as a different logical operation |
| Server reports an idempotency-key conflict | Display a clear error and do not silently generate a replacement key for the same request |
| Network connection is lost after submission | Represent the outcome as uncertain when the result cannot be established |
| User initiates a genuinely new operation | Generate a new idempotency key |

A frontend test cannot independently prove server-side idempotency. The backend must have separate tests for duplicate requests, concurrent requests, and atomic persistence.

## 7. Unknown Operation Outcomes

A financial operation can have an unknown outcome if the frontend does not receive a definitive response.

For example, the backend may commit a transfer while the response is lost in transit.

Tests MUST verify that the frontend:

- Does not automatically claim the operation failed.
- Does not automatically create a new operation with a new key.
- Preserves sufficient operation context for safe recovery.
- Provides a clear message that the outcome could not be confirmed.
- Follows the documented retry or reconciliation strategy.
- Does not display an unconfirmed success message.

The exact recovery workflow depends on the final API contract and remains an open decision.

## 8. API Client Testing

The API client SHOULD be tested for:

- Correct HTTP methods and paths.
- Correct request serialization.
- Required headers.
- Correct handling of monetary strings.
- Successful response parsing.
- Empty or malformed responses where relevant.
- Problem Details parsing.
- Mapping of HTTP status codes to application errors.
- Network failures and timeouts.
- Request cancellation.
- Authentication failures.
- Cursor pagination parameters.

Tests SHOULD verify that sensitive authentication information is not included in logs.

## 9. Authentication and Authorization UI Testing

Frontend tests SHOULD cover:

- Initial authentication-state loading.
- Authenticated and unauthenticated navigation.
- Session expiration.
- Logout behavior where supported.
- Clearing user-specific state after identity changes.
- Prevention of stale responses repopulating another user's view.
- Development authentication behavior in Development.
- Absence of development-only authentication controls in Production builds.

Frontend tests MUST NOT be treated as proof of backend authorization. Direct API tests must independently verify that users cannot access wallets or transfers they do not own.

## 10. Transaction History Testing

Transaction history tests SHOULD cover:

- Correct rendering of transaction types.
- Correct signed amount from the current user's perspective.
- Consistent date and time formatting.
- Empty history.
- Loading and error states.
- Cursor-based pagination.
- Duplicate items at page boundaries.
- Prevention of stale requests overwriting newer data.
- Refresh behavior after a successful financial operation.

The frontend MUST NOT assume that client-side sorting or filtering can replace the backend's pagination contract.

## 11. Accessibility Testing

Accessibility checks SHOULD be integrated into component and E2E testing where practical.

Tests SHOULD verify:

- Accessible names for form fields and buttons.
- Keyboard navigation.
- Visible focus.
- Accessible validation errors.
- Announcements for important operation states.
- Dialog behavior, if dialogs are implemented.
- Meaningful transaction labels that do not rely on color alone.

Automated accessibility checks SHOULD be supplemented with manual review.

See `docs/frontend/accessibility.md`.

## 12. Test Data and Environment Isolation

Tests involving Alice and Bob SHOULD use predictable, isolated test data.

The test environment MUST NOT use production credentials or production financial data.

Tests SHOULD reset or recreate their state between independent scenarios.

E2E tests SHOULD avoid depending on execution order.

If tests modify wallet balances or create transfers, the environment must ensure that parallel execution cannot introduce unintended cross-test dependencies.

## 13. Mocking Strategy

Mocks are appropriate for isolated frontend unit and component tests.

They SHOULD be used to simulate:

- Successful API responses.
- Validation errors.
- Insufficient-funds errors.
- Authentication failures.
- Network timeouts.
- Lost responses.
- Unexpected server errors.

Mocks MUST NOT be the only validation method for critical workflows.

At least the critical frontend-to-backend workflows SHOULD be tested against a running backend in an isolated environment.

## 14. Continuous Integration

The frontend CI pipeline SHOULD execute the following stages as applicable:

1. Dependency installation using the project's lockfile.
2. Formatting and static analysis.
3. Type checking.
4. Unit tests.
5. Component tests.
6. Integration tests.
7. Production build.
8. Automated accessibility checks.
9. Selected E2E tests.

The pipeline SHOULD fail when required checks fail.

Long-running or environment-dependent E2E tests may be separated from fast feedback checks, but critical financial workflows must remain part of the release validation process.

The specific tools and required coverage thresholds remain to be selected.

## 15. Definition of Done

A frontend feature SHOULD NOT be considered complete until:

- Relevant unit and component tests pass.
- API interactions are covered.
- Loading and error states are tested.
- Monetary validation is tested where applicable.
- Financial-operation retry behavior is tested where applicable.
- Authentication-dependent state is handled correctly.
- Accessibility checks are completed.
- Critical behavior is covered by integration or E2E tests where appropriate.
- Known gaps and unresolved risks are documented.

## 16. Open Decisions

1. Which frontend framework and testing libraries will be used?
2. Which E2E framework will be selected?
3. Which CI platform will run frontend tests?
4. What minimum coverage targets, if any, will be required?
5. How will E2E test data be seeded and reset?
6. Which financial-operation recovery endpoint or workflow will be available?
7. Which browsers and viewport sizes must be covered?
8. Which accessibility checks will be mandatory in CI?
9. How will frontend tests coordinate with SQL Server-backed integration environments?

## 17. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/api-integration.md`
- `docs/frontend/state-management.md`
- `docs/frontend/forms-and-validation.md`
- `docs/frontend/authentication.md`
- `docs/frontend/accessibility.md`
- `docs/development/testing.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/project/open-questions.md`