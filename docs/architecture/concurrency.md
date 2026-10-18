# Concurrency Control

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `docs/architecture/concurrency.md`

## 1. Purpose

This document defines the concurrency requirements and proposed strategies for the Wallet API MVP.

The primary objective is to ensure that concurrent deposits, transfers, and retries cannot violate wallet balance limits, create duplicate financial effects, or leave financial records inconsistent.

The design targets SQL Server and assumes that multiple API requests may execute concurrently against the same wallet or idempotency key.

## 2. Core Invariants

Concurrency control must preserve the following invariants regardless of request timing:

- A wallet balance never becomes negative.
- A wallet balance never exceeds `$1,000,000.00`.
- A successful transfer debits the sender and credits the recipient by equal amounts.
- A successful transfer has exactly two corresponding ledger entries.
- A successful deposit has exactly one corresponding ledger entry.
- A failed operation leaves no partial financial effects.
- A successful financial operation and its idempotency result are committed atomically.
- Concurrent requests using the same idempotency scope cannot create duplicate financial operations.
- Every committed wallet balance change is consistent with the corresponding ledger entries.

Application-level validation alone is insufficient to guarantee these invariants under concurrency.

## 3. Concurrency Hazards

### 3.1 Lost Updates

A lost update occurs when two requests read the same wallet balance and independently calculate a new balance, causing one update to overwrite the other.

Example:

- Initial balance: `$100.00`.
- Request A intends to debit `$30.00`.
- Request B intends to debit `$40.00`.
- Both read `$100.00`.
- Both write independently calculated balances.

A naive read-then-update implementation can produce an incorrect final balance. The implementation must use an atomic update strategy or suitable locking and transaction isolation to prevent this outcome.

### 3.2 Overspending

Two concurrent transfers may both pass a preliminary sufficient-funds check.

Example:

- Initial balance: `$100.00`.
- Transfer A requests `$80.00`.
- Transfer B requests `$70.00`.

The system must not allow both transfers to succeed. At most one may debit the wallet successfully if both compete against that initial balance and neither receives additional funds during the race.

### 3.3 Balance Limit Violation

Concurrent deposits or incoming transfers may independently observe sufficient capacity and then both increase the same wallet balance beyond the configured maximum.

The recipient's maximum-balance check must therefore be enforced as part of a concurrency-safe write, not only as a preliminary read.

### 3.4 Duplicate Idempotency Requests

Two requests may arrive concurrently with the same user, operation type, and idempotency key before either request has stored its result.

Both requests must not independently execute the financial operation. A unique database constraint and deterministic conflict-handling logic are required.

### 3.5 Opposite-Direction Transfers and Deadlocks

Two transfers may lock the same wallets in opposite orders:

- Request A transfers from Wallet 1 to Wallet 2.
- Request B transfers from Wallet 2 to Wallet 1.

If each request locks its sender first, each may hold a lock required by the other. SQL Server may detect a deadlock and terminate one transaction.

The implementation should use a consistent lock-acquisition order where practical and handle deadlock victims safely.

## 4. Proposed Concurrency Strategy

The proposed strategy combines:

1. Atomic conditional balance updates.
2. Explicit database transactions for complete financial operations.
3. Unique database constraints for idempotency.
4. A consistent lock-acquisition order for transfers.
5. Bounded handling of retryable database concurrency failures.
6. Integration tests against actual SQL Server.

The exact EF Core APIs, SQL statements, and transaction isolation settings will be finalized during implementation and captured in the relevant ADRs.

## 5. Atomic Wallet Updates

### 5.1 Conditional Debit

A debit must be performed as a conditional database operation that succeeds only when the wallet has sufficient funds.

Conceptually:

```sql
UPDATE Wallets
SET Balance = Balance - @Amount,
    UpdatedAtUtc = @Now
WHERE Id = @WalletId
  AND Balance >= @Amount;
```

The application must verify the affected-row count.

- One affected row means the conditional update succeeded, subject to the enclosing transaction's outcome.
- Zero affected rows means the wallet was not updated; the application must determine whether the cause is insufficient funds, a missing wallet, or another applicable condition.
- Any unexpected outcome must fail safely.

The actual statement must also enforce the maximum-balance and monetary-range constraints where applicable.

### 5.2 Conditional Credit

A credit must prevent the resulting balance from exceeding the maximum.

Conceptually:

```sql
UPDATE Wallets
SET Balance = Balance + @Amount,
    UpdatedAtUtc = @Now
WHERE Id = @WalletId
  AND Balance <= @MaximumBalance - @Amount;
```

The application must verify the affected-row count. A failed conditional credit must cause the entire financial operation to roll back.

### 5.3 Why Conditional Updates Matter

A preliminary balance read can be useful for validation or user feedback, but it is not a sufficient concurrency guarantee.

The conditional update evaluates the relevant predicate as part of the database write. This avoids relying on a stale balance value observed earlier by the application.

Database constraints remain necessary as additional protection against invalid persisted values.

## 6. Transfer Transaction

A successful transfer changes two wallets and creates multiple related records. These changes must belong to one database transaction.

### 6.1 Required Atomic Unit

The transaction includes:

- Sender debit.
- Recipient credit.
- Transfer record.
- Sender ledger entry.
- Recipient ledger entry.
- Successful idempotency result.

No subset of these changes may commit independently as a successful transfer.

### 6.2 Proposed Processing Sequence

The following is a logical sequence; the implementation may adjust the order to support its transaction and idempotency design.

1. Validate the request format and normalize the operation input.
2. Resolve the authenticated sender and recipient.
3. Establish the idempotency scope and determine whether a matching completed operation already exists.
4. Begin or enter the transaction strategy chosen for the database operation.
5. Acquire wallet updates or locks in a consistent order where applicable.
6. Apply the conditional sender debit.
7. Apply the conditional recipient credit.
8. Insert the transfer record and its two ledger entries.
9. Persist the successful idempotency response.
10. Commit the transaction.
11. Return the committed result to the caller.

If any required step fails before commit, all changes made within the transaction must be rolled back.

The implementation must be careful not to report an insufficient-funds error when the actual cause is a missing wallet, a database failure, or another distinct condition.

### 6.3 Commit and Response Failure

A client may lose its connection after the database commits but before it receives the HTTP response.

In that case, the financial operation may have succeeded even though the client did not observe the response. A retry with the same idempotency key must retrieve the original successful result rather than apply the operation again.

The application must not attempt to undo a committed operation merely because response delivery failed.

## 7. Deposit Transaction

A deposit must use a transaction that atomically persists:

- The conditional wallet credit.
- The deposit record.
- The positive ledger entry.
- The successful idempotency result.

If the credit would exceed the wallet's maximum balance, the operation must fail without creating a successful deposit or ledger entry.

Concurrent deposits into the same wallet must be safe even when both requests initially observe the same balance.

## 8. Idempotency Concurrency

### 8.1 Unique Scope

The database must enforce uniqueness over:

`(UserId, OperationType, IdempotencyKey)`

This prevents concurrent requests from independently claiming the same logical idempotency scope.

### 8.2 Same Key, Same Request

When a completed successful record exists and its request fingerprint matches the incoming request, the API must replay the original successful response without applying any new financial changes.

### 8.3 Same Key, Different Request

When a record exists for the same scope but its request fingerprint differs, the API must return the documented conflict response.

The new request must not modify the original operation or its stored result.

### 8.4 Simultaneous First Requests

When two matching requests arrive before a result exists, the implementation must safely coordinate them.

The unique constraint is the final database safeguard. The application must handle the losing request deterministically by reading and evaluating the winning record when appropriate.

It must distinguish a duplicate-key race from unrelated database errors. It must not treat every integrity exception as a successful replay.

### 8.5 Failed Operations

A failed operation must not leave a permanent successful idempotency record for a financial effect that did not occur.

The implementation must define which outcomes are persisted and which permit a retry with the same key. Financial changes and successful idempotency results must remain atomic.

### 8.6 In-Progress Coordination

When a matching `InProgress` record exists, the waiting policy is:

- Poll every `CONST-014` (100 ms) up to `CONST-011` (3 s total).
- If the record completes, replay.
- If the bounded wait expires, return `503 IDEMPOTENCY_IN_PROGRESS`.
- If the lease `CONST-012` (30 s) has expired, a single request may claim recovery via a conditional update; all other requests continue to wait.

The unique constraint is the authoritative arbiter. Application-level `SELECT` before `INSERT` is not sufficient and must not be used as the primary mechanism.

## 9. Lock Ordering and Deadlock Handling

A transfer updates wallet rows in ascending `Wallet.Id` order (`Guid` ordinal comparison, `CONST-021`), regardless of the sender/recipient role. See `ADR-004 §5`.

The implementation must not assume that consistent ordering eliminates every possible deadlock. Other indexes, constraints, query plans, and concurrent operations may still produce deadlocks.

When SQL Server selects a transaction as a deadlock victim (`1205`), the transaction is rolled back and the operation may be retried up to `CONST-017` (3) times with exponential backoff `CONST-018` (100 ms base), `CONST-019` (×2), and jitter `CONST-020` (±20%). The retry re-reads state within a new transaction and preserves the original idempotency key.

Other transient errors are not retried in-process in the MVP. An uncertain commit outcome returns `503 IDEMPOTENCY_IN_PROGRESS` and is resolved by client retry with the same idempotency key, as described in `ADR-004 §8` and `ADR-005 §9`.

## 10. Isolation and Optimistic Concurrency

Financial write operations use SQL Server `READ COMMITTED` isolation (`CONST-016`) with explicit conditional updates and deterministic lock ordering. The isolation level is not the primary correctness mechanism; the conditional updates and transaction boundary are.

`SERIALIZABLE` and `SNAPSHOT` are not used in the MVP. See `ADR-004 §6` for rationale.

Conditional updates can protect individual balance predicates without requiring the application to rely on an earlier read. Operations that span multiple rows still require an atomic transaction.

Optimistic concurrency tokens may be useful for some updates, but they are not automatically a replacement for conditional debits, conditional credits, unique idempotency constraints, or transaction-level consistency.

The implementation must document:

- The isolation level used for financial writes.
- Whether explicit row or range locks are needed.
- Which database errors are retryable.
- How retry limits and cancellation are handled.
- How correctness is preserved if a commit outcome is uncertain.

These choices should be recorded in the concurrency-control ADR.

## 11. Failure Classification

Concurrency-related outcomes must be distinguished from unexpected infrastructure failures.

| Situation | Required behavior |
|---|---|
| Insufficient sender balance | Reject the transfer; no financial changes commit. |
| Recipient would exceed maximum balance | Reject the transfer; no financial changes commit. |
| Same idempotency key and same request | Replay the original successful result. |
| Same idempotency key and different request | Return a conflict; preserve the original result. |
| Duplicate-key race on idempotency record | Resolve using the persisted winning record when appropriate. |
| Deadlock victim | Roll back and apply only the documented bounded retry policy. |
| Missing wallet | Return the applicable documented resource or validation error. |
| Unexpected database failure | Fail safely, log diagnostic details, and avoid exposing internal database information. |
| Uncertain commit outcome | Do not assume rollback; use idempotent recovery or retry behavior. |

The final mapping from application outcomes to HTTP status codes is defined in `docs/api/error-model.md`.

## 12. Concurrency Test Scenarios

Concurrency correctness must be verified against an actual SQL Server instance using integration tests.

### 12.1 Competing Transfers

Given a sender balance of `$100.00`, issue two concurrent transfers of `$80.00` and `$70.00` to valid recipients.

Expected outcome:

- No more than one transfer succeeds.
- The sender balance never becomes negative.
- Failed operations leave no partial ledger or transfer records.
- All committed balances reconcile with their ledger entries.

### 12.2 Concurrent Credits at the Maximum

Given a wallet balance of `$999,950.00`, issue two concurrent credits of `$30.00`.

Expected outcome:

- No more than one credit succeeds.
- The balance never exceeds `$1,000,000.00`.
- The successful operation, if any, has matching operation and ledger records.

### 12.3 Concurrent Identical Requests

Submit the same deposit or transfer request concurrently using the same idempotency key.

Expected outcome:

- At most one financial operation is created.
- All successful replays refer to the same logical operation and original response.
- The balance and ledger reflect the operation only once.

### 12.4 Concurrent Key Reuse With Different Payloads

Submit concurrent requests with the same idempotency scope but different normalized request fingerprints.

Expected outcome:

- At most one request claims and executes that scope as a successful operation.
- The conflicting payload must not create a second financial effect.
- The persisted result remains internally consistent.

### 12.5 Opposite-Direction Transfers

Run transfers in both directions between two wallets concurrently.

Expected outcome:

- No partial transfers are committed.
- Both wallet balances remain valid.
- Deadlocks, if they occur, are handled according to the bounded retry policy.
- Ledger totals remain consistent.

### 12.6 Failure During a Transfer

Cause a controlled failure after one wallet update but before all required records are persisted.

Expected outcome:

- The database transaction rolls back the incomplete operation.
- Neither wallet retains a partial balance change.
- No successful transfer or incomplete ledger set remains committed.
- No successful idempotency response exists for the rolled-back financial effect.

### 12.7 Commit Followed by Lost Response

Simulate a client retry after the original operation has committed but its response was not received.

Expected outcome:

- The retry returns the original successful result.
- No second financial operation is created.

Tests must assert both HTTP/application outcomes and persisted database state.

## 13. Operational Considerations

The application should log relevant diagnostic context for concurrency failures, including operation type, correlation identifier, and classified database failure where safe.

Logs must not expose credentials, authentication tokens, or unnecessary sensitive data.

Monitoring may include:

- Deadlock frequency.
- Database timeout frequency.
- Retry counts.
- Idempotency conflicts.
- Transaction failures.
- Unexpected balance or ledger reconciliation discrepancies.

These signals are diagnostic aids and do not replace correctness constraints or integration tests.

## 14. Open Decisions

The following choices require explicit confirmation:

- Exact SQL Server isolation level for financial operations. (tracked in OQ-006)
- Whether to use explicit locking hints in selected queries. (unique to this document)
- Whether conditional updates alone are sufficient for each balance operation. (unique to this document)
- Exact transaction ownership and EF Core transaction APIs. (unique to this document)
- Which SQL Server error numbers are eligible for bounded retry. (tracked in OQ-007)
- Retry count, backoff, and request-duration limits. (tracked in OQ-007)
- Idempotency coordination and recovery after a unique-constraint race. (resolved in OQ-008)
- Whether any optimistic concurrency tokens are useful in addition to atomic updates. (unique to this document)

No implementation should assume that a preliminary balance read is sufficient to prevent overspending.

## 15. Deposit Transaction Flow

A deposit combines a pre-committed idempotency reservation with a single financial transaction:

1. Resolve the user by username.
2. Compute the request fingerprint.
3. Check the idempotency record for `(UserId, "deposit", IdempotencyKey)`.
4. Reserve the idempotency record (`INSERT`); the unique constraint arbitrates concurrent duplicates.
5. Begin transaction.
6. Conditional `UPDATE Wallets SET Balance = Balance + @Amount WHERE UserId = @UserId AND Balance + @Amount <= 1000000.00`.
   - `0` rows affected → `BALANCE_LIMIT_EXCEEDED` → rollback → `409`.
7. `INSERT Deposit`.
8. `INSERT LedgerEntry` (positive).
9. `UPDATE IdempotencyRecord` (result id, `201`, stored body).
10. `COMMIT`.

The reservation is committed before the transaction begins, so the database unique constraint—not an application-level read—arbitrates competing first requests. A `4xx` business rejection releases the reservation so the same key can be retried.

## 16. Transfer Transaction Flow

A transfer combines a pre-committed idempotency reservation with a single financial transaction that touches two wallets:

1. Resolve the sender (authenticated principal) and recipient by username.
2. Reject self-transfer (`409 SELF_TRANSFER`).
3. Compute the request fingerprint.
4. Check the idempotency record for `(UserId, "transfer", IdempotencyKey)`.
5. Reserve the idempotency record (`INSERT`); the unique constraint arbitrates concurrent duplicates.
6. Begin transaction (`READ COMMITTED`, `CONST-016`).
7. Conditional `UPDATE Wallets SET Balance = Balance - @Amount WHERE Id = @SenderWalletId AND Balance >= @Amount`.
   - `0` rows affected → `INSUFFICIENT_FUNDS` → rollback → `409`.
8. Conditional `UPDATE Wallets SET Balance = Balance + @Amount WHERE Id = @RecipientWalletId AND Balance + @Amount <= 1000000.00`.
   - `0` rows affected → `BALANCE_LIMIT_EXCEEDED` → rollback → `409`.
9. `INSERT Transfer`.
10. `INSERT LedgerEntry` (negative for the sender) and `INSERT LedgerEntry` (positive for the recipient).
11. `UPDATE IdempotencyRecord` (transfer id, `201`, stored body).
12. `COMMIT`.

The two wallet updates are issued in ascending `Wallet.Id` order (`CONST-021`), regardless of which wallet is the sender and which is the recipient. The debit and credit predicates are still evaluated per row inside the transaction; ordering only reduces avoidable deadlocks between simultaneous transfers in opposite directions. The transfer row, ledger entries, and idempotency completion happen after the wallet updates and are not part of the lock-order rule.

Because the reservation is committed before the transaction begins, the database unique constraint—not an application-level read—arbitrates competing first requests. A `4xx` business rejection releases the reservation so the same key can be retried.

A residual SQL Server deadlock victim (`1205`) is retried with the bounded policy in `ADR-004 §8` (`CONST-017`–`CONST-020`): the transaction is re-executed from a clean change tracker, the existing idempotency reservation is reused, and the state is re-read inside the new transaction.

## 17. Related Documents

- `docs/architecture/architecture.md`
- `docs/architecture/data-model.md`
- `docs/requirements/domain-rules.md`
- `docs/requirements/non-functional-requirements.md`
- `docs/api/error-model.md`
- `docs/development/testing.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/adr/ADR-004-concurrency-control.md`
- `docs/adr/ADR-005-idempotency.md`
