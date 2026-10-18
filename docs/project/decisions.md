# Project Decisions Register

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document is the central register of significant decisions made or proposed during the development of the Wallet API project.

It provides a concise overview of architectural, domain, security, and technical decisions and links to their detailed Architecture Decision Records (ADRs).

This document is an index and status register. Detailed context, alternatives, rationale, and consequences belong in the corresponding ADR.

**Important:** A decision marked `Proposed` has not been formally accepted. Its presence in this register does not mean the implementation is finalized.

## 2. Decision Statuses

| Status | Meaning |
|---|---|
| Proposed | Suggested but not formally accepted |
| Accepted | Reviewed and approved for implementation |
| Deprecated | No longer recommended, but retained for historical context |
| Superseded | Replaced by a later decision |

A decision SHOULD be marked `Accepted` only after the relevant trade-offs and outstanding questions have been reviewed.

## 3. Decision Register

| ID | Decision Area | Current Proposal | Status | Detailed Record |
|---|---|---|---|---|
| DEC-001 | Architecture style | Clean Architecture-inspired modular monolith | Accepted | `docs/adr/ADR-001-architecture-style.md` |
| DEC-002 | Monetary precision | C# `decimal`, SQL Server `decimal(19,2)`, and validated monetary strings in the API | Accepted | `docs/adr/ADR-002-money-and-precision.md` |
| DEC-003 | Transaction atomicity | Financial changes and the successful idempotency result commit atomically | Accepted | `docs/adr/ADR-003-transaction-atomicity.md` |
| DEC-004 | Concurrency control | Atomic balance updates protected against concurrent operations | Accepted | `docs/adr/ADR-004-concurrency-control.md` |
| DEC-005 | Idempotency | Require `Idempotency-Key` for deposits and transfers | Accepted | `docs/adr/ADR-005-idempotency.md` |
| DEC-006 | Ledger model | Maintain the current wallet balance alongside an append-only ledger | Accepted | `docs/adr/ADR-006-ledger-model.md` |
| DEC-007 | Development authentication | Provide simplified development authentication that is unavailable in Production | Accepted | `docs/adr/ADR-007-development-authentication.md` |
| DEC-008 | Project constants | Numeric and format constants tracked in a single file | Accepted | `docs/project/constants.md` |

## 4. Decision Summaries

### Accepted Decisions

DEC-001 (architecture style) is accepted and recorded in `docs/adr/ADR-001-architecture-style.md`: a Clean Architecture-inspired modular monolith organized around Domain, Application, Infrastructure, and API responsibilities, with microservices outside the current MVP scope.

DEC-002 (monetary precision) is accepted and recorded in `docs/adr/ADR-002-money-and-precision.md`: C# `decimal` for monetary calculations, SQL Server `decimal(19,2)` for persisted values, validated monetary strings at the API boundary, and explicit rejection of amounts with more than two fractional decimal digits.

DEC-003 (transaction atomicity) is accepted and recorded in `docs/adr/ADR-003-transaction-atomicity.md`: a successful deposit or transfer must persist all related financial changes in one database transaction, with the sender debit, recipient credit, transfer record, ledger entries, and successful idempotency result committing atomically.

DEC-004 (concurrency control) is accepted and recorded in `docs/adr/ADR-004-concurrency-control.md`: conditional balance updates (`WHERE Balance >= @Amount`, `WHERE Balance + @Amount <= @Max`) under `READ COMMITTED` isolation (`CONST-016`), deterministic `Wallet.Id` lock ordering (`CONST-021`), and a bounded deadlock-retry policy (`CONST-017`–`CONST-020`).

### DEC-005: Idempotency

Deposit and transfer requests must include an `Idempotency-Key`. The same key and the same request replay the original successful result; the same key with a different request produces a conflict; concurrent requests using the same key must not produce duplicate financial effects; the idempotency result is committed atomically with the successful operation; and failed attempts do not permanently prevent a valid retry unless an explicitly documented rule requires otherwise.

DEC-005 — **Status:** Accepted. All MVP decisions resolved (OQ-010, OQ-018, CONST-005/006). Post-MVP: KI-008 (cleanup).

### DEC-006: Ledger Model

The model stores both the current wallet balance and an append-only ledger. A deposit creates one positive ledger entry; a transfer creates two entries of equal magnitude and opposite signs. Normal application operations must not edit or delete historical ledger entries; corrections use compensating transactions.

DEC-006 — **Status:** Accepted. All MVP decisions resolved (OQ-030 reconciliation, OQ-018 response fields). Post-MVP: audit metadata (unique to this document).

### DEC-007: Development Authentication

Development authentication provides pre-seeded users Alice and Bob in Development and must not be available in Production. The backend remains responsible for validating the authenticated identity and enforcing resource ownership.

DEC-007 — **Status:** Accepted. Mechanism resolved (ADR-007 §4.1). Production auth (OQ-021) deferred.

## 5. Relationship to Open Questions

The decision register must be maintained alongside `docs/project/open-questions.md`.

An unresolved question may block acceptance of a proposed decision or require an explicit limitation in its ADR.

See `docs/project/open-questions.md §12 Current Blocking Decisions` for the authoritative list of blocking questions.

The open-questions document remains the detailed list of unresolved items; this register should not duplicate it.

## 6. How to Record a New Decision

A new significant architectural or technical decision SHOULD follow this workflow:

1. Identify the problem or constraint.
2. Review relevant requirements and open questions.
3. Consider reasonable alternatives.
4. Document the selected approach and its rationale in a new ADR.
5. Record important consequences and trade-offs.
6. Assign a unique decision ID.
7. Add the decision and its status to this register.
8. Update related documents when necessary.

New ADRs SHOULD use sequential identifiers, such as `ADR-008`, and decisions SHOULD use sequential register identifiers, such as `DEC-008`.

The decision ID identifies the register entry; the ADR number identifies the detailed decision record. Both identifiers should remain stable.

## 7. Updating Existing Decisions

When a proposal is accepted, update its status in both the ADR and this register.

When a decision is materially changed:

- Preserve the historical ADR.
- Create a new ADR describing the replacement where appropriate.
- Mark the previous decision as `Superseded`.
- Link the old and new records.
- Update the register and affected documentation.

Minor editorial corrections do not require a new ADR. Material changes to business invariants, data consistency, security boundaries, or API behavior should be reviewed explicitly.

## 8. Decision Acceptance Criteria

Before accepting a decision, reviewers SHOULD confirm that:

- It addresses a documented requirement or problem.
- Important alternatives and trade-offs have been considered.
- It does not violate established domain invariants.
- Its security and data-integrity implications are understood.
- Related open questions have been resolved or explicitly deferred.
- Required implementation and testing work is identified.
- Related documentation is consistent with the proposed outcome.

## 9. Related Documents

- `docs/project/open-questions.md`
- `docs/project/roadmap.md`
- `docs/project/glossary.md`
- `docs/requirements/overview.md`
- `docs/requirements/domain-rules.md`
- `docs/architecture/architecture.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/api/endpoints.md`
- `docs/security/security-model.md`
- `docs/adr/ADR-001-architecture-style.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/adr/ADR-004-concurrency-control.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/adr/ADR-006-ledger-model.md`
- `docs/adr/ADR-007-development-authentication.md`