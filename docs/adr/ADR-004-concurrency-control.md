# ADR-004: Concurrency Control

- **Status:** Proposed
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

The exact SQL statements, transaction isolation level, and retry policy must be finalized during implementation and validated by integration tests.

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

SQL Server manages locks for the selected isolation level and statements.

The implementation should keep transactions short and use a consistent resource-access order where practical.

For transfers involving two wallets, the implementation should acquire or update wallet resources in a deterministic order, such as ascending wallet ID, when this can be done without weakening the required conditional-update guarantees.

A consistent order reduces the likelihood of deadlocks between simultaneous transfers in opposite directions.

The final strategy must be tested against SQL Server rather than assumed to be correct from application-level logic alone.

## 6. Isolation Level

The initial implementation will use SQL Server's normal transactional behavior with explicit concurrency-safe balance updates.

The precise isolation level remains an implementation decision.

The selected level must be documented after the transaction and query strategy is implemented and tested. Stronger isolation should not be added without understanding its impact on locking, throughput, and deadlocks.

Correctness must not depend solely on increasing the isolation level if an atomic conditional update or database constraint is required to enforce a specific invariant.

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

The application must:

- Keep financial transactions as short as practical.
- Apply a consistent resource-access order where feasible.
- Recognize relevant deadlock and transient-failure conditions.
- Use bounded retries only when the operation can be safely retried.
- Avoid retry loops without limits.
- Preserve idempotency and transaction atomicity during retries.

A retry must not repeat an already committed financial effect.

If the commit outcome is uncertain, the application must establish the operation's result through a safe idempotency strategy rather than blindly rerunning the financial writes.

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

