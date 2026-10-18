# Frontend Architecture

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Scope:** Out of MVP; future phase.

## 1. Purpose

This document defines the proposed architectural structure of the Wallet API frontend.

The frontend is a client application responsible for presenting wallet information, collecting user input, communicating with the Wallet API, and displaying operation results.

The backend remains the authoritative system for financial business rules, authorization, concurrency control, idempotency, and data persistence.

The frontend architecture SHOULD remain simple and modular enough to support the MVP without introducing unnecessary abstractions.

## 2. Architectural Principles

The frontend SHOULD follow these principles:

- **Separation of concerns:** UI rendering, API communication, and application state have distinct responsibilities.
- **Backend authority:** Financial rules and security decisions are enforced by the API.
- **Explicit state:** Loading, success, empty, error, and uncertain operation states are represented clearly.
- **Predictable data flow:** Components receive data and trigger explicit actions rather than directly manipulating unrelated application state.
- **Typed contracts:** API request and response models are represented with appropriate types.
- **Minimal complexity:** Introduce architectural patterns only when they solve a demonstrated problem.
- **Testability:** Core UI behavior and API integration should be testable independently where practical.
- **Accessibility:** Architectural choices should support accessible forms, navigation, and status messages.

## 3. Logical Architecture

The frontend is organized into the following logical areas.

```text
Frontend Application
│
├── Presentation
│   ├── Pages
│   ├── Layouts
│   └── Reusable Components
│
├── Features
│   ├── Wallet
│   ├── Deposits
│   ├── Transfers
│   └── Transaction History
│
├── Application State
│   ├── Server Data
│   ├── Form State
│   └── Request Status
│
├── API Client
│   ├── HTTP Transport
│   ├── Request and Response Types
│   └── Error Normalization
│
└── Cross-Cutting Concerns
    ├── Routing
    ├── Authentication Integration
    ├── Configuration
    └── Accessibility
```

This is a logical decomposition, not a requirement to create a separate package or directory for every item.

## 4. Architectural Responsibilities

### 4.1 Presentation Layer

The presentation layer contains pages, layouts, and reusable UI components.

Responsibilities:

- Render wallet balances and transaction records.
- Display forms and validation feedback.
- Show loading, success, empty, and error states.
- Provide accessible navigation and interactions.
- Trigger application actions in response to user input.

The presentation layer MUST NOT directly access the database or implement authoritative financial rules.

### 4.2 Feature Modules

Feature modules organize behavior around user-visible capabilities.

Proposed features:

- `wallet`: wallet overview and balance.
- `deposits`: deposit form and submission flow.
- `transfers`: transfer form and result.
- `transactions`: transaction history and pagination.

Each feature SHOULD own its relevant UI components and feature-specific state where practical.

Shared components SHOULD be introduced when reuse or consistency justifies them, not simply because two components have superficial similarities.

### 4.3 Application State

Application state coordinates user interactions and API operations.

It includes:

- Current server data.
- Form input and local validation state.
- Request lifecycle state.
- Pagination cursors.
- Submission state.
- The idempotency key associated with an in-progress logical financial operation.

Server data SHOULD be distinguished from temporary UI state.

The implementation may use framework-native state, a server-state library, or a dedicated state-management library. The specific approach remains undecided.

### 4.4 API Client

The API client encapsulates communication with the backend.

Responsibilities:

- Build HTTP requests.
- Serialize request bodies according to the API contract.
- Parse successful responses.
- Normalize API errors.
- Apply request cancellation where appropriate.
- Support idempotency headers for financial write operations.
- Expose typed functions to feature modules.

Feature components SHOULD NOT independently construct inconsistent API URLs, headers, or error-parsing logic.

The API client MUST NOT silently retry financial operations with a different idempotency key.

### 4.5 Routing

Routing maps application URLs to pages and controls navigation between features.

Proposed routes include:

- `/wallet`
- `/wallet/transactions`
- `/transfers`
- `/transfers/:id`

These routes are illustrative and may change when the frontend framework and navigation requirements are selected.

Client-side route protection is a usability mechanism only. The backend must independently authorize every protected request.

### 4.6 Authentication Integration

Authentication integration provides the frontend with the ability to participate in the selected authentication flow.

Its implementation depends on the eventual identity provider and session model.

The frontend MUST NOT treat a username, user ID, or other client-controlled value as proof of identity.

The backend derives the authenticated user from the verified request identity.

## 5. Dependency Rules

The following dependency rules SHOULD be maintained:

1. Presentation components depend on feature actions or application-facing interfaces.
2. Feature logic uses the shared API client rather than implementing separate transport logic.
3. API request and response types reflect the documented backend contract.
4. Shared UI components remain independent of financial persistence details.
5. Authentication state is not used as a substitute for server-side authorization.
6. Financial business rules are not duplicated as authoritative frontend logic.
7. Low-level HTTP implementation details SHOULD NOT leak into every presentation component.

The frontend does not need to reproduce the backend's Clean Architecture project structure. The two applications have different responsibilities and complexity.

## 6. Data Flow

A typical read operation follows this flow:

1. A page or feature requests data.
2. Application state marks the request as loading.
3. The API client sends the HTTP request.
4. The API response is parsed and validated against expected shapes where appropriate.
5. Application state is updated.
6. The UI renders the resulting data or an error state.

A financial write operation follows this flow:

1. The user submits a validated form.
2. The feature creates or reuses the idempotency key associated with that logical operation.
3. The API client sends the request.
4. The backend performs authoritative validation and transaction processing.
5. The frontend handles the returned success or error response.
6. The UI displays a result based on the API response.

If the outcome is uncertain because the connection fails, the frontend MUST NOT assume either success or failure. It should preserve the logical operation's idempotency key and follow the documented retry or recovery behavior.

## 7. Financial Operation State

Financial operations require more careful state handling than ordinary UI interactions.

A deposit or transfer feature SHOULD distinguish at least:

- `idle`: no submission is in progress.
- `submitting`: the request is being sent or processed.
- `succeeded`: the API has confirmed success.
- `failed`: the API has returned a definitive failure.
- `outcomeUnknown`: the client cannot determine the final result.

The exact implementation may use another representation, provided that these states remain distinguishable.

While a request is being submitted, the interface SHOULD reduce accidental duplicate submissions. This UI protection does not replace backend idempotency.

An uncertain outcome MUST NOT be presented as a confirmed failure when the server may already have committed the operation.

## 8. Monetary Data Handling

The frontend MUST follow the monetary representation defined by the API contract.

- Monetary values are transmitted as decimal strings.
- Values with more than two fractional decimal digits are rejected.
- Monetary values MUST NOT be converted to binary floating-point for authoritative financial calculations.
- Formatting for display MUST NOT alter the value sent to the backend.
- Client-side checks do not replace server-side validation of amount and balance limits.

If arithmetic is needed for presentation, the implementation SHOULD use decimal-safe techniques appropriate to the chosen language and framework.

## 9. Error Handling

The API client SHOULD normalize errors into a consistent application representation.

The normalized error may contain:

- HTTP status code.
- API error code.
- Human-readable message.
- Field-level validation errors.
- Correlation or trace identifier, when available.
- Whether the financial outcome is known or uncertain.

The UI SHOULD display actionable messages without exposing internal server details.

Business conflicts such as insufficient funds should be handled differently from network failures and unexpected server errors.

## 10. Testing Boundaries

The frontend testing strategy SHOULD include:

- Unit tests for pure helpers and feature logic.
- Component tests for rendering and user interactions.
- API-client tests for request construction and error parsing.
- Integration tests for important feature flows.
- End-to-end tests for critical user journeys when the required infrastructure is available.

Tests SHOULD verify that:

- Failed validation prevents an invalid submission.
- Successful results are rendered from API responses.
- Business errors are presented correctly.
- Pagination uses the returned cursor.
- An uncertain write operation does not receive a new idempotency key on retry.
- Duplicate-submit prevention behaves as intended.

Backend tests remain responsible for proving financial atomicity, database consistency, authorization enforcement, and concurrency correctness.

## 11. Configuration and Environments

The frontend SHOULD use environment-appropriate configuration for non-secret settings such as the API base URL.

The frontend MUST NOT contain database credentials, server-side secrets, or privileged API keys.

Development authentication helpers, if used, MUST be isolated from production builds and deployment configuration.

The deployment approach and exact configuration mechanism remain open decisions.

## 12. Proposed Directory Structure

The following is an illustrative structure, subject to the selected framework:

```text
frontend/
├── src/
│   ├── app/
│   │   ├── routing/
│   │   └── configuration/
│   ├── features/
│   │   ├── wallet/
│   │   ├── deposits/
│   │   ├── transfers/
│   │   └── transactions/
│   ├── shared/
│   │   ├── api/
│   │   ├── components/
│   │   ├── errors/
│   │   └── formatting/
│   └── tests/
├── public/
└── README.md
```

This structure is a starting point, not a finalized implementation requirement.

## 13. Open Decisions

Decisions made for the scaffold:

- Vue 3, TypeScript, Vite.
- Pinia for state.
- axios for HTTP.
- naive-ui for components.

Decisions deferred until the frontend enters MVP scope:

- API hosting and CORS strategy beyond local development.
- Authentication and session management.
- Routing and state-management libraries beyond the current defaults.
- API-client type generation.
- UI component library beyond naive-ui.
- Browser automation and E2E testing.
- Build and deployment process.

## 14. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/routing.md`
- `docs/frontend/state-management.md`
- `docs/frontend/api-integration.md`
- `docs/frontend/authentication.md`
- `docs/frontend/testing.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/architecture/architecture.md`
- `docs/project/open-questions.md`