# AGENTS.md

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `AGENTS.md`

## 1. Project Overview

Wallet API is an educational backend-first project demonstrating production-oriented engineering practices for financial operations.

The application provides virtual USD wallets, deposits, transfers, and transaction history. It does not handle real money or integrate with banks or payment processors.

The project prioritizes:

- Financial correctness.
- Database transaction atomicity.
- Concurrency safety.
- Idempotent financial operations.
- Explicit domain rules.
- Security and authorization.
- Automated testing.
- Clear architecture and documentation.

## 2. Mandatory Rules

All contributors and AI agents MUST follow the requirements and domain rules documented in this repository.

Before changing code, review the relevant requirements, architecture documents, API contracts, ADRs, and open questions.

Do not silently change established domain invariants, security boundaries, API behavior, or accepted architectural decisions.

If requirements conflict or a critical behavior is undefined, identify the conflict and document the proposed resolution instead of inventing an authoritative rule.

## 3. Architecture

The proposed architecture is a Clean Architecture-inspired modular monolith.

The intended dependency direction is:

- Domain contains core business concepts and rules.
- Application coordinates use cases and application-level policies.
- Infrastructure implements persistence and external technical concerns.
- API exposes HTTP endpoints and handles transport concerns.

Dependencies MUST follow the architecture documented in `docs/architecture/architecture.md`.

Business rules MUST NOT depend on HTTP controllers, database-specific APIs, or frontend implementation details.

Do not introduce microservices, unnecessary abstractions, or additional frameworks without a clear requirement and an appropriate decision record.

## 4. Financial Domain Rules

The following invariants MUST be preserved:

- All monetary values represent USD.
- C# monetary calculations use `decimal`, never `float` or `double`.
- Persisted monetary amounts use SQL Server `decimal(19,2)` per `ADR-002` and `docs/project/constants.md`.
- Monetary input with more than two fractional decimal digits is rejected; it is not silently rounded.
- Deposit and transfer amounts must be positive.
- The maximum operation amount and wallet balance are defined in `docs/project/constants.md` and enforced per `DR-005`, `DR-006`.
- A wallet balance must never become negative.
- A transfer must debit the sender and credit the recipient by equal amounts.
- A failed transfer must not leave a partial financial result.
- Financial operation records, ledger entries, balance changes, and successful idempotency results must be committed atomically.
- Ledger history is append-only during normal application operation.
- Corrections to completed financial operations require an explicitly designed compensating operation.

Do not weaken these invariants to make a test pass.

## 5. Transactions and Concurrency

Never implement a balance update using an unprotected read-then-update sequence.

Financial writes MUST use appropriate database transaction boundaries and concurrency protection.

Concurrent requests MUST preserve all wallet and ledger invariants.

Transfers involving multiple wallets must follow the documented lock-ordering strategy once finalized.

Handle deadlocks and transient failures only according to a documented, bounded retry policy.

Do not claim that application-level validation alone prevents concurrent balance violations.

Integration tests for concurrency MUST use an actual SQL Server environment rather than relying exclusively on mocks or EF Core InMemory.

## 6. Idempotency

Deposit and transfer endpoints require the documented `Idempotency-Key` contract.

The implementation MUST:

- Scope keys according to the accepted idempotency design.
- Detect reuse of a key with a different request.
- Prevent concurrent duplicate requests from creating duplicate financial effects.
- Persist the successful idempotency result atomically with the financial operation.
- Return the original successful result when the same logical request is replayed.
- Follow the documented behavior for failed requests and uncertain commits.

Do not generate a new key automatically when retrying an operation whose outcome is unknown.

Do not introduce a retry policy that can duplicate financial effects.

## 7. API and Validation

Follow the versioned API contract under `docs/api/`.

Use the documented endpoint paths, HTTP methods, status codes, monetary representations, pagination model, and Problem Details error format.

Validate external input at the API boundary and enforce critical financial invariants in the domain and persistence layers as appropriate.

Never trust a client-supplied sender identifier. The authenticated principal determines the current user's identity.

Do not expose internal exception details, stack traces, credentials, or sensitive implementation information in API responses.

## 8. Security

The backend MUST enforce authentication and authorization independently of frontend behavior.

Users may access only the wallet data and transfer details permitted by the accepted security model.

Development authentication MUST NOT be available in Production.

Never commit credentials, access tokens, production secrets, or sensitive user data.

Do not log authentication secrets or unnecessary financial or personal information.

Review `docs/security/security-model.md` and `docs/security/threat-model.md` before changing authentication, authorization, configuration, or logging behavior.

## 9. Database and Migrations

Use Entity Framework Core migrations for schema changes unless an accepted decision specifies otherwise.

Review migrations for:

- Monetary precision.
- Unique constraints.
- Foreign keys.
- Nullability.
- Check constraints.
- Indexes.
- Data preservation.
- Compatibility with existing records.

Do not modify an already shared migration to conceal a schema change. Create a new migration when required.

Do not apply destructive migrations to production without an explicit, reviewed deployment and recovery plan.

Follow `docs/development/database-migrations.md`.

## 10. Coding Standards

Follow `docs/development/coding-standards.md`.

General requirements:

- Use meaningful names.
- Prefer explicit, readable code.
- Enable nullable reference types where supported by the project.
- Use asynchronous APIs for asynchronous I/O.
- Propagate cancellation tokens where appropriate.
- Avoid unnecessary duplication and speculative abstractions.
- Keep domain behavior testable.
- Do not suppress warnings without justification.
- Do not introduce dependencies without explaining their purpose.

## 11. Testing

Add or update tests for every behavior change.

Use:

- Unit tests for domain rules and isolated application logic.
- API tests for HTTP behavior and validation.
- SQL Server-backed integration tests for persistence, constraints, transactions, idempotency, and concurrency.
- End-to-end tests where they provide meaningful coverage of critical workflows.

Financial tests MUST cover relevant failure paths, not just successful requests.

Never weaken or remove a meaningful test solely to make the build pass.

Follow `docs/development/testing.md` and the frontend testing guide when applicable.

## 12. Frontend

Frontend is **out of MVP scope** for **business functionality**. The `docs/frontend/` documents are drafts for a future phase.

A **scaffold** exists at `src/Frontend/PS.Wallet/` (Vue 3 + TypeScript + Vite + Pinia + axios + naive-ui) for future development. It contains:

- Layout components (`AppHeader`, `AppFooter`, `MainLayout`).
- Placeholder views (`HomePage`, `AboutPage`).
- An empty API client (`src/services/api/client.ts`).

The scaffold does **not** implement any wallet business functionality. Do not extend it with routing to wallet screens, business logic, state management for financial data, or API calls beyond health checks until the project scope is explicitly changed (see `OQ-031`).

If frontend code is implemented, follow the `docs/frontend/` documentation.

The frontend MUST preserve monetary precision, use the documented API contract, handle authentication correctly, and distinguish confirmed results from uncertain financial operation outcomes.

Client-side validation and route guards MUST NOT be treated as substitutes for backend validation and authorization.

### Frontend Dependency Policy

The committed scaffold uses Vue 3, TypeScript, Vite, Pinia, axios, and naive-ui. These choices are recorded as the frontend baseline for future phases.

Adding a **new** frontend framework, build tool, state-management library, HTTP client, component library, or other UI dependency requires an explicit decision record:

- Update `docs/project/decisions.md` or create an ADR.
- Reference the decision in the pull request.
- Do not introduce a dependency that replaces or duplicates the existing baseline without justification.

This rule applies to `src/Frontend/PS.Wallet/` even though the frontend is out of MVP scope for business functionality.

## 13. Documentation and Decisions

Update relevant documentation when changing requirements, domain behavior, API contracts, configuration, security controls, or operational procedures.

Record significant architectural and technical decisions in `docs/adr/` and update `docs/project/decisions.md`.

The decision register and ADRs MUST distinguish Proposed decisions from Accepted decisions.

Do not present an unresolved proposal as a finalized implementation requirement.

Track unresolved questions in `docs/project/open-questions.md`.

### Open questions and known issues

Two registers track unresolved and deferred items:

- `docs/project/open-questions.md` — decisions that are not yet made. Every entry has an `OQ-NNN` ID, a priority (`P0`–`P3`), a status (`Open` / `Proposed` / `Decided` / `Deferred`), and a target phase.
- `docs/project/known-issues.md` — accepted deviations or deferred workarounds. Every entry has a `KI-NNN` ID, a status (`Open` / `Resolved` / `Wontfix`), a `Risk`, a `Mitigation`, and a `Revisit by`.

Rules:

- Do not invent new IDs. Use the next sequential number.
- Do not silently resolve an OQ or KI. Update the entry with a `Decision:` (for OQ) or `Mitigation`/`Resolved` (for KI), and update affected documents.
- A `Decided` OQ must contain a `Decision:` line that names the implementation location (ADRs, constants, endpoints, etc.).
- A `Resolved` KI must state what changed and where.
- When an OQ or KI blocks a phase, reference it explicitly in the corresponding roadmap phase.

## 14. Format and Naming Conventions

Numeric constants appear in two forms, and the form depends on context:

- **Prose** (sentences, explanations, requirement text): use thousands separators — `1,000,000.00`.
- **Machine-readable** (JSON examples, SQL, code, table cells under a "Value" column, regex): no separators — `1000000.00`.

Both forms must represent the same value. When a document needs a numeric constant that is listed in `docs/project/constants.md`, reference the constant by name instead of restating the value, unless the value is required for readability in that specific document.

Do not introduce a third format (e.g. `1 000 000.00`, `1_000_000.00`).

### Entity vs table vs concept naming

`Wallet` requires disambiguation:

- `Wallet` (capital W, prose) — the business concept.
- `WalletAccount` — the C# entity class `PS.Wallet.Domain.Entities.WalletAccount`.
- `Wallets` — the SQL Server table name.
- `wallet` (lowercase) — a generic instance in prose.

The C# entity is `WalletAccount` because the root namespace `PS.Wallet` shadows the simple name `Wallet`. Do not introduce a C# type named `Wallet` in any `PS.Wallet.*` project.

Other entities (`User`, `Deposit`, `Transfer`, `LedgerEntry`, `IdempotencyRecord`) do not have this collision and keep their natural names.

### JSON serialization of monetary values

Monetary `decimal` values are serialized as JSON **strings** with exactly two fractional digits (`"0.00"`, `"125.50"`, `"1000000.00"`), per `ADR-002 §9`. JSON numbers are rejected on input.

The global `MoneyJsonConverter` (`src/Backend/PS.Wallet/PS.Wallet.API/Json/MoneyJsonConverter.cs`) implements this contract:

- Writes `decimal` as `"0.00"` using invariant culture.
- Reads only JSON strings; a JSON number raises `JsonException`.

Register the converter for **both** JSON pipelines. `ConfigureHttpJsonOptions` affects `WriteAsJsonAsync` and minimal APIs; it does **not** affect MVC controllers. Register it in both places:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter()));

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new MoneyJsonConverter()));
```

Do not introduce per-property `[JsonConverter]` attributes on DTOs. The converter is global.

Integration tests that deserialize API responses must use `HttpClient.ReadFromJsonAsync<T>` or `JsonSerializerDefaults.Web`, which include `NumberHandling = AllowReadingFromString` and therefore accept monetary values as strings. Tests must not use raw `JsonSerializer.Deserialize` without those options for monetary DTOs.

## 15. Phase 1 Build Infrastructure

Phase 1 creates the build and solution infrastructure defined in `docs/project/roadmap.md §5`:

- `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` at the repository root.
- `src/Backend/PS.Wallet/PS.Wallet.slnx` (`.slnx` format).
- Backend projects `PS.Wallet.Domain`, `PS.Wallet.Application`, `PS.Wallet.Infrastructure`, `PS.Wallet.API` under `src/Backend/PS.Wallet/`.
- Test projects `PS.Wallet.UnitTests`, `PS.Wallet.IntegrationTests` under `tests/`.

Do not create `.sln` files, do not use a different project-name prefix, and do not add packages outside the Phase 1 package set. Future packages are added by their respective phases and recorded in `Directory.Packages.props`.

## 16. Development Workflow

Before making changes:

1. Inspect the existing implementation and relevant documentation.
2. Identify the requirements and invariants affected.
3. Check related ADRs and open questions.
4. Determine the smallest appropriate change.
5. Identify tests required to validate the change.

After making changes:

1. Run relevant tests.
2. Run formatting, static analysis, and build checks where available.
3. Review the diff for unintended changes.
4. Update documentation when needed.
5. Report what changed, which checks ran, and any unresolved issues.

Never claim that tests, migrations, builds, or other commands succeeded unless they were actually executed and their results were observed.

## 17. Scope Control

The MVP uses virtual USD balances only.

Do not introduce real payment processing, bank integration, multi-currency support, production account registration, or distributed services unless the project scope is explicitly changed and the relevant decisions are reviewed.

Prefer the simplest implementation that satisfies documented requirements and preserves financial invariants.

## 18. Handling Uncertainty

When a required decision is missing:

- Do not invent an accepted decision.
- Identify the specific ambiguity.
- Explain its impact on correctness, security, or implementation.
- Propose a reasonable option when useful.
- Update the relevant open question or decision record after agreement.

Correctness and data integrity take precedence over implementation convenience.
