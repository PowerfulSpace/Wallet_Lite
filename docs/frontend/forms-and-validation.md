# Frontend Forms and Validation

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Scope:** Out of MVP; future phase.

## 1. Purpose

This document defines form behavior and validation rules for the Wallet API frontend.

It covers monetary input, recipient identification, validation feedback, submission state, and recovery from failed or uncertain requests.

The frontend MAY validate input early to improve usability. The backend remains authoritative and MUST independently validate every request.

## 2. General Principles

All forms SHOULD follow these principles:

- Validate user input before submission where practical.
- Provide clear, actionable validation messages.
- Associate field-level errors with their corresponding inputs.
- Preserve user input after recoverable errors when safe.
- Prevent accidental duplicate submissions.
- Never silently modify a financial amount.
- Distinguish client-side validation errors from server-side business errors.
- Represent uncertain financial-operation outcomes explicitly.
- Ensure all form controls are keyboard accessible.

Client-side validation MUST NOT be treated as a security or financial-integrity boundary.

## 3. Monetary Input Rules

The wallet uses USD with two fractional decimal digits.

The frontend MUST enforce the following input rules before submitting a deposit or transfer:

- The amount must be present.
- The amount must be a valid positive monetary value.
- The decimal separator in the API representation is a period.
- No more than two fractional decimal digits are allowed.
- The amount must not exceed $1,000,000.00.
- The input MUST NOT be silently rounded or truncated.

The backend MUST enforce these rules independently.

### 3.1 Accepted Examples

The following are valid monetary representations when the value is within the applicable limits:

```text
1
1.0
1.00
25.50
1000000.00
```

The frontend MAY normalize a valid user-entered representation into the API's agreed canonical format, provided that normalization does not change the monetary value.

For example, `1` may be serialized as `"1.00"` if the input parser confirms that the value is valid.

### 3.2 Rejected Examples

```text
0
-1.00
1.001
1000000.01
abc
```

These examples must be rejected because they are respectively non-positive, have excess precision, exceed the operation limit, or are not valid numeric input.

The implementation MUST also reject empty input and any representation that cannot be parsed unambiguously under the chosen input rules.

### 3.3 Decimal Precision

The frontend MUST NOT use binary floating-point arithmetic as the authoritative means of parsing, validating, or transforming monetary values.

A monetary string SHOULD be validated using a decimal-safe parser or explicit string-based validation.

The API request MUST preserve the intended decimal value.

## 4. Deposit Form

The deposit form SHOULD include:

- An amount input.
- Field-level validation feedback.
- A submit button.
- A submission progress indicator.
- A result message.

### 4.1 Validation

Before submission, the frontend SHOULD verify that:

1. The amount is present.
2. The amount is a valid positive decimal.
3. The amount has at most two fractional decimal digits.
4. The amount does not exceed $1,000,000.00.

A successful client-side validation does not guarantee that the deposit will succeed. The backend may reject the request because of other business rules or a service failure.

### 4.2 Submission

When the user submits a valid deposit:

1. The frontend creates an idempotency key for the new logical operation.
2. The request is sent to the deposit endpoint.
3. The submit action is guarded against accidental duplicate activation.
4. The frontend waits for the API result.
5. The interface displays a confirmed success or an appropriate failure state.

The frontend MUST NOT display a successful deposit before receiving confirmation from the API.

## 5. Transfer Form

The transfer form SHOULD include:

- A recipient username input.
- An amount input.
- Field-level validation feedback.
- A submit button.
- A submission progress indicator.
- An operation result message.

### 5.1 Recipient Validation

The frontend SHOULD verify that the recipient username is present and conforms to any documented input-format constraints.

The frontend MUST NOT assume that a syntactically valid username identifies an existing recipient.

Recipient existence, account eligibility, and applicable access rules MUST be checked by the backend.

The canonicalization and case-sensitivity rules for usernames remain open decisions and MUST be aligned with the backend contract.

### 5.2 Amount Validation

Transfer amounts follow the same monetary precision and positivity rules as deposit amounts.

The frontend MAY provide early feedback for a maximum operation amount of $1,000,000.00.

It MUST NOT assume that the user's current balance is sufficient based solely on a previously retrieved balance.

The backend MUST validate the available balance and recipient balance limit when processing the transfer.

### 5.3 Submission

When the user submits a valid transfer:

1. The frontend creates an idempotency key for the new logical operation.
2. The recipient username and amount are associated with that key.
3. The request is sent to the transfer endpoint.
4. Duplicate activation is guarded against.
5. The frontend displays the result returned by the API.

The sender identity MUST be determined by the backend from the authenticated principal. The frontend MUST NOT accept a user-controlled sender identifier as authoritative input.

## 6. Validation Timing

The frontend SHOULD balance immediate feedback with usability.

Recommended behavior:

- Validate required fields when the user attempts submission.
- Use appropriate input constraints where supported.
- Validate monetary syntax as the user types without making normal editing unnecessarily difficult.
- Avoid showing a field error before the user has had a reasonable opportunity to provide input.
- Revalidate a field after its value changes.
- Display server-returned validation errors when the request is rejected.

The exact validation timing may vary by field and framework.

## 7. Field-Level and Form-Level Errors

Field-level errors SHOULD appear next to the relevant input.

Examples include:

- Amount is required.
- Enter a valid monetary amount.
- Amount must have no more than two fractional decimal digits.
- Amount exceeds the permitted operation limit.
- Recipient username is required.

Form-level errors SHOULD be used when the issue cannot be attributed to one field.

Examples include:

- Insufficient funds.
- Recipient balance limit exceeded.
- Idempotency key conflicts with a previously submitted request.
- The operation outcome could not be confirmed.

The UI MUST distinguish an invalid form from a valid form whose operation was rejected by the backend.

## 8. Server-Side Validation Errors

The frontend SHOULD map API validation errors to form fields when the API provides sufficient field information.

The mapping MUST be defensive because server responses may contain unexpected fields or messages.

If an error cannot be mapped to a known field, the frontend SHOULD display a form-level message.

The frontend MUST NOT expose raw stack traces, SQL errors, internal exception details, or sensitive diagnostic information.

The exact error schema is defined in `docs/api/error-model.md`.

## 9. Submission State

Deposit and transfer forms SHOULD represent their submission lifecycle explicitly.

Suggested states:

- `idle`: no request is in progress.
- `submitting`: a request has been sent and is awaiting a result.
- `succeeded`: the API confirmed success.
- `failed`: a definitive failure was returned.
- `outcomeUnknown`: the request outcome cannot be determined.

The implementation MAY use different internal names, but it MUST preserve the distinction between a confirmed failure and an uncertain outcome.

### 9.1 Disabled Controls

While a request is in progress, the submit button SHOULD be disabled or guarded against repeated activation.

Disabling the button is a usability safeguard, not a substitute for idempotency.

The frontend MUST NOT rely on button state to prevent duplicate financial operations across refreshes, multiple tabs, or network retries.

## 10. Idempotency and Retries

Every deposit and transfer request MUST include an `Idempotency-Key` header.

The frontend MUST associate the key with the logical operation and its request payload.

If the request times out or the connection fails:

- The frontend MUST NOT automatically create a new key for the same logical operation.
- The original key and payload association SHOULD be preserved.
- The interface SHOULD explain that the result could not be confirmed.
- A retry, when offered, MUST reuse the original key and payload.

If the user changes the amount or recipient after an uncertain submission, the modified form MUST NOT be silently submitted under the original key.

The user should be guided through resolving or deliberately abandoning the previous workflow before creating a distinct logical operation, subject to the documented recovery design.

## 11. Confirmation and Success Messages

Success messages MUST be based on confirmed API responses.

For a confirmed deposit or transfer, the UI SHOULD display the relevant operation result and amount when those values are available in the response.

The interface MAY request refreshed wallet data after success.

A failure to refresh the balance or transaction history MUST NOT overwrite a confirmed operation result with a message claiming that the financial operation failed.

Confirmation dialogs MAY be used for transfers if user testing or product requirements justify the additional step. They are not required solely to implement backend safety guarantees.

## 12. Accessibility Requirements

All forms SHOULD provide:

- Programmatic labels for inputs.
- Clear instructions for monetary formats.
- Field errors associated with the relevant controls.
- Visible keyboard focus.
- Keyboard-accessible submission.
- Appropriate announcements for validation and operation results.
- Sufficient contrast for text and error indicators.
- Error identification that does not rely on color alone.

The focus SHOULD move to an appropriate location when submission reveals validation errors, provided that doing so does not disrupt the user's workflow.

## 13. Testing Requirements

Form tests SHOULD cover:

- Empty and missing values.
- Zero and negative amounts.
- Amounts with valid fractional precision.
- Amounts with more than two fractional decimal digits.
- Amounts exceeding the operation limit.
- Invalid numeric representations.
- Missing recipient username.
- Server-returned field errors.
- Business-rule rejection.
- Duplicate-submit prevention.
- Idempotency-key reuse on retry.
- Uncertain operation outcomes.
- Preservation of confirmed success when a subsequent data refresh fails.
- Keyboard accessibility and error announcements.

Backend tests MUST independently verify all financial validation and consistency rules.

## 14. Open Decisions

The following items are tracked. Their labels indicate status:

1. What exact monetary input syntax will be accepted in the UI?
2. Will the input accept locale-specific decimal separators?
3. What are the username format and normalization rules?
4. Will transfer confirmation be required?
5. How will uncertain operations be recovered after a browser refresh?
6. Will form values be preserved when navigating away from a page?
7. Which API validation codes will map to each field?
8. What exact canonical monetary string format will the API require?

## 15. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/state-management.md`
- `docs/frontend/api-integration.md`
- `docs/frontend/ui-components.md`
- `docs/frontend/accessibility.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/requirements/domain-rules.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/project/open-questions.md`