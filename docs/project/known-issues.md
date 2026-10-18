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

- **Status:** Open
- **Affects:** `docs/adr/ADR-005`, `docs/adr/ADR-006`, `docs/adr/ADR-007`, `docs/api/api-guidelines.md` (partially labeled), `docs/architecture/architecture.md`, `docs/architecture/concurrency.md`, `docs/architecture/data-model.md`, `docs/architecture/system-context.md`, `docs/development/database-migrations.md`, `docs/operations/configuration.md`, `docs/operations/observability.md`, `docs/operations/recovery.md`, `docs/project/roadmap.md`, `docs/requirements/domain-rules.md`, `docs/requirements/functional-requirements.md`, `docs/requirements/non-functional-requirements.md`, `docs/requirements/overview.md`
- **Description:** Five high-traffic documents (`docs/development/setup.md`, `docs/development/testing.md`, `docs/development/coding-standards.md`, `docs/security/security-model.md`, `docs/security/threat-model.md`) have labeled `Open Decisions` entries as of this revision. Fifteen other documents still have unlabeled entries, which may duplicate or diverge from `docs/project/open-questions.md`.
- **Risk:** A future reader may treat an unlabeled document-local open decision as authoritative when it is already resolved in `open-questions.md`, or vice versa.
- **Mitigation:** The five most-referenced documents are labeled. The remaining fifteen are explicitly deferred and should be labeled before the corresponding implementation phase begins.
- **Revisit by:** Before Phase 6 (Transaction History and API Consistency), or earlier if a specific document becomes the subject of an ADR.

## 4. Related Documents

- `docs/project/open-questions.md`
- `docs/project/decisions.md`
- `docs/project/constants.md`
- `docs/project/roadmap.md`
