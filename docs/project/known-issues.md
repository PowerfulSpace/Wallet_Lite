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

_No known issues recorded yet._

## 4. Related Documents

- `docs/project/open-questions.md`
- `docs/project/decisions.md`
- `docs/project/constants.md`
- `docs/project/roadmap.md`
