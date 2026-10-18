# Wallet_Lite# Wallet API

A backend-first educational project demonstrating reliable financial operations, transaction atomicity, concurrency control, idempotency, and clean software architecture.

**Project status:** MVP planning and documentation  
**Current scope:** Virtual USD wallets only

## 1. Overview

Wallet API is a REST API for managing virtual wallets and transferring funds between users.

The project focuses on engineering problems that arise in financial software, including concurrent balance updates, database transaction boundaries, duplicate requests, and consistency between wallet balances and transaction history.

It is an educational project. It does not handle real money, connect to banks, or process payments through external financial providers.

The repository includes requirements, architecture proposals, API contracts, security documentation, testing guidance, and Architecture Decision Records (ADRs).

The implementation stack and some design decisions remain subject to review and approval.

## 2. MVP Features

The planned MVP includes:

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

The initial development environment is planned to include pre-seeded users named Alice and Bob.

The authentication mechanism for development and the production identity strategy must be documented separately. Development authentication must never be available in Production.

## 3. Domain Rules

The following rules define the current project baseline and must be preserved unless explicitly revised:

- Currency: USD only.
- Monetary calculations: C# `decimal`.
- Proposed database monetary type: SQL Server `decimal(19,2)`.
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

Some implementation details remain proposed and are tracked in the ADRs and open questions.

## 4. Proposed Technology Stack

| Area | Proposed technology |
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

The frontend is optional for the initial backend-focused milestone. If implemented, its framework, tooling, and hosting approach must be explicitly selected.

The technology table describes the proposed stack, not a guarantee that all components have already been installed or implemented.

## 5. Architecture

The proposed backend is divided into four logical projects:

- **Domain** — business entities, value objects, and domain invariants.
- **Application** — use cases, coordination, and application policies.
- **Infrastructure** — persistence and technical integrations.
- **API** — HTTP endpoints, authentication integration, request handling, and response mapping.

The application is intended to remain a modular monolith rather than a distributed system.

Financial operations must use database transactions and concurrency controls appropriate to the accepted design. A transfer must not be partially committed.

See `docs/architecture/architecture.md` and `docs/architecture/data-model.md`.

## 6. API Overview

The planned API endpoints are:

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/v1/wallet` | Retrieve the authenticated user's wallet |
| GET | `/api/v1/wallet/transactions` | Retrieve transaction history |
| POST | `/api/v1/wallet/deposits` | Deposit virtual funds |
| POST | `/api/v1/transfers` | Transfer funds to another user |
| GET | `/api/v1/transfers/{id}` | Retrieve an authorized transfer |

Deposit and transfer requests require an `Idempotency-Key` header.

The API is designed to use standardized HTTP status codes and Problem Details responses. The exact response schemas and error codes are documented in `docs/api/`.

These endpoints represent the planned API contract and should be checked against the implementation as it evolves.

## 7. Repository Structure

The planned documentation structure is:

```text
wallet-api/
├── AGENTS.md
├── README.md
├── docs/
│   ├── requirements/
│   ├── architecture/
│   ├── api/
│   ├── security/
│   ├── development/
│   ├── adr/
│   ├── operations/
│   ├── project/
│   └── frontend/
├── src/
├── tests/
└── frontend/
```

This is a target structure. Directories and implementation files may not exist until the corresponding development work has been completed.

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
- `docs/adr/` — detailed Architecture Decision Records.

### Operations

- `docs/operations/configuration.md` — application configuration.
- `docs/operations/observability.md` — logging, metrics, tracing, and health checks.
- `docs/operations/recovery.md` — failure handling and recovery.

### Frontend

- `docs/frontend/overview.md` — frontend scope and goals.
- `docs/frontend/architecture.md` — frontend boundaries and data flow.
- `docs/frontend/api-integration.md` — API client behavior.
- `docs/frontend/authentication.md` — frontend authentication integration.
- `docs/frontend/accessibility.md` — accessibility requirements.
- `docs/frontend/testing.md` — frontend test strategy.
- `docs/frontend/development.md` — local frontend development.

## 9. Getting Started

The project is currently in the planning and documentation phase. A complete, verified application setup cannot be assumed yet.

Once the implementation and repository structure are in place, the expected development workflow will be:

1. Install the required .NET SDK and SQL Server.
2. Configure the development connection string and other required settings.
3. Apply the database migrations.
4. Start the ASP.NET Core API.
5. Run unit and integration tests.
6. Start the frontend if it has been implemented.

Use `docs/development/setup.md` for the detailed backend setup procedure.

Do not use production credentials or production financial data for local development or automated testing.

## 10. Testing Strategy

The project is intended to include:

- Unit tests for domain rules and application logic.
- API tests for validation, HTTP responses, and authorization.
- SQL Server-backed integration tests for persistence and transaction behavior.
- Concurrency tests for competing balance updates and transfers.
- Idempotency tests for repeated and concurrent requests.
- Frontend tests if a frontend is implemented.

Mocks alone are insufficient to validate database transaction semantics and concurrency behavior.

See `docs/development/testing.md`.

## 11. Current Status and Open Decisions

The project documentation establishes the proposed scope and design direction. The implementation must be developed and validated against these documents.

Important decisions still requiring confirmation include:

- Initial wallet balance and ledger initialization.
- Final concurrency and transaction-isolation strategy.
- Idempotency retention and recovery after uncertain commits.
- Exact API response schemas and pagination behavior.
- Production authentication and identity management.
- Frontend technology and hosting.
- Performance targets and operational recovery objectives.

See `docs/project/open-questions.md` for the complete list and `docs/project/decisions.md` for the status of proposed decisions.

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
