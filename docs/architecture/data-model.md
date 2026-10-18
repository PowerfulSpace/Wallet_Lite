# Data Model

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `docs/architecture/data-model.md`

## 1. Purpose

This document defines the proposed logical data model for the Wallet API MVP.

It describes the core entities, their relationships, persistence constraints, and the consistency requirements that must be preserved by the database and application.

This is a logical model, not a final Entity Framework Core mapping or SQL migration specification. Physical table names, index definitions, column mappings, and provider-specific details may be refined during implementation without weakening the documented invariants.

## 2. Design Principles

The data model follows these principles:

1. **Explicit monetary precision.** Monetary values use .NET `decimal` and SQL Server `decimal(19,2)`. Floating-point types are prohibited for money.
2. **Current state and financial history.** A wallet stores its current balance, while an append-only ledger records the financial effects of successful operations.
3. **Atomic financial writes.** A successful operation persists all related financial changes in one database transaction.
4. **Database-enforced integrity.** Unique constraints, foreign keys, and appropriate check constraints protect critical invariants wherever practical.
5. **Traceability.** Every ledger entry references the financial operation that produced it.
6. **Explicit ownership.** Each user owns exactly one wallet.
7. **Safe retries.** Idempotency records prevent repeated requests from producing duplicate financial effects.
8. **No silent correction.** Existing ledger entries are not edited or deleted during normal business operation. Corrections, if needed in the future, use compensating transactions.
9. **Timestamp precision and format.** All persisted UTC timestamps use SQL Server `datetime2(7)` (`CONST-009`). API timestamps use ISO 8601 with explicit UTC (`CONST-026`). The application generates timestamps server-side; clients never supply them.

## 3. Entity Overview

The proposed model contains the following core entities:

| Entity | Purpose |
|---|---|
| `User` | Represents an application user and their unique username. |
| `WalletAccount` | Stores the current balance for one user. |
| `Deposit` | Records a successful virtual deposit. |
| `Transfer` | Records a successful transfer between two wallets. |
| `LedgerEntry` | Records an individual signed monetary effect on a wallet. |
| `IdempotencyRecord` | Stores request identity and the successful result required for safe replay. |

The exact mapping between entities and tables is an implementation decision. The model must preserve the relationships and constraints described below.

## 4. User

The `User` entity represents an application identity that can own a wallet.

### Proposed attributes

| Attribute | Type / constraint | Description |
|---|---|---|
| `Id` | Primary key | Stable internal user identifier. |
| `Username` | Required, unique | Username used to identify a transfer recipient. |

Additional authentication-related fields are not defined here. Credential storage, if introduced, belongs to the security design.

### Invariants

- Each user has one stable internal identifier.
- Usernames are unique according to the normalization policy below.
- A recipient lookup must resolve to at most one user.
- A client's supplied username must never determine the transfer sender.

### Username Normalization Policy

- **Accepted input format:** `^[A-Za-z0-9_]{3,32}$` after trimming leading and trailing whitespace.
- **Stored form:** lowercased (`alice`, `bob`). The database stores only the canonical lowercase form.
- **Lookup:** case-insensitive, by comparing the lowercased input against the stored canonical form.
- **Uniqueness:** enforced by a unique constraint on the stored canonical form.
- **Rejection:** inputs failing the format check are rejected with `400 VALIDATION_ERROR` before any database lookup.
- **No Unicode normalization** is applied in the MVP. Only ASCII letters, digits, and underscore are accepted.

The canonical lowercase form is what the request fingerprint uses (see `ADR-005 §5`). See `CONST-013` in `docs/project/constants.md`.

## 5. Wallet

The C# entity class is named `WalletAccount` to avoid a name collision with the root namespace `PS.Wallet`. The database table remains `Wallets`.

The `WalletAccount` entity represents the current monetary state of a user's wallet.

### Proposed attributes

| Attribute | Type / constraint | Description |
|---|---|---|
| `Id` | Primary key | Stable wallet identifier. |
| `UserId` | Required, unique foreign key | Owner of the wallet. |
| `Balance` | `decimal(19,2)`, required | Current available balance in USD. |
| `CreatedAtUtc` | UTC timestamp | Wallet creation time. |
| `UpdatedAtUtc` | UTC timestamp | Time of the most recent balance change, where maintained. |

### Invariants

- Every user has exactly one wallet.
- A wallet belongs to exactly one user.
- A wallet balance must never be negative.
- A wallet balance must not exceed `$1,000,000.00`.
- All balance changes must be associated with a valid financial operation and its ledger effects.
- The initial wallet balance is `0.00` and does not require a ledger entry (`DR-047`).
- Any non-zero starting state must be accompanied by an explicit ledger entry.

A unique constraint on `WalletAccount.UserId` enforces at most one wallet per user. The process that creates users or initializes wallets must ensure that every supported user receives a wallet, thereby enforcing the one-wallet-per-user requirement operationally.

**Initial balance rule:** newly initialized wallets start at `0.00` with no opening ledger entry. Any non-zero starting balance must be represented by an explicit, auditable ledger entry. The reconciliation invariant (`Balance = SUM(LedgerEntry.Amount)`) holds under this rule.

## 6. Deposit

The `Deposit` entity records a successful virtual top-up of a wallet.

### Proposed attributes

| Attribute | Type / constraint | Description |
|---|---|---|
| `Id` | Primary key | Deposit identifier. |
| `WalletId` | Required foreign key | Wallet receiving the deposit. |
| `Amount` | `decimal(19,2)`, required | Positive deposit amount. |
| `CreatedAtUtc` | UTC timestamp | Time the deposit was created. |

### Invariants

- The amount is strictly greater than zero.
- The amount has no more than two fractional decimal digits.
- The amount does not exceed `$1,000,000.00`.
- The resulting wallet balance does not exceed `$1,000,000.00`.
- A successful deposit produces exactly one corresponding positive ledger entry.
- A deposit is a virtual top-up, not a transfer from another user.
- The deposit record, wallet balance change, ledger entry, and successful idempotency result commit atomically.

Failed deposits must not remain as successful financial records or leave a partial balance change.

## 7. Transfer

The `Transfer` entity represents one successful transfer between two wallets.

### Proposed attributes

| Attribute | Type / constraint | Description |
|---|---|---|
| `Id` | Primary key | Transfer identifier. |
| `SenderWalletId` | Required foreign key | Wallet debited by the transfer. |
| `RecipientWalletId` | Required foreign key | Wallet credited by the transfer. |
| `Amount` | `decimal(19,2)`, required | Positive transfer amount. |
| `CreatedAtUtc` | UTC timestamp | Time the transfer was created. |

### Invariants

- The sender and recipient wallets must be different.
- The amount is strictly greater than zero.
- The amount has no more than two fractional decimal digits.
- The amount does not exceed `$1,000,000.00`.
- The sender must have sufficient funds.
- The recipient's resulting balance must not exceed `$1,000,000.00`.
- A successful transfer produces exactly two ledger entries: one negative sender entry and one positive recipient entry.
- The absolute values of those two entries must equal the transfer amount.
- The sum of the transfer's ledger entries must equal zero.
- The transfer, both wallet balance changes, both ledger entries, and successful idempotency result commit atomically.

A failed transfer must not debit only one wallet, credit only one wallet, or persist a successful transfer record without its corresponding ledger entries.

## 8. Ledger Entry

The `LedgerEntry` entity records the financial effect of an operation on a particular wallet.

The ledger is append-only during normal operation. Existing entries must not be updated or deleted to correct a balance.

### Proposed attributes

| Attribute | Type / constraint | Description |
|---|---|---|
| `Id` | Primary key | Ledger entry identifier. |
| `WalletId` | Required foreign key | Wallet affected by the entry. |
| `DepositId` | Nullable foreign key | Deposit responsible for the entry, when applicable. Exactly one of `DepositId` or `TransferId` is non-null. |
| `TransferId` | Nullable foreign key | Transfer responsible for the entry, when applicable. Exactly one of `DepositId` or `TransferId` is non-null. |
| `Amount` | `decimal(19,2)`, required | Signed monetary effect on the wallet. |
| `CreatedAtUtc` | `datetime2(7)`, required | Time the entry was created. |

### Entry semantics

The signed amount is interpreted from the wallet owner's perspective:

- Deposit: positive amount.
- Transfer received: positive amount.
- Transfer sent: negative amount.

### Invariants

- Each entry belongs to exactly one wallet.
- Each entry references exactly one supported financial operation: either a deposit or a transfer, but not both.
- The database enforces the exactly-one rule with a `CHECK` constraint:
  `(DepositId IS NOT NULL AND TransferId IS NULL) OR (DepositId IS NULL AND TransferId IS NOT NULL)`.
- Both FKs are `ON DELETE RESTRICT`; ledger entries are never cascade-deleted.
- A deposit has exactly one ledger entry.
- A transfer has exactly two ledger entries.
- A transfer's sender entry is negative and its recipient entry is positive.
- The two transfer entries have equal absolute values.
- Every entry amount uses `decimal(19,2)`.
- Ledger entries are not modified or deleted during normal application operation.

The database can enforce the foreign keys and the rule that an entry references one operation rather than both.

### Cardinality Enforcement

The database enforces only that each entry references exactly one operation (`DepositId` xor `TransferId`). The cardinality rules — one entry per deposit, two entries per transfer — are enforced by the application inside the same transaction that creates the operation and its entries.

Reasons for application-level enforcement:

- SQL Server has no declarative way to enforce "exactly N child rows per parent" without triggers.
- Triggers are outside the MVP scope (see `ADR-006 §7` on ledger immutability and scope).
- The transaction boundary already guarantees atomicity: if the application writes the wrong number of entries, the operation and its entries are rolled back together only if the application detects the error. Therefore the application must validate the count before committing.

Integration tests must verify:

- Every `Deposit` has exactly one `LedgerEntry` after commit.
- Every `Transfer` has exactly two `LedgerEntry` rows after commit (one negative, one positive, equal magnitude).
- A failed transaction leaves no operation without its required entries and no orphan entries.

## 9. Idempotency Record

The `IdempotencyRecord` entity stores the information required to recognize repeated requests and replay the original successful result.

### Proposed attributes

| Attribute | Type / constraint | Description |
|---|---|---|
| `Id` | Primary key | Idempotency record identifier. |
| `UserId` | Required foreign key | User who initiated the request. |
| `OperationType` | Required | Type of operation, such as deposit or transfer. |
| `IdempotencyKey` | Required | Client-supplied idempotency key. |
| `RequestFingerprint` | Required | Stable representation or hash of the normalized request. |
| `OperationId` | Nullable reference | Identifier of the resulting deposit or transfer, if applicable. |
| `ResponseStatusCode` | Required for completed success | HTTP status code to replay. |
| `ResponsePayload` | Required for completed success | Stored response representation needed for replay. |
| `CreatedAtUtc` | UTC timestamp | Record creation time. |

The schema may require additional fields for the chosen implementation, including a response content type or an explicit completion state. Those fields must not permit a partially completed financial operation to be mistaken for a successful one.

### Idempotency scope

The logical uniqueness scope is:

`(UserId, OperationType, IdempotencyKey)`

A unique database constraint must protect this scope against concurrent requests.

The `RequestFingerprint` column stores `SHA-256` of the canonical request bytes defined in `ADR-005 §5`, as `varbinary(32)`. It is not a string and must not be compared case-insensitively.

### Invariants

- The same user, operation type, and key cannot create multiple independent successful financial operations.
- A matching key with an equivalent normalized request replays the original successful result.
- Reuse of the same scoped key with a different request fingerprint returns a conflict.
- A successful idempotency result is committed atomically with the financial operation.
- A failed operation must not permanently reserve a key in a way that prevents a valid retry, according to the documented failure-handling rules.
- Concurrent requests using the same scoped key must be resolved safely using database-enforced uniqueness and appropriate transaction handling.

The exact key length, allowed characters, request normalization, fingerprint algorithm, response serialization, and retention policy remain to be specified in the API and idempotency design.

## 10. Entity Relationships

The principal relationships are:

- `User` 1 — 1 `WalletAccount`
- `WalletAccount` 1 — many `Deposit`
- `WalletAccount` 1 — many `LedgerEntry`
- `WalletAccount` 1 — many outgoing `Transfer`
- `WalletAccount` 1 — many incoming `Transfer`
- `Deposit` 1 — 1 `LedgerEntry`
- `Transfer` 1 — 2 `LedgerEntry`
- `User` 1 — many `IdempotencyRecord`

The one-to-one relationship between `Deposit` and `LedgerEntry`, and the requirement for exactly two ledger entries per transfer, represent business invariants. Ordinary foreign keys alone do not fully enforce these cardinalities.

## 11. Money Representation

All persisted monetary amounts use SQL Server `decimal(19,2)` and application-level .NET `decimal`.

The model must reject amounts that have more than two fractional decimal digits. It must not silently round input values to fit the storage scale.

For API requests, the agreed representation is a JSON string with a decimal point, such as `"50.00"`. Parsing and validation must occur before any financial state is changed.

The supported wallet balance range is:

`0.00` through `1,000,000.00` USD, inclusive.

The maximum individual deposit or transfer amount is `1,000,000.00` USD, inclusive, subject to the resulting balance constraints.

## 12. Balance and Ledger Consistency

The wallet balance is maintained as current state for efficient reads. The ledger records the history of financial effects.

For a wallet initialized at zero, the expected reconciliation relationship is:

`Wallet.Balance = SUM(LedgerEntry.Amount)`

This relationship applies when the ledger contains all financial effects that contribute to the wallet balance and no unrecorded initialization or adjustment exists.

If non-zero initial balances or future administrative adjustments are introduced, they must be represented by an explicit auditable operation and ledger entry.

The application must update the wallet balance and create the corresponding ledger entries in the same transaction. A successful operation must never leave one updated without the other.

A reconciliation mechanism may be added to detect discrepancies. Its implementation and operational response are outside the scope of this logical model.

## 13. Referential Integrity and Constraints

The database design should include, where applicable:

- Primary keys for every entity.
- Foreign keys for all entity relationships.
- A unique constraint on `WalletAccount.UserId`.
- A unique constraint on `User.Username`.
- A unique constraint on the idempotency scope.
- Check constraints preventing negative wallet balances and balances above the maximum.
- Check constraints for positive deposit and transfer amounts.
- A constraint preventing a transfer from referencing the same wallet as both sender and recipient.
- A `CHECK` constraint ensuring a ledger entry references exactly one supported operation (`DepositId` xor `TransferId`).
- FK indexes on `LedgerEntry.WalletId`, `LedgerEntry.DepositId`, `LedgerEntry.TransferId` for query performance and FK integrity checks.
- Appropriate indexes for wallet ownership, operation lookup, history pagination, and idempotency lookup.

Database constraints should provide defense in depth. Invariants that span multiple rows, such as the complete set of ledger entries for a transfer, require deliberate transaction handling and verification.

### Numeric Check Constraints

The database enforces the following numeric constraints as a defense-in-depth layer behind application validation:

| Table | Constraint | Purpose |
|---|---|---|
| `Wallets` | `Balance >= 0 AND Balance <= 1000000.00` | Prevent negative and over-limit balances |
| `Deposits` | `Amount > 0 AND Amount <= 1000000.00` | Prevent non-positive and over-limit deposits |
| `Transfers` | `Amount > 0 AND Amount <= 1000000.00` | Prevent non-positive and over-limit transfers |
| `LedgerEntries` | `Amount <> 0 AND ABS(Amount) <= 1000000.00` | Prevent zero and over-limit ledger entries |

The following invariants are **not** enforced by `CHECK` constraints:

- **"At most 2 fractional digits":** `decimal(19,2)` rounds silently. The application must reject invalid input before persistence (see `ADR-002 §3`, `DR-003`).
- **"Recipient balance after credit <= 1000000.00":** cross-row and time-sensitive. Enforced by conditional `UPDATE` inside the transaction (see `ADR-004 §3`).
- **"Sum of transfer ledger entries == 0":** cross-row. Enforced by application logic and verified by integration tests.

## 14. Transaction History

The transaction history API presents a wallet owner's financial activity in a unified chronological view.

The underlying ledger provides the signed monetary effects, while deposit and transfer records provide operation-specific details.

The API exposes the agreed history types:

- `deposit`
- `transfer_in`
- `transfer_out`

The default order is newest first, with cursor pagination based on `(CreatedAtUtc, Id)` to provide deterministic ordering when timestamps are equal.

History queries must be restricted to the authenticated user's wallet. Database query and index design must support this ownership restriction and pagination efficiently.

The final public response shape is defined in `docs/api/endpoints.md`.

## 15. Open Data-Model Decisions

The following items require confirmation or more detailed design:

- Whether the proposed `$0.00` initial wallet balance is accepted. (resolved in OQ-001)
- Exact SQL types and timestamp precision for UTC timestamps. (resolved in CONST-009, CONST-026)
- Username normalization and case-sensitivity rules. (resolved in OQ-002)
- Exact idempotency schema, response serialization, and retention policy. (resolved in OQ-010, OQ-018, ADR-005)
- The migration and deployment approach for schema changes. (resolved in OQ-025, OQ-027)
- Whether audit metadata beyond financial records is required for the MVP. (unique to this document)

These decisions may refine the physical schema but must preserve the core financial invariants.

## 16. Related Documents

- `docs/architecture/system-context.md`
- `docs/architecture/architecture.md`
- `docs/architecture/concurrency.md`
- `docs/requirements/domain-rules.md`
- `docs/api/endpoints.md`
- `docs/security/security-model.md`
- `docs/development/database-migrations.md`
- `docs/development/testing.md`
- `docs/adr/ADR-002-money-and-precision.md`
- `docs/adr/ADR-003-transaction-atomicity.md`
- `docs/adr/ADR-005-idempotency.md`
- `docs/adr/ADR-006-ledger-model.md`
