# Development Setup

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document describes how to prepare a local development environment, configure SQL Server, initialize the database, run the Wallet API, and execute automated tests.

The instructions target the proposed stack:

- .NET 10 SDK
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- xUnit
- NSubstitute

Exact package versions, project names, and launch profiles must be confirmed when the repository structure is created.

## 2. Prerequisites

Install the following tools:

| Tool | Purpose |
|---|---|
| .NET 10 SDK | Build and run the application |
| Git | Source control |
| SQL Server | Relational database |
| SQL Server Management Studio, Azure Data Studio, or another SQL client | Inspect and manage the database |
| IDE or editor | Develop and debug the application |

- Node.js `^22.18.0` or `>=24.12.0` and npm — for the frontend scaffold.

Docker is recommended for running SQL Server locally if a suitable Docker environment is available.

Verify the .NET installation:

```bash
dotnet --info
dotnet --list-sdks
```

The installed SDK must support the target framework used by the repository.

Verify Docker if it will be used:

```bash
docker --version
docker compose version
```

## 3. Clone and Inspect the Repository

Clone the repository and move to its root directory:

```bash
git clone <repository-url>
cd wallet-api
```

The repository URL is a placeholder and must be replaced with the actual location.

Before running commands, inspect the solution and project files:

```bash
dotnet sln list
```

If the solution has not yet been created, initialize it according to the agreed repository structure before proceeding.

## 4. Configuration

Use environment-specific configuration for local development.

The application requires a SQL Server connection string. Authentication-related configuration may also be required depending on the selected development authentication mechanism.

Example local configuration values:

```text
ASPNETCORE_ENVIRONMENT=Development
```

Example connection string format:

```text
Server=localhost,1433;
Database=WalletApi;
User Id=sa;
Password=<local-development-password>;
Encrypt=True;
TrustServerCertificate=True;
```

The connection string is illustrative. Its exact format depends on the SQL Server deployment and authentication method.

### Configuration Rules

- Never commit real credentials or production connection strings.
- Use local environment variables, user secrets, or an equivalent secure development mechanism.
- Keep development and test databases separate.
- Do not use production data for local testing.
- Do not enable development authentication in Production.
- Treat `TrustServerCertificate=True` as a local-development convenience, not a recommended production default.

If ASP.NET Core User Secrets are configured for the API project, initialize them with:

```bash
dotnet user-secrets init --project src/Backend/PS.Wallet/PS.Wallet.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local-connection-string>" --project src/Backend/PS.Wallet/PS.Wallet.API
```

Replace the project path and connection string with the actual values.

## 5. Start SQL Server with Docker

If Docker is used, create a local SQL Server container.

Example command:

```bash
docker run \
  --name wallet-api-sqlserver \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='<local-development-password>' \
  -p 1433:1433 \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Use a strong local password that satisfies SQL Server's password policy.

Check the container status:

```bash
docker ps
docker logs wallet-api-sqlserver
```

Wait until SQL Server has completed initialization before applying migrations.

Stop and restart the container when needed:

```bash
docker stop wallet-api-sqlserver
docker start wallet-api-sqlserver
```

This setup is intended for local development only. Container persistence, data volumes, and cleanup procedures should be defined in the repository's Docker configuration.

## 6. Restore Dependencies and Build

From the repository root:

```bash
dotnet restore
dotnet build
```

The build must complete successfully before running the API or tests.

If the solution contains multiple projects, verify that all required projects are included in the solution and that project references follow the documented architecture.

## 7. Apply Database Migrations

Entity Framework Core migrations define the database schema.

After the database is available and the connection string is configured, apply migrations using the repository's documented approach.

For example, if the required EF Core tools and project structure are configured:

```bash
dotnet ef database update \
  --project src/Backend/PS.Wallet/PS.Wallet.Infrastructure \
  --startup-project src/Backend/PS.Wallet/PS.Wallet.API
```

Replace the placeholders with the actual project paths.

If the EF Core command-line tool is not installed, install or invoke the version compatible with the repository's EF Core packages.

### Database Initialization

Database initialization must be deterministic and safe to repeat where applicable.

The initial setup should:

- Create the schema through migrations.
- Apply the agreed development seed data.
- Create pre-seeded users such as Alice and Bob only in the intended development or test environment.
- Ensure each seeded user has the expected wallet.
- Avoid inserting duplicate seed records on repeated application startup.

The seed data consists of two development users:

- Alice — username `alice`, email `alice@example.com` (`CONST-024`).
- Bob — username `bob`, email `bob@example.com` (`CONST-025`).

Each user has exactly one wallet starting at `0.00` (`DR-047`). No opening ledger entry is created.

Seed application must be idempotent. It must not create duplicate users, wallets, or ledger entries when applied multiple times. The mechanism may use EF Core `HasData` in a development-only migration, or a startup seeder with `IF NOT EXISTS` semantics. Production must not run development seed data.

Do not manually modify the schema in a way that bypasses migrations.

## 8. Run the API

Start the API project:

```bash
dotnet run --project src/Backend/PS.Wallet/PS.Wallet.API
```

The actual project path and launch profile depend on the final solution structure.

Check the startup output for the listening URL. If Swagger or OpenAPI is enabled for development, use the configured endpoint to inspect the API contract.

Verify that:

- The API starts successfully.
- SQL Server is reachable.
- The database schema is up to date.
- The expected development authentication mechanism is available only in the intended environment.
- The documented endpoints are registered.

Do not assume that a particular port, Swagger path, or launch profile exists until it is defined in the repository.

## 9. Run Automated Tests

Run all tests:

```bash
dotnet test
```

For a specific test project:

```bash
dotnet test tests/PS.Wallet.UnitTests
```

The test suite should include:

- Domain unit tests.
- Application and validation tests.
- API integration tests.
- SQL Server integration tests for transaction atomicity, idempotency, and concurrency.

### Integration Test Database

Integration tests that verify database behavior must use an actual SQL Server instance compatible with the application.

EF Core's InMemory provider is not a substitute for testing SQL Server transactions, locking, uniqueness constraints, or concurrent updates.

Use a separate test database and ensure the test setup does not connect to a production database.

Tests should clean up their data or use isolated database state so results are repeatable.

## 10. Local Development Workflow

A typical development cycle is:

1. Pull the latest source changes.
2. Review migration and configuration changes.
3. Start SQL Server.
4. Restore dependencies.
5. Apply database migrations when needed.
6. Build the solution.
7. Run unit and integration tests.
8. Start the API.
9. Exercise endpoints using the available API documentation or an HTTP client.
10. Review logs and test results before committing changes.

Database migrations should be reviewed alongside changes to domain entities and persistence configuration.

## 11. Troubleshooting

### SQL Server Connection Failure

Check that:

- The SQL Server process or container is running.
- The host and port match the connection string.
- Credentials are correct.
- The database server is reachable from the API process.
- Encryption settings match the local SQL Server configuration.

### Migration Failure

Check that:

- The configured connection string targets the intended database.
- The migration project and startup project are correct.
- The database account has the required development permissions.
- The migration history is consistent with the schema.

Do not resolve migration failures by deleting a database that contains data you need to preserve.

### Build Failure

Check the installed SDK, target framework, package restore output, and project references.

### Integration Test Failure

Check that SQL Server is available and that the tests use the intended test database. Investigate isolation, cleanup, migration state, and concurrency assumptions before changing assertions.

## 12. Security Notes

This setup is for local development.

- Never expose a development SQL Server instance publicly.
- Never use production credentials in local configuration.
- Never seed development identities into Production.
- Do not assume local authentication settings are safe for deployment.
- Do not commit local secrets, database backups, or sensitive logs.

Production deployment instructions belong in the operations documentation.

## 13. Open Decisions

1. Confirm the final solution and project names. (unique to this document)
2. Decide whether Docker Compose will be the standard local SQL Server setup. (unique to this document)
3. Define the supported SQL Server version and image tag. (unique to this document)
4. Finalize the development authentication mechanism. (resolved in ADR-007)
5. Decide how seed data is applied and restricted by environment. (resolved in OQ-032; setup.md §7)
6. Define the API's local URL, launch profiles, and OpenAPI configuration. (unique to this document)
7. Define the integration-test database lifecycle. (tracked in OQ-026)
8. Confirm whether a repository-level setup script will automate initialization. (unique to this document)

## 14. Frontend Scaffold

The frontend scaffold lives at `src/Frontend/PS.Wallet/`. It is not part of the MVP but is included for future development.

```bash
cd src/Frontend/PS.Wallet
npm ci
npm run dev
```

Dev server: http://localhost:5173 (strict port).

Vite proxies /api to the backend at http://localhost:5254.

The backend must be running for API calls to succeed.

The frontend currently contains placeholder views and no business functionality.
