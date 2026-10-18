# ADR-003: Financial Transaction Atomicity

- **Status:** Accepted
- **Date:** 2026-10-18
- **Decision Owners:** Project Maintainers

## 1. Context

The Wallet API performs financial operations that modify multiple related database records.

A successful deposit may require:

- Updating the wallet balance.
- Creating a deposit record.
- Creating a ledger entry.
- Persisting the successful idempotency result.

A successful transfer may require:

- Debiting the sender's wallet.
- Crediting the recipient's wallet.
- Creating a transfer record.
- Creating two ledger entries.
- Persisting the successful idempotency result.

If only some of these changes are committed, the database may contain inconsistent financial state.

Examples of unacceptable outcomes include:

- A sender is debited but the recipient is not credited.
- A wallet balance changes without a corresponding ledger entry.
- A transfer record exists without its required ledger entries.
- A retry repeats a financial operation because its original successful result was not persisted.

The system therefore requires a reliable way to commit or roll back all database changes belonging to one financial operation.

## 2. Decision

Use SQL Server database transactions to ensure that each deposit or transfer is atomic.

**All database changes belonging to a single successful financial operation must commit together or roll back together.**

The application must not report a financial operation as successful until its required database changes have committed successfully.

The atomicity boundary includes:

- Wallet balance changes.
- Deposit or transfer records.
- All required ledger entries.
- The successful idempotency record and its replayable response data.

The precise implementation may use EF Core transaction APIs and appropriate SQL Server statements, provided the complete operation remains atomic.

## 3. Deposit Transaction

A successful deposit must execute as one atomic database operation.

Conceptually, the transaction performs the following steps:

1. Validate the request and authenticated user.
2. Resolve the user's wallet.
3. Ensure the idempotency key is not associated with a conflicting request.
4. Apply the deposit while enforcing the wallet balance limit.
5. Create the deposit record.
6. Create the corresponding positive ledger entry.
7. Persist the successful idempotency result.
8. Commit the transaction.

The operation may be implemented in a different internal order where required for correctness, but the database must never expose a partially committed result.

If any required step fails, the transaction must roll back its financial changes.

## 4. Transfer Transaction

A successful transfer must execute as one atomic database operation.

Conceptually, the transaction performs the following steps:

1. Validate the request and authenticated user.
2. Resolve the sender's wallet and recipient's wallet.
3. Ensure the idempotency key is not associated with a conflicting request.
4. Verify that the transfer can be performed under the applicable business rules.
5. Debit the sender's wallet using a concurrency-safe operation.
6. Credit the recipient's wallet while enforcing the maximum balance.
7. Create one transfer record.
8. Create the sender's negative ledger entry.
9. Create the recipient's positive ledger entry.
10. Persist the successful idempotency result.
11. Commit the transaction.

All financial writes must be committed together.

If the debit succeeds but the credit fails, the sender's debit must be rolled back.

If either ledger entry or the transfer record cannot be persisted, both wallet balance changes must be rolled back.

A transfer must never be represented as successful when only one side of the financial movement has been committed.

## 5. Transaction Ownership

The application must have a clear transaction boundary for each financial use case.

Transaction ownership should be explicit and must not be distributed across unrelated services in a way that makes atomicity difficult to reason about.

The implementation must avoid accidental partial commits caused by:

- Multiple independent database contexts.
- Separate transactions for related writes.
- Repository methods that commit unexpectedly.
- External side effects performed before the transaction outcome is known.

The selected transaction strategy must be compatible with EF Core and SQL Server.

## 6. Concurrency and Atomicity

Atomicity alone does not prevent lost updates or overspending.

Concurrent operations must use appropriate database-level concurrency controls, such as conditional updates and suitable locking.

For example, if a wallet contains USD 100.00 and two concurrent transfers attempt to debit USD 80.00 and USD 70.00, at most one may succeed.

The database transaction must preserve this outcome without allowing the wallet balance to become negative.

The implementation must also protect the recipient's balance limit against concurrent credits.

Concurrency-control details are documented separately in `docs/architecture/concurrency.md` and `docs/adr/ADR-004-concurrency-control.md`.

## 7. Idempotency and Atomicity

Successful idempotency state must be persisted atomically with the financial operation.

This prevents a situation in which a transfer commits but its successful response is lost from the idempotency store, allowing a retry to execute the transfer again.

Likewise, the application must not persist a successful idempotency result for an operation whose financial changes were rolled back.

Concurrent requests using the same idempotency key must be handled using an appropriate database uniqueness constraint and conflict-handling strategy.

A repeated request with the same key and the same logical payload must not create additional financial effects.

A repeated request with the same key but a different logical payload must be rejected according to the API's idempotency contract.

The handling of failed attempts must follow the documented retry policy. A failed financial operation must not permanently reserve a key in a way that prevents a valid retry unless an explicit rule states otherwise.

## 8. External Side Effects

The MVP does not integrate with banks, payment processors, message brokers, or external financial systems.

Financial transaction processing must therefore avoid introducing external side effects that cannot participate in the SQL Server transaction.

If future requirements introduce notifications, messaging, or external integrations, they must not be treated as automatically atomic with the database transaction.

Such capabilities may require an outbox pattern, retryable delivery, or other separately designed consistency mechanisms.

## 9. Failure Handling

The application must distinguish business failures from unexpected infrastructure failures.

Examples of business failures include:

- Insufficient funds.
- Recipient balance limit exceeded.
- Invalid transfer amount.
- Conflicting idempotency key reuse.
- Recipient or wallet not available under the applicable API rules.

Business failures must not leave partial financial changes.

Unexpected database errors must not result in a success response.

The API must return a safe, documented error response without exposing database internals.

The application must not retry an entire financial operation blindly when it cannot determine whether the previous attempt committed. The implementation must use transaction and idempotency mechanisms to establish the correct outcome.

## 10. Isolation and Locking

The database transaction must operate with an isolation and concurrency strategy appropriate for the invariants being protected.

The exact isolation level and locking approach remain implementation decisions to be finalized in the concurrency ADR.

The implementation must consider:

- Concurrent debits from the same wallet.
- Concurrent credits to the same wallet.
- Transfers in opposite directions.
- Concurrent requests with the same idempotency key.
- Unique constraint violations.
- Deadlocks and transaction retries.

Any retry mechanism must be bounded and must not create duplicate financial effects.

## 11. Testing Requirements

Integration tests against SQL Server must verify atomicity.

Required scenarios include:

- Successful deposit updates the balance and creates exactly one deposit record and one ledger entry.
- Successful transfer updates both balances and creates one transfer record and two ledger entries.
- A failed debit does not change either wallet.
- A recipient balance-limit violation rolls back the sender debit.
- A ledger persistence failure rolls back all associated financial writes.
- A transfer record persistence failure does not leave balance changes committed.
- A failed operation does not persist a successful idempotency result.
- A repeated successful request does not duplicate financial effects.
- Concurrent requests preserve all wallet and ledger invariants.

Where practical, tests should inject failures at relevant persistence steps or use controlled database constraints to verify rollback behavior.

Mocks alone are insufficient to prove SQL Server transaction semantics.

## 12. Consequences

### Positive Consequences

- Partial financial operations are prevented.
- Wallet balances and ledger entries remain consistent.
- Successful transfers represent complete movements of funds.
- Idempotency state remains aligned with committed financial effects.
- Recovery and debugging become easier because financial state has a clear transaction boundary.

### Negative Consequences

- Financial operations may hold locks while multiple records are written.
- Transactions may fail due to deadlocks, timeouts, or constraint violations.
- Transaction scope and retry behavior require careful implementation.
- Longer transactions can reduce throughput under contention.

The implementation should keep transactions as short as practical while preserving correctness.

## 13. Alternatives Considered

### Independent Commits

Rejected because independently committing balance changes, operation records, ledger entries, and idempotency results permits partial financial state.

### Application-Only Compensation

Rejected as the primary consistency mechanism because compensating after a partial commit cannot provide the same guarantees as an atomic database transaction.

Compensating transactions may be appropriate for future business corrections, but they do not replace atomic persistence for the initial operation.

### Distributed Transactions

Not required for the current architecture because the MVP uses a single SQL Server database and does not coordinate financial writes across independent services.

## 14. Review Conditions

Revisit this decision if:

- Financial writes must span multiple databases or services.
- External financial providers are introduced.
- Transaction duration becomes a material scalability concern.
- The persistence architecture changes.
- The application requires asynchronous financial workflows.

Any revised approach must explicitly preserve the required financial consistency guarantees.

## 15. References

- `docs/architecture/architecture.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-004-concurrency-control.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/requirements/domain-rules.md`

