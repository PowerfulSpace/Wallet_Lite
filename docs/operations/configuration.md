# Application Configuration

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines how the Wallet API application is configured across development, testing, and production environments.

It establishes conventions for configuration sources, database connectivity, secrets, environment-specific behavior, and configuration validation.

The goal is to make configuration predictable, secure, and reproducible without embedding environment-specific values in application code.

## 2. Configuration Principles

The application MUST follow these principles:

- Environment-specific values MUST NOT be hardcoded in application code.
- Secrets MUST NOT be committed to source control.
- Development-only authentication MUST NOT be available in Production.
- Configuration MUST be validated during application startup when invalid values could prevent safe operation.
- Database connection settings MUST be supplied through configuration.
- Configuration defaults MUST be safe and suitable for their documented purpose.
- Missing required settings MUST cause a clear startup failure rather than an unexpected runtime error.
- Sensitive configuration values MUST NOT appear in application logs or error responses.

## 3. Configuration Sources

The application is expected to use the standard ASP.NET Core configuration system.

Supported configuration sources may include:

1. `appsettings.json` for non-sensitive defaults.
2. `appsettings.{Environment}.json` for environment-specific, non-sensitive settings.
3. User Secrets for local development.
4. Environment variables for deployment-specific settings.
5. A dedicated secret manager for deployed environments, if introduced.

The application MUST follow the configured provider precedence. Values supplied by higher-priority providers override values supplied by lower-priority providers.

Environment-specific files MUST NOT contain production credentials or other sensitive values.

## 4. Environment Separation

The application distinguishes the following environments.

### 4.1 Development

Purpose:

- Local development and debugging.
- Use of seeded users such as Alice and Bob.
- Local SQL Server connectivity.
- Use of development-only authentication where explicitly enabled.

Development authentication MUST be restricted to the Development environment. It MUST NOT be activated solely because a configuration flag was accidentally enabled in another environment.

### 4.2 Test

Purpose:

- Automated unit, API, and integration tests.
- Isolated test data.
- Verification of database constraints, transactions, idempotency, and concurrency behavior.

Integration tests MUST use a real SQL Server instance or an equivalent SQL Server test environment. An in-memory database MUST NOT be treated as a substitute for testing SQL Server transaction and locking behavior.

Test credentials and database configuration MUST remain separate from development and production settings.

### 4.3 Production

Purpose:

- Running the deployed API in its intended hosted environment.

Production MUST use an explicitly configured authentication mechanism appropriate to the deployment. The development authentication mechanism MUST be unavailable.

Production configuration MUST:

- Obtain secrets from an approved secure source.
- Use a database identity with only the permissions required by the application.
- Avoid enabling detailed exception pages or sensitive diagnostic output.
- Use secure transport for external connections.
- Fail startup if essential security or database settings are invalid.

The production identity provider and deployment platform remain undecided.

## 5. Database Configuration

The API uses SQL Server through Entity Framework Core.

The database connection string MUST be supplied through configuration rather than embedded in source code.

A local development connection string may be configured using User Secrets or an environment variable.

Example environment variable name:

`ConnectionStrings__WalletDatabase`

Example placeholder configuration:

```json
{
  "ConnectionStrings": {
    "WalletDatabase": ""
  }
}
```

The empty value is a placeholder, not a usable connection string. The application MUST validate that the required connection string is present before attempting database operations.

### 5.1 Database Credentials

- Production credentials MUST NOT be committed to source control.
- The application MUST NOT log the complete connection string.
- Database access MUST use a dedicated application identity where practical.
- Database permissions MUST follow the principle of least privilege.
- Administrative database credentials MUST NOT be used by the API at runtime.

The exact authentication mode for SQL Server—SQL authentication, integrated authentication, or managed identity—depends on the deployment environment and remains an open decision.

## 6. Secrets Management

Secrets may include:

- Database credentials.
- Authentication signing keys or client secrets, if applicable.
- External service credentials introduced in future versions.

Secrets MUST NOT be stored in:

- Committed `appsettings.json` files.
- Source code or test fixtures.
- Example configuration files containing real values.
- Logs, exception messages, or API responses.
- Unprotected deployment scripts.

For local development, .NET User Secrets may be used. User Secrets are a development convenience and are not an encrypted production secret store.

For production, use the hosting platform's supported secret-management mechanism.

If the MVP does not require an application-owned signing key, one MUST NOT be introduced without a clear authentication requirement.

## 7. Configuration Categories

The following categories are expected to be configurable.

| Category | Examples | Sensitive? |
|---|---|---|
| Database | Connection string, command timeout | Yes, if credentials are embedded |
| Authentication | Provider settings, issuer, audience | Depends on the value |
| Development authentication | Enablement flag, permitted environment | Potentially |
| Logging | Log levels and output configuration | Usually no |
| API behavior | Maximum page size, if configurable | No |
| Diagnostics | Health-check behavior and tracing options | Depends on implementation |

Financial invariants MUST NOT be casually configurable.

The following rules are domain constraints, not ordinary operational tuning settings:

- Maximum operation amount: USD 1,000,000.00.
- Maximum wallet balance: USD 1,000,000.00.
- Monetary precision: two fractional decimal digits.
- Wallet balances MUST NOT become negative.
- A transfer MUST be atomic.

If any such rule becomes configurable in the future, the change MUST include explicit validation, documentation, and tests proving that changing it cannot violate financial invariants.

## 8. Startup Validation

The application SHOULD validate essential configuration during startup.

Validation SHOULD cover:

- Presence of the database connection string.
- Valid environment-specific authentication configuration.
- Prohibition of development authentication outside Development.
- Valid numeric ranges for supported operational settings.
- Consistency of settings that depend on one another.

Error messages MUST identify the invalid setting without exposing its secret value.

Invalid configuration that could compromise security or financial correctness MUST prevent the application from starting.

## 9. Logging Configuration

Logging configuration SHOULD support different verbosity levels for Development, Test, and Production.

Production logging SHOULD prioritize actionable operational information while avoiding excessive sensitive detail.

The application MUST NOT log:

- Passwords, tokens, or authentication secrets.
- Complete database connection strings.
- Full payment-like payloads when they contain sensitive data.
- Sensitive user data that is not required for diagnosis.

Financial operation logs SHOULD include a correlation or trace identifier and a relevant operation identifier when available. Logs MUST NOT be treated as the authoritative financial ledger.

See `docs/operations/observability.md` for logging, metrics, tracing, and health-check conventions.

## 10. Configuration Examples and Source Control

The repository SHOULD include safe example configuration containing placeholders and documented defaults.

Example files MUST:

- Be usable as a reference for required settings.
- Avoid real secrets.
- Clearly identify values that must be supplied locally or by deployment configuration.

Local overrides and secret files MUST be excluded from source control where appropriate.

Changes to required configuration MUST update the example configuration and setup documentation.

## 11. Configuration Changes

Configuration changes MUST be reviewed for their impact on:

- Security boundaries.
- Authentication and authorization.
- Database connectivity and permissions.
- Transactional behavior.
- Financial limits and domain invariants.
- Observability and operational recovery.

Changes that affect financial correctness MUST include relevant tests and MUST NOT rely on configuration alone to enforce database-level integrity.

## 12. Open Decisions

The following decisions remain open:

1. Which hosting platform will be used for Production?
2. Which production secret-management mechanism will be adopted?
3. Which SQL Server authentication mode will be used in each environment?
4. Which authentication provider will replace development-only authentication?
5. Which operational settings should be configurable in the MVP?
6. Should the application validate database connectivity at startup, or only validate connection-string configuration and report connectivity through health checks?
7. Which logging provider and structured logging format will be used?

These decisions should be resolved before the relevant deployment or implementation work is finalized.

## 13. Related Documents

- `docs/development/setup.md`
- `docs/development/database-migrations.md`
- `docs/security/security-model.md`
- `docs/operations/observability.md`
- `docs/operations/recovery.md`
- `docs/adr/ADR-007-development-authentication.md`
- `docs/requirements/domain-rules.md`
