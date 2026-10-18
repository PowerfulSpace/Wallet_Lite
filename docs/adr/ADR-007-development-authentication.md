# ADR-007: Development Authentication

- **Status:** Accepted
- **Date:** 2026-10-18
- **Decision Owners:** Project Maintainers

## 1. Context

The Wallet API MVP requires authenticated user identity to enforce wallet ownership and prevent unauthorized financial operations.

A full identity-management system with user registration, password recovery, email verification, and account lifecycle management is outside the initial project scope.

However, local development and automated testing need a convenient way to act as different users, such as Alice and Bob, without integrating an external identity provider.

The development authentication mechanism must be simple to configure while maintaining a strict security boundary between Development and Production.

## 2. Decision

Provide a simplified authentication mechanism exclusively for the Development environment.

The mechanism may allow developers to select a predefined test identity, such as Alice or Bob, through a development-only authentication handler or equivalent isolated implementation.

The mechanism must not be available in Production.

Production authentication must use a properly configured authentication scheme appropriate to the deployment environment. The specific provider and protocol remain open decisions.

Development authentication must be treated as a testing convenience, not as a production authentication system.

## 3. Development Identities

The development environment should provide predefined test users, including:

- Alice
- Bob

Each identity must map to a persisted application user and that user's wallet.

The seed data should be deterministic so developers and automated tests can reproduce expected scenarios.

The seed process must not create duplicate users or wallets when it is executed more than once.

Development identity selection must not grant access to arbitrary user IDs or bypass the normal wallet-ownership authorization rules.

## 4. Authentication Mechanism

The implementation may use a dedicated ASP.NET Core authentication handler or another isolated development-only scheme.

For example, a local request might identify a predefined user through a development-only header.

Any such header is untrusted input and must only be interpreted by the development authentication scheme.

The implementation must ensure that:

- The selected identity belongs to the approved development test identities.
- The authenticated principal is created by the server.
- Invalid identity values are rejected.
- Normal application authorization rules remain active.
- The development mechanism is not registered or enabled in Production.

The exact header format and authentication-handler implementation must be documented in the setup guide once selected.

## 4.1 Identity Mapping

The development authentication handler produces a `ClaimsPrincipal` with a `Name` claim equal to the canonical lowercase username (`alice` or `bob`).

`ICurrentUser` (Application) exposes `Username`. Application handlers resolve the internal `UserId` by querying `Users` by `Username`.

This keeps the authentication mechanism independent of the database: the handler does not need a `DbContext`, and the application layer owns the mapping from authenticated identity to persisted user.

## 5. Environment Isolation

Development authentication must be explicitly environment-restricted.

The implementation must fail safely if development authentication is requested while the application is running in Production.

At minimum:

- Production must not register the development authentication scheme.
- Production must not accept development identity headers as proof of identity.
- Production startup must validate its authentication configuration.
- Environment-specific configuration must be explicit and reviewable.
- Automated tests must verify that the development scheme is unavailable in Production.

The application must not rely solely on a client-side flag, an HTTP header, or a convention to distinguish trusted environments.

## 6. Authorization Requirements

Development authentication must not weaken the application's authorization rules.

The current user's identity must be obtained from the authenticated principal.

The API must not trust a sender user ID supplied in the transfer request or use an arbitrary client-provided user ID to determine wallet ownership.

Authorization must ensure that:

- A user can query only their own wallet balance.
- A user can view only their own wallet transaction history.
- A user cannot initiate a transfer on behalf of another sender.
- A user cannot retrieve a transfer unless they are its sender or recipient.
- A user cannot use an idempotency key to access another user's stored response.

These requirements apply in Development and Production.

## 7. Test Strategy

Automated tests must verify:

- Alice can authenticate using the development mechanism in Development.
- Bob can authenticate using the development mechanism in Development.
- An unknown development identity is rejected.
- Unauthenticated requests to protected endpoints are rejected.
- Alice cannot access Bob's wallet or private transaction history.
- Alice cannot initiate a transfer using Bob as the sender identity.
- Transfer access is limited to the sender and recipient.
- The development authentication scheme is not enabled in Production.
- Production startup fails safely when required production authentication configuration is missing or invalid.

Integration tests should exercise the actual ASP.NET Core authentication and authorization pipeline.

## 8. Operational Requirements

The application must make its active authentication configuration clear through safe startup diagnostics.

Diagnostics must not expose credentials, tokens, secrets, or sensitive configuration values.

Development-only configuration must be documented separately from Production configuration.

Production deployments must not reuse development authentication settings or test identity selection mechanisms.

## 9. Consequences

### Positive Consequences

- Local development does not require an external identity provider.
- Tests can reproduce user-specific wallet scenarios.
- Developers can test authorization between Alice and Bob.
- Authentication remains separated from application business rules.
- The Production security boundary is explicit.

### Negative Consequences

- A dedicated development authentication implementation must be maintained.
- Environment-specific configuration requires validation.
- The actual Production identity provider remains to be selected.
- Tests must guard against accidental exposure of the development scheme.

## 10. Alternatives Considered

### Full Identity Management in the MVP

Not selected because user registration, password lifecycle management, and account recovery are outside the current scope.

### Trusting Arbitrary User IDs in Requests

Rejected because clients could impersonate other users and access or modify their wallets.

### Enabling Development Authentication Everywhere

Rejected because a development identity selector is not a secure Production authentication mechanism.

### External Identity Provider From the Start

Deferred because it would introduce additional setup and configuration requirements before the core financial behavior has been implemented.

A production identity provider may be introduced later without changing the principle that the authenticated principal determines the current user.

## 11. Open Decisions

**Status note:** Accepted for the MVP. The production authentication provider (OQ-021) and production secret management are tracked as deferred in `open-questions.md §12`. The development authentication mechanism is implemented and verified in Phase 3.

1. Select the exact development authentication handler and header format. (resolved in ADR-007 §4.1)
2. Define how authenticated external identities map to application users. (resolved in OQ-022)
3. Choose the Production authentication provider and protocol. (tracked in OQ-021)
4. Define how missing or invalid Production authentication configuration is detected at startup. (unique to this document)
5. Decide how development seed data is initialized and reset. (resolved in OQ-032)
6. Define whether test identities may be used in non-production shared environments. (unique to this document)

## 12. References

- `docs/security/security-model.md`
- `docs/security/threat-model.md`
- `docs/development/setup.md`
- `docs/architecture/system-context.md`
- `docs/api/api-guidelines.md`
- `docs/requirements/non-functional-requirements.md`

