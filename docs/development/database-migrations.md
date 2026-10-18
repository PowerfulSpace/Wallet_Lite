# Database Migrations

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the process for creating, reviewing, applying, and maintaining Entity Framework Core database migrations for the Wallet API MVP.

Database schema changes must be version-controlled, repeatable, reviewable, and compatible with the application's data-integrity requirements.

## 2. Core Principles

- All intentional schema changes must be represented by EF Core migrations.
- Migrations must be committed to source control.
- The database schema must not be modified manually in a way that bypasses the migration history.
- Migration changes must be reviewed alongside domain and persistence changes.
- Existing financial records must be preserved unless an explicit, reviewed data migration requires otherwise.
- Migrations must be tested against SQL Server.
- Production migration execution must be controlled and must not happen implicitly during ordinary application startup.

## 3. Migration Ownership

EF Core migrations belong to the project's designated persistence or Infrastructure project.

The API project may act as the startup project when EF Core tools require application configuration and dependency registration.

The final project names and migration assembly must be confirmed when the solution structure is created.

The application must use a consistent migration assembly and configuration across local development, tests, and deployment.

## 4. Creating a Migration

Before creating a migration:

1. Update the relevant domain model, persistence configuration, or entity mapping.
2. Confirm that the change is consistent with the architecture and data-model documentation.
3. Build the solution.
4. Review the expected schema change.

Example command:

```bash
dotnet ef migrations add AddTransferReference \
  --project <path-to-infrastructure-project> \
  --startup-project <path-to-api-project>
```

The migration name should describe the change clearly.

Examples:

- `CreateInitialSchema`
- `AddIdempotencyRecords`
- `AddLedgerOperationIndexes`
- `AddWalletBalanceConstraint`

Replace the example project paths with the actual repository paths.

## 5. Reviewing Migration Files

Every migration must be reviewed before it is applied to a shared environment.

Review the generated migration and its model snapshot.

Verify that:

- Only intended tables, columns, indexes, and constraints are changed.
- Monetary columns use the expected SQL types and precision.
- Foreign keys and unique constraints match the data model.
- Required indexes are present.
- Existing data is not accidentally deleted or overwritten.
- Any data transformation is explicit and safe.
- The rollback method does not destroy data unexpectedly.

EF Core-generated migrations are not automatically correct. Review is required even when generation succeeds without errors.

## 6. Applying Migrations Locally

Configure the local SQL Server connection and apply migrations:

```bash
dotnet ef database update \
  --project <path-to-infrastructure-project> \
  --startup-project <path-to-api-project>
```

This command applies unapplied migrations to the configured database.

Before execution, confirm that the connection string targets the intended local database.

Do not run migration commands against a production database using local development configuration.

## 7. Migration Testing

Migrations must be tested against a supported SQL Server version.

At minimum, verify that:

- A new empty database can be created from the migration history.
- All migrations apply successfully in sequence.
- The resulting schema matches the expected model.
- Required constraints and indexes exist.
- Seed data is created only in the intended environment.
- Existing records are preserved when applying incremental migrations.
- The application can read and write data using the migrated schema.

For significant schema changes, test both a clean database and an existing database containing representative data.

EF Core InMemory is not suitable for validating SQL Server schema changes, constraints, or migration behavior.

## 8. Financial Data Integrity

Database migrations must preserve the financial invariants defined in the domain and data-model documentation.

### Money

- Monetary columns must use the agreed decimal representation.
- Do not migrate money columns to `float` or `real`.
- Precision or scale changes require explicit analysis of existing values.
- Any conversion must reject or account for values that cannot be represented without loss.

### Wallet Balances

Changes to wallet balance storage or constraints must ensure that negative balances remain prohibited and the configured maximum balance remains enforced.

### Ledger Entries

The ledger must remain append-only during normal application operation.

Migrations must not casually update or delete historical ledger entries. Corrections to financial history require a separately designed, auditable approach.

### Transfers and Deposits

Schema changes must preserve the relationship between operations and their ledger entries.

Any migration that changes these relationships must include validation of historical records and transaction consistency.

### Idempotency

Idempotency constraints must preserve the uniqueness rules defined by the API and architecture documentation.

Changes to key scope, request fingerprints, or stored response data require explicit compatibility and concurrency analysis.

## 9. Constraints and Indexes

Database constraints are part of the application's integrity model.

Review the following whenever the affected entities change:

- Primary keys.
- Foreign keys.
- Unique constraints.
- Check constraints where appropriate.
- Indexes used by wallet and history queries.
- Uniqueness constraints required for idempotency.
- Constraints that protect valid monetary values.

A check constraint alone may not enforce invariants involving concurrent changes across multiple rows. Such rules still require an appropriate transaction and concurrency strategy.

## 10. Data Migrations

A migration that transforms existing data must specify:

- Why the transformation is required.
- Which records are affected.
- How existing data is validated.
- Whether the transformation is lossless.
- How failure is detected.
- How the resulting data is verified.

Large or potentially destructive transformations require special planning and must not be treated as ordinary generated schema changes.

For financial data, prefer additive and auditable transformations over silent historical rewrites.

## 11. Rollback and Recovery

EF Core supports migration rollback, but rolling a schema backward is not necessarily safe after the application has written new data.

Before rolling back a migration:

1. Identify the affected schema and data.
2. Determine whether the down migration loses information.
3. Confirm application compatibility with the previous schema.
4. Ensure an appropriate backup or recovery procedure exists.
5. Define how data integrity will be verified afterward.

Do not assume that every migration can be safely reversed.

For destructive or high-risk changes, a forward-fix migration may be safer than a schema rollback.

Database backup and restoration procedures are documented separately in the operations documentation.

## 12. Application Startup

The application should not automatically apply arbitrary pending migrations in Production during ordinary startup.

Production schema changes should be applied through a controlled deployment step with appropriate permissions and verification.

Local development and automated test environments may apply migrations automatically when the process is explicitly configured to target an isolated development or test database.

## 13. Migration Compatibility

Schema changes must be evaluated against the version of the application that will run with the database.

For changes requiring multiple deployment steps, prefer an expand-and-contract strategy:

1. Add new schema elements without immediately removing old ones.
2. Deploy application code compatible with the transitional schema.
3. Migrate or backfill data when required.
4. Verify that the new representation is used correctly.
5. Remove obsolete schema elements in a later, separately reviewed migration.

This strategy reduces the risk of incompatible application and database versions during deployment.

The exact deployment sequence depends on the future hosting model and is outside the initial local-development scope.

## 14. Source Control Requirements

A schema change must include the relevant migration files and model snapshot.

The pull request should explain:

- The reason for the schema change.
- The affected entities and constraints.
- Whether existing data is modified.
- How the migration was tested.
- Whether deployment ordering or rollback considerations apply.

Do not commit temporary migration experiments or remove migration history without a documented reason.

## 15. Troubleshooting

### Migration Already Applied

Check the migration history table and the configured database before attempting to reapply or delete migrations.

### Unexpected Schema Changes

Inspect the migration code and model snapshot. Correct an uncommitted migration if appropriate; use a new migration for changes already shared or applied to an environment.

### Migration Failure

Identify whether the failure occurred during schema modification, data transformation, or constraint creation.

Do not assume that a failed deployment leaves the database unchanged. Inspect the actual database state and follow the documented recovery procedure.

### Model and Schema Mismatch

Verify the migration assembly, model snapshot, applied migration history, and database connection string.

Never resolve a mismatch by manually changing financial data or dropping a database without understanding the consequences.

## 16. Open Decisions

1. Confirm the Infrastructure project and migration assembly.
2. Define the supported SQL Server versions.
3. Decide whether local and test environments apply migrations automatically.
4. Choose how Production migration scripts are generated and reviewed.
5. Define database permissions for application runtime and migration execution.
6. Establish backup and recovery requirements before deploying to shared environments.
7. Decide whether migration compatibility checks are required in CI.
