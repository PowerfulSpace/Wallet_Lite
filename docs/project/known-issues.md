# Known Issues and Deferred Deviations

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `docs/project/known-issues.md`

## 1. Purpose

This document records **accepted deviations** from the documented contracts, requirements, or ADRs that are consciously deferred beyond the current phase.

It is not a list of open questions. Use `docs/project/open-questions.md` for decisions that are not yet made.

The distinction:

- **Open question** — no decision exists yet. Tracked in `open-questions.md`.
- **Known issue** — a decision exists, but the implementation or contract intentionally deviates from the ideal. Tracked here.
- **Bug** — unintended incorrect behavior. Not tracked here; fix it.

## 2. Entry Format

Each entry uses the following fields:

- **ID** — `KI-NNN`, sequential, never reused.
- **Title** — short description.
- **Status** — `Open`, `Resolved`, or `Wontfix`.
- **Affects** — documents, endpoints, ADRs, or requirements.
- **Description** — what deviates and why.
- **Risk** — what could go wrong because of the deviation.
- **Mitigation** — how the risk is contained in the current phase.
- **Revisit by** — phase or condition under which this entry must be re-evaluated.

## 3. Entries

### KI-001 — `4xx` business rejections are not replayable

- **Status:** Open
- **Affects:** `ADR-005 §10`, `docs/api/endpoints.md §5/§6`, `docs/api/error-model.md`
- **Description:** A business rejection (`insufficient_funds`, `balance_limit_exceeded`, `invalid_transfer`) does not persist a replayable result. A retry with the same key and same payload re-executes validation. This deviates from strict "one key = one outcome" semantics.
- **Risk:** A client that retries after a rejection expecting the same response may instead receive a different response if the underlying state changed. This is intentional: it allows valid retries after funding the wallet.
- **Mitigation:** The API contract explicitly states this behavior. Clients must not cache rejections. The `Idempotency-Key` is documented as "one logical operation", not "one HTTP response".
- **Revisit by:** Post-MVP if strict replay semantics become a requirement.

### KI-002 — 15 documents have unlabeled Open Decisions entries

- **Status:** Resolved
- **Affects:** — (resolved; no current impact)
- **Description:** (historical) Fifteen documents originally had unlabeled `Open Decisions` entries. As of Phase 8 finalization, all 19 documents with `Open Decisions` sections have their items labeled (123/123).
- **Resolution (post-MVP cleanup):** All 19 documents that contain `Open Decisions` sections have their items labeled. The remaining documents do not have such a section. 123/123 items labeled; 0 unlabeled.
- **Risk:** (historical) A future reader may treat an unlabeled document-local open decision as authoritative when it is already resolved in `open-questions.md`, or vice versa. No current impact: every document-local entry is labeled.
- **Mitigation:** All 123 items across 19 documents are labeled. No action required unless a new `Open Decisions` section is added without labels.
- **Revisit by:** Only if a new document with an unlabeled `Open Decisions` section is added.

### KI-003 — Bounded wait and on-demand lease recovery not implemented

- **Status:** Resolved
- **Affects:** `ADR-005 §9`, `docs/architecture/concurrency.md §8.6`, `IIdempotencyCoordinator`, `IdempotencyRecord.LeaseExpiresAtUtc`
- **Description:** `ADR-005 §9` specifies a bounded wait (`CONST-011` 3 s, polling `CONST-014` 100 ms) for concurrent requests with the same idempotency key, followed by on-demand lease recovery (`CONST-012` 30 s). The Phase 4 implementation returned `503 IDEMPOTENCY_IN_PROGRESS` **immediately** when a matching request was already in progress, without waiting or recovering. In Phase 7 the policy is implemented by `IdempotencyCoordinator` (`src/Backend/PS.Wallet/PS.Wallet.Infrastructure/Persistence/IdempotencyCoordinator.cs`), which owns reservation, replay, bounded wait, release, and completion; `CreateDepositCommandHandler` and `CreateTransferCommandHandler` delegate to `IIdempotencyCoordinator`. Lease expiry is persisted in `IdempotencyRecord.LeaseExpiresAtUtc` (`datetime2(7)`, migration `20261018130511_AddIdempotencyLease`).
- **Resolution (Phase 7):** `IdempotencyCoordinator` implements bounded wait (`CONST-011`, `CONST-014`) and on-demand lease recovery (`CONST-012`). A matching in-progress request waits, replays if completed, or returns `503 IDEMPOTENCY_IN_PROGRESS` after the bounded wait.
- **Risk:** (Before fix) A client that retried while the original request was still running received `503` immediately instead of waiting for the result. This was safe (no duplicate financial effect) but increased client-visible retries. (After fix) A live lease makes the retry wait up to `CONST-011`; an expired lease is claimed and the operation is resumed exactly once.
- **Mitigation:** Only one request wins the conditional lease claim (`UPDATE ... WHERE ResponseStatusCode IS NULL AND LeaseExpiresAtUtc < @now`); all other requests keep waiting or return `503 IDEMPOTENCY_IN_PROGRESS`. Completion is guarded (`WHERE ResponseStatusCode IS NULL`) and commits inside the financial transaction, so recovery can never produce a second financial effect.
- **Revisit by:** Post-MVP, if a background recovery service replaces on-demand recovery.

### KI-004 — Transfer deadlock-retry exhaustion leaves the idempotency reservation `InProgress`

- **Status:** Resolved
- **Affects:** `ADR-004 §8`, `docs/architecture/concurrency.md §16`, `CreateTransferCommandHandler`, `TransferTransactionExecutor`, `UncertainCommitException`
- **Description:** `ADR-004 §8` bounds deadlock retries (`CONST-017`–`CONST-020`) but does not define the terminal behavior when every attempt is a deadlock victim. The Phase 5 transfer implementation stopped retrying and surfaced `500 Internal Server Error`, and the reservation committed by `CreateTransferCommandHandler` stayed `InProgress` instead of being released. The same bounded-wait gap as `KI-003` also applied to the transfer handler: a matching in-progress request returned `503 IDEMPOTENCY_IN_PROGRESS` immediately. In Phase 7 both executors raise `UncertainCommitException` (`src/Backend/PS.Wallet/PS.Wallet.Infrastructure/Persistence/UncertainCommitException.cs`) for failures that occur after the financial transaction begins, the exception is mapped to `503 IDEMPOTENCY_IN_PROGRESS` by `ExceptionHandlingMiddleware`, and the reservation is kept `InProgress` for lease recovery (the command handlers never release it). Deadlock victims remain excluded from that wrap so the bounded retry loop is preserved, and exhaustion ends in `UncertainCommitException` instead of `500`.
- **Resolution (Phase 7):** Uncertain commit raises `UncertainCommitException`, mapped to `503 IDEMPOTENCY_IN_PROGRESS`. The idempotency record remains `InProgress`; recovery is handled by `IdempotencyCoordinator`.
- **Risk:** (After fix) The reservation is neither released nor falsely marked failed; the client retries with the same key and the outcome is resolved by the bounded wait and lease recovery in `ADR-005 §9`. A confirmed rollback and an uncertain commit are indistinguishable to the client, so both leave the record `InProgress`.
- **Mitigation:** Retries are bounded, the change tracker is cleared before each retry, and the existing reservation is reused, so no second transfer, ledger entry, or idempotency record is created. Deterministic `CONST-021` lock ordering reduces the deadlock rate. Verified by `IdempotencyRecoveryTests` (`UncertainCommit_Returns503AndLeavesRecordInProgress_ThenRecoversOnRetry`).
- **Revisit by:** Post-MVP, if a confirmed rollback should release the key immediately instead of waiting for lease recovery.

### KI-005 — Transaction history N+1 queries and missing composite pagination index

- **Status:** Resolved
- **Affects:** `GetTransactionsQueryHandler`, `LedgerEntryConfiguration`, `docs/architecture/data-model.md §14`
- **Description:** The transaction-history handler resolved each ledger entry's transfer and counterparty user with separate queries (N+1), and the index was `(WalletId, CreatedAtUtc)`, which does not cover the pagination sort tie-break by `(CreatedAtUtc DESC, Id DESC)`.
- **Resolution (Phase 7):** The handler now uses a projection join; the composite index `(WalletId, CreatedAtUtc, Id)` was added in migration `AddLedgerEntryPaginationIndex`. The transfer counterparty usernames are resolved inside the ledger-entry projection (`GetTransactionsQueryHandler`), and the resulting rows are mapped to `TransactionDto` in memory, so no query is issued per ledger entry.
- **Risk:** (Before fix) Under large histories, the endpoint performed many small queries and index lookups that scaled linearly with the page size. (After fix) The counterparties are resolved inside a single projection command, and the index covers the full `(WalletId, CreatedAtUtc, Id)` pagination predicate.
- **Mitigation:** Mapping behavior is unchanged (deposit, `transfer_in`, `transfer_out`, `unknown` counterparty fallback), and pagination remains stable via the `(CreatedAtUtc, Id)` tie-break. Verified by the existing transaction-history integration tests, including equal-timestamp pagination.
- **Revisit by:** Post-MVP, if history volume requires further query or index tuning (e.g. covering indexes for larger page sizes).

### KI-006 — Monetary JSON serialization contract

- **Status:** Resolved
- **Affects:** `PS.Wallet.API/Json/MoneyJsonConverter`, `Program.cs`, `docs/api/endpoints.md §4`
- **Description:** `ADR-002 §9` specifies monetary values as JSON strings with exactly two fractional digits. Before Phase 6 follow-up, `decimal` values were serialized as JSON numbers, contradicting the documented contract. A global `MoneyJsonConverter` now serializes `decimal` as `"0.00"` and rejects JSON numbers on input.
- **Resolution (Phase 6 follow-up):** `MoneyJsonConverter` (`src/Backend/PS.Wallet/PS.Wallet.API/Json/`) enforces the contract globally for MVC and HTTP JSON. See `AGENTS.md §14`.
- **Risk:** (Before fix) clients expecting string amounts would fail to parse. (After fix) JSON numbers are rejected; clients must send strings.
- **Mitigation:** Global converter applies to all `decimal` properties in API responses and requests. Integration tests verify string-on-write, string-on-read, and number-on-input rejection.
- **Revisit by:** Only if `ADR-002 §9` is revised.

### KI-007 — Uncertain commit is simulated, not reproduced

- **Status:** Resolved
- **Affects:** `IdempotencyRecoveryTests`, `UncertainCommitException`, `ADR-005 §9`
- **Description:** The uncertain-commit scenario (commit succeeded but response lost) cannot be reproduced deterministically in the test suite without a connection drop or a network proxy. The current test uses a trigger (`INSTEAD OF INSERT`) that rolls back the effect, so it exercises recovery but not the "commit succeeded, replay stored response" path.
- **Resolution (MVP finalization):** Uncertain commit is not deterministically reproducible in the current test environment (no connection proxy). The behavior is covered indirectly: `KI-003` recovery tests prove that `InProgress` records are recoverable; replay from `Completed` is covered by existing idempotency tests. The `UncertainCommitException` path is exercised by unit tests. Reproducing the exact "commit succeeded, response lost" scenario is deferred to a post-MVP fault-injection mechanism.
- **Risk:** A regression in the uncertain-commit handling (e.g., marking the record failed instead of leaving it `InProgress`) would not be caught by an automated test.
- **Mitigation:** The behavior is covered indirectly: `KI-003` recovery tests prove that `InProgress` records are recoverable; replay from `Completed` is covered by existing idempotency tests.
- **Revisit by:** Post-MVP, if a fault-injection middleware is added.

### KI-008 — Expired idempotency records are not cleaned up

- **Status:** Open
- **Affects:** `ADR-005 §8`, `ADR-005 §17 item 2`, `OQ-010` Decision, `CONST-029`
- **Description:** `ADR-005 §8` and `OQ-010` state that successful (`2xx`) idempotency records are retained for `CONST-029` (24 hours), with physical cleanup "deferred to Phase 7". Phase 7 is complete and no cleanup job exists. The current implementation never deletes idempotency records.
- **Risk:** The `IdempotencyRecords` table grows unbounded. For a local educational MVP with manual seeding and short lifetimes, this is harmless. For any long-running deployment, storage growth and index fragmentation would accumulate.
- **Mitigation:** The 24-hour retention is documented; a client retry after 24 hours is documented as a new logical operation. No functional bug exists in the MVP. `KI-008` tracks the missing cleanup implementation.
- **Revisit by:** Post-MVP, when a background cleanup service is added. The cleanup job must respect `CONST-029` and must not delete `InProgress` records.

### KI-009 — Observability thresholds and alerting not configured

- **Status:** Open
- **Affects:** `docs/operations/observability.md §12`
- **Description:** Latency/error-rate thresholds, notification channels, and escalation procedures are not configured. The MVP has no production deployment, so alerting is not actionable.
- **Risk:** None for the MVP. Required before any production deployment.
- **Mitigation:** Structured logging and the `GET /health` endpoint are present. `docs/operations/observability.md` documents the intended production requirements.
- **Revisit by:** Post-MVP, before production deployment.

## 4. Related Documents

- `docs/project/open-questions.md`
- `docs/project/decisions.md`
- `docs/project/constants.md`
- `docs/project/roadmap.md`
