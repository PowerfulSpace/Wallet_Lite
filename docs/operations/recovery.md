# Recovery and Failure Handling

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines recovery principles for the Wallet API MVP when application or infrastructure failures occur.

The primary objective is to preserve financial correctness and restore service safely without creating duplicate deposits, duplicate transfers, or inconsistent wallet balances.

This document covers:

- Application crashes and restarts.
- SQL Server outages and connectivity failures.
- Transaction rollback and uncertain commit outcomes.
- Safe retries using idempotency keys.
- Database recovery and integrity verification.
- Operational response to suspected data inconsistencies.

## 2. Recovery Principles

The application MUST follow these principles:

- Financial consistency takes priority over availability.
- A deposit or transfer MUST commit all required financial changes atomically or leave none of those changes committed.
- An operation MUST NOT be repeated blindly when its commit outcome is unknown.
- Clients SHOULD use the same idempotency key when retrying the same logical operation.
- Recovery procedures MUST NOT modify financial records arbitrarily to make the service appear healthy.
- The database transaction ledger and persisted operation records remain the authoritative sources for financial investigation.
- Recovery actions MUST be traceable and restricted to authorized operators.
- The application MUST NOT accept financial operations when it cannot safely establish or enforce the required database guarantees.

## 3. Application Failure

### 3.1 Failure Before a Transaction Begins

If the application fails before beginning a database transaction, no financial changes should have occurred.

A client may retry the request using the same idempotency key.

### 3.2 Failure During a Transaction

If the application process terminates while a financial transaction is in progress, SQL Server is responsible for transaction recovery.

An uncommitted transaction should be rolled back when the database determines that the transaction cannot complete.

The application MUST NOT rely on in-memory state to restore wallet balances or reconstruct the outcome of a transaction.

### 3.3 Failure After Commit

If the transaction commits but the application fails before sending the HTTP response, the financial operation may already have succeeded.

The client may therefore observe a timeout or connection failure even though the database has committed the operation.

The client MUST retry with the same idempotency key and the same logical request. The server MUST return the previously persisted result when the original operation is confirmed to have succeeded.

A retry MUST NOT create another deposit, transfer, or set of ledger entries.

## 4. Database Connectivity Failures

When SQL Server is unavailable, the application SHOULD fail affected requests safely and report a suitable error response.

The API MUST NOT report a successful financial operation unless the operation's successful commit has been established.

Database failures MUST NOT be converted into success responses or misleading business errors.

### 4.1 Before Commit

If the database transaction has not committed and is known to have failed, the operation should be rolled back.

A retry may be attempted according to the application's retry policy, provided that retrying is safe and the transaction can be restarted without duplicating side effects.

### 4.2 During Commit

A connection failure during commit may leave the application uncertain whether SQL Server committed the transaction.

The application MUST treat this as an uncertain outcome rather than assuming either success or failure.

The client should retry with the same idempotency key. The application must resolve the outcome using persisted database state and the idempotency protocol.

The MVP returns `503 SERVICE_UNAVAILABLE` with code `IDEMPOTENCY_IN_PROGRESS` from the original request and leaves the idempotency record `InProgress`. Recovery happens on the client's retry, through the bounded wait and on-demand lease recovery defined in `ADR-005 §9`.

It MUST NOT create a new logical operation with a new idempotency key merely to bypass uncertainty.

### 4.3 After Commit

If the commit succeeded but a subsequent database read or HTTP response fails, the application must preserve the committed financial result.

Any subsequent request must use the same idempotency key to retrieve the original outcome.

## 5. Retry Policy

Retries SHOULD be limited to failures that are transient and safe to retry.

Potential candidates include:

- Temporary database connectivity failures.
- Selected transient SQL Server errors.
- Deadlocks where the affected transaction is known to have been aborted.

Retries MUST NOT be used to bypass:

- Insufficient funds.
- Invalid monetary amounts.
- Recipient balance limits.
- Idempotency key conflicts.
- Authentication or authorization failures.

### 5.1 Retry Requirements

Any automatic retry of a financial database operation MUST:

1. Preserve the original logical operation and idempotency key.
2. Execute the financial changes within a correctly scoped transaction.
3. Avoid repeating external side effects.
4. Apply a bounded retry count and an appropriate delay where required.
5. Stop when the outcome cannot be safely determined.
6. Emit sufficient diagnostic information for investigation.

Retry counts, delay policy, and the list of transient SQL Server errors are implementation decisions.

The application MUST avoid unbounded retry loops that amplify an outage or exhaust database connections.

## 6. Idempotency and Recovery

Idempotency is a primary mechanism for safely handling repeated requests after failures.

The application MUST persist the successful idempotency result atomically with the financial changes it protects.

For a successful deposit, the wallet update, deposit record, ledger entry, and idempotency result MUST commit together.

For a successful transfer, the sender debit, recipient credit, transfer record, both ledger entries, and idempotency result MUST commit together.

If the transaction rolls back, none of these changes may remain committed as part of that failed attempt.

### 6.1 Recovery of Repeated Requests

When a client retries a request:

- If the original operation succeeded, return its stored result.
- If the same key is associated with a different request, return the documented idempotency conflict.
- If the previous attempt did not commit and no successful result exists, allow the request to be processed according to the idempotency protocol.
- If the previous attempt's outcome remains uncertain, resolve it safely without creating duplicate financial effects.

The exact persistence strategy for tracking in-progress requests and resolving uncertain commits must be defined during implementation.

## 7. Database Restart and Application Restart

Application instances should be stateless with respect to authoritative financial state. Persisted database records, rather than process memory, determine wallet balances and operation outcomes.

After restart, the application SHOULD:

1. Load and validate essential configuration.
2. Establish database connectivity as required by its startup and readiness policies.
3. Confirm that required database schema migrations have been applied.
4. Report readiness only when the required dependencies are available.
5. Resume serving requests without replaying financial operations from application memory.

Application startup MUST NOT automatically recreate missing financial records or reset existing wallet balances.

Database migrations MUST follow the rules in `docs/development/database-migrations.md`.

## 8. Data Integrity Verification

Following a suspected database incident, operators SHOULD verify relevant financial invariants before declaring the system fully recovered.

Verification SHOULD include:

- Wallet balances are not negative.
- Wallet balances do not exceed the configured maximum.
- Each successful deposit has its expected operation record and ledger entry.
- Each successful transfer has its expected operation record and corresponding debit and credit entries.
- Transfer debit and credit entries have equal absolute values.
- The transfer ledger entries sum to zero.
- Successful idempotency records correspond to their committed operations.
- No duplicate financial effects exist for a single logical idempotent request.

If the agreed reconciliation model is `Wallet.Balance = SUM(LedgerEntry.Amount)`, the verification process must account for initial balances and any explicitly approved balance adjustments.

Reconciliation results MUST be interpreted according to the documented ledger model. Operators MUST NOT assume that every mismatch can be repaired by directly changing a wallet balance.

## 9. Handling Suspected Inconsistency

If a financial invariant appears to be violated:

1. Record the incident and its scope.
2. Preserve relevant logs, trace identifiers, and database evidence.
3. Restrict affected operations if continued processing could worsen the inconsistency.
4. Investigate the persisted operation records, ledger entries, idempotency records, and transaction history.
5. Identify the root cause before making corrective changes.
6. Apply an authorized and documented correction procedure.
7. Verify the affected financial invariants after correction.
8. Document the outcome and any preventive measures.

The application MUST NOT silently delete ledger entries, rewrite historical transfers, or directly overwrite balances to hide inconsistencies.

Where a financial correction is required, a compensating transaction is preferred when compatible with the approved domain model. The exact correction workflow remains outside the initial MVP scope unless explicitly added.

## 10. Backup and Restore

Production database backup and restore procedures depend on the selected hosting and database deployment model.

Before production deployment, the operational plan MUST define:

- Backup frequency.
- Backup retention.
- Recovery Point Objective (RPO).
- Recovery Time Objective (RTO).
- Restore permissions and responsibilities.
- Backup protection and encryption.
- Restore verification procedures.

A backup is not considered a proven recovery mechanism until a restore procedure has been tested.

After restoring a database backup, operators MUST consider the consistency of all persisted wallet, operation, ledger, and idempotency data.

Clients may have received successful responses for operations that occurred after the restored backup's recovery point. Recovery procedures must account for this possibility and prevent unverified client retries from creating duplicate financial effects.

The precise recovery strategy depends on the database recovery model, available backups, and any transaction-log recovery capabilities.

## 11. Operational Safety

Recovery procedures MUST:

- Require appropriate authorization.
- Record significant operator actions.
- Protect secrets and financial data.
- Avoid destructive manual database changes.
- Define when financial operations should be temporarily disabled.
- Include a verification step before normal processing resumes.

Manual changes to wallet balances or ledger entries MUST NOT be used as routine troubleshooting measures.

If a manual database intervention is unavoidable, it requires an explicit procedure, a recorded reason, an approved execution method, and post-change integrity checks.

## 12. Testing Requirements

Recovery behavior SHOULD be tested using a real SQL Server environment.

Tests SHOULD cover:

- Application termination during an uncommitted transaction.
- Database disconnection before transaction completion.
- Deadlock handling and bounded retry behavior.
- A successful commit followed by a simulated response failure.
- Repeated requests with the same idempotency key.
- Concurrent duplicate requests during a transient failure.
- Application restart with persisted financial data.
- Database restoration and reconciliation procedures.
- Detection of deliberately introduced integrity violations in an isolated test database.

Tests MUST verify financial state, not just HTTP response codes.

For successful transfers, verification should include both wallet balances, the transfer record, both ledger entries, and the persisted idempotency result.

## 13. Reconciliation Procedure

Reconciliation compares the stored wallet balance with the wallet's ledger entries and reports every wallet where the two disagree. It is a read-only verification step; it never repairs data (`OQ-030` Decided).

### 13.1 Invariant

For every wallet, `Wallet.Balance` must equal the sum of that wallet's ledger entry amounts. Ledger amounts are signed: a deposit and a transfer credit are positive, a transfer debit is negative.

This invariant holds only under the approved initialization model (`OQ-001`): a new wallet starts at `0.00` and has no opening ledger entry. Any non-zero starting state or approved adjustment must be represented by an explicit ledger entry; otherwise reconciliation is not valid for that wallet (`ADR-006 §5`).

### 13.2 Checks

The following SQL queries verify the financial invariants after an incident or on a schedule. Each returns the rows that fail; an empty result set is a clean check.

1. **Wallet balances are non-negative and within the limit** (`CONST-001`).

```sql
SELECT Id, Balance
FROM Wallets
WHERE Balance < 0 OR Balance > 1000000.00;
```

2. **Ledger sum matches the wallet balance.**

```sql
SELECT w.Id, w.Balance, COALESCE(SUM(l.Amount), 0) AS LedgerSum
FROM Wallets AS w
LEFT JOIN LedgerEntries AS l ON l.WalletId = w.Id
GROUP BY w.Id, w.Balance
HAVING w.Balance <> COALESCE(SUM(l.Amount), 0);
```

3. **Every deposit has exactly one ledger entry.**

```sql
SELECT d.Id
FROM Deposits AS d
LEFT JOIN LedgerEntries AS l ON l.DepositId = d.Id
GROUP BY d.Id
HAVING COUNT(l.Id) <> 1;
```

4. **Every transfer has exactly two ledger entries with equal magnitude, opposite signs, and a net `0.00`** (`ADR-006 §4`).

```sql
SELECT t.Id
FROM Transfers AS t
LEFT JOIN LedgerEntries AS l ON l.TransferId = t.Id
GROUP BY t.Id, t.Amount
HAVING COUNT(l.Id) <> 2
   OR SUM(CASE WHEN l.Amount < 0 THEN -l.Amount ELSE l.Amount END) <> 2 * t.Amount
   OR SUM(l.Amount) <> 0;
```

5. **No duplicate ledger entries.**

```sql
SELECT DepositId, COUNT(*) FROM LedgerEntries WHERE DepositId IS NOT NULL GROUP BY DepositId HAVING COUNT(*) <> 1;
SELECT TransferId, COUNT(*) FROM LedgerEntries WHERE TransferId IS NOT NULL GROUP BY TransferId HAVING COUNT(*) <> 2;
```

6. **Idempotency coverage.** Every `Completed` idempotency record corresponds to a committed operation, and no logical request produced two financial effects (`ADR-005`). This check is performed by inspecting `IdempotencyRecords` against the operation tables rather than by a single query.

### 13.3 Reporting

Each run records:

- The UTC timestamp of the run (`CONST-026`) and the database it ran against.
- The number of wallets checked.
- Every failing row: wallet identifier, stored balance, ledger sum, delta, and the failing check.
- The offending identifiers for operation-level and ledger-level checks (operation identifiers, ledger entry identifiers).
- The exact query or commit used, so the result can be reproduced.

A run with no failing rows is reported as clean. A run with failing rows is an incident: it is recorded per §9, investigated to a root cause before any corrective change, and never resolved by updating `Wallets.Balance` or `LedgerEntries` directly (§9, §11).

Because balances and ledger entries are read while the application may still be processing requests, a mismatch reported for a wallet with an in-flight operation MUST be re-checked after the operation settles before it is treated as a real discrepancy. A reconciliation of a running system is a diagnostic, not a proof.

### 13.4 Cadence

Reconciliation is performed:

- After any suspected inconsistency (§9).
- Before declaring recovery from an incident complete (§8).
- After any authorized corrective action.
- Periodically, as an operational diagnostic, once operational tooling exists.

### 13.5 Implementation Status

No automated reconciliation endpoint exists in the MVP. These queries are run manually by an operator after an incident or as a scheduled operational check (post-MVP), and the same invariants are covered by SQL Server integration tests (`tests/PS.Wallet.IntegrationTests`). Automated reconciliation tooling, scheduling, and alerting depend on the hosting, RPO/RTO, and observability decisions (`OQ-027`, `OQ-028`, `OQ-029`).

## 14. Open Decisions

The following items are tracked. Their labels indicate status:

1. What retry limits and delay policy will be used for transient database failures? (resolved in CONST-017, CONST-018, CONST-019, CONST-020)
2. Which SQL Server errors will be treated as retryable? (resolved in ADR-004 §8 — deadlock victim 1205)
3. What are the target RPO and RTO? (tracked in OQ-028)
4. Which backup and restore strategy will be used in Production? (tracked in OQ-028)
5. What is the authorized process for compensating financial transactions? (unique to this document)
6. Under what conditions should financial endpoints be disabled during recovery? (unique to this document)

These decisions should be finalized before production deployment or before implementing the corresponding operational capabilities.

Resolved decisions related to this document:

- Uncertain transaction outcomes in the idempotency implementation (resolved in OQ-011, KI-004): `ADR-004 §8`, `ADR-005 §9`, and §4.2.
- Reconciliation execution and reporting (resolved in OQ-030): §13.

## 15. Related Documents

- `docs/operations/configuration.md`
- `docs/operations/observability.md`
- `docs/development/database-migrations.md`
- `docs/development/testing.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/adr/ADR-004-concurrency-control.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/adr/ADR-006-ledger-model.md`
- `docs/requirements/domain-rules.md`