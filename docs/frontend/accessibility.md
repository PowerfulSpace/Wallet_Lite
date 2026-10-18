# Frontend Accessibility

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines accessibility requirements for the Wallet API frontend.

The interface should be usable by people with different abilities, input methods, and assistive technologies. Accessibility must be considered throughout implementation rather than added as a final styling step.

Particular attention is required for financial operations, where unclear labels, validation errors, or submission states may cause users to misunderstand an action or its outcome.

## 2. Accessibility Goals

The frontend SHOULD:

- Support keyboard-only navigation.
- Provide meaningful semantic structure.
- Associate form controls with accessible labels.
- Communicate validation errors clearly.
- Expose important dynamic updates to assistive technologies.
- Maintain readable text and sufficient visual contrast.
- Avoid relying on color alone to communicate status.
- Support browser zoom and responsive layouts.
- Make financial actions and their outcomes understandable.
- Prevent accidental duplicate submissions without making controls inaccessible.

## 3. Accessibility Standard

The project SHOULD target **WCAG 2.2 Level AA**.

The implementation should use semantic HTML and native browser capabilities wherever practical. Custom controls and interaction patterns must provide equivalent keyboard and assistive-technology support.

Accessibility testing should combine automated checks with manual keyboard and screen-reader testing. Automated tools alone cannot establish full conformance.

The exact conformance scope and supported browser and assistive-technology combinations remain to be confirmed.

## 4. Semantic Structure

Pages SHOULD use appropriate semantic elements, including:

- `header` for page-level introductory content.
- `nav` for navigation.
- `main` for the primary page content.
- `section` for logically grouped content.
- `form` for user input and submission.
- `button` for actions.
- `table` for genuinely tabular transaction data where appropriate.

Each page SHOULD have a descriptive document title and a clear primary heading.

Heading levels MUST reflect the content hierarchy rather than being selected solely for visual appearance.

Interactive elements MUST use appropriate native elements instead of non-semantic containers with click handlers wherever possible.

## 5. Keyboard Navigation

All interactive functionality MUST be available using a keyboard.

Requirements include:

- Logical focus order.
- Visible keyboard focus indicators.
- No keyboard traps.
- Keyboard-operable navigation and forms.
- Accessible operation of dialogs, menus, and other custom controls if introduced.
- Focus management after significant navigation or state changes.

The interface MUST NOT remove the browser focus indicator without providing a sufficiently visible alternative.

After a form submission, the frontend SHOULD place focus appropriately when needed to help the user understand the result or correct invalid input.

## 6. Forms and Monetary Inputs

Deposit and transfer forms MUST provide clear labels for every input.

For monetary inputs:

- The field label MUST identify the amount being entered.
- The currency MUST be clear to the user.
- Input instructions SHOULD explain the accepted decimal format.
- Required fields MUST be identified accessibly.
- Validation errors MUST identify the affected field.
- Invalid values MUST NOT be corrected silently through rounding.
- Keyboard users MUST be able to enter, edit, and submit valid values.

For example, a monetary field may communicate that the accepted currency is USD and that no more than two fractional decimal digits are allowed.

Placeholders MUST NOT be the only source of a field's accessible name or essential instructions.

See `docs/frontend/forms-and-validation.md` for monetary validation rules.

## 7. Validation and Error Messages

Validation messages MUST be understandable without relying on color or visual position alone.

The frontend SHOULD:

- Explain what is invalid.
- Describe how the user can correct the input when possible.
- Associate the error message with the relevant field.
- Indicate invalid fields programmatically.
- Preserve valid input when doing so is safe.
- Avoid moving focus unexpectedly while the user is editing a form.

For errors affecting the whole operation, such as insufficient funds, the frontend SHOULD provide a clearly identifiable message near the relevant form or result area.

Errors MUST NOT disclose sensitive internal details.

## 8. Dynamic Status Updates

Wallet interfaces frequently update without a full page reload. Important status changes must be communicated accessibly.

Examples include:

- A deposit or transfer is being submitted.
- A request succeeds.
- A request fails validation.
- A transfer is rejected because of insufficient funds.
- The authentication session expires.
- Transaction history is loading or has no results.
- The result of a financial operation is uncertain.

Appropriate live regions, status messages, or alert patterns SHOULD be used according to the urgency and nature of the update.

Routine updates SHOULD NOT interrupt screen-reader users unnecessarily. Important errors and security-related changes SHOULD be announced in a timely manner.

The frontend MUST distinguish a confirmed failure from an unknown outcome. It MUST NOT announce that a transfer failed merely because a network connection was lost if the backend may already have committed the operation.

## 9. Financial Operation Safety

Financial actions require clear, unambiguous interaction.

The frontend SHOULD:

- Label deposit and transfer actions explicitly.
- Display the recipient and amount before submission when appropriate.
- Distinguish submission-in-progress from confirmed success.
- Prevent accidental repeated activation while a request is in progress.
- Provide an understandable confirmation result.
- Preserve accessible status information during loading and recovery.
- Avoid relying on animation or color alone to communicate success or failure.

If confirmation dialogs are introduced, they MUST be keyboard accessible, have an appropriate accessible name, manage focus correctly, and return focus to a logical location when closed.

A disabled submission button MUST remain understandable to assistive-technology users. Any reason a user cannot proceed SHOULD be communicated clearly.

## 10. Balance and Transaction History

The wallet balance MUST have a meaningful accessible label or context.

Transaction history SHOULD communicate:

- Transaction type.
- Date and time.
- Signed amount from the current user's perspective.
- Relevant transaction reference or details where available.

Positive and negative amounts MUST NOT be distinguished by color alone. Text, signs, labels, or other non-color indicators SHOULD communicate whether a transaction increases or decreases the user's balance.

Tables SHOULD use appropriate header cells and associations. If a responsive layout replaces a table with cards, the resulting structure MUST preserve understandable relationships between labels and values.

Pagination controls MUST be keyboard accessible and expose their purpose clearly.

## 11. Color, Contrast, and Typography

The interface SHOULD provide sufficient contrast between text, controls, focus indicators, and their backgrounds according to WCAG 2.2 Level AA.

Requirements include:

- Normal text contrast ratio of at least 4.5:1.
- Large text contrast ratio of at least 3:1.
- Applicable non-text interface components and visual indicators meeting the relevant contrast requirements.
- No essential information conveyed by color alone.
- Text resizing without loss of content or functionality.
- Reflow at narrow viewport widths without unnecessary two-dimensional scrolling, except where the content inherently requires it.

Text and controls SHOULD remain readable under supported browser zoom settings.

## 12. Motion and Animation

Animations MUST NOT be necessary to understand an operation or navigate the interface.

The frontend SHOULD respect the user's reduced-motion preference where animations are used.

Motion SHOULD be limited, especially for loading indicators, dialogs, route transitions, and status changes.

Flashing content that could create a health risk MUST NOT be introduced.

## 13. Navigation and Focus Management

Navigation changes SHOULD produce predictable focus behavior.

The frontend SHOULD ensure that:

- Main content is identifiable after route changes.
- Focus does not remain inside removed interface elements.
- Dialogs and menus manage focus correctly.
- Error summaries, when present, help users reach invalid fields.
- Authentication redirects do not create confusing focus or navigation loops.

A skip link to the main content SHOULD be provided when repeated navigation elements make it useful.

## 14. Responsive and Touch Interaction

The interface SHOULD work across desktop and mobile viewport sizes.

Interactive controls SHOULD have sufficient size and spacing to reduce accidental activation. Applicable WCAG 2.2 target-size requirements and their exceptions must be considered.

Core wallet actions MUST remain available when the viewport is narrow or the interface is zoomed.

Responsive layouts MUST preserve meaningful reading order and must not rely on visual placement that conflicts with the underlying document order.

## 15. Testing Requirements

Accessibility testing SHOULD include:

### Automated checks

- Semantic structure and accessible-name checks.
- Detection of missing form labels.
- Color-contrast analysis.
- Detection of common accessibility violations.
- Automated checks integrated into frontend CI where practical.

### Manual checks

- Keyboard-only navigation.
- Focus visibility and focus order.
- Form completion and validation.
- Screen-reader interpretation of transaction history.
- Announcement of loading, success, failure, and uncertain outcomes.
- Responsive layouts and zoom.
- Authentication redirects and dialogs, if implemented.
- Deposit and transfer flows using assistive technologies.

Tests MUST include error states as well as successful interactions.

Automated accessibility tests SHOULD run as part of the normal frontend quality process, but failures that require human judgment must also be tracked.

## 16. Definition of Done

A frontend feature SHOULD NOT be considered complete until:

- All controls have appropriate accessible names.
- Keyboard interaction works.
- Focus remains visible and predictable.
- Validation and operation outcomes are communicated accessibly.
- Color is not the only status indicator.
- Layouts remain usable at supported viewport sizes.
- Relevant automated and manual accessibility checks have been performed.
- Known accessibility defects are documented and prioritized.

## 17. Open Decisions

The following decisions remain open:

1. Which frontend framework and component library will be selected?
2. Which automated accessibility testing tools will be used?
3. Which browser and assistive-technology combinations will be supported?
4. Will a dedicated accessibility review be required before release?
5. How will accessibility defects be prioritized and tracked?
6. What accessibility evidence will be required for the MVP release?

## 18. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/ui-components.md`
- `docs/frontend/forms-and-validation.md`
- `docs/frontend/routing.md`
- `docs/frontend/state-management.md`
- `docs/frontend/testing.md`
- `docs/api/error-model.md`
- `docs/project/open-questions.md`