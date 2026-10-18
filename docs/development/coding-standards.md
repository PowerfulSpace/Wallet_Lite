# Coding Standards

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines coding conventions and implementation practices for the Wallet API MVP.

The goal is to keep the codebase consistent, testable, maintainable, and safe for financial operations.

These standards apply to all application code unless a more specific architectural decision or documented exception takes precedence.

## 2. General Principles

The codebase must follow these principles:

- Prefer simple, explicit, readable code over unnecessary abstraction.
- Keep business rules independent of infrastructure concerns.
- Give each class and method a clear responsibility.
- Avoid duplicated business logic.
- Make invalid states difficult to represent.
- Use dependency injection for application dependencies.
- Make financial side effects explicit.
- Prefer deterministic behavior and automated tests.
- Document non-obvious decisions and invariants.

Do not introduce abstractions, frameworks, or design patterns without a clear benefit to the MVP.

## 3. C# and .NET Conventions

The proposed target framework is .NET 10.

Use the repository's configured C# language version and SDK consistently.

### Naming

| Element | Convention | Example |
|---|---|---|
| Namespace | PascalCase segments | `WalletApi.Domain` |
| Type and interface | PascalCase | `TransferService` |
| Public method | PascalCase | `CreateTransferAsync` |
| Property | PascalCase | `AvailableBalance` |
| Parameter | camelCase | `recipientUsername` |
| Local variable | camelCase | `currentBalance` |
| Private field | `_camelCase` | `_walletRepository` |
| Constant | PascalCase | `MaximumBalance` |
| Async method | Suffix `Async` | `GetWalletAsync` |

Use meaningful names that describe business intent. Avoid unexplained abbreviations.

### Formatting

- Use consistent indentation and brace placement.
- Keep methods focused and reasonably short.
- Use file-scoped namespaces where consistent with the project.
- Prefer explicit types when they improve clarity; use `var` when the type is evident from the expression.
- Use named arguments when they materially improve readability.
- Follow the repository's `.editorconfig` and analyzer configuration.

Formatting and analyzer rules should be enforced automatically where practical.

## 4. Nullable Reference Types

Enable nullable reference types for application projects.

- Represent optional values explicitly.
- Validate external input at application boundaries.
- Avoid the null-forgiving operator unless a documented invariant justifies it.
- Do not use null as an implicit representation of multiple unrelated states.
- Handle missing entities explicitly.

Compiler warnings should be resolved rather than suppressed without justification.

## 5. Architecture and Dependency Rules

The application follows the Clean Architecture-inspired structure defined in `../architecture/architecture.md`.

Expected dependency direction:

- `Domain` contains core business concepts and rules.
- `Application` orchestrates use cases and defines application-facing contracts.
- `Infrastructure` implements persistence and other external dependencies.
- `API` exposes HTTP endpoints and connects the application to ASP.NET Core.

Dependency rules:

- Domain must not depend on Application, Infrastructure, or API.
- Application may depend on Domain and abstractions it defines.
- Infrastructure implements interfaces required by the application.
- API may reference Application and the composition-root dependencies required to configure the application.
- Infrastructure-specific details must not leak into Domain.

Avoid placing financial business rules in controllers, database entities, or HTTP middleware.

## 6. Domain Logic

Domain rules must be explicit and centrally enforced.

Examples include:

- Transfer amounts must be positive.
- Monetary values must not exceed the permitted precision.
- Wallet balances must never become negative.
- Wallet balances must not exceed the configured maximum.
- A transfer cannot target the sender's own wallet.
- A transfer must preserve equal and opposite ledger entries.
- A failed financial operation must not leave partial changes.

Controllers must not independently reimplement these rules.

The database must also enforce appropriate integrity constraints. Application validation alone is insufficient for invariants affected by concurrency.

## 7. Monetary Values

Money must use exact decimal arithmetic.

- Use C# `decimal`.
- Use SQL Server `decimal(19,2)` for persisted monetary amounts.
- Never use `float` or `double` for money.
- Represent amounts as strings in JSON according to the API contract.
- Reject values with more than two fractional decimal digits.
- Never silently round an invalid input.
- Use invariant culture for parsing and formatting API monetary values.
- Make sign conventions explicit for ledger entries.

The configured maximum operation amount and maximum wallet balance are both `$1,000,000.00`.

Avoid hidden currency conversions or implicit currency assumptions. The MVP supports USD only.

## 8. Async and Cancellation

Use asynchronous APIs for I/O-bound operations, including database access.

- Use `async` and `await` rather than blocking asynchronous work.
- Pass `CancellationToken` through API, application, and persistence calls where supported.
- Avoid `.Result` and `.Wait()` on asynchronous operations.
- Do not use `Task.Run` merely to wrap database I/O.
- Ensure cancellation cannot cause partially committed financial state.

Cancellation must not be treated as a substitute for transaction rollback or concurrency control.

## 9. Error Handling

Use explicit error handling for expected business failures.

Examples include:

- Insufficient funds.
- Invalid monetary amounts.
- Balance limit exceeded.
- Idempotency key reused with a different request.
- Missing or inaccessible resources.

Map application failures to HTTP responses at the API boundary using the conventions in `../api/error-model.md`.

Rules:

- Do not expose stack traces or raw exception messages to clients.
- Do not use exceptions as ordinary control flow for expected validation outcomes without a clear justification.
- Do not swallow unexpected exceptions.
- Log unexpected failures with appropriate diagnostic context.
- Avoid logging secrets or unnecessary sensitive financial data.
- Preserve the original exception when rethrowing or adding context.

Error responses must use the standardized Problem Details format.

## 10. Database Access and Transactions

Use Entity Framework Core for persistence unless a documented performance or correctness requirement justifies another approach.

- Keep persistence implementation in Infrastructure.
- Use asynchronous database operations.
- Use explicit transaction boundaries for financial operations.
- Ensure all changes belonging to a deposit or transfer commit atomically.
- Use concurrency-safe balance updates.
- Avoid unsafe read-then-update logic for wallet balances.
- Use database constraints to enforce uniqueness and referential integrity.
- Keep schema changes in versioned migrations.

A successful transfer must persist both wallet changes, the transfer record, both ledger entries, and the successful idempotency result as one atomic operation.

Do not assume that wrapping code in an EF Core transaction alone solves race conditions. The update and locking strategy must also be correct for SQL Server.

## 11. Idempotency

Deposits and transfers require idempotency keys.

Implementation rules:

- Scope keys to the authenticated user and operation type.
- Compute a deterministic request fingerprint from the relevant normalized request data.
- Enforce uniqueness at the database level.
- Handle concurrent requests using the same key safely.
- Replay the original successful response for an equivalent retry.
- Reject a conflicting payload with the same key.
- Do not allow failed attempts to permanently block valid retries contrary to the documented failure policy.

Idempotency behavior must be tested using SQL Server integration tests.

## 12. API Controllers and DTOs

Controllers must remain thin.

They should:

- Accept and validate the HTTP request shape.
- Obtain the authenticated identity through the established authentication mechanism.
- Call the appropriate application use case.
- Map application results to HTTP responses.
- Respect cancellation tokens.

Controllers should not:

- Directly implement transfer accounting.
- Independently update wallet balances.
- Construct ledger entries through ad hoc logic.
- Expose persistence entities as public response models.
- Trust a client-supplied sender ID.

Use separate request and response DTOs for the public API contract.

## 13. Dependency Injection and Configuration

Register application services and infrastructure dependencies through the application's composition root.

- Prefer constructor injection.
- Avoid service locator patterns.
- Avoid static mutable state for application services.
- Validate required configuration at startup.
- Keep secrets out of source control.
- Make environment-specific authentication behavior explicit.
- Ensure development authentication cannot be enabled in Production.

Do not introduce global service access merely to reduce constructor parameters; address excessive dependencies by improving design.

## 14. Testing Standards

Tests must be readable, deterministic, and focused.

Use:

- xUnit for automated tests.
- NSubstitute for unit-test substitutes where appropriate.
- Actual SQL Server instances for database integration and concurrency tests.

Tests must cover:

- Domain invariants and boundary conditions.
- Input validation and monetary precision.
- Application use cases.
- Authorization behavior.
- Idempotency and concurrent duplicate requests.
- Transfer atomicity.
- Negative-balance prevention.
- Maximum-balance enforcement.
- Error mapping and safe error responses.

Use descriptive test names that state the scenario and expected outcome.

Avoid tests that depend on execution order, uncontrolled external services, or shared mutable database state.

## 15. Logging and Observability

Use structured logging through the application's configured logging infrastructure.

- Include useful operation context and trace identifiers.
- Record unexpected failures and relevant operational events.
- Avoid credentials, tokens, connection strings, and sensitive headers.
- Do not treat expected business conflicts as unexpected application crashes.
- Never modify or delete ledger entries merely to simplify diagnostic workflows.

The detailed observability requirements belong in `../operations/observability.md`.

## 16. Comments and Documentation

Comments should explain why code behaves a particular way when that reason is not obvious.

Prefer clear code over comments that simply restate it.

Document:

- Non-obvious financial invariants.
- Concurrency and locking decisions.
- Transaction boundaries.
- Idempotency behavior.
- Security-sensitive configuration.
- Deviations from the architecture or coding standards.

Update the relevant documentation when an API contract, domain rule, or architectural decision changes.

## 17. Prohibited Practices

The following practices are not permitted:

- Using binary floating-point types for money.
- Silently rounding invalid monetary input.
- Trusting client-supplied sender identity.
- Performing financial updates without concurrency protection.
- Persisting only part of a transfer.
- Creating duplicate financial effects on a successful idempotent retry.
- Exposing internal exception details through the API.
- Enabling development authentication in Production.
- Bypassing migrations through undocumented schema changes.
- Treating mocks or EF Core InMemory tests as proof of SQL Server concurrency correctness.

## 18. Open Decisions

1. Finalize `.editorconfig` and analyzer severity.
2. Decide whether to enable warnings-as-errors in CI.
3. Finalize the application result and error representation conventions.
4. Decide which use cases require explicit SQL transaction management.
5. Finalize the repository's testing naming conventions.
6. Decide whether analyzers beyond the .NET SDK defaults are required.
