# ADR-005: Idempotency for Financial Operations

- **Status:** Accepted
- **Date:** 2026-10-18
- **Decision Owners:** Project Maintainers

## 1. Context

Clients may retry a deposit or transfer when a response is delayed, a connection fails, or the outcome of a request is uncertain.

Without idempotency, retrying a successful operation may create duplicate financial effects. A client could unintentionally submit the same deposit twice or transfer the same amount more than once.

The Wallet API must therefore recognize retries of the same logical request and prevent duplicate execution while preserving the original result.

Idempotency must work across concurrent requests and application instances. An in-memory cache alone cannot provide the required guarantees.

## 2. Decision

Require an `Idempotency-Key` header for the following endpoints:

- `POST /api/v1/wallet/deposits`
- `POST /api/v1/transfers`

The key identifies one logical operation within the applicable user and operation scope.

The database must enforce uniqueness for the chosen idempotency scope. Successful idempotency state must be persisted atomically with the corresponding financial changes.

The implementation must support these outcomes:

1. A new key may execute a new operation.
2. A previously successful key with the same logical request replays the original successful result.
3. A previously used key with a different logical request is rejected.
4. Concurrent requests with the same key cannot create duplicate financial effects.
5. A failed operation must not leave partial financial changes or a successful idempotency result.

## 3. Key Scope

The idempotency scope must include:

- The authenticated user.
- The operation type.
- The idempotency key.

The operation type distinguishes deposits from transfers.

This prevents a key used for one type of operation from accidentally identifying a different operation type, and prevents one user from replaying another user's result.

The authenticated identity must come from the validated request principal. The client must not be allowed to select another user's idempotency scope by supplying an arbitrary user identifier.

The database must enforce the selected uniqueness rule.

## 4. Key Format and Validation

The API must require a non-empty `Idempotency-Key` header for supported operations.

The implementation must define and validate:

- Maximum key length.
- Accepted characters.
- Whitespace handling.
- Case sensitivity.
- Behavior for missing or malformed keys.

The key should be treated as an opaque identifier. The server must not derive business meaning from its contents.

Clients should generate a new key for each new logical operation and reuse that key when retrying the same operation.

A new intentional deposit or transfer must use a different key, even when its request body matches an earlier operation.

The accepted format is `^[A-Za-z0-9_-]{8,128}$` (`CONST-005`), with a maximum length of 128 characters (`CONST-006`). Keys that do not match are rejected with `400 VALIDATION_ERROR` before any reservation is attempted.

## 5. Request Fingerprinting

The server must associate an idempotency key with a fingerprint of the logical request.

The fingerprint must cover the inputs that determine the financial effect, including:

- Operation type.
- Validated monetary amount.
- Recipient identity for transfers, represented by the server's canonical lowercase username per `docs/architecture/data-model.md §4 Username Normalization Policy` (`CONST-013`).

The canonical fingerprint input is constructed as a UTF-8 byte sequence:

```text
operationType "\n" canonicalUsername "\n" canonicalAmount
```

where:

- `operationType` is `"deposit"` or `"transfer"`.
- `canonicalUsername` is the lowercased recipient username for transfers; for deposits, the literal `"-"` (no recipient).
- `canonicalAmount` is the validated monetary value formatted with exactly two fractional digits using invariant culture, without thousands separators (e.g. `25.50`, `1000000.00`).

The fingerprint is `SHA-256` of that byte sequence, stored as `varbinary(32)` in the database. Comparison is byte-for-byte.

The authenticated user identity is **not** included in the fingerprint bytes; it is part of the uniqueness scope `(UserId, OperationType, IdempotencyKey)` and is enforced by the database constraint. Including it in the hash would be redundant.

The fingerprint must not depend on irrelevant JSON formatting or property order.

For example, if the API accepts both `"10.5"` and `"10.50"` as valid representations of the same monetary value, the fingerprinting strategy should treat them consistently after successful validation and normalization.

The implementation must define a deterministic canonical representation before computing the fingerprint.

If a request using an existing key has a different logical fingerprint, the server must reject it with the documented conflict response.

## 6. Successful Request Lifecycle

A successful operation follows this conceptual lifecycle:

1. Authenticate the caller.
2. Validate the request and idempotency key.
3. Calculate the request fingerprint.
4. Check or establish the idempotency record using the concurrency-safe persistence strategy.
5. Execute the financial operation within a database transaction.
6. Persist all required operation records and ledger entries.
7. Persist the successful idempotency result.
8. Commit the transaction.
9. Return the successful response.

The exact internal order may vary to accommodate uniqueness constraints and transaction behavior.

The financial writes and successful idempotency state must commit together. The application must not expose a successful response before the commit succeeds.

A replay must not create additional balance changes, operation records, or ledger entries.

## 7. Replaying a Successful Request

When a previously successful key is submitted again with the same logical request, the server must return the original operation result without repeating the financial effect.

The response should preserve the original operation identity and the documented response semantics.

The implementation must decide which response details are stored, such as:

- HTTP status code.
- Response body or sufficient data to reconstruct it.
- Operation identifier.
- Relevant response metadata.

The replay contract must be documented consistently for deposits and transfers.

For deposits, a successful response is `201 Created` with `Location: /api/v1/wallet/deposits/{depositId}` and a JSON body. The `Location` value and the body are part of the stored replay contract: a replay of a successful request returns the same status, `Location`, and body (`OQ-018` Decided; see `docs/api/endpoints.md §5`).

Transport-specific metadata, such as a new request trace identifier, may differ between attempts when appropriate. It must not change the underlying financial result.

## 8. Reusing a Key with a Different Request

If the same key is used in the same scope with a different logical request, the API must return a conflict response.

Example:

- First request: transfer USD 25.00 to `bob`.
- Retry: transfer USD 40.00 to `bob` using the same key.

The second request must not execute a new transfer or alter the original result.

The same rule applies if the recipient changes or if the operation type or another fingerprinted input differs.

The API should use the documented idempotency conflict error code from `docs/api/error-model.md`.

## 9. Concurrent Duplicate Requests

Two requests may arrive simultaneously with the same key before either request has completed.

Application-level check-then-insert logic is insufficient to guarantee correctness.

The implementation must use a database-enforced uniqueness rule and a transaction strategy that safely handles competing requests.

The system must ensure that:

- At most one execution creates the financial effect.
- A successful duplicate can obtain the original result.
- A duplicate with a different fingerprint is rejected.
- Unique constraint conflicts are handled deliberately.
- A concurrent request cannot observe or return a fabricated success.

The behavior when a duplicate arrives while the first request is still executing is:

1. The database uniqueness constraint on `(UserId, OperationType, IdempotencyKey)` decides which request reserves the key.
2. A request that observes an existing `InProgress` record with a matching fingerprint waits for a bounded period (`CONST-011`, 3 seconds) by polling the record every `CONST-014` (100 milliseconds).
3. If the record transitions to `Completed` within the bounded wait, the waiting request replays the stored result.
4. If the bounded wait expires, the waiting request returns `503 SERVICE_UNAVAILABLE` with code `IDEMPOTENCY_IN_PROGRESS` and instructs the client to retry with the same key and request.
5. If the record's lease (`CONST-012`, 30 seconds) has expired and the outcome is still `InProgress`, the waiting request may claim the record with a conditional update and resume the operation under the claimed lease. Only one request wins the claim; all others continue to wait or return `503`.
6. A request with a different fingerprint returns `409 IDEMPOTENCY_KEY_REUSED` immediately, without waiting.

No background recovery service exists. Recovery is on-demand, triggered by a retry for the same key after the lease has expired.

### Implementation Notes (Phase 7)

`IdempotencyCoordinator` (`src/Backend/PS.Wallet/PS.Wallet.Infrastructure/Persistence/IdempotencyCoordinator.cs`) owns the policy above for both financial commands. `CreateDepositCommandHandler` and `CreateTransferCommandHandler` delegate to `IIdempotencyCoordinator` and no longer read or write idempotency records directly.

- The lease is persisted as `IdempotencyRecord.LeaseExpiresAtUtc` (`datetime2(7)`, NOT NULL, migration `20261018130511_AddIdempotencyLease`, column default `2026-01-01T00:00:00`). Any record whose lease is in the past is treated as expired, so a stale `InProgress` record created before the lease existed is recoverable instead of blocking its key permanently.
- Reservation is a raw `INSERT` whose uniqueness is arbitrated by the `(UserId, OperationType, IdempotencyKey)` unique index. A duplicate-key violation (SQL error `2601`/`2627`) is an expected concurrency outcome, not an error.
- Bounded wait: a matching `InProgress` record with a live lease is re-read every `CONST-014` (100 ms) for at most `CONST-011` (3 s), then the request returns `503 IDEMPOTENCY_IN_PROGRESS`.
- Recovery: an expired lease is claimed with `UPDATE IdempotencyRecords SET LeaseExpiresAtUtc = @newLease WHERE Id = @id AND ResponseStatusCode IS NULL AND LeaseExpiresAtUtc < @now`. Exactly one request wins (one affected row) and resumes the operation under the claimed lease; every other request keeps waiting or returns `503`.
- Completion is guarded (`WHERE Id = @id AND ResponseStatusCode IS NULL`) and is executed inside the financial transaction by the transaction executors, so the replayable result and the financial effect commit atomically (§6, `ADR-004 §4`). The command handler repeats the same guarded call after the executor returns; it affects no rows and confirms the documented call order.
- A fingerprint mismatch is evaluated before lease state, so `409 IDEMPOTENCY_KEY_REUSED` is returned immediately in every case, including while a request is `InProgress`.
- Only a `4xx` business rejection releases the reservation (`CONST-015`). `5xx` and uncertain outcomes leave the record `InProgress` (`ADR-004 §8`).
- A failure after the financial transaction has begun is raised as `UncertainCommitException` (`src/Backend/PS.Wallet/PS.Wallet.Infrastructure/Persistence/UncertainCommitException.cs`) and surfaced as `503 IDEMPOTENCY_IN_PROGRESS`; the reservation is neither released nor marked completed, because the durable state does not yet prove rollback or commit. A retry with the same key resolves the outcome through the bounded wait or, once the lease expires, through on-demand recovery.

## 10. Failed Requests and Retryability

The initial policy is that failed financial operations must not permanently reserve a key in a way that prevents a valid retry.

For example, if a transfer fails because of insufficient funds and the client later retries the same logical request after funding the wallet, the previous failure must not automatically block the retry indefinitely.

The implementation must distinguish between:

- Validation failures before execution.
- Business failures such as insufficient funds.
- Database failures that roll back the transaction.
- Successful operations whose response was lost after commit.

A failed transaction must not leave a successful idempotency record.

The MVP policy distinguishes three outcome classes:

| Outcome class | Persisted as replayable? | Idempotency record final state |
|---|---|---|
| `2xx` success | Yes | `Completed`; retained for `CONST-029` (`24` hours) |
| `4xx` business rejection (validation, insufficient funds, balance limit, self-transfer, idempotency key reuse) | No | Record is deleted after the response is returned, or left to expire per `CONST-015`; the key is released for a fresh attempt |
| `5xx` unexpected / uncertain commit | No | Remains `InProgress`; resolved by on-demand recovery after the lease expires |

Consequences:

- A client that retries a business-rejected request with the **same** key and **same** payload receives the same rejection only while the rejection is replayable. Under the MVP policy, `4xx` rejections are **not** replayable, so the same key with the same payload **re-executes** validation and may succeed if the underlying condition changed (e.g. insufficient funds resolved by a prior deposit).
- A client that retries an `5xx`-affected request with the same key while the record is `InProgress` follows the bounded-wait and recovery policy in §9.
- A client that wants a **different** logical operation must use a **new** key, regardless of prior failures.

The `idempotency_key_reused` conflict applies only to a **different fingerprint** for an **existing** key, not to a retry of the same request after a business rejection.

The application must be particularly careful when a connection or commit failure leaves the operation outcome uncertain. It must use the persisted idempotency state to determine whether the operation already succeeded rather than blindly executing it again.

## 11. Persistence Model

The logical idempotency record should contain enough information to enforce uniqueness, compare request fingerprints, and replay successful results.

A proposed record includes:

- Record identifier.
- Authenticated user identifier.
- Operation type.
- Idempotency key.
- Request fingerprint.
- Operation identifier or reference.
- Successful response status.
- Successful response data or the information needed to reconstruct it.
- Creation timestamp.

The exact schema, column types, indexes, and retention policy are defined in the data model and implementation.

The database must have an appropriate unique constraint over the selected scope.

The implementation must avoid storing unnecessary sensitive information in replay data.

## 12. Retention

A successful (`2xx`) idempotency record is retained for `CONST-029` (`24` hours) (`OQ-010` Decided). Deleting a record while a client may legitimately retry its original request can permit duplicate execution, so the retention window must exceed any supported client retry window.

`4xx` business rejections are released immediately (`CONST-015`), and `5xx` uncertain outcomes remain `InProgress` until on-demand recovery resolves them. Physical cleanup of expired records and any long-term storage growth control are deferred beyond the MVP (Phase 7).

## 13. Security and Observability

Idempotency records and replay results must be scoped to the authenticated caller.

A user must not be able to retrieve another user's stored response by guessing or reusing a key.

Logs may include the operation type, operation identifier, and a safe correlation value. They must not expose authentication credentials or unnecessary sensitive request data.

The system should record relevant outcomes, including:

- New successful operation.
- Successful replay.
- Key reuse with a different fingerprint.
- Concurrent duplicate handling.
- Failed operation.
- Idempotency persistence failure.

Logs must not be treated as the authoritative source of idempotency state; the database remains authoritative.

## 14. Testing Requirements

Integration tests against SQL Server must cover:

- A new deposit succeeds and stores its idempotency result.
- A repeated successful deposit does not duplicate the credit.
- A repeated successful transfer does not duplicate the debit or credit.
- The replay returns the original operation identity.
- Reusing a key with a different amount is rejected.
- Reusing a key with a different recipient is rejected.
- A key used for one operation type does not collide with another operation type.
- Different users can use the same key without sharing results.
- Concurrent requests with the same key create at most one financial effect.
- A failed transaction does not persist a successful result.
- A valid retry after a retryable business failure can succeed.
- A lost response after a successful commit can be recovered through a retry.
- Idempotency uniqueness constraints behave correctly under real SQL Server concurrency.

Tests must verify both HTTP outcomes and persisted financial state.

## 15. Consequences

### Positive Consequences

- Client retries do not duplicate successful financial operations.
- Uncertain network outcomes can be resolved safely.
- Concurrent duplicate requests are handled consistently.
- Idempotency state remains aligned with financial commits.
- The API has an explicit contract for request replay.

### Negative Consequences

- The application must persist and manage idempotency records.
- Database uniqueness conflicts require deliberate handling.
- Fingerprinting and response replay add implementation complexity.
- Retention and failed-request semantics require explicit decisions.
- Additional database writes may increase transaction duration.

## 16. Alternatives Considered

### In-Memory Idempotency Cache

Rejected as the authoritative mechanism because it does not reliably coordinate multiple application instances and may lose state during restarts.

### Client-Side Duplicate Prevention Only

Rejected because clients can retry requests, crash, or send concurrent requests. Server-side enforcement is required.

### Permanent Storage of Every Failed Response

Not selected as the default because the MVP requires valid retries after retryable failures, such as insufficient funds. Permanent failure replay would require a more nuanced policy.

## 17. Open Decisions

1. Exact response fields persisted for replay. (resolved in OQ-018)
2. Retention period and cleanup strategy. (retention resolved in OQ-010 / `CONST-029`; cleanup tracked in KI-008)
3. Exact conflict and retryable error codes. (resolved in OQ-020; see `docs/api/error-model.md §4`)
4. Whether response replay includes the original `Location` header. (resolved in OQ-018)

The remaining items are deferred beyond the MVP and tracked in `docs/project/known-issues.md` where they represent accepted deviations, or remain open in this section until resolved.

### Resolved during Phase 4

- **IdempotencyRecord location:** The `IdempotencyRecord` entity is placed in `PS.Wallet.Domain.Entities`, not in `PS.Wallet.Infrastructure.Persistence.Entities`. Reason: the Application layer needs to query and update idempotency records, and Application must not reference Infrastructure. The table name (`IdempotencyRecords`), columns, and constraints are unchanged. See `docs/architecture/data-model.md §9`.
- **Response fields and Location header (`OQ-018`):** Resolved. See `docs/api/endpoints.md §5`.
- **Retention (`OQ-010`):** Resolved. See `CONST-029` and `§10`.
- **Bounded wait and on-demand lease recovery (`§9`):** Not implemented in Phase 4. A matching in-progress request returns `503 IDEMPOTENCY_IN_PROGRESS` immediately. Tracked as `KI-003` in `docs/project/known-issues.md`.

## 18. References

- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/adr/ADR-004-concurrency-control.md`
- `docs/api/api-guidelines.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/requirements/domain-rules.md`

