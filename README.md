# Wallet API

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `README.md`

A backend-first educational project demonstrating reliable financial operations, transaction atomicity, concurrency control, idempotency, and clean software architecture.

**Project status:** MVP backend implemented, tested (158 tests), and documented. Frontend scaffold exists; frontend business functionality is out of MVP scope.
**Current scope:** Virtual USD wallets only

Frontend business functionality is out of MVP scope. A scaffold exists at `src/Frontend/PS.Wallet/`; see `AGENTS.md §12`.

## 1. Overview

Wallet API is a REST API for managing virtual wallets and transferring funds between users.

The project focuses on engineering problems that arise in financial software, including concurrent balance updates, database transaction boundaries, duplicate requests, and consistency between wallet balances and transaction history.

It is an educational project. It does not handle real money, connect to banks, or process payments through external financial providers.

The repository includes requirements, architecture decisions, API contracts, security documentation, testing guidance, and Architecture Decision Records (ADRs).

## 2. MVP Features

The MVP includes:

- One virtual USD wallet per user.
- Wallet balance retrieval.
- Virtual deposits.
- Transfers between users.
- Transaction history with cursor-based pagination.
- Idempotent deposit and transfer requests.
- Consistent financial records and an append-only ledger.
- Authentication and wallet ownership checks.
- Validation and standardized API errors.
- Automated unit, API, integration, and concurrency tests.

The initial development environment includes pre-seeded users named Alice and Bob.

The authentication mechanism for development and the production identity strategy must be documented separately. Development authentication must never be available in Production.

## 3. Domain Rules

The following rules define the current project baseline and must be preserved unless explicitly revised:

- Currency: USD only.
- Monetary calculations: C# `decimal`.
- Database monetary type: SQL Server `decimal(19,2)`.
- API monetary values: decimal strings using a period as the decimal separator.
- Maximum operation amount: USD 1,000,000.00.
- Maximum wallet balance: USD 1,000,000.00.
- Amounts must be positive.
- Inputs with more than two fractional decimal digits must be rejected rather than silently rounded.
- Wallet balances must never become negative.
- Transfers must debit and credit equal amounts.
- Financial changes and successful idempotency results must commit atomically.
- Concurrent operations must preserve financial invariants.
- Ledger history is append-only during normal operation.

Implementation details are tracked in ADRs and resolved/open questions.

## 4. Technology Stack

| Area | Technology |
|---|---|
| Backend language | C# |
| Runtime | .NET 10 LTS |
| HTTP API | ASP.NET Core Web API |
| Architecture | Clean Architecture-inspired modular monolith |
| Persistence | Entity Framework Core |
| Database | Microsoft SQL Server |
| Unit testing | xUnit |
| Mocking | NSubstitute |
| API errors | RFC 9457 Problem Details |

The frontend baseline is Vue 3, TypeScript, Vite, Pinia, axios, and naive-ui (see `AGENTS.md §12`). Frontend business functionality is out of MVP scope.

All listed components are installed and used in the current implementation.

## 5. Architecture

The backend is divided into four projects:

- **Domain** — business entities, value objects, and domain invariants.
- **Application** — use cases, coordination, and application policies.
- **Infrastructure** — persistence and technical integrations.
- **API** — HTTP endpoints, authentication integration, request handling, and response mapping.

The application is intended to remain a modular monolith rather than a distributed system.

Financial operations must use database transactions and concurrency controls appropriate to the accepted design. A transfer must not be partially committed.

Monetary precision is `decimal(19,2)` in SQL Server and `decimal` in C#; see `ADR-002`.

See `docs/architecture/architecture.md` and `docs/architecture/data-model.md`.

## 6. API Overview

The API endpoints are:

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/v1/wallet` | Retrieve the authenticated user's wallet |
| GET | `/api/v1/wallet/transactions` | Retrieve transaction history |
| POST | `/api/v1/wallet/deposits` | Deposit virtual funds |
| GET | `/api/v1/wallet/deposits/{id}` | Retrieve an authorized deposit |
| POST | `/api/v1/transfers` | Transfer funds to another user |
| GET | `/api/v1/transfers/{id}` | Retrieve an authorized transfer |

Deposit and transfer requests require an `Idempotency-Key` header.

The API is designed to use standardized HTTP status codes and Problem Details responses. The exact response schemas and error codes are documented in `docs/api/`.

## 7. Repository Structure

The current repository structure is:

```text
Wallet_Lite/
+-- .editorconfig
+-- .gitignore
+-- AGENTS.md
+-- Directory.Build.props
+-- Directory.Packages.props
+-- global.json
+-- LICENSE
+-- README.md
+-- docs/
|   +-- adr/
|   +-- api/
|   +-- architecture/
|   +-- development/
|   +-- frontend/
|   +-- operations/
|   +-- project/
|   |   +-- demo.md
|   |   L-- ...
|   +-- requirements/
|   L-- security/
+-- src/
|   +-- Backend/
|   |   L-- PS.Wallet/
|   |       +-- PS.Wallet.slnx
|   |       +-- PS.Wallet.Domain/
|   |       +-- PS.Wallet.Application/
|   |       +-- PS.Wallet.Infrastructure/
|   |       L-- PS.Wallet.API/
|   L-- Frontend/
|       L-- PS.Wallet/
L-- tests/
    +-- PS.Wallet.UnitTests/
    L-- PS.Wallet.IntegrationTests/
```

This is the current structure. The frontend scaffold lives at `src/Frontend/PS.Wallet/` and implements no wallet business functionality.

## 8. Documentation Guide

### Requirements

- `docs/requirements/overview.md` — project goals and scope.
- `docs/requirements/functional-requirements.md` — functional requirements.
- `docs/requirements/non-functional-requirements.md` — quality and operational requirements.
- `docs/requirements/domain-rules.md` — financial invariants and business rules.

### Architecture

- `docs/architecture/system-context.md` — system boundary and external actors.
- `docs/architecture/architecture.md` — application structure and dependency rules.
- `docs/architecture/data-model.md` — entities, relationships, and constraints.
- `docs/architecture/concurrency.md` — transaction safety and concurrent operations.

### API

- `docs/api/api-guidelines.md` — API conventions.
- `docs/api/endpoints.md` — endpoint contracts.
- `docs/api/error-model.md` — error response format.

### Security

- `docs/security/security-model.md` — authentication and authorization.
- `docs/security/threat-model.md` — identified threats and mitigations.

### Development

- `docs/development/setup.md` — local backend setup.
- `docs/development/coding-standards.md` — coding conventions.
- `docs/development/testing.md` — backend test strategy.
- `docs/development/database-migrations.md` — database schema changes.

### Decisions and Project Management

- `docs/project/decisions.md` — central decision register.
- `docs/project/open-questions.md` — unresolved questions.
- `docs/project/roadmap.md` — development phases.
- `docs/project/glossary.md` — shared terminology.
- `docs/project/constants.md` — single source of truth for numeric and format constants.
- `docs/project/known-issues.md` — accepted deviations deferred beyond the current phase.
- `docs/project/demo.md` — reproducible end-to-end demo workflow.
- `docs/adr/` — detailed Architecture Decision Records.
- `docs/adr/ADR-000-template.md` — ADR template.
- `docs/adr/ADR-001-architecture-style.md` — architecture style.
- `docs/adr/ADR-002-money-and-precision.md` — money representation and precision.
- `docs/adr/ADR-003-transaction-atomicity.md` — financial transaction atomicity.
- `docs/adr/ADR-004-concurrency-control.md` — concurrency control.
- `docs/adr/ADR-005-idempotency.md` — idempotency for financial operations.
- `docs/adr/ADR-006-ledger-model.md` — wallet balance and append-only ledger.
- `docs/adr/ADR-007-development-authentication.md` — development authentication.

### Operations

- `docs/operations/configuration.md` — application configuration.
- `docs/operations/observability.md` — logging, metrics, tracing, and health checks.
- `docs/operations/recovery.md` — failure handling and recovery.

### Frontend

> Frontend is out of MVP scope. These documents are drafts for a future phase. See `AGENTS.md` §12.

- `docs/frontend/overview.md` — frontend scope and goals.
- `docs/frontend/architecture.md` — frontend boundaries and data flow.
- `docs/frontend/api-integration.md` — API client behavior.
- `docs/frontend/authentication.md` — frontend authentication integration.
- `docs/frontend/accessibility.md` — accessibility requirements.
- `docs/frontend/testing.md` — frontend test strategy.
- `docs/frontend/development.md` — local frontend development.
- `docs/frontend/forms-and-validation.md` — form behavior and validation rules.
- `docs/frontend/routing.md` — routing and navigation model.
- `docs/frontend/state-management.md` — state-management strategy.
- `docs/frontend/ui-components.md` — component system.

## 9. Getting Started

The MVP backend is implemented and verified against the documented contract. Use `docs/development/setup.md` for the full procedure.

```bash
# Build the solution
dotnet build src/Backend/PS.Wallet/PS.Wallet.slnx

# Apply migrations (SQL Server or LocalDB)
dotnet ef database update \
  --project src/Backend/PS.Wallet/PS.Wallet.Infrastructure \
  --startup-project src/Backend/PS.Wallet/PS.Wallet.API

# Run the API (http://localhost:5254)
dotnet run --project src/Backend/PS.Wallet/PS.Wallet.API

# Run all tests
dotnet test src/Backend/PS.Wallet/PS.Wallet.slnx
```

In Development, protected endpoints require the `X-Test-User` header (`alice` or `bob`); see `docs/development/setup.md §15`.

A reproducible end-to-end walkthrough of the running API is described in `docs/project/demo.md`.

Do not use production credentials or production financial data for local development or automated testing.

## 10. Testing Strategy

The project includes:

- Unit tests for domain rules and application logic.
- API tests for validation, HTTP responses, and authorization.
- SQL Server-backed integration tests for persistence and transaction behavior.
- Concurrency tests for competing balance updates and transfers.
- Idempotency tests for repeated and concurrent requests, including lease recovery and uncertain-commit handling.
- Frontend tests if a frontend is implemented.

The current suite contains 158 tests: 105 unit and 53 integration tests. Integration tests require a real SQL Server instance (SQL Server or LocalDB); see `docs/development/setup.md §9` and `docs/development/testing.md §3.4`.

Mocks alone are insufficient to validate database transaction semantics and concurrency behavior.

See `docs/development/testing.md`.

## 11. Current Status and Open Decisions

The MVP is implemented and verified against the documented scope.

All P0 blocking decisions are resolved. P1 decisions are either `Decided` or `Deferred` with a target phase. The MVP is feature-complete for the documented scope. Remaining items are tracked in `docs/project/open-questions.md §12` and `docs/project/known-issues.md`.

## 12. Contributing

Before implementing a feature:

1. Read the relevant requirements and domain rules.
2. Review related architecture documents and ADRs.
3. Check unresolved questions.
4. Implement the smallest change that preserves the project's invariants.
5. Add appropriate tests.
6. Update the documentation when behavior or contracts change.

All contributors and AI coding agents must follow `AGENTS.md`.

## 13. Project Limitations

The MVP does not include:

- Real money movement.
- Bank or payment-provider integrations.
- Multi-currency balances.
- Production user registration and password management.
- Distributed microservices.
- A production deployment guarantee.

These features require explicit scope changes and additional design, security, and operational review.

## Demo Workflow

A reproducible end-to-end demonstration is documented in [`docs/project/demo.md`](docs/project/demo.md). It covers:

- Alice deposits funds.
- Alice transfers to Bob.
- Idempotent retry (same key, same request).
- Idempotency conflict (same key, different request).
- Transaction history for both users.
- Concurrent debit protection.
- Balance limit.
- Unauthorized access.

Run the API and use the `X-Test-User` header (`alice` or `bob`) as described in `docs/development/setup.md §15`.
