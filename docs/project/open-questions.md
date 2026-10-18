# Open Questions and Pending Decisions

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document tracks unresolved product, domain, architecture, API, security, testing, and operational decisions for the Wallet API MVP.

Its purpose is to:

- Prevent unresolved questions from being overlooked during implementation.
- Identify decisions that block dependent work.
- Record the urgency and expected resolution phase of each question.
- Link decisions to the relevant documentation.
- Keep assumptions distinct from approved requirements.

An item MUST NOT be considered resolved merely because a possible solution has been discussed. Resolution requires an explicit decision and updates to the affected documentation.

## 2. Priority Definitions

| Priority | Meaning |
|---|---|
| P0 — Blocking | Must be resolved before the affected implementation can safely proceed |
| P1 — High | Should be resolved during the current phase or before dependent functionality is finalized |
| P2 — Normal | Can be resolved during implementation or MVP hardening |
| P3 — Deferred | Can remain open until a later release, provided it does not affect MVP correctness |

## 3. Status Definitions

- **Open** — No decision has been approved.
- **Proposed** — A candidate solution exists but has not been accepted.
- **Decided** — The decision has been approved and documented.
- **Deferred** — The decision is intentionally postponed with a reason.

## 4. Domain and Data Model

### OQ-001 — Initial Wallet Balance

**Priority:** P0  
**Status:** Decided  
**Resolve by:** Phase 2 — Domain and Persistence  
**Related:** `docs/architecture/data-model.md`, `docs/requirements/domain-rules.md`, `docs/adr/ADR-006-ledger-model.md`

Should every new wallet start with a balance of USD 0.00?

The proposed default is zero, with the initial balance represented consistently in the ledger model.

**Decision required:** Confirm the initial balance and the relationship between wallet creation, opening balance, and ledger reconciliation.

**Decision:** New wallets start at `0.00` with no opening ledger entry. Any non-zero starting state must have an explicit ledger entry. See `DR-047`, `ADR-006 §3.1`, `data-model.md §5`.

### OQ-002 — Username Normalization

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 2 — Domain and Persistence  
**Related:** `docs/architecture/data-model.md`, `docs/api/endpoints.md`

How should usernames be normalized and compared?

The decision must define:

- Case sensitivity.
- Whitespace handling.
- Uniqueness rules.
- Database collation or canonicalization behavior.
- How recipient usernames are resolved.

**Decision:** `^[A-Za-z0-9_]{3,32}$` after trim; stored and compared in lowercase; no Unicode normalization; uniqueness on canonical lowercase form. See `docs/architecture/data-model.md §4 Username Normalization Policy`, `CONST-013`.

### OQ-003 — Ledger Reference Model

**Priority:** P1  
**Status:** Proposed  
**Resolve by:** Phase 2 — Domain and Persistence  
**Related:** `docs/architecture/data-model.md`, `docs/adr/ADR-006-ledger-model.md`

How should ledger entries reference their originating operations?

The implementation must preserve referential integrity and make it possible to distinguish deposit entries from transfer debit and credit entries.

**Decision required:** Select a schema that enforces valid operation references and supports reliable reconciliation.

### OQ-004 — Ledger Cardinality Enforcement

**Priority:** P1  
**Status:** Open  
**Resolve by:** Phase 2 — Domain and Persistence  
**Related:** `docs/architecture/data-model.md`, `docs/requirements/domain-rules.md`

How will the system enforce the expected number and relationship of ledger entries for each successful operation?

Expected behavior:

- One positive ledger entry per deposit.
- Two equal-and-opposite ledger entries per transfer.

The design must distinguish constraints enforceable directly by SQL Server from invariants that require transactional application logic and verification.

### OQ-005 — Database Numeric Constraints

**Priority:** P1  
**Status:** Proposed  
**Resolve by:** Phase 2 — Domain and Persistence  
**Related:** `docs/adr/ADR-002-money-and-precision.md`, `docs/architecture/data-model.md`

Which monetary rules will be enforced through database constraints in addition to application validation?

The design must consider positive operation amounts, supported precision, valid balance ranges, and non-negative wallet balances.

**Resolution path:** Values for monetary precision and limits are registered in `docs/project/constants.md` as `CONST-001`–`CONST-004`. This OQ remains open only for the **enforcement mechanism** (database CHECK constraints vs application-only), not for the values themselves.

## 5. Financial Transactions and Concurrency

### OQ-006 — Transaction Isolation Level

**Priority:** P0  
**Status:** Decided  
**Resolve by:** Phase 5 — Transfers  
**Related:** `docs/architecture/concurrency.md`, `docs/adr/ADR-004-concurrency-control.md`

Which SQL Server transaction isolation level will be used for financial write operations?

The decision must be based on the chosen conditional-update and locking strategy, correctness requirements, and integration-test evidence.

The selected isolation level must prevent invalid concurrent financial outcomes without relying on unsafe read-then-update logic.

**Decision:** `READ COMMITTED` + explicit conditional updates + deterministic lock ordering. Not `SERIALIZABLE`, not `SNAPSHOT`. See `ADR-004 §6`, `CONST-016`.

### OQ-007 — Deadlock Retry Policy

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 5 — Transfers  
**Related:** `docs/architecture/concurrency.md`, `docs/operations/recovery.md`

Which database errors are eligible for retry, and what retry limits and delays will be applied?

Retries must be bounded and must preserve transaction atomicity and idempotency.

**Decision:** Retry only SQL error `1205` (deadlock victim); max `CONST-017` (3) attempts; base `CONST-018` (100 ms); multiplier `CONST-019` (×2); jitter `CONST-020` (±20%). Other transient errors surface as `500` and rely on client idempotent retry. See `ADR-004 §8`.

### OQ-008 — Concurrent Idempotency Requests

**Priority:** P0  
**Status:** Decided  
**Resolve by:** Phase 4 — Deposits  
**Related:** `docs/adr/ADR-005-idempotency.md`, `docs/architecture/concurrency.md`

How will the application handle simultaneous requests using the same idempotency key?

The implementation must handle unique-constraint races and concurrent duplicates without creating duplicate financial effects.

**Decision required:** Finalize the database uniqueness constraint, request fingerprinting, and transaction strategy.

**Resolution path:** The bounded-wait and lease constants are registered as `CONST-011` and `CONST-012` in `docs/project/constants.md`. This OQ remains open for the **concurrency coordination mechanism** (unique constraint behavior, wait policy, recovery), not for the numeric parameters.

**Decision:** Unique constraint `(UserId, OperationType, IdempotencyKey)`; bounded wait `CONST-011` (3 s) with polling `CONST-014` (100 ms); `503 IDEMPOTENCY_IN_PROGRESS` on timeout; on-demand lease recovery with `CONST-012` (30 s). See `ADR-005 §9` and `docs/architecture/concurrency.md §8.6`.

### OQ-009 — Failed Request Idempotency Policy

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 4 — Deposits  
**Related:** `docs/adr/ADR-005-idempotency.md`, `docs/api/error-model.md`

Which failures, if any, will be persisted as replayable idempotency results?

The baseline requirement is that failed attempts must not permanently prevent a valid retry merely because a key was previously used.

The policy must distinguish validation failures, business-rule failures, transient infrastructure failures, and successful committed operations.

**Resolution path:** Once decided, the policy must state which outcomes are persisted as replayable. See `docs/project/known-issues.md` for any deferred deviation.

**Decision:** `2xx` → `Completed` (replayable); `4xx` business → not persisted as replayable (key released, same key re-validates); `5xx` → remains `InProgress` (recovery via lease). See `ADR-005 §10` and `endpoints.md §5/§6`.

### OQ-010 — Idempotency Retention

**Priority:** P1  
**Status:** Open  
**Resolve by:** Phase 4 — Deposits  
**Related:** `docs/adr/ADR-005-idempotency.md`, `docs/architecture/data-model.md`

How long must successful idempotency records remain available?

The retention policy must account for client retry windows and the risk of a previously used key producing a second financial effect after its record is deleted.

**Cross-reference:** Tracked as `ADR-005 §17` item "Retention period and cleanup strategy".

### OQ-011 — Uncertain Commit Resolution

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 5 — Transfers  
**Related:** `docs/operations/recovery.md`, `docs/adr/ADR-003-transaction-atomicity.md`, `docs/adr/ADR-005-idempotency.md`

How will the application resolve a database connection failure during commit?

The implementation must distinguish a confirmed rollback from an uncertain outcome and avoid blindly repeating the operation.

**Decision:** An uncertain commit outcome returns `503 IDEMPOTENCY_IN_PROGRESS` and leaves the idempotency record `InProgress`. The client retries with the same key; the bounded wait and on-demand recovery in `ADR-005 §9` resolve the outcome. The application does not blindly retry financial writes. See `ADR-004 §8`, `recovery.md §4.2`.

### OQ-012 — Lock Ordering

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 5 — Transfers  
**Related:** `docs/architecture/concurrency.md`, `docs/adr/ADR-004-concurrency-control.md`

Will the application lock or update wallets in a deterministic order, such as by wallet ID?

The strategy should reduce avoidable deadlocks while preserving atomic transfers.

**Decision:** Wallet rows are updated in ascending `Wallet.Id` (Guid ordinal) order (`CONST-021`), independent of sender/recipient role. See `ADR-004 §5`.

## 6. API Contract

### OQ-013 — Transaction History Page Size

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 6 — Transaction History and API Consistency  
**Related:** `docs/api/endpoints.md`, `docs/api/api-guidelines.md`

What default and maximum page sizes will the transaction history endpoint support?

The final values must balance usability, response size, and database efficiency.

**Resolution path:** Final values will be registered as `CONST-007` and `CONST-008` in `docs/project/constants.md`.

**Decision:** Default `CONST-007` (20); maximum `CONST-008` (100). See `endpoints.md §4`, `api-guidelines.md §11`.

### OQ-014 — Cursor Format

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 6 — Transaction History and API Consistency  
**Related:** `docs/api/endpoints.md`, `docs/api/api-guidelines.md`

What format will be used for opaque pagination cursors?

The ordering strategy must provide deterministic pagination using the agreed timestamp and unique identifier fields.

**Resolution path:** Final format will be registered as `CONST-010` in `docs/project/constants.md`.

**Decision:** `base64(JSON { createdAtUtc: ISO8601, id: GUID })` (`CONST-010`). Ordering tuple `(CreatedAtUtc DESC, Id DESC)`. Invalid cursor returns `400 INVALID_CURSOR`. See `endpoints.md §4`, `error-model.md §4`.

### OQ-015 — Recipient Enumeration Behavior

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 5 — Transfers  
**Related:** `docs/api/endpoints.md`, `docs/security/threat-model.md`

How should the API respond when the recipient username does not exist?

The decision must balance a clear client contract against unnecessary disclosure of registered usernames.

**Decision:** Unknown recipient returns `404 RESOURCE_NOT_FOUND`, indistinguishable from inaccessible transfer (`CONST-022`). See `endpoints.md §6`, `error-model.md §6`.

### OQ-016 — Username Canonicalization in Requests

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 3 — Wallet Query and Identity  
**Related:** `docs/api/endpoints.md`, `docs/architecture/data-model.md`

Should the API trim whitespace or normalize recipient usernames, or reject non-canonical input?

The API and database must apply consistent rules.

**Decision:** The API trims leading/trailing whitespace and lowercases the username before lookup and fingerprinting. Non-canonical input that would canonicalize to a valid username is accepted; input that fails `^[A-Za-z0-9_]{3,32}$` after trimming is rejected with `400 VALIDATION_ERROR`.

### OQ-017 — Request Fingerprinting

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 4 — Deposits  
**Related:** `docs/adr/ADR-005-idempotency.md`

Which normalized request fields will be included in the idempotency fingerprint?

The fingerprint must distinguish materially different logical operations while avoiding differences caused only by irrelevant serialization details.

**Resolution path:** The idempotency key format and length will be registered as `CONST-005` and `CONST-006` in `docs/project/constants.md`.

**Decision:** `SHA-256( operationType "\n" canonicalUsername "\n" canonicalAmount )`, stored as `varbinary(32)`, byte-for-byte comparison. See `ADR-005 §5` and `docs/architecture/data-model.md §9`.

### OQ-018 — Response Fields and Location Headers

**Priority:** P2  
**Status:** Open  
**Resolve by:** Phase 4 — Deposits  
**Related:** `docs/api/endpoints.md`, `docs/api/api-guidelines.md`

Which response fields and headers will be standardized for successful deposits and transfers?

The contract should specify operation identifiers, relevant timestamps, resulting balance information if included, and whether a `Location` header is returned.

**Cross-reference:** Tracked as `ADR-005 §17` items "Exact response fields persisted for replay" and "Whether response replay includes the original `Location` header".

### OQ-019 — Timestamp Precision and Ordering

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 6 — Transaction History and API Consistency  
**Related:** `docs/api/endpoints.md`, `docs/architecture/data-model.md`

What database timestamp precision and API representation will be used?

The decision must support deterministic ordering and stable cursor pagination when multiple operations share the same timestamp.

**Resolution path:** Final precision will be registered as `CONST-009` in `docs/project/constants.md`.

**Decision:** SQL `datetime2(7)` (`CONST-009`); API ISO 8601 round-trip `O` format (`CONST-026`). See `data-model.md §2`, `api-guidelines.md §12`.

### OQ-020 — HTTP Status for Business Conflicts

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 4 — Deposits  
**Related:** `docs/api/error-model.md`, `docs/api/endpoints.md`

Confirm the HTTP status and stable error codes for insufficient funds, balance-limit violations, and idempotency conflicts.

The proposed baseline uses HTTP 409 for business conflicts, with distinct application error codes.

**Decision:** The error-code catalogue in `error-model.md §4` is final for the MVP. `SELF_TRANSFER` (409) is added as a distinct code from `INVALID_TRANSFER` (409) for client clarity. See `error-model.md §4`, `endpoints.md §6`.

## 7. Security and Authentication

### OQ-021 — Production Authentication Provider

**Priority:** P0 before production  
**Status:** Deferred  
**Resolve by:** Before Production deployment  
**Related:** `docs/security/security-model.md`, `docs/adr/ADR-007-development-authentication.md`

Which authentication provider will be used outside local development?

Development-only authentication must remain unavailable in Production. The MVP may be demonstrated locally before a production provider is selected, provided the environment boundary is enforced and documented.

### OQ-022 — User Identity Mapping

**Priority:** P1  
**Status:** Proposed  
**Resolve by:** Phase 3 — Wallet Query and Identity  
**Related:** `docs/security/security-model.md`, `docs/architecture/data-model.md`

How will the authenticated principal map to an internal user record?

The mapping must use a stable identity and must not rely on a client-supplied user or sender identifier.

### OQ-023 — Rate Limiting

**Priority:** P2  
**Status:** Open  
**Resolve by:** Phase 8 — MVP Hardening and Demonstration  
**Related:** `docs/security/threat-model.md`, `docs/requirements/non-functional-requirements.md`

Which rate limits, if any, are required for the MVP?

The decision should consider resource protection, authentication failures, and repeated financial requests without breaking safe idempotent retries.

## 8. Testing and Quality

### OQ-024 — Performance Targets

**Priority:** P3 — Deferred  
**Status:** Deferred  
**Resolve by:** Post-MVP, before performance acceptance testing  
**Related:** `docs/requirements/non-functional-requirements.md`, `docs/project/roadmap.md`

What response-time, throughput, and concurrency targets should be used?

Targets must be measurable and tied to a documented test environment.

**Decision (MVP):** Performance targets are deferred beyond the MVP. The MVP does not claim a numeric performance level. Correctness under concurrency is verified by `NFR-003` and `NFR-024`, independently of throughput or latency targets. See `NFR-012`.

### OQ-025 — CI Platform and Quality Gates

**Priority:** P2  
**Status:** Open  
**Resolve by:** Deferred to a later phase (Phase 1 does not include CI setup).  
**Related:** `docs/development/testing.md`, `docs/project/roadmap.md`

Which CI platform will run builds, tests, formatting checks, and migration verification?

The minimum quality gates must be defined even if the specific CI provider remains undecided.

### OQ-026 — Integration Test Environment

**Priority:** P1  
**Status:** Proposed  
**Resolve by:** Deferred to implementation; Phase 1 creates the test project scaffolding but does not provision a SQL Server test environment.  
**Related:** `docs/development/testing.md`, `docs/development/setup.md`

How will SQL Server be provisioned for automated integration tests?

The solution must support repeatable tests against real SQL Server behavior, including transactions, constraints, and concurrency.

**Phase 1 note:** The `Wallet.IntegrationTests` project is created in Phase 1 but contains no SQL Server-dependent tests. Test environment provisioning is resolved before Phase 2.

## 9. Operations and Recovery

### OQ-027 — Hosting Platform

**Priority:** P2  
**Status:** Open  
**Resolve by:** Phase 8 — MVP Hardening and Demonstration  
**Related:** `docs/operations/configuration.md`, `docs/operations/recovery.md`

Which hosting platform will be used for a deployed environment?

The choice affects secret management, database connectivity, health probes, monitoring, and backup procedures.

### OQ-028 — Backup, RPO, and RTO

**Priority:** P2  
**Status:** Deferred  
**Resolve by:** Before Production deployment  
**Related:** `docs/operations/recovery.md`

What backup strategy and recovery objectives are required?

Recovery Point Objective (RPO) and Recovery Time Objective (RTO) must be defined before claiming operational readiness for Production.

### OQ-029 — Observability Stack

**Priority:** P2  
**Status:** Open  
**Resolve by:** Phase 7 — Reliability and Verification  
**Related:** `docs/operations/observability.md`, `docs/operations/configuration.md`

Which logging provider, metrics exporter, tracing implementation, and monitoring platform will be used?

The MVP should retain safe structured logging and correlation even if a full monitoring platform is deferred.

### OQ-030 — Reconciliation Procedure

**Priority:** P1  
**Status:** Open  
**Resolve by:** Phase 7 — Reliability and Verification  
**Related:** `docs/operations/recovery.md`, `docs/architecture/data-model.md`

How will wallet balances be compared with ledger entries, and how will discrepancies be reported?

The procedure must reflect the approved initialization model and any legitimate adjustment records.

## 10. Product and Scope

### OQ-031 — Frontend Scope

**Priority:** P2  
**Status:** Deferred  
**Resolve by:** Before frontend implementation  
**Related:** `docs/frontend/overview.md`, `docs/project/roadmap.md`

Will a frontend be implemented as part of the MVP, or will the API be demonstrated using API clients?

The documentation structure includes a frontend section, but that does not by itself commit the project to implementing a frontend.

### OQ-032 — User Creation and Seed Data

**Priority:** P1  
**Status:** Decided  
**Resolve by:** Phase 2 — Domain and Persistence  
**Related:** `docs/development/setup.md`, `docs/adr/ADR-007-development-authentication.md`

How will the Alice and Bob demonstration users be provisioned?

The mechanism must be repeatable, safe to rerun, and unavailable as an insecure production account-creation mechanism.

**Decision:** Alice (`alice@example.com`, `CONST-024`) and Bob (`bob@example.com`, `CONST-025`), wallets at `0.00` (`DR-047`), idempotent seed. Development only; not in Production. See `setup.md §7`.

### OQ-033 — Initial MVP Release Criteria

**Priority:** P1  
**Status:** Proposed  
**Resolve by:** Phase 8 — MVP Hardening and Demonstration  
**Related:** `docs/project/roadmap.md`, `docs/requirements/non-functional-requirements.md`

Which test results and documented behaviors are mandatory before the MVP is considered complete?

The release criteria should include financial invariants, idempotency, concurrency, authorization, repeatable setup, and known limitations.

## 11. Resolution Process

When an item is resolved:

1. Record the approved decision.
2. Update the item status to **Decided**.
3. Update all affected requirements, architecture, API, or operational documents.
4. Create or update an ADR when the decision has meaningful architectural consequences.
5. Add or update tests where the decision changes expected behavior.
6. Ensure the roadmap reflects any resulting dependency changes.

If an item is intentionally postponed, record the reason and the milestone by which it must be revisited.

## 12. Current Blocking Decisions

All P0 blocking decisions for the MVP are resolved. P1 decisions that are not yet Decided are listed below and must be resolved before the corresponding implementation phase. The following items remain open but are **not blocking** for the start of implementation:

- OQ-003 — Ledger reference model (P1; Phase 2).
- OQ-004 — Ledger cardinality enforcement (P1; Phase 2).
- OQ-005 — Database numeric constraints enforcement mechanism (P1; Phase 2).
- OQ-022 — User identity mapping (P1; Phase 3).
- OQ-010 — Idempotency retention (needed before Phase 4 hardening, not before first deposit).
- OQ-018 — Response fields and `Location` header behavior (needed before Phase 4, but the contract defaults are workable).
- OQ-021 — Production authentication (Deferred; not part of MVP).
- OQ-023 — Rate limiting (P2; not part of MVP).
- OQ-024 — Performance targets (Deferred; see `NFR-012`).
- OQ-027, OQ-028, OQ-029, OQ-030 — Hosting, RPO/RTO, observability stack, reconciliation procedure (P2/Deferred).
- OQ-031 — Frontend scope (Deferred; frontend out of MVP).
- OQ-033 — MVP release criteria (P1; defined at Phase 8, not before).

Resolved blocking decisions (for reference):

- OQ-001, OQ-002, OQ-006, OQ-007, OQ-008, OQ-009, OQ-011, OQ-012, OQ-013, OQ-014, OQ-015, OQ-016, OQ-017, OQ-019, OQ-020, OQ-032.

## 13. Related Documents

- `docs/requirements/overview.md`
- `docs/requirements/functional-requirements.md`
- `docs/requirements/non-functional-requirements.md`
- `docs/requirements/domain-rules.md`
- `docs/architecture/architecture.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/api/api-guidelines.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/security/security-model.md`
- `docs/security/threat-model.md`
- `docs/development/testing.md`
- `docs/operations/configuration.md`
- `docs/operations/observability.md`
- `docs/operations/recovery.md`
- `docs/project/roadmap.md`
