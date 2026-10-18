# ADR-002: Money Representation and Precision

- **Status:** Accepted
- **Date:** 2026-10-18
- **Decision Owners:** Project Maintainers

## 1. Context

The Wallet API processes virtual USD deposits and transfers. Monetary calculations must remain exact and consistent across HTTP requests, application logic, domain rules, and database persistence.

Floating-point types such as `float` and `double` cannot represent all decimal fractions exactly. Using them for financial calculations can introduce precision errors that affect balances, transaction amounts, and ledger reconciliation.

The application also needs a consistent policy for decimal scale, amount validation, maximum limits, and serialization.

## 2. Decision

Use decimal arithmetic throughout the application and SQL Server's fixed-precision decimal type for persistent monetary values.

The following conventions apply:

| Layer | Representation |
|---|---|
| API request and response | JSON string using a decimal point |
| C# domain and application logic | `decimal` |
| Entity Framework Core mapping | Explicit precision and scale |
| SQL Server | `decimal(19,2)` |

The currency for the MVP is USD only. Multi-currency support is outside the current scope.

## 3. Monetary Precision

All monetary values must have no more than two fractional decimal digits.

Examples of valid amounts:

- `"1.00"`
- `"25.50"`
- `"999999.99"`

Examples of invalid amounts:

- `"1.001"`
- `"25.505"`
- `"0.001"`

The application must reject values with more than two fractional digits. It must not silently round or truncate them.

Trailing zeros do not change the monetary value. For example, `"10.5"` and `"10.50"` represent the same amount, assuming both conform to the API's accepted decimal-string syntax.

The API contract must define the exact accepted syntax and reject ambiguous formats.

## 4. C# Representation

Use `decimal` for monetary values.

Example:

```csharp
public sealed record DepositRequest(string Amount);
```

The HTTP boundary should parse the amount using an explicitly defined decimal format and culture-independent rules before passing a validated value into application logic.

Example domain-level representation:

```csharp
public readonly record struct Money(decimal Amount);
```

This type is illustrative. Whether to introduce a dedicated `Money` value object or use validated `decimal` values directly remains an implementation decision.

The application must not use binary floating-point types for:

- Wallet balances.
- Deposit amounts.
- Transfer amounts.
- Ledger entry amounts.
- Monetary limits.
- Financial calculations.

## 5. SQL Server Representation

All persisted monetary columns must use:

```sql
decimal(19,2)
```

This includes, as applicable:

- `Wallet.Balance`
- `Deposit.Amount`
- `Transfer.Amount`
- `LedgerEntry.Amount`

The corresponding EF Core mappings must explicitly configure precision and scale.

Example:

```csharp
builder.Property(x => x.Balance)
    .HasPrecision(19, 2);
```

The database schema must not rely on provider defaults for monetary precision.

SQL Server's `decimal(19,2)` supports values with up to 17 integer digits and two fractional digits. The Wallet API's business limits are intentionally much lower than the database type's representational limit.

The choice of precision 19 rather than 18 provides a small safety margin for integer digits beyond the MVP's business limit of USD 1,000,000.00. While the MVP does not require that margin, adopting `decimal(19,2)` now avoids a future schema migration if the operation or balance limits are raised, or if additional precision is required for reporting or reconciliation. A wider type such as `decimal(38,2)` was not selected because it exceeds SQL Server's efficient storage and arithmetic paths for typical OLTP workloads without a concrete requirement.

## 6. Amount Validation

Every deposit and transfer amount must satisfy all of the following conditions:

1. The value is syntactically valid.
2. The value is representable as a decimal.
3. The value has no more than two fractional decimal digits.
4. The value is greater than zero.
5. The value does not exceed the maximum operation amount of USD 1,000,000.00.

Validation must occur before any financial side effects.

The domain or application layer must enforce business limits even if equivalent input validation exists at the HTTP boundary.

The database schema should provide appropriate additional protection where practical.

## 7. Balance Limits

The maximum wallet balance is USD 1,000,000.00.

A deposit must be rejected if it would cause the resulting wallet balance to exceed this limit.

A transfer recipient must not receive funds if the resulting balance would exceed the limit.

A transfer must fail entirely if any required balance update cannot be completed. The sender must not be charged when the recipient's credit cannot be committed.

Balance limits must be checked as part of a concurrency-safe database operation or transaction. A separate preliminary read is not sufficient protection against concurrent requests.

## 8. Arithmetic and Rounding

The application must not introduce implicit rounding into deposit or transfer processing.

For the MVP:

- Reject excess fractional precision at the input boundary.
- Preserve the accepted monetary value through the application.
- Persist amounts using the configured fixed precision.
- Use the same exact amount for the transfer record and its corresponding ledger entries.
- Ensure the sender debit and recipient credit have equal absolute values.

If rounding becomes necessary for a future feature, its mode, scale, and business justification must be explicitly documented and tested.

## 9. API Serialization

Monetary amounts are represented as JSON strings using a decimal point.

Example request:

```json
{
  "amount": "25.50"
}
```

Example transfer request:

```json
{
  "recipientUsername": "bob",
  "amount": "25.00"
}
```

This representation avoids accidental conversion to binary floating-point numbers by clients or intermediate tooling.

The API must document whether responses use a canonical two-decimal format. The recommended convention is to serialize monetary values with exactly two fractional digits, such as `"25.50"`.

The API must use a culture-independent format. Locale-specific decimal separators and thousands separators must not be accepted unless explicitly defined by a future contract.

## 10. Ledger Consistency

The ledger must preserve exact monetary values.

For a successful transfer of amount \(A\):

- The sender receives a ledger entry of \(-A\).
- The recipient receives a ledger entry of \(+A\).
- The sum of the transfer's ledger entries equals zero.

For a successful deposit of amount \(A\), one positive ledger entry of \(+A\) is recorded.

Wallet balance changes, operation records, ledger entries, and successful idempotency results must commit atomically.

## 11. Testing Requirements

Automated tests must cover:

- Valid whole-number and fractional amounts.
- Values with one and two fractional digits.
- Values with more than two fractional digits.
- Zero and negative values.
- Values exceeding the maximum operation amount.
- Deposits that would exceed the wallet balance limit.
- Transfers that would exceed the recipient balance limit.
- Decimal serialization and parsing using invariant rules.
- Equality between transfer amounts and ledger entry magnitudes.
- Exact ledger reconciliation.
- Concurrent operations near balance limits.

Tests must verify that invalid monetary input does not cause partial financial updates.

## 12. Consequences

### Positive Consequences

- Monetary calculations use decimal arithmetic.
- API clients have an explicit representation for amounts.
- Database precision is predictable.
- Implicit rounding is avoided.
- Financial invariants can be tested consistently.

### Negative Consequences

- API clients must handle monetary values as decimal strings.
- Parsing and validation require explicit rules.
- Precision and scale must be configured and maintained in database mappings.
- Future multi-currency support would require additional domain concepts and currency-specific rules.

## 13. Alternatives Considered

### `double` or `float`

Rejected because binary floating-point arithmetic is unsuitable for exact monetary representation.

### Integer Minor Units

Storing amounts as integer cents can provide exact arithmetic for a single currency. It was not selected for the current design because the project standardizes on C# `decimal` and SQL Server `decimal(19,2)`.

### `decimal(18,2)`

A narrower alternative with 16 integer digits and two fractional digits. It was not selected because it leaves no margin above the MVP's business limit and would require a schema migration if the limit is raised. The functional difference for the current requirements is negligible; the decision is driven by forward compatibility rather than present need.

### Automatic Rounding

Rejected because silently changing a requested amount would make the executed financial operation differ from the submitted value.

## 14. Review Conditions

Revisit this decision if:

- The application introduces currencies with different fractional scales.
- A financial feature requires explicit rounding.
- The database precision must change.
- Regulatory or integration requirements impose a different monetary representation.

## 15. References

- `docs/requirements/domain-rules.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/api/api-guidelines.md`
- `docs/api/endpoints.md`

