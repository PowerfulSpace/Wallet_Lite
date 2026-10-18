# Domain Rules

**Document ID:** REQ-DOMAIN  
**Status:** Draft  
**Version:** 0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the domain rules and business invariants governing the Wallet API MVP.

These rules establish the conditions under which wallet operations are valid and the invariants that must remain true before, during, and after monetary operations.

The rules are independent of HTTP transport and should remain valid regardless of the specific application-layer or persistence implementation.

## 2. Domain Concepts

### 2.1. User

A user is an identity that owns a wallet and may initiate or receive internal transfers.

Each user is identified by a unique username within the system.

### 2.2. Wallet

A wallet represents a user's virtual monetary balance.

Each user has exactly one wallet. Each wallet holds a balance denominated in USD.

### 2.3. Deposit

A deposit is a virtual operation that increases the balance of one wallet.

A deposit does not represent a transfer from another wallet or a payment processed by an external financial institution.

### 2.4. Transfer

A transfer moves virtual value from one user's wallet to another user's wallet.

A successful transfer has exactly one sender, one recipient, and one positive transfer amount.

### 2.5. Ledger Entry

A ledger entry records the financial effect of a successful monetary operation on a wallet.

Entries are signed from the perspective of the wallet to which they belong:

- Positive amounts increase the wallet balance.
- Negative amounts decrease the wallet balance.

### 2.6. Operation Record

An operation record identifies a successfully executed deposit or transfer and provides the business context for its financial effects.

### 2.7. Idempotency Key

An idempotency key identifies a logical request within its defined scope and allows the system to recognize retries without repeating committed financial effects.

## 3. Monetary Rules

### DR-001 — Supported Currency

The MVP supports USD only.

### DR-002 — Monetary Representation

Monetary amounts MUST use decimal arithmetic and be persisted using a database representation with two fractional decimal places.

Binary floating-point types MUST NOT be used for monetary values.

### DR-003 — Monetary Scale

An accepted monetary amount MUST have no more than two fractional decimal places.

The system MUST reject amounts with greater precision rather than silently rounding them.

### DR-004 — Positive Operation Amount

Deposit and transfer amounts MUST be greater than zero.

### DR-005 — Maximum Operation Amount

A single deposit or transfer MUST NOT exceed USD 1,000,000.00.

### DR-006 — Maximum Wallet Balance

A wallet balance MUST NOT exceed USD 1,000,000.00.

### DR-007 — Exact Monetary Effects

Successful operations MUST apply exactly the accepted amount without an additional rounding step.

## 4. Wallet Invariants

### DR-008 — Wallet Ownership

Each wallet MUST belong to exactly one user.

### DR-009 — Single Wallet

Each user MUST have exactly one wallet within the MVP.

### DR-010 — Non-Negative Balance

A wallet balance MUST never be negative.

### DR-011 — Balance Upper Bound

A wallet balance MUST never exceed USD 1,000,000.00.

### DR-012 — Authorized Balance Mutation

Only a valid, successfully committed monetary operation may change a wallet balance.

### DR-013 — Consistency of Balance and Financial Records

A successful monetary operation MUST leave the wallet balance consistent with its corresponding financial records.

## 5. Deposit Rules

### DR-014 — Deposit Target

A deposit increases the balance of the authenticated user's wallet.

### DR-015 — Deposit Balance Calculation

For a successful deposit:

`NewBalance = PreviousBalance + DepositAmount`

### DR-016 — Deposit Acceptance

A deposit is valid only if:

- The amount is positive.
- The amount has no more than two fractional decimal places.
- The amount does not exceed the maximum operation amount.
- The resulting wallet balance does not exceed the maximum wallet balance.
- The operation satisfies the applicable authorization and idempotency rules.

### DR-017 — Deposit Financial Records

A successful deposit MUST create:

- One deposit operation record.
- One positive ledger entry associated with the affected wallet.

The ledger entry amount MUST equal the deposit amount.

### DR-018 — Deposit Atomicity

The balance increase, deposit operation record, ledger entry, and successful idempotency result MUST be committed atomically.

A failed deposit MUST NOT leave any of these financial effects partially committed.

## 6. Transfer Rules

### DR-019 — Transfer Participants

A transfer MUST have one sender and one recipient.

Both participants MUST be existing users with wallets.

### DR-020 — No Self-Transfer

The sender and recipient MUST be different users.

### DR-021 — Positive Transfer Amount

A transfer amount MUST be positive and comply with the monetary limits.

### DR-022 — Sufficient Available Balance

A transfer MUST be rejected if the sender's wallet balance is less than the transfer amount at the point the operation is safely validated and applied.

### DR-023 — Recipient Balance Limit

A transfer MUST be rejected if the recipient's resulting balance would exceed USD 1,000,000.00.

### DR-024 — Sender Balance Calculation

For a successful transfer:

`SenderNewBalance = SenderPreviousBalance - TransferAmount`

### DR-025 — Recipient Balance Calculation

For a successful transfer:

`RecipientNewBalance = RecipientPreviousBalance + TransferAmount`

### DR-026 — Equal Transfer Effects

The amount debited from the sender MUST equal the amount credited to the recipient.

### DR-027 — Transfer Financial Records

A successful transfer MUST create:

- One transfer operation record.
- One negative ledger entry for the sender.
- One positive ledger entry for the recipient.

The two ledger entries MUST reference the same transfer operation.

### DR-028 — Transfer Conservation

For each successful transfer:

`SenderLedgerAmount + RecipientLedgerAmount = 0`

### DR-029 — Transfer Atomicity

The sender debit, recipient credit, transfer record, both ledger entries, and successful idempotency result MUST be committed atomically.

If any required part of the operation fails, none of its financial effects may remain committed.

## 7. Ledger Rules

### DR-030 — Append-Only History

Ledger entries MUST be append-only during ordinary application operation.

Existing entries MUST NOT be edited or deleted to correct a previous monetary operation.

### DR-031 — Operation Association

Every ledger entry MUST be associated with the monetary operation that produced it.

### DR-032 — Deposit Entry Cardinality

A successful deposit MUST produce exactly one ledger entry.

### DR-033 — Transfer Entry Cardinality

A successful transfer MUST produce exactly two ledger entries: one debit and one credit.

### DR-034 — Ledger and Balance Reconciliation

The system's persisted balances and ledger history MUST remain consistent according to the agreed balance initialization and ledger model.

The precise reconciliation mechanism and treatment of any initial balance are defined in the data-model and architecture documentation.

### DR-035 — Corrections

If financial corrections are introduced in a future version, they SHOULD be represented through new compensating operations rather than modifications to historical ledger entries.

## 8. Idempotency Rules

### DR-036 — Idempotency Scope

An idempotency key MUST be scoped by the acting user and operation type.

### DR-037 — Equivalent Retry

A retry using the same key and an equivalent request MUST NOT repeat financial effects already committed by the original successful operation.

### DR-038 — Conflicting Reuse

Reusing the same key within the same scope for a different request MUST be treated as a conflict.

### DR-039 — Concurrent Duplicate Execution

Concurrent equivalent requests using the same idempotency key MUST result in no more than one set of committed financial effects.

### DR-040 — Atomic Persistence

The successful idempotency result and associated monetary effects MUST be committed atomically.

### DR-041 — Failed Operations

A failed operation MUST NOT permanently prevent a valid retry merely because its idempotency key was previously used.

The precise handling of validation errors, business conflicts, and uncertain transaction outcomes MUST be defined in the API and architecture documentation.

## 9. Concurrency and Consistency Rules

### DR-042 — Invariants Under Concurrency

All wallet invariants MUST hold when multiple monetary operations execute concurrently.

### DR-043 — No Overspending

If multiple outgoing operations compete for the same balance, the total amount successfully debited MUST NOT exceed the available funds.

### DR-044 — No Over-Crediting

Concurrent credits MUST NOT cause a wallet balance to exceed its maximum.

### DR-045 — Atomic State Transition

A monetary operation MUST transition the system from one valid financial state to another valid financial state.

Intermediate states MUST NOT become externally visible as committed financial results.

### DR-046 — Deterministic Business Outcome

Each monetary request MUST produce an outcome consistent with the committed database state and the documented retry semantics.

## 10. Rule Enforcement

Domain rules MUST be enforced at the appropriate system boundaries.

Depending on the rule, enforcement may include:

- Domain-level validation.
- Application use-case validation.
- Database constraints.
- Atomic conditional database updates.
- Transaction isolation and locking.
- Unique constraints for idempotency and record relationships.
- Automated unit, integration, and concurrency tests.

The selected enforcement mechanisms MUST be documented in the architecture and data-model documents.

Application validation alone MUST NOT be assumed sufficient to guarantee invariants under concurrent database access.

## 11. Related Documents

- `overview.md` — project scope and goals.
- `functional-requirements.md` — functional requirements.
- `non-functional-requirements.md` — quality attributes and verification expectations.
- `../architecture/architecture.md` — architectural boundaries and operation flows.
- `../architecture/data-model.md` — persistence model and constraints.
- `../architecture/concurrency.md` — concurrency control and transaction consistency.
- `../api/endpoints.md` — monetary request contracts.
- `../api/error-model.md` — error behavior.
- `../development/testing.md` — rule verification and test strategy.
- `../adr/` — significant architectural decisions.

---

**End of document**