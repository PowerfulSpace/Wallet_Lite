# Testing Strategy

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the testing strategy, test layers, required scenarios, and quality expectations for the Wallet API MVP.

Testing must verify both functional correctness and financial integrity. A successful build or passing unit tests alone does not demonstrate that database transactions, concurrency control, or idempotency work correctly.

## 2. Testing Principles

The test suite must:

- Verify observable behavior rather than implementation details wherever practical.
- Cover normal flows, invalid inputs, boundary conditions, and failure scenarios.
- Verify financial invariants after successful and failed operations.
- Use real SQL Server for database-specific correctness and concurrency tests.
- Remain deterministic and repeatable.
- Avoid dependence on test execution order.
- Keep unit tests fast and integration tests sufficiently isolated.
- Run automatically in continuous integration before changes are accepted.

Tests must not use production data or production credentials.

## 3. Test Layers

<text color="secondary" size="sm">Testing pyramid for the MVP</text>

<box border radius="lg" padding={3} gap={3}>
  <box align="center" background="surface-secondary" radius="md" padding={3} gap={1}>
    **Unit tests**
    <text size="sm" color="secondary">Domain rules, validation, calculations, error mapping</text>
  </box>
  <box align="center" background="surface" border radius="md" padding={3} gap={1}>
    **Application and API integration tests**
    <text size="sm" color="secondary">Use cases, HTTP contracts, authentication, authorization</text>
  </box>
  <box align="center" background="surface-secondary" radius="md" padding={3} gap={1}>
    **SQL Server integration tests**
    <text size="sm" color="secondary">Atomicity, constraints, idempotency, locking, concurrency</text>
  </box>
</box>

### 3.1 Unit Tests

Unit tests verify isolated domain and application behavior without requiring a database.

Use xUnit. NSubstitute may be used for dependencies where a substitute is appropriate.

Unit tests should cover:

- Positive amount validation.
- Monetary precision rules.
- Operation amount limits.
- Wallet balance limits.
- Self-transfer rejection.
- Insufficient-funds decisions.
- Domain invariants.
- Application-level error classification.
- Request fingerprint normalization, if implemented in the application layer.

Unit tests must not be treated as proof that SQL Server concurrency or transaction behavior is correct.

### 3.2 Application Integration Tests

These tests exercise multiple application components together, such as use cases, dependency injection, validation, and persistence boundaries.

They should verify:

- Correct orchestration of deposits and transfers.
- Mapping between application results and API responses.
- Idempotency behavior across the relevant application components.
- Expected handling of business conflicts.
- Cancellation and failure propagation where applicable.

Use a real SQL Server database for scenarios whose correctness depends on database behavior.

### 3.3 API Integration Tests

API integration tests send HTTP requests through the ASP.NET Core application pipeline.

They should verify:

- Routing and HTTP status codes.
- Request and response JSON contracts.
- Authentication and authorization.
- Validation and Problem Details responses.
- User isolation.
- Idempotency headers.
- Pagination behavior.
- Safe error responses.

Use controlled test identities and an isolated test database.

### 3.4 SQL Server Integration Tests

These tests use a real SQL Server instance compatible with the application's supported database version.

They are mandatory for verifying:

- Transaction atomicity.
- Unique constraints.
- Database migrations.
- Concurrent debits and credits.
- Idempotency under concurrent requests.
- Locking and isolation behavior.
- Rollback after failures.
- Ledger and wallet consistency.

EF Core InMemory is not an acceptable substitute for these scenarios. Its behavior does not reproduce SQL Server transaction, locking, or constraint semantics.

## 4. Financial Invariants

The following properties must hold after every completed test scenario.

### Wallet Balance

- Balance is never negative.
- Balance never exceeds `1,000,000.00`.
- All persisted amounts use the agreed decimal precision.
- Rejected operations do not change the wallet balance.

### Deposits

A successful deposit must create:

- One deposit operation record.
- One positive ledger entry.
- One corresponding wallet balance increase.
- One successful idempotency result for the logical request.

These changes must commit atomically.

A failed deposit must not leave a partial financial effect.

### Transfers

A successful transfer must create:

- One transfer operation record.
- One negative ledger entry for the sender.
- One positive ledger entry for the recipient.
- Equal absolute values for both ledger entries.
- Corresponding wallet balance updates.
- One successful idempotency result for the logical request.

The two ledger entries must sum to zero.

A failed transfer must not debit the sender, credit the recipient, or leave incomplete financial records.

### Ledger Consistency

Assuming the initial wallet balance is zero and every balance-changing operation is represented in the ledger, the expected reconciliation rule is:

`Wallet.Balance = SUM(LedgerEntry.Amount)`

This invariant must be revisited if the initial-balance model changes.

## 5. Required Test Scenarios

### 5.1 Wallet and Balance

| Scenario | Expected result |
|---|---|
| Authenticated user requests own wallet | `200 OK` with own balance |
| Unauthenticated user requests wallet | `401 Unauthorized` |
| User attempts to access another user's wallet | Access denied without data disclosure |
| Wallet balance is zero | Returned as `"0.00"` |
| Balance is at the maximum | Further credit is rejected if it would exceed the limit |

### 5.2 Deposit

| Scenario | Expected result |
|---|---|
| Valid deposit | `201 Created` |
| Zero amount | Rejected |
| Negative amount | Rejected |
| Amount with more than two fractional digits | Rejected without rounding |
| Amount exceeds operation limit | Rejected |
| Deposit exceeds maximum wallet balance | `409 Conflict`; no financial changes |
| Same key and same request repeated | Original successful result replayed |
| Same key with a different request | `409 Conflict` |
| Database failure during deposit | Full rollback |

### 5.3 Transfer

| Scenario | Expected result |
|---|---|
| Valid transfer with sufficient funds | `201 Created` |
| Recipient does not exist | Documented not-found or generic error response |
| Sender attempts to transfer to themselves | Rejected |
| Amount is zero or negative | Rejected |
| Sender has insufficient funds | `409 Conflict`; no financial changes |
| Recipient would exceed maximum balance | `409 Conflict`; no financial changes |
| Same key and same request repeated | Original successful result replayed |
| Same key with a different request | `409 Conflict` |
| Failure after sender debit but before full completion | Full rollback |
| Unauthorized transfer-detail request | `404 Not Found` when resource concealment applies |

### 5.4 Transaction History

Verify that:

- Deposits appear as positive entries.
- Incoming transfers appear as positive entries.
- Outgoing transfers appear as negative entries.
- Entries are ordered newest first.
- Pagination handles entries with identical timestamps.
- Invalid cursors are rejected.
- A user cannot access another user's history.
- `nextCursor` and `hasMore` accurately represent available results.

### 5.5 Authentication and Authorization

Verify that:

- Missing authentication is rejected.
- Invalid authentication is rejected.
- The sender is derived from the authenticated principal.
- Modifying client-supplied identity fields cannot change the effective sender.
- Users cannot read another user's balance or history.
- Only transfer participants can view transfer details.
- Development authentication is unavailable in Production.

## 6. Concurrency Testing

Concurrency tests must exercise overlapping requests against SQL Server.

### 6.1 Concurrent Debits

Initial condition:

- Alice's balance is `$100.00`.
- One request transfers `$80.00`.
- Another request transfers `$70.00`.
- Both requests execute concurrently.

Expected result:

- At most one transfer succeeds.
- Alice's balance never becomes negative.
- A rejected transfer leaves no partial records.
- The ledger and wallet balance remain consistent.

The test must synchronize or coordinate requests so that the operations genuinely overlap rather than relying on timing luck.

### 6.2 Concurrent Credits

Set up a recipient wallet near the maximum balance and issue concurrent deposits or incoming transfers that would individually appear valid based on a stale balance read.

Expected result:

- The maximum balance is never exceeded.
- Every rejected credit has no partial financial effect.
- All committed ledger entries match the committed balance changes.

### 6.3 Concurrent Idempotency Requests

Send multiple simultaneous requests with the same user, operation type, idempotency key, and payload.

Expected result:

- Only one financial operation is committed.
- Duplicate requests receive the original successful result according to the API contract.
- No duplicate ledger entries are created.

Repeat the test with the same key and different payloads.

Expected result:

- At most one conflicting request is accepted for that key scope.
- Requests with a different fingerprint receive `409 Conflict`.
- The original committed operation remains unchanged.

### 6.4 Concurrency Test Reliability

- Run tests against the supported SQL Server version.
- Use synchronization barriers or controlled coordination where appropriate.
- Assert final database state, not only HTTP status codes.
- Do not require a particular winner when the specification permits either operation to succeed.
- Repeat critical race-condition tests during development and CI where practical.

## 7. Atomicity and Failure Injection

Test failure at meaningful points within deposits and transfers.

Examples include:

- Failure while creating the operation record.
- Failure while writing a ledger entry.
- Failure while updating the recipient's wallet.
- Failure while persisting the successful idempotency result.

For every injected failure, verify that the database contains no partial financial effect.

The exact injection mechanism is implementation-specific. Tests must not depend on unreliable timing or production-only hooks.

## 8. Test Data and Isolation

Test data must be deterministic and isolated.

- Use dedicated test users and wallets.
- Never rely on a previous test having run.
- Reset or isolate database state between tests as appropriate.
- Avoid running conflicting tests against the same mutable wallet unless concurrency is the subject of the test.
- Keep test credentials separate from development and production credentials.
- Ensure tests cannot accidentally target a production database.

Seed data must be created through a repeatable test setup mechanism.

## 9. Migrations and Database Constraints

The test suite must verify that:

- A new database can be initialized using migrations.
- Migrations can be applied in sequence.
- Foreign keys reject invalid references.
- Unique constraints enforce required uniqueness.
- Monetary columns use the expected SQL types and precision.
- Ledger immutability policies are enforced to the extent supported by the chosen design.

Migration tests should validate the actual schema, not merely the presence of migration source files.

## 10. Test Naming and Organization

Organize tests by responsibility and behavior.

A possible naming convention is:

`MethodOrOperation_Scenario_ExpectedResult`

Examples:

- `DepositAsync_ZeroAmount_ReturnsValidationError`
- `TransferAsync_InsufficientFunds_DoesNotChangeBalances`
- `TransferAsync_ConcurrentDebits_NeverProducesNegativeBalance`
- `DepositAsync_RepeatedIdempotencyKey_DoesNotDuplicateDeposit`

Test project names and folder structure must follow the final repository layout.

## 11. Continuous Integration

The CI pipeline should run:

1. Restore dependencies.
2. Build the solution.
3. Run unit tests.
4. Start or connect to an isolated SQL Server test instance.
5. Apply migrations.
6. Run integration and API tests.
7. Publish test results.
8. Fail the pipeline when required checks fail.

The exact CI provider and database provisioning strategy are implementation decisions.

Tests must not be silently skipped because SQL Server is unavailable. If a required integration-test environment cannot be provisioned, the pipeline should fail clearly.

## 12. Quality Gates

Before a change affecting financial behavior is accepted:

- Relevant unit tests pass.
- Relevant API integration tests pass.
- Relevant SQL Server integration tests pass.
- Financial invariants are verified.
- New behavior and error cases are documented.
- Changes to schema or architecture have corresponding documentation updates.

Code coverage may be tracked as a quality indicator, but a high coverage percentage alone does not establish financial correctness.

## 13. Open Decisions

1. Define the test-project structure. (unique to this document)
2. Choose the standard SQL Server test provisioning mechanism. (tracked in OQ-026)
3. Define database reset and isolation strategies. (tracked in OQ-026)
4. Decide how failure injection will be implemented. (unique to this document)
5. Define the CI provider and required quality gates. (tracked in OQ-025)
6. Establish baseline coverage expectations without treating coverage as a substitute for scenario-based testing. (unique to this document)
