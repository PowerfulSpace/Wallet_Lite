# Frontend Development Guide

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Scope:** Out of MVP; future phase.

## 1. Purpose

This document describes the proposed local development workflow for the Wallet API frontend.

It covers prerequisites, environment configuration, running the application, connecting to the backend, testing, building, and troubleshooting.

The frontend framework, package manager, and build tooling have not yet been finalized. Commands in this document are therefore illustrative until the implementation stack is selected.

## 2. Prerequisites

Developers should have the following installed:

- Git.
- The runtime required by the selected frontend framework.
- The package manager selected for the project.
- A code editor with support for the chosen language and framework.
- Access to a running Wallet API backend.
- Access to the development database and seed data through the backend development environment, when required.

The frontend SHOULD be developed against the backend's documented API contract rather than relying on undocumented implementation details.

## 3. Repository Structure

The proposed frontend structure is:

```text
wallet-api/
├── src/
│   ├── Wallet.Api/
│   ├── Wallet.Application/
│   ├── Wallet.Domain/
│   └── Wallet.Infrastructure/
├── tests/
├── frontend/
│   ├── src/
│   ├── public/
│   ├── tests/
│   ├── package.json
│   └── README.md
├── docs/
└── README.md
```

This is an illustrative structure, not a finalized repository layout.

The frontend SHOULD remain logically separated from the backend while sharing the same repository unless a different repository strategy is explicitly selected.

The frontend MUST NOT depend directly on backend implementation assemblies.

## 4. Environment Configuration

The frontend requires configuration for connecting to the Wallet API.

At minimum, the development environment needs a configured API base URL.

An illustrative configuration might be:

```text
VITE_API_BASE_URL=http://localhost:5000
```

This example assumes a Vite-based frontend and an API listening on the specified address. The actual variable name, port, and configuration mechanism must match the chosen stack and local backend configuration.

Configuration requirements:

- Local development settings MUST be documented.
- Environment-specific values SHOULD be kept outside application source code.
- Public frontend environment variables MUST NOT contain secrets.
- Production API endpoints MUST be configured separately from local development endpoints.
- Missing or invalid required configuration SHOULD be detected early.
- The frontend MUST NOT silently fall back to an unintended production endpoint.

The exact `.env` file naming and loading rules depend on the selected build tool.

## 5. Running the Backend

Before developing API-dependent frontend features, start the Wallet API using the instructions in `docs/development/setup.md`.

Verify that:

- The API starts successfully.
- The configured database is available.
- Database migrations have been applied.
- Development seed data is available when required.
- The API base URL matches the frontend configuration.

The frontend SHOULD provide a clear diagnostic message if it cannot reach the backend.

It MUST NOT display fabricated wallet balances or pretend that financial operations succeeded when the backend is unavailable.

## 6. Installing Dependencies

Install frontend dependencies using the package manager selected for the project.

For example, if npm is selected:

```bash
cd frontend
npm install
```

For reproducible CI and clean development installations, prefer the package manager's lockfile-based installation command, such as:

```bash
npm ci
```

The project MUST commit its dependency lockfile.

Developers SHOULD avoid manually editing installed dependencies or committing generated dependency directories.

The exact package manager and installation commands remain to be finalized.

## 7. Starting the Development Server

For a Vite-based implementation, a typical command would be:

```bash
npm run dev
```

The actual command MUST be documented in the frontend's `package.json` and project README.

The development server SHOULD:

- Provide clear startup and error messages.
- Support the team's chosen local development workflow.
- Avoid exposing unnecessary development services to the network.
- Use the configured backend URL.
- Keep development-only features separate from production behavior.

The frontend and backend may run on different local ports. If so, CORS and authentication-related cross-origin behavior must be configured deliberately.

## 8. Development Authentication

The initial project proposal includes development-only authentication for the pre-seeded users Alice and Bob.

The frontend development workflow SHOULD document how to access each development identity once the authentication mechanism has been implemented.

The exact mechanism remains an open decision.

Requirements:

- Development authentication MUST be available only in the intended development environment.
- Identity switching MUST NOT be exposed as a production impersonation feature.
- The backend MUST independently determine and validate the authenticated identity.
- Frontend configuration MUST NOT be treated as an authorization boundary.
- Development authentication MUST NOT be enabled in Production.

See `docs/frontend/authentication.md` and `docs/adr/ADR-007-development-authentication.md`.

## 9. API Connectivity and CORS

When the frontend and backend use different origins, the backend must allow the required development origin through an explicit CORS policy.

The configuration SHOULD:

- Allow only the intended development origins.
- Permit only the HTTP methods and headers required by the application.
- Support the selected authentication mechanism.
- Avoid permissive wildcard settings when credentials are involved.
- Be separated from production CORS configuration.

The frontend MUST NOT attempt to bypass browser security restrictions.

If API requests fail because of CORS or connectivity problems, the application SHOULD report a useful diagnostic message without exposing internal secrets.

## 10. Development Workflow

The recommended feature workflow is:

1. Read the relevant requirement and API documentation.
2. Confirm the API request and response contract.
3. Implement or update the relevant frontend types.
4. Implement the API integration.
5. Add component and unit tests.
6. Implement the UI and interaction states.
7. Test loading, success, validation, and failure scenarios.
8. Verify accessibility and responsive behavior.
9. Run the required static checks and production build.
10. Update documentation when behavior or configuration changes.

Financial features SHOULD be tested against a running backend before being considered complete.

## 11. Quality Checks

The frontend SHOULD provide package scripts for the checks supported by the selected stack.

Illustrative commands for a typical npm project include:

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

These commands are examples only; the corresponding scripts must be implemented before the commands can be relied upon.

The quality workflow SHOULD include:

- Static analysis.
- Type checking.
- Unit and component tests.
- API integration tests.
- Accessibility checks.
- Production build verification.
- Selected end-to-end tests.

The build and test process SHOULD be reproducible in local development and CI.

## 12. Financial Feature Development Rules

Developers working on financial UI features MUST follow the project's domain and API contracts.

In particular:

- Monetary values MUST preserve the documented decimal precision.
- The frontend MUST NOT silently round invalid amounts.
- The API's idempotency requirements MUST be followed.
- An uncertain operation outcome MUST NOT be reported as a confirmed failure.
- A successful operation MUST NOT be shown before confirmation from the backend.
- User-specific data MUST be cleared or invalidated when the authenticated identity changes.
- Client-side validation MUST NOT replace backend validation.

See `docs/frontend/forms-and-validation.md`, `docs/frontend/api-integration.md`, and `docs/requirements/domain-rules.md`.

## 13. Troubleshooting

### Frontend cannot reach the API

Check that the backend is running and that the configured base URL is correct.

### Browser reports a CORS error

Verify the frontend origin and backend development CORS policy. Do not disable browser security as a workaround.

### Authentication requests fail

Check the selected authentication flow, environment configuration, and browser cookie or token behavior where applicable.

### Wallet data appears stale

Verify that the frontend invalidates or refreshes cached data after financial operations and identity changes.

### Tests pass locally but fail in CI

Check runtime versions, lockfile consistency, environment variables, test data isolation, and dependencies on execution order.

### Production build contains development behavior

Review build-time configuration, feature flags, and environment-specific authentication controls. Development-only authentication must not be available in Production.

## 14. Production Build

The frontend SHOULD produce a reproducible production build through a documented command.

The production build MUST:

- Exclude development-only authentication behavior.
- Avoid embedding secrets in client-delivered assets.
- Use the intended production API configuration.
- Pass required static checks and tests.
- Avoid exposing unnecessary debugging information.
- Be verified independently of the development server.

Deployment and hosting details will be documented separately when the hosting model is selected.

## 15. Open Decisions

1. Which frontend framework will be used?
2. Which runtime and package manager will be required?
3. Will the frontend live in the repository root or in a dedicated subdirectory?
4. Which build tool and development server will be selected?
5. Which ports and API base URL will be used locally?
6. How will development authentication be exposed to developers?
7. How will CORS and local HTTPS be configured?
8. Which package scripts will be required?
9. Which CI platform and build artifacts will be used?
10. Where will the production frontend be hosted?

## 16. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/api-integration.md`
- `docs/frontend/authentication.md`
- `docs/frontend/testing.md`
- `docs/frontend/accessibility.md`
- `docs/development/setup.md`
- `docs/development/testing.md`
- `docs/operations/configuration.md`
- `docs/project/open-questions.md`