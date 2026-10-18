# ADR-001: Architecture Style

- **Status:** Proposed
- **Date:** 2026-10-18
- **Decision Owners:** Project Maintainers

## 1. Context

The Wallet API MVP is an educational backend project intended to demonstrate production-oriented software engineering practices, particularly financial data integrity, transactional consistency, concurrency control, idempotency, and testability.

The application must support:

- Wallet balance queries.
- Virtual deposits.
- Transfers between users.
- Transaction history.
- An append-only financial ledger.
- Atomic database updates.
- Safe handling of concurrent requests.
- Automated unit, API, and SQL Server integration tests.

The project should remain straightforward to develop, run locally, debug, and extend.

The architecture must separate financial business rules from HTTP concerns and database implementation details without introducing unnecessary distributed-system complexity.

## 2. Decision Drivers

The architecture should provide:

1. Clear separation of responsibilities.
2. Explicit dependency boundaries.
3. Testable domain and application logic.
4. Reliable transaction handling through SQL Server.
5. Support for concurrent financial operations.
6. Maintainable database and persistence code.
7. A simple local development experience.
8. A reasonable foundation for future extensions.
9. Limited operational complexity for an MVP.

## 3. Considered Options

### Option A: Layered Monolith

A conventional layered application separates API, business logic, and data access while running as one deployable application.

**Advantages**
- Simple project structure.
- Familiar development model.
- Low deployment complexity.
- Easy local debugging.

**Disadvantages**
- Dependency boundaries may become informal.
- Business logic can become coupled to persistence and framework details.
- Architectural rules may be harder to enforce as the application grows.

### Option B: Clean Architecture Modular Monolith

A single deployable application is organized into layers with explicit dependency rules. Domain and application logic remain independent of infrastructure implementation details.

**Advantages**
- Clear separation of business rules and technical concerns.
- Domain logic can be tested without a database.
- Infrastructure implementations can be substituted behind interfaces.
- Financial use cases have explicit application boundaries.
- No distributed transaction or inter-service communication overhead.

**Disadvantages**
- More projects and abstractions than a minimal layered application.
- Additional interfaces and mappings may be unnecessary for trivial features.
- Dependency rules require discipline and automated checks where practical.

### Option C: Microservices

The system is divided into independently deployable services.

**Advantages**
- Independent deployment and scaling.
- Potential for separate ownership of distinct business capabilities.
- Independent technology choices where justified.

**Disadvantages**
- Increased deployment and operational complexity.
- Network communication introduces additional failure modes.
- Atomic transfers across service-owned data become substantially more difficult.
- Distributed consistency and observability require additional infrastructure.
- The complexity is disproportionate to the MVP scope.

## 4. Decision

Adopt **Clean Architecture-inspired modular monolith architecture** for the Wallet API MVP.

The application will be delivered as one deployable API service backed by one SQL Server database.

The proposed solution consists of the following projects:

- `WalletApi.Domain`
- `WalletApi.Application`
- `WalletApi.Infrastructure`
- `WalletApi.Api`

The exact project names may be adjusted when the repository is created, provided that the dependency boundaries remain intact.

### Dependency Rules

- **Domain** contains core business concepts and invariants. It must not depend on application frameworks, the API, or infrastructure implementations.
- **Application** contains use cases, application-level validation, and abstractions required by those use cases. It depends on Domain.
- **Infrastructure** implements persistence and other technical abstractions. It may depend on Application and Domain.
- **API** exposes HTTP endpoints, handles transport concerns, configures dependency injection, and composes the application. It may reference Application and Infrastructure for composition.

Dependencies must not flow inward from Domain or Application toward concrete Infrastructure or HTTP implementation details.

## 5. Financial Consistency

The architecture must support financial operations whose related database changes succeed or fail together.

A transfer must atomically persist:

- The sender's balance change.
- The recipient's balance change.
- The transfer record.
- The corresponding ledger entries.
- The successful idempotency result.

These changes must be committed in one appropriate SQL Server transaction.

The architecture alone does not guarantee atomicity. The implementation must also use correct database transactions, constraints, and concurrency controls.

Business rules must not rely exclusively on HTTP validation or client-side behavior.

## 6. Transaction Boundaries

Application use cases coordinate the execution of financial operations.

Infrastructure is responsible for the concrete persistence mechanisms and database transaction integration.

A single transfer must not be split into independently committed financial updates.

The implementation should make transaction ownership explicit and avoid nested or overlapping transaction abstractions that obscure commit and rollback behavior.

The precise EF Core transaction strategy will be documented in the relevant implementation and concurrency guidance.

## 7. Testing Strategy

The architecture should support the following test categories:

- **Domain unit tests:** Verify domain rules without external dependencies.
- **Application unit tests:** Verify use-case behavior using suitable test doubles where appropriate.
- **API tests:** Verify HTTP contracts, authorization, status codes, and error responses.
- **SQL Server integration tests:** Verify constraints, transactions, idempotency, migrations, and concurrency behavior against a real SQL Server instance.

Mocks and in-memory substitutes must not be treated as proof of SQL Server concurrency correctness.

## 8. Alternatives Rejected

A microservices architecture is not justified by the current project scope. The need to demonstrate concurrent financial operations does not require distributed services; a relational database transaction is a better fit for the MVP.

A minimal layered monolith remains a viable alternative. The Clean Architecture-inspired structure is preferred because explicit dependency boundaries and isolated business logic are important learning objectives for this project.

## 9. Consequences

### Positive Consequences

- Business rules remain relatively independent of transport and persistence frameworks.
- Application use cases provide clear locations for financial workflows.
- SQL Server-specific behavior can be tested separately from domain logic.
- The application remains a single deployable unit.
- Future changes can be introduced without immediately requiring distributed architecture.

### Negative Consequences

- The solution contains more projects and interfaces than a minimal implementation.
- Developers must maintain dependency boundaries.
- Mapping between HTTP DTOs, application models, and domain entities may introduce additional code.
- Poorly justified abstractions could make simple operations harder to follow.

These costs should be controlled by introducing abstractions only where they support meaningful boundaries or testability.

## 10. What This Decision Does Not Imply

This decision does not require:

- A generic repository abstraction for every entity.
- A separate microservice for each domain concept.
- CQRS with separate read and write databases.
- Event sourcing.
- A message broker.
- A distributed transaction coordinator.
- A mediator library or other specific application framework.

Such technologies may be evaluated separately if a concrete requirement justifies them.

## 11. Review Conditions

Revisit this decision if:

- The system requires independently deployable business capabilities.
- Multiple teams need separate service ownership.
- Operational or scaling requirements materially change.
- The chosen project boundaries create more complexity than value.
- Financial consistency requirements can no longer be satisfied effectively by the current transactional model.

## 12. References

- `docs/architecture/architecture.md`
- `docs/architecture/system-context.md`
- `docs/architecture/data-model.md`
- `docs/architecture/concurrency.md`
- `docs/requirements/non-functional-requirements.md`

