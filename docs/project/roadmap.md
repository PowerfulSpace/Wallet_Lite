# Project Roadmap

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the proposed implementation roadmap for the Wallet API MVP.

The roadmap organizes development into incremental phases with explicit objectives, dependencies, deliverables, and acceptance criteria.

The primary goal is to deliver a reliable backend service that demonstrates financial transaction integrity, concurrency control, idempotency, and maintainable software architecture.

The roadmap is a planning baseline, not a commitment to fixed calendar dates.

## 2. Delivery Principles

Development SHOULD follow these principles:

- Implement and validate the highest-risk financial invariants early.
- Establish database constraints and transaction boundaries before building extensive API functionality.
- Prefer small, testable increments over a single large implementation.
- Verify concurrency behavior against a real SQL Server instance.
- Keep architectural decisions documented and revisit them when new evidence emerges.
- Avoid implementing features that do not contribute to the agreed MVP.
- Require acceptance criteria to pass before considering a phase complete.

## 3. Roadmap Overview

| Phase | Name | Primary Outcome |
|---|---|---|
| 0 | Requirements and decisions | Agreed scope and documented constraints |
| 1 | Solution foundation | Buildable solution and development environment |
| 2 | Domain and persistence | Domain model and database schema |
| 3 | Wallet query and identity | Authenticated wallet access |
| 4 | Deposits | Atomic and idempotent deposits |
| 5 | Transfers | Safe wallet-to-wallet transfers |
| 6 | History and API consistency | Transaction history and consistent API errors |
| 7 | Reliability and verification | Concurrency, recovery, and integrity testing |
| 8 | MVP hardening and demonstration | Documented, reproducible MVP |

The phases may overlap where dependencies allow, but financial write operations MUST NOT be considered complete before their integrity and concurrency requirements are verified.

## 4. Phase 0 — Requirements and Decisions

### Objectives

- Establish the MVP scope.
- Define core domain rules.
- Document API contracts.
- Establish architecture and security principles.
- Identify unresolved design decisions.

### Deliverables

- Requirements and domain rules.
- System context and architecture documents.
- Data model and concurrency strategy.
- API guidelines, endpoint specifications, and error model.
- Security and threat model.
- Initial Architecture Decision Records (ADRs).

### Acceptance Criteria

- The MVP scope and out-of-scope features are documented.
- Monetary precision and amount limits are defined.
- Deposit and transfer atomicity requirements are documented.
- Idempotency behavior is specified.
- Development authentication restrictions are documented.
- Open questions are tracked explicitly.

### Exit Condition

The requirements are sufficiently stable to begin implementation. Unresolved implementation choices may remain, provided they are identified and do not invalidate the initial architecture.

## 5. Phase 1 — Solution Foundation

### Objectives

- Create the .NET solution and project structure.
- Establish dependency boundaries.
- Configure SQL Server connectivity.
- Establish build, test, and formatting conventions.
- Provide a repeatable local development setup.

### Deliverables

- Solution projects for Domain, Application, Infrastructure, and API.
- Dependency injection and application startup.
- Configuration and environment separation.
- Initial automated test projects.
- Database connection configuration.
- Local development instructions.
- Initial CI pipeline, if included in the project setup.

### Acceptance Criteria

- The solution builds successfully.
- Unit tests execute through the documented command.
- API startup validates required configuration.
- Development and Test settings are separated.
- Development authentication cannot be enabled in Production.
- No real secrets are committed to source control.

### Exit Condition

A developer can build, run, and test the application using the documented setup process.

## 6. Phase 2 — Domain and Persistence

### Objectives

- Implement the core domain model.
- Define database entities, relationships, and constraints.
- Establish monetary precision in application and database layers.
- Create the initial database migration.

### Deliverables

- User and Wallet entities.
- Deposit and Transfer records.
- LedgerEntry model.
- IdempotencyRecord model or an approved equivalent.
- EF Core mappings.
- SQL Server schema and indexes.
- Initial migrations.
- Development seed data for Alice and Bob, where applicable.

### Acceptance Criteria

- Money is represented using `decimal` in application code.
- Monetary database values use `decimal(19,2)`.
- Invalid amounts cannot silently round to two decimal places.
- Database constraints enforce applicable uniqueness and referential integrity.
- Ledger and operation relationships are documented and testable.
- Initial wallet balances and reconciliation rules are explicitly defined.
- Migrations can be applied to a clean database.

### Exit Condition

The database schema supports the required financial operations and their integrity constraints.

## 7. Phase 3 — Wallet Query and Identity

### Objectives

- Implement the initial authentication and identity mapping.
- Expose the authenticated user's wallet.
- Enforce wallet ownership and prevent cross-user data access.

### Deliverables

- Development-only authentication for local demonstration.
- Authenticated user resolution.
- `GET /api/v1/wallet`.
- Safe handling of missing or inaccessible wallet data.
- API tests for authentication and authorization.

### Acceptance Criteria

- The current user's identity comes from the authenticated principal.
- The API never trusts a client-supplied sender ID.
- A user cannot read another user's wallet.
- Unauthenticated requests receive the documented response.
- Development authentication is unavailable in Production.
- Wallet responses expose only the agreed public fields.

### Exit Condition

The API can securely return the authenticated user's wallet information.

## 8. Phase 4 — Deposits

### Objectives

Implement virtual deposits with atomic persistence and idempotency.

### Deliverables

- `POST /api/v1/wallet/deposits`.
- Amount validation.
- Deposit record persistence.
- Wallet balance update.
- Positive ledger entry.
- Idempotency key handling.
- Deposit integration tests.

### Acceptance Criteria

- Deposit amounts must be positive and within the configured limits.
- A wallet cannot exceed the maximum permitted balance.
- The balance update, deposit record, ledger entry, and successful idempotency result commit atomically.
- Repeating the same request with the same key does not create another deposit.
- Reusing a key with a different request produces the documented conflict.
- Concurrent duplicate requests do not create duplicate financial effects.
- Failed attempts do not leave partial financial changes.

### Exit Condition

Deposits satisfy the domain rules and pass persistence, API, and idempotency tests against SQL Server.

## 9. Phase 5 — Transfers

### Objectives

Implement wallet-to-wallet transfers with concurrency-safe debit and credit operations.

### Deliverables

- `POST /api/v1/transfers`.
- Recipient resolution by username.
- Sender debit and recipient credit.
- Transfer record.
- Paired ledger entries.
- Atomic transaction handling.
- Idempotency handling.
- Transfer status lookup, subject to the agreed access rules.

### Acceptance Criteria

- Transfer amounts must be positive and valid.
- The sender and recipient must satisfy the domain rules.
- Insufficient funds reject the transfer without partial effects.
- Recipient balance-limit violations reject the transfer without partial effects.
- Sender debit, recipient credit, transfer record, both ledger entries, and successful idempotency result commit atomically.
- Debit and credit entries have equal absolute amounts and sum to zero.
- Concurrent transfers cannot overdraw a wallet.
- Repeated requests cannot duplicate transfers.
- Only the sender and recipient can access the transfer details.

### Exit Condition

Transfers pass financial invariant, concurrency, idempotency, and authorization tests against SQL Server.

## 10. Phase 6 — Transaction History and API Consistency

### Objectives

Complete the agreed read endpoints and standardize API behavior.

### Deliverables

- `GET /api/v1/wallet/transactions`.
- Unified deposit and transfer history.
- Cursor-based pagination.
- Consistent Problem Details responses.
- Stable application error codes.
- Consistent timestamps and monetary serialization.
- API contract tests.

### Acceptance Criteria

- History includes only records visible to the authenticated wallet owner.
- Results are ordered newest first with a deterministic cursor strategy.
- Pagination does not depend solely on timestamps that may be identical.
- Amount signs reflect the wallet owner's perspective.
- Error responses follow the documented API error model.
- Internal exception details and secrets are not returned to clients.
- Pagination limits and cursor validation follow the finalized contract.

### Exit Condition

All agreed API endpoints follow consistent validation, authorization, serialization, and error-handling rules.

## 11. Phase 7 — Reliability and Verification

### Objectives

Verify the behavior of the system under concurrency, failures, and operational disturbances.

### Deliverables

- Expanded SQL Server integration tests.
- Concurrent transfer and duplicate-request tests.
- Transaction rollback tests.
- Database failure handling tests.
- Financial integrity verification.
- Structured logging and health checks.
- Recovery and troubleshooting procedures.

### Acceptance Criteria

- Competing transfers cannot produce a negative wallet balance.
- Duplicate idempotency keys do not produce duplicate financial effects.
- Failed transactions leave no partial financial changes.
- Uncertain commit outcomes can be safely resolved through the idempotency protocol.
- Database constraints and migrations are tested.
- Logs and error responses do not expose sensitive information.
- Recovery instructions are reproducible in the test environment.

### Exit Condition

The team has evidence that the main financial invariants hold under the tested failure and concurrency scenarios.

## 12. Phase 8 — MVP Hardening and Demonstration

### Objectives

Prepare a reproducible, understandable demonstration of the completed MVP.

### Deliverables

- Updated README and setup documentation.
- Finalized API examples.
- Documented demo workflow using Alice and Bob.
- Test execution instructions.
- Configuration and troubleshooting documentation.
- Known limitations and deferred decisions.
- A clean database initialization and migration procedure.

### Acceptance Criteria

- A new developer can follow the setup documentation successfully.
- The API builds and the automated test suite passes.
- The demonstration can show deposits, transfers, history, and idempotent retries.
- A concurrency scenario demonstrates that the wallet cannot be overdrawn.
- Development authentication is clearly identified as non-production.
- Known limitations and unresolved decisions are documented.
- No out-of-scope feature is required for the core demonstration.

### Exit Condition

The MVP can be built, run, tested, and demonstrated using documented procedures.

## 13. Dependencies and Critical Path

The principal dependency chain is:

1. Requirements and domain rules.
2. Solution foundation.
3. Domain model and database schema.
4. Identity and wallet access.
5. Deposits and idempotency foundations.
6. Transfers and concurrency control.
7. History, error consistency, and API contract verification.
8. Recovery testing and final hardening.

Deposits and transfers may share implementation work, but their transaction and idempotency mechanisms should use consistent, reviewed patterns.

The transfer implementation is the main financial-risk milestone because it combines multiple wallet updates, ledger entries, concurrency, and atomicity.

## 14. Explicitly Out of Scope for the MVP

Unless scope is formally revised, the roadmap does not include:

- Real banking integrations.
- Real-money payment processing.
- Multi-currency wallets.
- Cryptocurrency support.
- External payment settlement.
- Pending or asynchronous transfer workflows.
- Full user registration and password management.
- A production identity provider integration before one is selected.
- A mandatory frontend application.
- Microservice deployment.
- Advanced fraud detection.
- Automated financial correction workflows.

## 15. Progress Tracking

Each phase SHOULD be tracked using the following statuses:

- **Not Started** — Work has not begun.
- **In Progress** — Implementation or verification is ongoing.
- **Blocked** — Work depends on an unresolved issue.
- **Done** — Deliverables and acceptance criteria have been verified.

A phase MUST NOT be marked Done solely because code has been written. Its acceptance criteria must be checked, including relevant automated tests.

## 16. Open Decisions

The following items remain open:

1. Which CI platform will run build, test, and migration checks?
2. Which performance targets are required for the MVP?
3. Which production hosting and authentication technologies will be selected?
4. What are the final acceptance thresholds for reliability and latency?
5. Which observability and recovery capabilities are mandatory before the first demonstration?
6. Which unresolved data-model and API decisions must be closed before their implementation phases?

These decisions should be tracked in `docs/project/open-questions.md`.

## 17. Related Documents

- `docs/requirements/overview.md`
- `docs/requirements/functional-requirements.md`
- `docs/requirements/non-functional-requirements.md`
- `docs/requirements/domain-rules.md`
- `docs/architecture/architecture.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/api/endpoints.md`
- `docs/development/testing.md`
- `docs/development/database-migrations.md`
- `docs/operations/configuration.md`
- `docs/operations/observability.md`
- `docs/operations/recovery.md`
