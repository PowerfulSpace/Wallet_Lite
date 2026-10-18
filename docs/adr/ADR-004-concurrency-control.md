# ADR-004: Concurrency Control

- **Status:** Accepted
- **Date:** 2026-10-18
- **Decision Owners:** Project Maintainers

## 1. Context

The Wallet API allows multiple requests to modify wallet balances concurrently.

Two or more requests may attempt to debit the same wallet, credit the same recipient, or execute transfers in opposite directions at the same time.

Without database-level concurrency control, operations may read the same initial balance and independently conclude that sufficient funds are available. This can lead to lost updates, overspending, or balances exceeding the configured limit.

Example:

A wallet has USD 100.00. Two concurrent requests attempt to transfer USD 80.00 and USD 70.00.

Both requests must not succeed. The wallet must never become negative, and a failed transfer must not debit the sender without crediting the recipient.

The database is SQL Server, and the application uses Entity Framework Core.

## 2. Decision

Use **SQL Server database-level concurrency control**, with conditional balance updates as the preferred mechanism for enforcing debit and credit invariants.

Application code must not rely on a read-then-update sequence without concurrency protection.

The implementation must ensure that:

- A wallet cannot be debited below zero.
- A wallet cannot exceed USD 1,000,000.00.
- Concurrent transfers cannot create or destroy value.
- Each transfer is atomic.
- Duplicate idempotency requests cannot create duplicate financial effects.
- Deadlocks and transient database failures are handled safely.

The exact SQL statements are finalized in `docs/architecture/concurrency.md §5`. The isolation level is `READ COMMITTED` (`CONST-016`, §6). The retry policy is defined in §8 (`CONST-017`–`CONST-020`). All three are validated by SQL Server integration tests, not assumed from documentation.

## 3. Conditional Balance Updates

### 3.1 Debit

A debit must be performed through an atomic database operation that checks the available balance as part of the update.

Illustrative SQL:

```sql
UPDATE dbo.Wallets
SET Balance = Balance - @Amount
WHERE Id = @WalletId
  AND Balance >= @Amount;
```

The application must verify the affected row count.

- One affected row means the debit was applied.
- Zero affected rows means the debit condition was not satisfied or the wallet was not found.

The application must distinguish these cases when required by the API contract, without introducing an unsafe separate read-and-update sequence.

This operation must execute within the transfer's database transaction.

### 3.2 Credit

A credit must atomically enforce the maximum wallet balance.

Illustrative SQL:

```sql
UPDATE dbo.Wallets
SET Balance = Balance + @Amount
WHERE Id = @WalletId
  AND Balance + @Amount <= @MaximumBalance;
```

The application must verify the affected row count and treat a rejected credit as a failed transfer.

The SQL expression, parameter types, and limits must be implemented with the correct decimal precision.

The credit and debit must be part of the same transaction. If either side fails, all financial changes for the transfer must roll back.

## 4. Transfer Transaction

A transfer must use one SQL Server transaction for all related financial writes.

The transaction includes:

- Sender debit.
- Recipient credit.
- Transfer record.
- Sender ledger entry.
- Recipient ledger entry.
- Successful idempotency result.

The application may perform preliminary reads to validate the request, but those reads do not replace the atomic conditions enforced during the actual balance updates.

The transfer must commit only after all required writes succeed.

If the transaction fails, no partial financial result may remain committed.

## 5. Locking and Lock Order

A transfer must update two wallet rows. To reduce avoidable deadlocks between simultaneous transfers in opposite directions, the implementation acquires or updates wallet rows in ascending `Wallet.Id` order (`Guid` ordinal comparison, `CONST-021`), regardless of which wallet is the sender and which is the recipient.

Concrete consequences:

- The sender debit and recipient credit are executed against wallet rows sorted by `Id`, not in "sender-first" order.
- The conditional `UPDATE` predicates (`Balance >= @Amount`, `Balance + @Amount <= @Max`) are still evaluated per row inside the transaction. Sorting the rows does not weaken the condition.
- `Transfer`, `LedgerEntry`, and idempotency inserts happen after the wallet updates. They are not part of the lock-order rule.

Deterministic ordering reduces but does not eliminate deadlocks. SQL Server can still select a transaction as a deadlock victim due to index or constraint interactions. The bounded retry in §8 handles that residual case.

The lock order must be verified by SQL Server integration tests using opposite-direction concurrent transfers.

## 6. Isolation Level

Financial write operations use SQL Server `READ COMMITTED` isolation (`CONST-016`) combined with:

- Atomic conditional balance updates (`WHERE Balance >= @Amount`, `WHERE Balance + @Amount <= @Max`).
- Explicit deterministic lock ordering for multi-wallet transfers (see §5).
- Stable primary-key lookups for wallet rows.

`SERIALIZABLE` is not used because it introduces range locks that materially increase deadlock rate on transfers without adding correctness beyond what conditional updates provide. `SNAPSHOT` is not used because it requires database-level `ALLOW_SNAPSHOT_ISOLATION` and changes `UPDATE` conflict semantics (`3960`), complicating retry behavior for the MVP.

`READ COMMITTED` is safe here because correctness does not depend on the isolation level alone. It depends on conditional updates that evaluate their predicate inside the write, and on the transaction boundary that contains all writes for one transfer.

This choice must be verified by SQL Server integration tests, not assumed from documentation.

## 7. Idempotency Under Concurrency

Concurrent requests may submit the same idempotency key.

The database must enforce the relevant uniqueness rule. Application-level checks alone are insufficient because two requests can pass the same preliminary check concurrently.

The implementation must ensure that:

- Only one execution produces the financial effect.
- A duplicate request with the same logical payload receives the original successful result when available.
- Reuse of the same key with a different payload is rejected.
- The idempotency record and successful financial changes commit atomically.
- A losing concurrent request does not perform a second transfer or deposit.

Unique constraint violations and transaction conflicts must be handled as expected concurrency outcomes where appropriate, rather than always being surfaced as internal server errors.

## 8. Deadlocks and Retries

SQL Server may detect deadlocks when concurrent transactions acquire overlapping locks in different orders.

The MVP treats **SQL error 1205 (deadlock victim)** as retryable. Other transient errors (connection loss, timeout, Azure transient errors) are not automatically retried in the MVP; they surface as `500` and rely on the client's idempotent retry (`ADR-005`).

The retry policy is bounded:

- Maximum attempts: `CONST-017` (3).
- Base delay: `CONST-018` (100 ms).
- Exponential multiplier: `CONST-019` (×2).
- Jitter: `CONST-020` (±20%).

A retry must:

- Preserve the original transaction scope and all financial writes.
- Not repeat external side effects (there are none in the MVP).
- Not create a second idempotency record.
- Not attempt a new idempotency key.
- Re-read the state inside the new transaction; it must not reuse stale in-memory values.

**Uncertain commit outcomes.** A connection failure or exception raised by `SaveChangesAsync` or `CommitAsync` after the transaction has begun does **not** prove that the transaction rolled back. The application must not:

- Retry the financial writes blindly.
- Return `500` claiming failure.
- Delete or complete the idempotency record as failed.

Instead, the operation returns `503 SERVICE_UNAVAILABLE` with code `IDEMPOTENCY_IN_PROGRESS`. The idempotency record remains `InProgress`. A client retry with the same key triggers the bounded wait and on-demand recovery defined in `ADR-005 §9`. Only after durable state confirms either rollback or commit does the record transition to `Completed` or permit a safe retry.

### Implementation Notes (Phase 5)

`TransferTransactionExecutor` implements the retry above:

- A retry is attempted only for SQL error `1205`, detected either as a direct `SqlException` or as a `DbUpdateException` whose inner exception is a `SqlException` with `Number == 1205`.
- At most `CONST-017` (3) retries follow the initial attempt; the delay is `CONST-018` (100 ms) × `CONST-019` (×2) per attempt with `CONST-020` (±20%) jitter.
- Before each retry the EF Core change tracker is cleared, and the idempotency record is re-read inside the new transaction, so no stale in-memory entity or duplicate `INSERT` is carried over. The transfer handler owns the reservation, so a retry never creates a second idempotency record or a new key.
- The uncertain-commit rule is not automated: a failure after the transaction begins that is not a `1205` deadlock is not retried, and the reservation remains `InProgress`.

### Implementation Notes (Phase 7)

The uncertain-commit rule is now automated.

- `DepositTransactionExecutor` and `TransferTransactionExecutor` raise `UncertainCommitException` (`src/Backend/PS.Wallet/PS.Wallet.Infrastructure/Persistence/UncertainCommitException.cs`) for any failure that occurs after the financial transaction has begun, except confirmed business failures (`ConflictException`, `ValidationAppException`, `DomainException`) and cancellation. This covers failures raised by `SaveChangesAsync` as well as `CommitAsync`: a confirmed rollback and an uncertain commit are indistinguishable to the client, and this section forbids reporting either as a plain `500`.
- `ExceptionHandlingMiddleware` maps `UncertainCommitException` to `503 SERVICE_UNAVAILABLE` with code `IDEMPOTENCY_IN_PROGRESS`.
- The command handlers do not catch it, so the reservation is never released on an uncertain outcome; it remains `InProgress` for the bounded wait and lease recovery in `ADR-005 §9`. The outcome is resolved by durable verification: only a persisted `Completed` idempotency record proves the commit, and an expired lease with no completed record permits the operation to be resumed and re-proved.
- Deadlock victims remain excluded from that wrap, so the bounded retry loop above is preserved. Exhausting `CONST-017` (3) retries now ends in `UncertainCommitException` instead of `500 Internal Server Error`.
- Verified against SQL Server by `IdempotencyRecoveryTests` (`UncertainCommit_Returns503AndLeavesRecordInProgress_ThenRecoversOnRetry`).

## 9. Error Semantics

Concurrency-related failures must be translated into documented application outcomes.

Examples include:

- Insufficient funds: business conflict.
- Recipient balance limit exceeded: business conflict.
- Same idempotency key with a different request: conflict.
- Exhausted transient retries: a safe service error or another documented outcome appropriate to the failure.

The API must not expose SQL statements, lock details, connection information, or database exception messages.

The final status codes and error codes are governed by `docs/api/error-model.md`.

## 10. Testing Requirements

Concurrency behavior must be tested against a real SQL Server instance.

### Required Scenarios

**Concurrent debits**

Given a wallet balance of USD 100.00, start transfers of USD 80.00 and USD 70.00 concurrently.

Expected result:
- At most one transfer succeeds.
- The wallet never has a negative balance.
- A failed transfer leaves no partial financial changes.

**Concurrent credits**

Given a recipient wallet close to the maximum balance, start multiple credits concurrently.

Expected result:
- No committed balance exceeds USD 1,000,000.00.
- Transfers rejected by the recipient balance limit do not debit senders.

**Opposite-direction transfers**

Execute transfers from wallet A to wallet B and from wallet B to wallet A concurrently.

Expected result:
- No partial transfers.
- No permanent deadlock.
- Any retry behavior remains bounded and safe.

**Duplicate idempotency requests**

Submit the same request and key concurrently.

Expected result:
- At most one financial effect.
- Equivalent successful responses according to the idempotency contract.

**Ledger consistency**

After concurrent operations complete, verify that every successful transfer has its required ledger entries and that each transfer's entries sum to zero.

Tests must use independent requests and suitable database contexts so that they exercise genuine database concurrency.

## 11. Consequences

### Positive Consequences

- Balance invariants are enforced at the database update boundary.
- Concurrent requests cannot rely on stale application-level balance reads.
- Financial writes remain atomic.
- Idempotency protects against duplicate effects.
- SQL Server integration tests validate real locking and transaction behavior.

### Negative Consequences

- Conditional updates and affected-row handling require explicit persistence logic.
- Transactions may encounter deadlocks or transient errors.
- Concurrency tests require more careful setup than ordinary unit tests.
- Database behavior may limit throughput when many requests target the same wallet.

Correctness takes priority over maximizing throughput for the MVP.

## 12. Alternatives Considered

### Read, Validate, Then Update

Rejected as the sole concurrency strategy because another transaction can modify the balance after validation and before the update.

### Application-Level Locks

Rejected as the primary protection because in-process locks do not coordinate requests across multiple application instances and do not replace database guarantees.

### Optimistic Concurrency Tokens

A row version or similar token may be useful for some update patterns, but it must be implemented with explicit conflict handling and retry rules. It is not a substitute for the required balance invariants.

### Serializable Isolation for Every Operation

Not selected as a default without evidence that it is necessary. It may increase locking and contention, and it does not eliminate the need to design transaction boundaries and error handling correctly.

## 13. Review Conditions

Revisit this decision if:

- The application introduces additional database systems.
- Wallet contention becomes a material performance bottleneck.
- The transaction model changes.
- Integration tests reveal an unacceptable deadlock rate.
- New operations introduce more complex balance invariants.

Any change must preserve the invariants in `docs/requirements/domain-rules.md`.

## 14. References

- `docs/architecture/concurrency.md`
- `docs/architecture/data-model.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/requirements/domain-rules.md`
- `docs/api/error-model.md`

