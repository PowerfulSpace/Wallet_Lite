# Project Constants

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `docs/project/constants.md`

## 1. Purpose

This document is the **single source of truth** for numeric and format constants that appear in more than one project document.

Other documents **reference** constants by name instead of restating their values. When a constant changes, it changes here first.

This document does not replace domain rules. Domain rules (`DR-###`) and functional requirements (`FR-###`) define **what** must be true. This document defines **the numbers** used to express and enforce those rules.

## 2. Conventions

- Prose uses thousands separators: `1,000,000.00`.
- Machine-readable contexts (JSON, SQL, code, regex, "Value" table cells) use no separators: `1000000.00`.
- Every constant has a stable ID `CONST-NNN` for reference from other documents.
- A constant whose value is not yet decided is listed with `TBD` and a link to the open question that blocks it.

## 3. Constants Register

| ID | Name | Value | Unit | Source ADR | Status |
|---|---|---|---|---|---|
| CONST-001 | Maximum wallet balance | `1000000.00` | USD | ADR-002 | Accepted |
| CONST-002 | Maximum operation amount (deposit, transfer) | `1000000.00` | USD | ADR-002 | Accepted |
| CONST-003 | Monetary precision (scale) | `2` | decimal places | ADR-002 | Accepted |
| CONST-004 | Monetary SQL type | `decimal(19,2)` | — | ADR-002 | Accepted |
| CONST-005 | Idempotency key format | `^[A-Za-z0-9_-]{8,128}$` | regex | ADR-005 | Accepted |
| CONST-006 | Idempotency key maximum length | `128` | characters | ADR-005 | Accepted |
| CONST-007 | Transaction history default page size | `20` | items | — | Decided |
| CONST-008 | Transaction history maximum page size | `100` | items | — | Decided |
| CONST-009 | Timestamp precision (SQL) | `datetime2(7)` | — | — | Decided |
| CONST-010 | Cursor format | `base64(JSON {createdAtUtc: ISO8601, id: GUID})` | — | — | Decided |
| CONST-011 | Bounded wait for in-progress idempotent operation | `3` | seconds | ADR-005 | Decided |
| CONST-012 | Idempotency lease duration | `30` | seconds | ADR-005 | Decided |
| CONST-013 | Username format | `^[A-Za-z0-9_]{3,32}$` | regex | — | Decided |
| CONST-014 | Idempotency in-progress polling interval | `100` | milliseconds | ADR-005 | Decided |
| CONST-015 | Idempotency `4xx` record retention | `0` (delete immediately after response) | seconds | ADR-005 | Decided |
| CONST-016 | Transaction isolation level (financial writes) | `READ COMMITTED` | — | ADR-004 | Decided |
| CONST-017 | Deadlock retry maximum attempts | `3` | attempts | ADR-004 | Decided |
| CONST-018 | Deadlock retry base delay | `100` | milliseconds | ADR-004 | Decided |
| CONST-019 | Deadlock retry exponential multiplier | `2` | × | ADR-004 | Decided |
| CONST-020 | Deadlock retry jitter | `±20%` | — | ADR-004 | Decided |
| CONST-021 | Wallet lock/update order | `Wallet.Id` ascending (Guid ordinal comparison) | — | ADR-004 | Decided |
| CONST-022 | Unknown recipient response policy | `404 RESOURCE_NOT_FOUND` (concealment, indistinguishable from inaccessible transfer) | — | — | Decided |
| CONST-024 | Development seed user: Alice email | `alice@example.com` | — | — | Decided |
| CONST-025 | Development seed user: Bob email | `bob@example.com` | — | — | Decided |
| CONST-026 | API timestamp format | ISO 8601 with explicit UTC, round-trip `O` format | — | — | Decided |
| CONST-027 | Ledger entry operation reference | Two nullable FKs (`DepositId`, `TransferId`); exactly one non-null (`CHECK`) | — | — | Decided |
| CONST-028 | Integration test database name | `Wallet_Test` | — | — | Decided |
| CONST-029 | Idempotency `2xx` record retention | `24` | hours | ADR-005 | Decided |

## 4. How to Add or Change a Constant

1. If the value is already used anywhere in the documentation, update this file **and** replace the duplicated value in the affected document with a reference to the constant ID or name.
2. If the value is not yet decided, add an entry with `TBD` and a link to the blocking `OQ-###`.
3. When a decision is made, update the `Value` and `Status` columns, and update the affected ADR from `Proposed` to `Accepted` (or note the decision in `decisions.md`).
4. Do not introduce a numeric constant in a document without registering it here.

## 5. Related Documents

- `docs/project/decisions.md`
- `docs/project/open-questions.md`
- `docs/project/known-issues.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-005-idempotency.md`
