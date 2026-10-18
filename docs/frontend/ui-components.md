# Frontend UI Components

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Scope:** Out of MVP; future phase.

## 1. Purpose

This document defines the proposed component system for the Wallet API frontend.

The component system SHOULD provide consistent presentation, reusable interaction patterns, and accessible user interfaces while remaining appropriately small for the MVP.

The component library and styling technology have not yet been selected.

## 2. Component Design Principles

Components SHOULD follow these principles:

- **Single responsibility:** Each component has a clear purpose.
- **Reusability:** Shared components are introduced when they provide meaningful reuse.
- **Composition:** Complex pages are assembled from smaller components.
- **Consistency:** Similar operations use consistent layouts and interaction patterns.
- **Accessibility:** Components support keyboard navigation, semantic markup, and accessible feedback.
- **Explicit state:** Loading, success, error, disabled, and empty states are represented deliberately.
- **Financial accuracy:** Monetary values are displayed without changing their underlying value.
- **Backend authority:** Components never enforce financial rules as a substitute for server validation.

The project SHOULD avoid building a large design system before the actual interface requirements justify it.

## 3. Component Categories

The proposed component system consists of four categories:

1. **Layout components:** Application shell, navigation, page containers, and section layouts.
2. **Shared UI components:** Buttons, inputs, alerts, dialogs, loading indicators, and status labels.
3. **Financial display components:** Balance display, money formatting, transaction rows, and transfer details.
4. **Feature components:** Wallet overview, deposit form, transfer form, and transaction history.

Feature components MAY compose shared components rather than duplicating their presentation logic.

## 4. Application Layout Components

### 4.1 Application Shell

The application shell provides the common structure for authenticated pages.

Responsibilities:

- Render the primary navigation.
- Provide a consistent page layout.
- Display global status messages where appropriate.
- Support responsive behavior.
- Provide semantic landmarks and a predictable heading structure.

The shell SHOULD NOT contain financial business logic.

### 4.2 Navigation

The navigation component provides links to the primary features:

- Wallet overview.
- Transaction history.
- Transfer form.

The current route SHOULD be visually and programmatically identifiable.

Navigation MUST remain usable with a keyboard and SHOULD expose meaningful accessible labels.

### 4.3 Page Container

A page container provides consistent spacing, content width, and responsive layout.

It SHOULD allow feature pages to define their own content without duplicating global layout rules.

## 5. Shared UI Components

The following components are proposed for reuse.

| Component | Responsibility |
|---|---|
| `Button` | Standard user actions and submission controls |
| `TextField` | Single-line text input |
| `MoneyInput` | Monetary amount entry |
| `Alert` | Informational, success, warning, or error message |
| `LoadingIndicator` | Indicates an active asynchronous operation |
| `EmptyState` | Explains that no data is currently available |
| `ErrorState` | Presents a recoverable page-level error |
| `ConfirmationDialog` | Confirms an action when confirmation is justified |
| `StatusBadge` | Displays a concise operation status |
| `PaginationControls` | Navigates through paginated results |

These are conceptual component names. Actual names and component boundaries may change with the selected framework.

Shared components SHOULD accept explicit properties and avoid hidden dependencies on global application state unless that dependency is intentional and documented.

## 6. Financial Display Components

### 6.1 Money Display

A shared money-display component SHOULD format monetary values consistently.

Responsibilities:

- Display USD values.
- Use the agreed locale and formatting policy.
- Preserve the semantic sign of the amount.
- Avoid converting the authoritative monetary string through binary floating-point.
- Distinguish a monetary value from a missing or unavailable value.

The display format remains subject to the project's locale and internationalization decisions.

### 6.2 Wallet Balance

The wallet balance component displays the balance returned by the API.

It SHOULD:

- Show the currency clearly.
- Support loading and error states.
- Avoid presenting a locally calculated balance as authoritative.
- Indicate when a refresh is in progress if stale data remains visible.

A failed balance refresh MUST NOT imply that a previously confirmed financial operation failed.

### 6.3 Transaction Row

A transaction row represents one entry in the wallet's transaction history.

It SHOULD display, where provided by the API:

- Transaction type.
- Signed amount from the wallet owner's perspective.
- Creation timestamp.
- A relevant transaction or transfer reference.

Incoming transfers and deposits increase the wallet balance. Outgoing transfers decrease it.

The frontend MUST use the transaction data returned by the API rather than infer financial entries from the current balance.

### 6.4 Transfer Details

The transfer details component displays information returned for a transfer.

It MAY include:

- Transfer identifier.
- Recipient or sender information permitted by the API.
- Amount.
- Creation timestamp.
- Operation status, if a status is part of the actual contract.

The frontend MUST NOT invent a `pending`, `processing`, or other operation status when the backend does not define one.

The current MVP assumes synchronous operations and does not require artificial intermediate processing states.

## 7. Feature Components

### 7.1 Wallet Overview

The wallet overview composes:

- Wallet balance.
- Deposit form or deposit entry point.
- Recent transaction summary, if included.
- Links to transaction history and transfers.

The page SHOULD keep balance retrieval and deposit submission responsibilities distinct even when both appear on the same screen.

### 7.2 Deposit Form

The deposit form SHOULD contain:

- Monetary amount input.
- Inline validation feedback.
- Submission button.
- Submission progress state.
- Success or failure feedback.

The form MUST submit a valid monetary string and the required idempotency key.

The form MUST NOT silently round the entered amount or report success before the API confirms it.

### 7.3 Transfer Form

The transfer form SHOULD contain:

- Recipient username input.
- Monetary amount input.
- Inline validation feedback.
- Submission button.
- Submission progress state.
- Result feedback.

The form MAY display a confirmation step before submission if this improves usability.

The backend remains responsible for recipient resolution, available funds, balance limits, and authorization.

### 7.4 Transaction History

The transaction history component SHOULD display a paginated list of transaction rows.

It SHOULD support:

- Initial loading.
- Loaded results.
- Empty history.
- Loading additional entries.
- Recoverable request errors.
- End-of-history state.

The component MUST treat the pagination cursor as opaque and MUST use the cursor supplied by the API.

## 8. Component State and Data Flow

Shared presentation components SHOULD generally receive their data through explicit properties and emit user actions through callbacks or the selected framework's equivalent mechanism.

For example:

- A money-display component receives a monetary value.
- A transaction row receives a transaction record.
- A button receives a disabled state and an action handler.
- A form receives submission state and reports a submit event.

Components SHOULD NOT independently fetch the same resource unless the chosen architecture explicitly supports that pattern.

API communication and server-state management SHOULD remain in the appropriate feature or data-access layer.

## 9. Financial Interaction Safety

Financial UI components require explicit handling of submission state.

For deposits and transfers:

- The submit control SHOULD prevent accidental repeated activation while a request is in progress.
- The logical operation's idempotency key MUST be preserved across retries.
- A network failure MUST NOT automatically be presented as a confirmed financial failure.
- An uncertain outcome SHOULD be explained clearly.
- A confirmed success SHOULD be rendered from the API response.
- Refreshing balance or transaction history SHOULD NOT resubmit the financial operation.

UI-level duplicate-submit prevention is supplementary. Backend idempotency and database transaction guarantees remain mandatory.

## 10. Validation and Error Presentation

Form components SHOULD support both client-side validation feedback and server-returned validation errors.

Field-level errors SHOULD be associated programmatically with the relevant inputs.

Page-level or operation-level errors SHOULD be displayed in a predictable location.

The interface SHOULD distinguish:

- Invalid input.
- Business-rule rejection.
- Authentication failure.
- Resource-access failure.
- Network failure.
- Uncertain financial outcome.

The UI MUST NOT expose stack traces, database details, secrets, or internal exception messages.

## 11. Accessibility Requirements

Components SHOULD follow established accessibility practices.

Requirements include:

- Semantic HTML elements.
- Labels for all form controls.
- Keyboard-accessible interaction.
- Visible focus indicators.
- Accessible validation messages.
- Appropriate status announcements for asynchronous operations.
- Sufficient text and control contrast.
- Correct heading hierarchy.
- Accessible dialogs with appropriate focus management.

Color MUST NOT be the sole indicator of an operation type or error state.

Accessibility should be considered during component design and verified in testing.

## 12. Responsive Behavior

The component system SHOULD support the screen sizes required by the MVP.

The layout SHOULD remain usable on smaller screens, including when displaying transaction history.

Wide tables MAY be replaced with responsive transaction cards if that provides better usability without removing essential information.

Important monetary values, operation results, and validation errors MUST remain visible and understandable at supported screen sizes.

## 13. Testing Requirements

Shared and feature components SHOULD be tested for:

- Correct rendering of monetary values.
- Handling of missing or invalid display data.
- Loading, empty, and error states.
- Form validation and accessible error association.
- Disabled and submitting button states.
- Rendering of incoming and outgoing transaction amounts.
- Pagination behavior.
- Keyboard interaction.
- Handling of uncertain financial-operation outcomes.
- Prevention of accidental duplicate submissions.

Tests SHOULD verify observable behavior rather than depend unnecessarily on component implementation details.

## 14. Open Decisions

The following items are tracked. Their labels indicate status:

1. Which component library, if any, will be used?
2. Which styling approach will be adopted?
3. Which locale and currency-formatting policy will apply?
4. Will the wallet overview include recent transactions?
5. Will transfers require a confirmation dialog?
6. Will transaction history use a table, list, or responsive card layout?
7. Which icon and typography systems will be used?
8. What accessibility conformance target will be adopted?

## 15. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/state-management.md`
- `docs/frontend/api-integration.md`
- `docs/frontend/forms-and-validation.md`
- `docs/frontend/accessibility.md`
- `docs/frontend/testing.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/project/open-questions.md`