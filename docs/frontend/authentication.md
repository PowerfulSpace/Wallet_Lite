# Frontend Authentication

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the proposed frontend authentication integration for the Wallet API.

The frontend must interact with the authentication mechanism selected for the backend while protecting user-specific interface state and providing predictable navigation.

The production authentication provider, session model, and frontend authentication library have not yet been selected.

The current project proposal includes simplified development-only authentication for the pre-seeded users Alice and Bob. This mechanism MUST NOT be available in Production.

## 2. Authentication Principles

The frontend authentication implementation SHOULD follow these principles:

- The backend is responsible for verifying identity.
- The authenticated principal determines the user's identity for API operations.
- Client-side state MUST NOT be treated as proof of identity.
- Authentication and authorization are separate concerns.
- User-specific data MUST NOT be shared across authenticated identities.
- Authentication failures MUST be handled consistently.
- Development authentication MUST remain isolated from production configuration.
- Credentials and authentication secrets MUST NOT be logged or exposed in URLs.
- The authentication implementation SHOULD remain replaceable without requiring major changes to wallet features.

## 3. Authentication and Authorization

Authentication establishes the identity associated with a request.

Authorization determines whether that identity may access a resource or perform an operation.

The frontend MAY use authentication state to control navigation and improve the user experience. However, the backend MUST independently enforce authorization for every protected endpoint.

In particular:

- The sender of a transfer is derived from the authenticated principal.
- A user MUST NOT be able to access another user's wallet by changing client-side state.
- A user MUST NOT gain access to another user's transfer by modifying a route parameter.
- The frontend MUST NOT rely on hidden buttons or protected routes as security controls.

## 4. Authentication State

The frontend SHOULD represent authentication state explicitly.

A conceptual model includes:

- `loading`: authentication status is being resolved.
- `authenticated`: an authenticated identity is available.
- `unauthenticated`: no valid authenticated identity is available.
- `error`: authentication status could not be determined because of an unexpected failure.

The exact state representation depends on the selected framework and authentication provider.

The frontend MUST NOT treat an unresolved authentication state as authenticated.

## 5. Authentication Integration Boundary

Authentication logic SHOULD be isolated behind a dedicated integration layer.

That layer is responsible for:

- Obtaining the current authentication state.
- Initiating the supported authentication flow.
- Ending the session or signing out where supported.
- Providing the API client with the required authentication context.
- Handling authentication-related API responses.
- Clearing or invalidating user-specific application state when identity changes.

Wallet, deposit, transfer, and transaction-history features SHOULD NOT implement their own independent authentication mechanisms.

## 6. Development-Only Authentication

The current development proposal provides simplified access to two pre-seeded users:

- Alice.
- Bob.

The exact development authentication mechanism has not been finalized.

It may use a local development-only identity provider, a development authentication handler, or another controlled mechanism appropriate to the selected hosting model.

Regardless of implementation, the following requirements apply:

1. Development authentication MUST be explicitly restricted to Development.
2. It MUST be unavailable in Production.
3. Production startup MUST fail safely if development authentication is enabled.
4. The frontend MUST NOT expose a production-accessible identity-switching mechanism that allows users to impersonate Alice or Bob.
5. Development shortcuts MUST NOT bypass backend authorization checks.
6. Development configuration MUST be documented and tested separately from production configuration.

The presence of Alice and Bob in development seed data does not establish a production identity-management strategy.

## 7. Production Authentication

Production authentication remains an open architectural decision.

Before production authentication is implemented, the project must define:

- Identity provider.
- Supported authentication flow.
- Session or token model.
- Login and logout behavior.
- Session expiration and renewal.
- Credential or token storage.
- Cross-origin request requirements.
- Protection against cross-site request forgery where applicable.
- Authentication failure and recovery behavior.

The frontend MUST NOT invent its own authentication protocol or treat a user-supplied username as a verified identity.

The selected solution must be compatible with the backend's authentication and authorization model.

## 8. Session and Token Handling

The final session and token strategy depends on the selected authentication mechanism.

The frontend SHOULD follow these general rules:

- Use the storage mechanism recommended for the selected authentication architecture.
- Avoid storing credentials or tokens in insecure locations.
- Never place access tokens or session secrets in URLs.
- Do not log authentication headers or credential material.
- Handle expired or invalid sessions consistently.
- Clear user-specific state when the session ends or the authenticated identity changes.

If cookie-based authentication is selected, the project must define the appropriate cookie security attributes and CSRF protections.

If token-based authentication is selected, the project must define token acquisition, storage, renewal, and expiration handling.

These alternatives are not interchangeable implementation details; the final decision must account for the deployment and threat model.

## 9. Handling Authentication Errors

The frontend SHOULD handle authentication failures centrally through the API client or authentication integration layer.

### 9.1 Unauthorized Responses

When a protected API request returns `401 Unauthorized`, the frontend SHOULD:

1. Determine whether the authentication mechanism supports session recovery or renewal.
2. Attempt recovery only according to the documented authentication flow.
3. Avoid unbounded retry or redirect loops.
4. Clear invalid authentication state when appropriate.
5. Redirect to the supported authentication entry point if necessary.
6. Preserve a safe return destination where appropriate.

A failed financial write request MUST NOT be blindly resubmitted under a new idempotency key during authentication recovery.

If the operation outcome is uncertain, the frontend must preserve the operation's identity and follow the documented recovery behavior.

### 9.2 Forbidden Responses

If the backend returns `403 Forbidden`, the frontend SHOULD display an appropriate access-denied message.

It MUST NOT assume that changing the URL, user identifier, or local authentication state will grant access.

### 9.3 Network and Authentication Provider Failures

A network error or authentication-provider outage MUST NOT be interpreted as proof that the user is authenticated.

The frontend SHOULD distinguish an inability to verify authentication from an explicitly unauthenticated state.

## 10. User-Specific State and Identity Changes

When the authenticated identity changes, the frontend MUST prevent data belonging to the previous identity from remaining visible to the new identity.

The frontend SHOULD:

- Clear or invalidate cached wallet information.
- Clear transaction history.
- Clear transfer details.
- Reset authentication-dependent navigation state.
- Reset transient user-specific form state where appropriate.
- Prevent stale responses from repopulating the previous user's data after the identity change.

If a financial operation has an uncertain outcome, the application must handle its recovery metadata carefully rather than silently associating it with a different user.

## 11. Logout Behavior

If logout is supported, the frontend SHOULD:

1. Invoke the logout mechanism provided by the selected authentication solution.
2. Clear user-specific client state.
3. Navigate to an appropriate unauthenticated page.
4. Ensure that protected pages do not continue to display stale private data.

The backend or identity provider remains responsible for invalidating the applicable session or credentials where required by the chosen authentication model.

Client-side logout alone MUST NOT be assumed to invalidate server-side credentials.

## 12. Routing Integration

Protected routes MAY use authentication state to determine whether to display a page or redirect the user.

The routing layer SHOULD handle:

- Authentication initialization.
- Unauthenticated navigation.
- Session expiration.
- Access-denied responses.
- Safe return navigation after authentication.

Route guards improve usability but do not replace backend authorization.

See `docs/frontend/routing.md` for the routing contract.

## 13. Security and Privacy Requirements

The frontend MUST NOT:

- Store server-side secrets in browser-delivered code.
- Expose credentials in logs or URLs.
- Allow user-controlled sender identity to determine transfer ownership.
- Treat a client-side user ID as proof of identity.
- Enable development authentication in Production.
- Retain user-specific data across identity changes without an explicit security justification.

The implementation SHOULD minimize the amount of authentication-related information retained in client state.

Security-sensitive configuration and behavior must be verified through environment-specific tests.

## 14. Testing Requirements

Authentication tests SHOULD cover:

- Initial authentication-state resolution.
- Authenticated and unauthenticated navigation.
- Authentication initialization failures.
- Unauthorized API responses.
- Session expiration and recovery where supported.
- Logout and user-specific state cleanup.
- Identity changes between Alice and Bob in Development.
- Prevention of stale data exposure after identity changes.
- Safe redirect behavior.
- Development authentication being rejected in Production.
- Backend authorization independently enforcing ownership rules.

Tests MUST NOT rely exclusively on frontend route guards to prove authorization correctness.

## 15. Open Decisions

The following decisions remain open:

1. Which production identity provider will be used?
2. Will authentication use cookies, bearer tokens, or another supported mechanism?
3. What is the development authentication implementation?
4. How will production startup detect and reject development authentication?
5. Will login and logout have dedicated frontend pages?
6. How will sessions expire and recover?
7. What CSRF protections are required?
8. How will cross-origin requests be configured?
9. How will uncertain financial operations be recovered after authentication expires?
10. What identity information may be exposed to the frontend?

## 16. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/routing.md`
- `docs/frontend/state-management.md`
- `docs/frontend/api-integration.md`
- `docs/security/security-model.md`
- `docs/security/threat-model.md`
- `docs/adr/ADR-007-development-authentication.md`
- `docs/api/error-model.md`
- `docs/project/open-questions.md`