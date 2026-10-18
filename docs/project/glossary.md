# Project Glossary

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This glossary defines the key terms used throughout the Wallet API project.

The purpose is to establish consistent terminology across requirements, architecture documentation, API contracts, implementation, tests, and operational procedures.

When a term has a specific meaning in this glossary, project documentation SHOULD use that meaning consistently.

## 2. Domain Terms

### Wallet

A virtual account associated with one user that holds a USD-denominated balance.

A wallet is an internal application concept and is not a bank account or a real-money payment instrument.

### User

An application identity associated with a wallet.

In the MVP, each user has exactly one wallet. Full user registration and password management are outside the initial scope.

### Wallet Balance

The current amount of virtual USD held in a wallet.

The balance is represented using decimal arithmetic and persisted using SQL Server `decimal(19,2)`.

A wallet balance MUST NOT become negative or exceed the configured maximum of USD 1,000,000.00.

### Deposit

A virtual top-up operation that increases a wallet balance.

A successful deposit creates a deposit record and one positive ledger entry. The operation is not a transfer from another wallet and does not represent a real payment.

### Transfer

A virtual movement of value from one wallet to another.

A successful transfer debits the sender, credits the recipient, creates one transfer record, and produces two corresponding ledger entries.

The debit and credit must have equal absolute amounts, and their sum must be zero.

### Sender

The user whose wallet is debited by a transfer.

The sender is determined from the authenticated identity. The client MUST NOT be trusted to choose the sender by supplying an arbitrary user identifier.

### Recipient

The user whose wallet is credited by a transfer.

In the MVP, the recipient is identified by username, subject to the project's username normalization and lookup rules.

### Operation

A persisted business action that changes financial state, such as a deposit or transfer.

An operation record describes the action but does not replace the ledger entries that represent its financial effects.

### Transaction History

A read-only view of the financial activity associated with a wallet.

The MVP history combines deposits and transfers and presents signed amounts from the wallet owner's perspective.

### Virtual USD

The single supported currency in the MVP.

Virtual USD is a demonstration unit of value. It does not represent actual funds held by a financial institution.

## 3. Ledger and Accounting Terms

### Ledger

The persisted record of financial changes associated with wallets.

In this project, the ledger is append-only during normal operation. Existing ledger entries are not edited or deleted to correct ordinary business mistakes.

### Ledger Entry

An immutable record representing one financial effect on a wallet.

Each entry contains a signed amount and references the relevant wallet and originating operation.

- A deposit creates a positive entry.
- A transfer creates a negative entry for the sender and a positive entry for the recipient.

### Signed Amount

A monetary value whose sign indicates the direction of the financial effect from the wallet's perspective.

Positive amounts increase the wallet balance; negative amounts decrease it.

### Reconciliation

The process of checking that persisted financial records satisfy the expected accounting and domain invariants.

Depending on the approved data model, reconciliation may compare wallet balances with the sum of their ledger entries and verify that transfer entries balance.

### Compensating Transaction

A new financial operation that counteracts a previously committed operation without modifying or deleting its historical ledger entries.

A formal correction workflow is outside the initial MVP scope unless explicitly added.

### Financial Invariant

A condition that must remain true for the system to preserve financial correctness.

Examples include non-negative wallet balances, permitted balance limits, atomic transfers, and balanced transfer ledger entries.

## 4. Consistency and Transaction Terms

### Database Transaction

A database unit of work whose changes commit together or are rolled back together.

The Wallet API uses database transactions to protect multi-record financial operations.

### Atomicity

The property that a group of related changes either all commit or none commit.

For a transfer, atomicity applies to the sender debit, recipient credit, transfer record, both ledger entries, and successful idempotency result.

### Consistency

The property that a successful transaction preserves the applicable database constraints and financial invariants.

### Isolation

The database transaction property that controls how concurrent transactions interact and observe one another's changes.

The implementation must select and test an appropriate SQL Server isolation and locking strategy.

### Durability

The property that a committed transaction remains persisted according to the database's durability guarantees.

### Concurrency

The condition in which multiple operations execute or overlap in time and may compete to access the same data.

The application must prevent concurrent operations from violating financial invariants.

### Race Condition

A defect in which the correctness of an operation depends on the timing or ordering of concurrent actions.

For example, two transfers must not both spend the same available wallet funds when their combined amounts exceed the balance.

### Deadlock

A database condition in which transactions wait on one another in a cycle and the database selects a transaction as a deadlock victim.

The application may retry eligible aborted transactions under a bounded retry policy.

### Conditional Update

A database update whose success depends on a condition evaluated as part of the update itself.

A conditional wallet debit can prevent a balance from becoming negative without relying on an unsafe separate read followed by an update.

## 5. Idempotency Terms

### Idempotency

The property that repeating the same logical request with the same idempotency key does not repeat its financial effect.

### Idempotency Key

A client-supplied request identifier used to recognize retries of the same logical deposit or transfer.

The key is scoped according to the approved idempotency design and is not an authorization credential.

### Request Fingerprint

A representation of the normalized, semantically relevant request fields used to determine whether a repeated key refers to the same logical request.

### Idempotency Replay

The handling of a repeated request by returning the result of a previously successful operation rather than executing the financial operation again.

### Idempotency Conflict

An error that occurs when an idempotency key is reused for a materially different request within the applicable scope.

### Uncertain Commit Outcome

A situation in which the application cannot immediately determine whether a database transaction committed, for example because the connection failed during commit.

An uncertain outcome must be resolved safely before another financial effect is created.

## 6. API and Security Terms

### API

Application Programming Interface. In this project, the HTTP interface exposed by the Wallet API backend.

### Endpoint

An HTTP method and route that exposes a particular API operation, such as `POST /api/v1/transfers`.

### Authenticated Principal

The identity established by the authentication mechanism for the current request.

The API uses this identity to determine which user is making the request.

### Authentication

The process of establishing the identity associated with a request.

### Authorization

The process of determining whether the authenticated identity is permitted to access a resource or perform an action.

### Ownership Check

A validation that confirms a user is allowed to access a particular wallet or operation.

### Development Authentication

A simplified authentication mechanism intended only for local development and demonstration.

It MUST NOT be available in Production.

### Problem Details

A standardized HTTP error response format used to communicate machine-readable error information.

The Wallet API uses the documented Problem Details error model for API failures.

### Correlation Identifier

An identifier used to connect related logs and diagnostic events associated with a request or operation.

A correlation identifier is not a financial operation identifier, idempotency key, or authorization token.

## 7. Architecture and Engineering Terms

### Modular Monolith

An application deployed as one service while maintaining explicit internal module and dependency boundaries.

The Wallet API is planned as a modular monolith rather than a set of independently deployed microservices.

### Clean Architecture

An architectural approach that separates domain and application logic from infrastructure and delivery mechanisms.

The proposed solution uses Domain, Application, Infrastructure, and API projects with explicit dependency rules.

### Domain Layer

The layer responsible for core business concepts, domain rules, and invariants.

### Application Layer

The layer responsible for coordinating use cases, enforcing application workflows, and defining relevant application contracts.

### Infrastructure Layer

The layer responsible for technical implementations such as database access and persistence.

### API Layer

The layer responsible for HTTP routing, request and response contracts, authentication integration, and translation of application results into HTTP responses.

### Integration Test

A test that verifies the interaction between multiple components or external dependencies.

For this project, important integration tests use a real SQL Server environment to verify database-specific behavior.

### Migration

A versioned database schema change managed through the database migration tooling.

Migrations must be reviewed and tested before being applied to a shared or production database.

### Health Check

An endpoint or diagnostic operation that reports an application's availability or readiness.

Health checks must not modify financial state.

## 8. Environment Terms

### Development

The environment used for local implementation and debugging.

Development may use seeded demonstration users and development-only authentication.

### Test

An isolated environment used to execute automated tests and verify application behavior.

### Production

The deployed environment intended for operational use.

Production must not enable development authentication and must use an explicitly approved security configuration.

### Configuration

Settings that control application behavior and infrastructure connections.

Secrets must be supplied through appropriate secure configuration mechanisms rather than committed to source control.

### Secret

A sensitive value such as a password, token, signing key, or credential that requires restricted access and secure storage.

## 9. Amount and Precision Terms

### Decimal Arithmetic

Base-10 numeric arithmetic used for monetary calculations in the application.

The application uses C# `decimal` rather than binary floating-point types for money.

### Monetary Precision

The number of digits permitted after the decimal separator for a monetary amount.

The MVP supports exactly two fractional decimal digits at most and must reject inputs containing additional fractional digits rather than silently rounding them.

### Operation Amount Limit

The maximum permitted amount for an individual deposit or transfer.

The MVP limit is USD 1,000,000.00.

### Wallet Balance Limit

The maximum permitted balance of a wallet.

The MVP limit is USD 1,000,000.00.

## 10. Documentation Status Terms

### Proposed

A design or decision candidate that has not yet been formally accepted.

### Draft

A document under development that may still change.

### Accepted

A decision or requirement that has been explicitly approved for implementation.

### Deferred

A question or feature intentionally postponed, with its future resolution point documented where applicable.

### MVP

Minimum Viable Product. The smallest agreed scope that demonstrates the essential behavior and engineering goals of the project.

## 11. Naming Conventions

Project documentation and code SHOULD use the following names consistently:

| Concept | Preferred term |
|---|---|
| Virtual top-up | Deposit |
| Movement between wallets | Transfer |
| Persisted financial effect | LedgerEntry |
| Current amount held by a wallet | Balance |
| Request retry identifier | IdempotencyKey |
| Normalized request comparison value | RequestFingerprint |
| Identity established for a request | AuthenticatedPrincipal |
| Historical financial activity | TransactionHistory |
| Check of accounting invariants | Reconciliation |

The term **transaction** requires context because it may refer either to a business operation or a database transaction. Documentation SHOULD use **transfer**, **deposit**, or **database transaction** when the distinction matters.

## 12. Related Documents

- `docs/requirements/overview.md`
- `docs/requirements/functional-requirements.md`
- `docs/requirements/domain-rules.md`
- `docs/architecture/architecture.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/api/api-guidelines.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/security/security-model.md`
- `docs/development/testing.md`
- `docs/operations/recovery.md`
- `docs/project/open-questions.md`
- `docs/project/roadmap.md`
