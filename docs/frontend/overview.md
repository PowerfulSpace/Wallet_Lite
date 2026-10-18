# Frontend Overview

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the proposed scope and responsibilities of a potential frontend application for the Wallet API project.

**Scope note:** The frontend is **out of MVP scope**. This document and the other `docs/frontend/` documents are drafts for a future phase. They do not commit the project to implementing a frontend as part of the MVP.

The frontend would provide a user interface for interacting with virtual wallets, making deposits, initiating transfers, and reviewing transaction history.

The backend API remains the authoritative component responsible for authentication, authorization, validation, financial calculations, concurrency control, idempotency, and persistence.

The existence of this documentation does not commit the project to implementing a frontend as part of the initial backend MVP.

## 2. Frontend Goals

If implemented, the frontend SHOULD:

- Provide a clear view of the authenticated user's wallet balance.
- Allow users to submit virtual deposit requests.
- Allow users to initiate transfers to another user by username.
- Display wallet transaction history.
- Present validation errors and business conflicts in an understandable way.
- Handle loading, success, empty, and error states consistently.
- Prevent accidental duplicate submissions where practical.
- Support keyboard navigation and accessible interaction patterns.
- Keep financial business rules enforced by the backend rather than relying on client-side validation alone.

## 3. Scope

### 3.1 Potential MVP Features

The initial frontend scope may include:

- Wallet overview and current balance.
- Deposit form.
- Transfer form.
- Transaction history with pagination.
- Transfer result or status details.
- Authentication integration appropriate to the selected environment.
- Error and confirmation messages.
- Basic responsive layout.

These features are proposed and must be confirmed before frontend implementation begins.

### 3.2 Out of Scope

Unless the project scope changes, the frontend does not include:

- Real banking or payment-provider integrations.
- Real-money transactions.
- Multi-currency support.
- Cryptocurrency functionality.
- Full user registration and password management.
- Administrative wallet adjustment tools.
- Financial reconciliation interfaces.
- A complete back-office administration portal.
- Advanced analytics or financial reporting.

## 4. Relationship with the Backend

The frontend communicates with the Wallet API through documented HTTP endpoints.

The backend is the source of truth for all financial state.

The frontend MUST NOT:

- Calculate and persist wallet balances independently.
- Assume a transfer succeeded before receiving confirmation.
- Bypass backend validation.
- Choose an arbitrary sender identity.
- Treat client-side checks as authorization controls.
- Retry a financial operation with a new idempotency key merely because the original request timed out.

The frontend MAY perform early validation to improve usability, provided that the backend independently validates every request.

## 5. Core User Flows

### 5.1 View Wallet

1. The user opens the wallet overview.
2. The frontend requests the current wallet information.
3. The interface displays the returned balance.
4. If the request fails, the interface presents an appropriate error and offers a safe retry.

The displayed balance should be treated as a snapshot returned by the API, not a guarantee that no concurrent operation has changed it since the response was generated.

### 5.2 Make a Deposit

1. The user enters a deposit amount.
2. The frontend performs basic input validation.
3. The frontend submits the deposit request with an idempotency key.
4. The interface displays the confirmed result when the API returns success.
5. If the result is uncertain because of a timeout or connection failure, the frontend preserves the logical request identity and follows the documented retry behavior.

The frontend MUST NOT generate a new idempotency key for an automatic retry of the same logical deposit.

### 5.3 Transfer Funds

1. The user enters the recipient username and amount.
2. The frontend performs basic input validation.
3. The frontend submits the transfer with an idempotency key.
4. The backend validates the request and performs the financial transaction atomically.
5. The frontend displays the confirmed result or a suitable error.

The interface MUST make clear that a transfer may fail because of insufficient funds, recipient balance limits, an invalid recipient, or another documented business rule.

### 5.4 Review Transaction History

1. The frontend requests the first page of transaction history.
2. The interface displays deposits, incoming transfers, and outgoing transfers.
3. The user requests additional entries.
4. The frontend uses the returned pagination cursor to request the next page.

The displayed amount sign must reflect the wallet owner's perspective: deposits and incoming transfers increase the balance, while outgoing transfers decrease it.

## 6. Frontend Architecture

The specific framework and implementation architecture have not been selected.

If the frontend is implemented, it SHOULD separate the following concerns:

- **Presentation:** Pages, layouts, and reusable UI components.
- **Routing:** Navigation between wallet, transfer, and history views.
- **API integration:** HTTP requests, response parsing, and error normalization.
- **State management:** Server data, request status, and temporary form state.
- **Validation:** User-friendly input validation that mirrors, but does not replace, backend rules.
- **Authentication:** Integration with the approved identity mechanism.
- **Testing:** Unit, component, and end-to-end verification where appropriate.

The frontend architecture should remain proportional to the size and complexity of the MVP.

## 7. Financial Input and Display

The frontend MUST preserve the monetary precision required by the API contract.

- Amounts must support at most two fractional decimal digits.
- Invalid extra fractional digits must not be silently rounded.
- Monetary values must be transmitted in the format specified by the API contract.
- Client-side calculations must not be treated as authoritative.
- Display formatting must clearly identify USD.
- The UI must distinguish an entered amount from a confirmed financial result.

The exact input component behavior, locale handling, and formatting rules should be documented before implementation.

## 8. Error Handling

The frontend SHOULD handle the documented API error model consistently.

Examples include:

- Authentication required.
- Invalid input.
- Insufficient funds.
- Recipient balance limit exceeded.
- Idempotency conflict.
- Resource not found or not accessible.
- Temporary service or network failure.

Error messages should be actionable and understandable without exposing internal exception details.

For uncertain outcomes of financial operations, the interface should explain that the result could not be confirmed and provide a safe way to retry the same logical request.

## 9. Security and Privacy

The frontend is not a security boundary.

The backend MUST enforce authentication, authorization, ownership checks, financial limits, idempotency, and transaction integrity regardless of frontend behavior.

The frontend SHOULD:

- Avoid storing sensitive authentication material in insecure locations.
- Avoid logging credentials or sensitive request payloads.
- Use secure transport outside local development.
- Avoid exposing internal identifiers or implementation details unnecessarily.
- Clear user-specific state when the authenticated identity changes or the user signs out.
- Avoid rendering untrusted content as executable markup.

The appropriate browser authentication and session-storage strategy depends on the identity provider and deployment model, both of which remain open decisions.

## 10. Accessibility and Responsive Design

If implemented, the frontend SHOULD follow established accessibility practices.

This includes:

- Keyboard-accessible controls.
- Visible focus indicators.
- Accessible labels for form fields.
- Clear association between validation errors and their fields.
- Sufficient contrast.
- Semantic headings and landmarks.
- Responsive layouts for supported screen sizes.
- Accessible status messages for asynchronous operations.

Accessibility requirements should be verified during implementation rather than postponed until final styling.

## 11. Testing Strategy

The frontend testing strategy SHOULD cover:

- Rendering wallet information.
- Form validation.
- API success and error handling.
- Loading and empty states.
- Pagination.
- Idempotency-key reuse during retries.
- Preventing accidental duplicate submissions.
- Authentication and user-specific state transitions.
- Keyboard navigation and accessible form feedback.
- End-to-end financial flows against a suitable test API environment.

Frontend tests MUST NOT replace backend tests for financial invariants, database atomicity, or concurrency.

## 12. Technology Decisions

The following decisions remain open:

1. Which frontend framework will be used?
2. Will the frontend be a separate application or hosted alongside the API?
3. Which routing and state-management approach is appropriate?
4. How will authentication be integrated?
5. Which component library, if any, will be adopted?
6. Will end-to-end tests use a browser automation framework?
7. Is the frontend required for the initial MVP, or will API tooling be sufficient for demonstration?

These decisions should be recorded before the frontend implementation begins.

## 13. Related Documents

- `docs/api/api-guidelines.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/security/security-model.md`
- `docs/project/roadmap.md`
- `docs/project/open-questions.md`
- `docs/project/glossary.md`