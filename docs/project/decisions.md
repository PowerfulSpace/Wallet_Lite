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
| DEC-001 | Architecture style | Clean Architecture-inspired modular monolith | Proposed | `docs/adr/ADR-001-architecture-style.md` |
| DEC-002 | Monetary precision | C# `decimal`, SQL Server `decimal(19,2)`, and validated monetary strings in the API | Proposed | `docs/adr/ADR-002-money-and-precision.md` |
| DEC-003 | Transaction atomicity | Financial changes and the successful idempotency result commit atomically | Proposed | `docs/adr/ADR-003-transaction-atomicity.md` |
| DEC-004 | Concurrency control | Atomic balance updates protected against concurrent operations | Proposed | `docs/adr/ADR-004-concurrency-control.md` |
| DEC-005 | Idempotency | Require `Idempotency-Key` for deposits and transfers | Proposed | `docs/adr/ADR-005-idempotency.md` |
| DEC-006 | Ledger model | Maintain the current wallet balance alongside an append-only ledger | Proposed | `docs/adr/ADR-006-ledger-model.md` |
| DEC-007 | Development authentication | Provide simplified development authentication that is unavailable in Production | Proposed | `docs/adr/ADR-007-development-authentication.md` |

## 4. Summary of Proposed Decisions

### DEC-001: Architecture Style

The application is proposed as a modular monolith organized around Domain, Application, Infrastructure, and API responsibilities.

The architecture aims to keep business rules independent of infrastructure concerns and support focused unit and integration testing.

Microservices are outside the current MVP scope.

**Open considerations:** Final project boundaries, dependency enforcement, and the need for additional libraries.

### DEC-002: Monetary Precision

Financial values are proposed to use:

- C# `decimal` for monetary calculations.
- SQL Server `decimal(19,2)` for persisted monetary values.
- Decimal strings in JSON API contracts.
- Explicit validation that rejects amounts with more than two fractional decimal digits.
- A maximum operation amount and wallet balance of USD 1,000,000.00.

The implementation must not silently round invalid input.

**Open considerations:** Exact database constraints and consistent validation across the API and database layers.

### DEC-003: Transaction Atomicity

A successful deposit or transfer must persist all related financial changes in one database transaction.

For transfers, the sender debit, recipient credit, transfer record, ledger entries, and successful idempotency result must commit atomically.

A partial financial operation must not be visible as a successful completed operation.

**Open considerations:** Transaction ownership, isolation configuration, and behavior when the client cannot determine whether a commit succeeded.

### DEC-004: Concurrency Control

Concurrent operations must preserve wallet balance and ledger invariants.

The proposal uses atomic database updates with appropriate conditions and transaction protection rather than relying on an unprotected read-then-update sequence.

**Open considerations:** Isolation level, lock ordering, deadlock handling, and retry policy.

### DEC-005: Idempotency

Deposit and transfer requests must include an `Idempotency-Key`.

The proposed behavior is:

- The same key and same request replay the original successful result.
- The same key with a different request produces a conflict.
- Concurrent requests using the same key must not produce duplicate financial effects.
- The idempotency result must be committed atomically with the successful operation.
- Failed attempts must not permanently prevent a valid retry unless an explicitly documented rule requires otherwise.

**Open considerations:** Key retention, request fingerprinting, concurrent key reservation, and recovery after an uncertain commit.

### DEC-006: Ledger Model

The proposed model stores both the current wallet balance and an append-only ledger.

A deposit creates one positive ledger entry. A transfer creates two entries of equal magnitude and opposite signs.

Normal application operations must not edit or delete historical ledger entries. Corrections should use compensating transactions.

**Open considerations:** Initial balance policy, ledger initialization, operation references, and enforcement of ledger cardinality.

### DEC-007: Development Authentication

The project proposes simplified development authentication for pre-seeded users Alice and Bob.

Development authentication must not be available in Production. The backend remains responsible for validating the authenticated identity and enforcing resource ownership.

**Open considerations:** The development authentication mechanism, production identity provider, session model, and startup safeguards.

## 5. Relationship to Open Questions

The decision register must be maintained alongside `docs/project/open-questions.md`.

An unresolved question may block acceptance of a proposed decision or require an explicit limitation in its ADR.

Important questions to resolve before implementing financial write operations include:

1. What is the initial wallet balance and how is it represented in the ledger?
2. How are concurrent balance updates and deadlocks handled?
3. How are concurrent requests with the same idempotency key coordinated?
4. What is the exact behavior after an uncertain database commit?
5. Which database constraints enforce monetary and ledger invariants?
6. What is the final request and response contract for deposits and transfers?

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