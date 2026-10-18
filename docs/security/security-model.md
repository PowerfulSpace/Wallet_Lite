# Security Model

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document defines the security model for the Wallet API MVP, including authentication, authorization, identity handling, data access restrictions, and environment-specific security requirements.

The application manages virtual USD balances and transaction records. Although no real money or external payment providers are involved, financial integrity and access control are core requirements.

## 2. Security Objectives

The security model must ensure that:

- Only authenticated users can access protected endpoints.
- Every financial operation is attributed to the authenticated user.
- Users cannot access or modify another user's wallet without authorization.
- Users cannot transfer funds on behalf of another user by manipulating request data.
- Financial operations preserve atomicity and balance invariants.
- Development authentication cannot be enabled in Production.
- Sensitive implementation details are not exposed through API responses or logs.

## 3. Authentication

### 3.1 General Requirements

All wallet and transaction endpoints require an authenticated identity.

The application must obtain the current user's identity from a validated authentication principal, not from request-body fields or untrusted headers.

Authentication establishes **who the caller is**. Authorization determines **which resources and operations that caller may access**.

### 3.2 Development Authentication

The MVP may provide a simplified authentication mechanism for local development and automated testing.

The initial development setup uses pre-seeded users, such as Alice and Bob. Full user registration, password management, email verification, and production identity management are outside the MVP scope.

Development authentication must:

- Be explicitly enabled through environment-specific configuration.
- Be unavailable in the Production environment.
- Never rely solely on a client-supplied user ID in an ordinary production request.
- Be covered by tests verifying that Production startup fails safely or the development authentication handler is not registered when the application runs in Production.
- Avoid introducing reusable secrets or credentials into source control.

The development authentication mechanism is defined in `ADR-007`. The MVP proposal is a development-only authentication handler that maps a controlled request value (for example, a header) to a pre-seeded user. The exact handler implementation, header name, and accepted values are recorded in `ADR-007` and will be finalized when the development environment is implemented.

### 3.3 Production Authentication

The MVP does not define a complete production identity provider or account lifecycle.

If production-grade authentication is introduced later, it must provide validated identity claims and secure credential or token handling. The wallet authorization model should remain independent of the specific authentication provider.

## 4. Identity and User Resolution

After successful authentication, the API resolves the application user associated with the authenticated principal.

The mapping between an authentication identity and an application user must be explicit and stable.

Requirements:

- A request must not be able to choose its effective identity by changing a body field.
- The application must reject or safely handle authenticated principals that cannot be mapped to an application user.
- Usernames are used to identify transfer recipients, not to establish the sender's identity.
- Username normalization and uniqueness rules must be defined consistently.
- Internal user and wallet identifiers must not be treated as authentication credentials.

In the MVP, the development authentication handler resolves the username from `X-Test-User` and places it in the principal. `ICurrentUser` exposes the username; application handlers resolve the internal user by username. Production authentication must follow the same principle: the verified principal determines the username, and the application resolves the internal user.

## 5. Authorization Rules

Authorization must be enforced server-side for every request.

| Resource or operation | Authorization rule |
|---|---|
| Current wallet | The authenticated user can access only their own wallet |
| Wallet balance | The authenticated user can read only their own balance |
| Transaction history | The authenticated user can read only their own ledger entries |
| Deposit | The deposit is applied only to the authenticated user's wallet |
| Transfer | The sender is the authenticated user |
| Transfer details | The authenticated user must be the sender or recipient |
| Other users' wallets | Direct access is not permitted |
| Other users' transaction history | Access is not permitted |

Authorization checks must not depend on the frontend hiding buttons or restricting navigation.

### 5.1 Transfer Recipient

A transfer recipient is selected by `recipientUsername`.

The server must resolve the recipient to an existing application user and wallet. It must not accept a client-supplied sender identity.

The server must reject transfers to the same user.

### 5.2 Resource Enumeration

Where revealing the existence of a resource could disclose another user's information, the API should return a response indistinguishable from the response for a nonexistent resource.

For example, `GET /api/v1/transfers/{id}` returns `404 Not Found` both when a transfer does not exist and when the caller is not a participant in that transfer.

Whether unknown recipient usernames should be concealed is an open decision.

## 6. Financial Operation Security

Security controls must work together with domain rules, database transactions, and concurrency protection.

Requirements:

- The server validates every amount independently of client-side validation.
- Amounts use exact decimal arithmetic and the documented precision rules.
- The sender's balance is checked and debited using concurrency-safe database operations.
- The recipient's maximum balance is enforced under concurrent updates.
- A transfer is committed entirely or has no financial effect.
- Idempotency is scoped to the authenticated user and operation type according to the API contract.
- Reusing an idempotency key with a different request must not alter the original operation.
- Database constraints and application checks should provide appropriate defense in depth.

Authentication or authorization failure must never result in a partial financial operation.

## 7. Data Exposure

The API returns only fields required by the public contract.

It must not expose:

- Passwords, credentials, tokens, or secret configuration.
- Internal authentication or authorization implementation details.
- Another user's balance or transaction history.
- Database connection strings or SQL statements.
- Stack traces or raw exception messages.
- Internal records unrelated to the requested operation.

Transfer details may include the sender's and recipient's usernames and the transfer amount, subject to the rule that only a participant can view the transfer.

Logs must be designed to support troubleshooting without unnecessarily recording sensitive data.

## 8. Configuration and Environment Separation

Environment-specific security configuration must be explicit.

### Development

- Development authentication may be enabled.
- Pre-seeded test users may be available.
- Test credentials, if any, must be local-only and must not be committed as production secrets.
- Test databases and production data must remain separated.

### Test

- Automated tests may use controlled test identities.
- Integration tests must exercise authentication and authorization behavior.
- Tests must verify that a caller cannot access another user's resources.

### Production

- Development authentication must not be available.
- Secrets must be supplied through appropriate deployment configuration or a secret-management mechanism.
- Debugging details must not be returned to clients.
- Only explicitly configured authentication mechanisms may be accepted.

Production deployment and real-user account management are not part of the MVP's initial scope.

## 9. Security Testing

The test suite must cover at least:

1. Requests without authentication are rejected.
2. Invalid authentication does not grant access.
3. Alice cannot read Bob's wallet or transaction history.
4. Alice cannot submit a transfer with Bob as the sender.
5. Only the sender or recipient can read transfer details.
6. A deposit affects only the authenticated user's wallet.
7. A failed authorization check creates no financial records or balance changes.
8. Development authentication is unavailable in Production.
9. Repeated requests cannot bypass idempotency rules.
10. Concurrent operations cannot violate balance invariants.

Where database transactions, uniqueness constraints, or concurrency behavior are involved, integration tests should run against SQL Server.

## 10. Threat Model Relationship

This document defines intended security controls. The threats, attack scenarios, and mitigation mapping are documented separately in `threat-model.md`.

The security model must be reviewed when authentication mechanisms, API endpoints, financial rules, or deployment environments change.

## 11. Open Decisions

1. Select the development authentication mechanism. (resolved in ADR-007)
2. Define the mapping from authentication claims to application users. (tracked in OQ-022)
3. Finalize username normalization and case-sensitivity rules. (resolved in OQ-002; data-model.md §4)
4. Decide whether unknown recipient usernames should be concealed. (resolved in OQ-015; CONST-022)
5. Define secret-management requirements for any future production deployment. (tracked in OQ-021)
6. Determine whether rate limiting is included in the MVP. (tracked in OQ-023)
7. Define the minimum security logging and audit requirements. (tracked in OQ-029)
