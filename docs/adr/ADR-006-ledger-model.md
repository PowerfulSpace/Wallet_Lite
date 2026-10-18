# ADR-006: Wallet Balance and Append-Only Ledger

- **Status:** Accepted
- **Date:** 2026-10-18
- **Decision Owners:** Project Maintainers

## 1. Context

The Wallet API must provide efficient wallet balance queries while preserving a traceable history of deposits and transfers.

Two related requirements must be satisfied:

- The current balance must be available without recalculating the entire transaction history for every request.
- Financial changes must be represented by persistent records that allow operations to be inspected and balances to be reconciled.

Storing only the current balance makes it difficult to reconstruct how the balance changed over time.

Recalculating the balance exclusively from a complete event or ledger history on every request would add unnecessary complexity to the MVP.

The application therefore needs a model that combines efficient balance reads with a durable financial history.

## 2. Decision

Use a **hybrid wallet balance and append-only ledger model**.

The `Wallet` entity stores the current balance. The `LedgerEntry` entity records individual monetary movements.

The application also maintains business operation records:

- `Deposit` represents a virtual deposit.
- `Transfer` represents a transfer between two wallets.
- `LedgerEntry` records the balance movement associated with an operation.

The current balance and corresponding operation and ledger records must be updated atomically within the same database transaction.

## 3. Data Model

### 3.1 Wallet

A wallet contains the current balance for one user.

Relevant fields include:

- Wallet identifier.
- User identifier.
- Current balance.
- Creation timestamp.
- Relevant audit timestamps, if required.

Each user has exactly one wallet in the MVP.

Every newly created wallet starts at USD 0.00, recorded as `Balance = 0.00`. No opening ledger entry is created for the initial zero balance. This is an **accepted** rule (`DR-047`).

Any non-zero starting state — whether from a future opening-balance feature, a data migration, or an administrative adjustment — must be represented by an explicit, auditable ledger entry. Direct modification of the `Balance` column without a corresponding ledger entry is prohibited.

### 3.2 Deposit

A deposit represents a virtual top-up.

Relevant fields include:

- Deposit identifier.
- Wallet identifier.
- Amount.
- Creation timestamp.
- Any required idempotency or audit reference.

A successful deposit creates one deposit record and one corresponding positive ledger entry.

Deposits do not represent actual transfers from a bank account or payment provider.

### 3.3 Transfer

A transfer represents a movement of virtual funds between two users.

Relevant fields include:

- Transfer identifier.
- Sender wallet identifier.
- Recipient wallet identifier.
- Amount.
- Creation timestamp.
- Any required audit or idempotency reference.

A successful transfer creates one transfer record and two corresponding ledger entries: one debit and one credit.

### 3.4 Ledger Entry

A ledger entry records one monetary movement affecting a wallet.

Relevant fields include:

- Ledger entry identifier.
- Wallet identifier.
- Signed amount.
- Related operation type and reference.
- Creation timestamp.

Amounts use C# `decimal` and SQL Server `decimal(19,2)`.

A positive amount increases the wallet balance. A negative amount decreases it.

The ledger is append-only during normal application operation. Existing entries must not be silently updated or deleted to correct a balance.

The exact relational representation of operation references remains an implementation decision.

## 4. Ledger Rules

### 4.1 Deposit

For a successful deposit of amount \(A\):

- The wallet balance increases by \(A\).
- One deposit record is created.
- One ledger entry of \(+A\) is created.

### 4.2 Transfer

For a successful transfer of amount \(A\):

- The sender's wallet balance decreases by \(A\).
- The recipient's wallet balance increases by \(A\).
- One transfer record is created.
- One sender ledger entry of \(-A\) is created.
- One recipient ledger entry of \(+A\) is created.

The two ledger entries must have equal absolute values and opposite signs.

The sum of ledger entries associated with a successful transfer must equal zero.

### 4.3 Failed Operations

A failed deposit or transfer must not leave partial balance changes, operation records, or ledger entries.

The database transaction must ensure that the related financial changes commit together or roll back together.

## 5. Balance Reconciliation

The ledger provides a way to reconcile the stored wallet balance.

Under the proposed model, where wallets begin at zero and all balance changes are represented by ledger entries:

\[
\text{WalletBalance}
=
\sum \text{LedgerEntry.Amount}
\]

The sum is calculated for the relevant wallet across its ledger entries.

This invariant depends on all initial balances and subsequent balance changes being represented consistently. If the project introduces opening balances or a migration from an existing system, those amounts must also be represented by an explicit, auditable initialization process.

The implementation must not assume reconciliation is valid without accounting for the wallet's initial state.

Reconciliation checks may be implemented as integration tests and, where useful, operational diagnostics.

## 6. Atomicity and Consistency

A successful financial operation must update the current balance, business operation record, ledger entries, and successful idempotency result in one database transaction.

For transfers, both wallet changes and both ledger entries must commit together.

The database transaction is the primary mechanism for preventing partially persisted operations.

Additional constraints and concurrency-safe updates must protect applicable invariants, including:

- Non-negative wallet balances.
- Maximum wallet balance.
- Positive operation amounts.
- Correct operation-to-ledger relationships.
- Idempotency uniqueness.

The precise enforcement mechanism for each invariant must be defined in the data model and persistence implementation.

## 7. Ledger Immutability

Normal application operations must not update or delete existing ledger entries.

Corrections to financial history should use an explicit compensating operation when such functionality is introduced and approved.

A compensating operation records a new financial movement instead of rewriting the historical record.

The MVP does not require a general-purpose adjustment or reversal endpoint. Such capabilities remain outside the current scope.

Database administrators may still require exceptional maintenance procedures, but those procedures must not be confused with ordinary business operations and should be appropriately audited.

## 8. Query Strategy

The current wallet balance should be read directly from the `Wallet` record.

Transaction history should be read from ledger entries and their related operation data.

The history endpoint should:

- Return entries relevant to the authenticated user's wallet.
- Include deposits, incoming transfers, and outgoing transfers.
- Present amounts from the wallet owner's perspective.
- Order results newest first.
- Use cursor-based pagination based on a stable ordering such as `(CreatedAtUtc, Id)`.

The final response shape and query implementation are defined in the API documentation.

The MVP does not require reconstructing the entire wallet balance by replaying ledger entries for every balance request.

## 9. Why Not Store Only the Current Balance?

A balance-only model would simplify persistence, but it would not provide a complete record of monetary movements.

Without durable financial history, investigating discrepancies, reviewing transfers, and validating balance changes would be harder.

The append-only ledger provides a traceable record without requiring a full event-sourced architecture.

## 10. Why Not Use Full Event Sourcing?

Full event sourcing would make persisted events the primary source of truth from which current state is derived.

That approach introduces additional design requirements, such as event versioning, event replay, projection management, and event evolution.

Those capabilities are not necessary for the MVP's requirements.

The hybrid model provides an explicit ledger and efficient balance reads while keeping persistence and transaction processing relatively straightforward.

The stored wallet balance is a maintained current-state value. The ledger remains the historical record used for traceability and reconciliation.

## 11. Testing Requirements

Automated tests must verify:

- A successful deposit creates one deposit record and one positive ledger entry.
- A successful transfer creates one transfer record and two equal-and-opposite ledger entries.
- Transfer ledger entries sum to zero.
- Wallet balances reconcile with the ledger under the agreed initialization model.
- Failed operations leave no partial financial records.
- Concurrent operations preserve balance and ledger invariants.
- Ledger entries cannot be changed through ordinary application operations.
- Transaction history returns only entries belonging to the authenticated user's wallet.
- Pagination does not introduce duplicate or missing entries under the documented consistency expectations.

Tests involving transactions, constraints, and concurrent operations must run against SQL Server.

## 12. Consequences

### Positive Consequences

- Wallet balance queries are efficient.
- Financial movements remain traceable.
- Ledger entries support reconciliation and integrity checks.
- Transfers have an explicit representation of both sides of the movement.
- The design avoids the additional complexity of full event sourcing.

### Negative Consequences

- The application maintains both a current balance and a historical ledger.
- Every financial write must keep those representations consistent.
- Reconciliation checks and integration tests require careful implementation.
- The operation-to-ledger relationship must be designed to preserve referential integrity.

## 13. Open Decisions

1. Decide which ledger fields are required for audit and troubleshooting. (resolved in ADR-006 §3.4)
2. Define whether reconciliation runs only in tests or also through operational diagnostics. (resolved in OQ-030)
3. Determine the required transaction-history response fields. (resolved in OQ-018)
4. Establish how future compensating operations will be represented if reversal functionality is introduced. (unique to this document)

The remaining items are deferred beyond the MVP and tracked in `docs/project/known-issues.md` where they represent accepted deviations, or remain open in this section until resolved.

## 14. References

- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/requirements/domain-rules.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`

