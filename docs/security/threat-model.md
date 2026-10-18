# Threat Model

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18

## 1. Purpose

This document identifies security threats affecting the Wallet API MVP and defines mitigation requirements.

The system manages virtual USD balances and transaction records. Although it does not handle real money, unauthorized access, incorrect transfers, or corrupted financial data would undermine the project's primary goals.

The threat model is intended to guide implementation, testing, and future security reviews. It is not a formal security certification.

## 2. Scope and Assumptions

### In Scope

- Authentication and user identity.
- Authorization and resource access.
- Deposits and transfers.
- Wallet balances and ledger entries.
- Idempotency and request replay.
- Concurrent financial operations.
- Input validation and API error handling.
- Configuration and environment separation.
- Logging and sensitive data exposure.
- Database integrity and operational failures.

### Assumptions

- The MVP uses ASP.NET Core and SQL Server.
- Development may use pre-seeded users and a simplified authentication mechanism.
- Production-grade identity management is outside the initial MVP scope.
- The API is the trusted boundary for financial business rules.
- The database is expected to enforce appropriate integrity constraints.
- There are no external banking or payment-provider integrations.

## 3. Assets to Protect

| Asset | Security objective | Potential impact of compromise |
|---|---|---|
| Wallet balances | Integrity | Incorrect balances or unauthorized value creation |
| Ledger entries | Integrity and traceability | Loss of reliable transaction history |
| Transfer records | Integrity and confidentiality | Unauthorized or misleading transfer information |
| User identity | Authenticity | Operations attributed to the wrong user |
| Idempotency records | Integrity | Duplicate financial effects or inconsistent retries |
| Authentication configuration | Confidentiality and integrity | Unauthorized access |
| Database credentials | Confidentiality | Database compromise |
| Application logs | Confidentiality and integrity | Exposure of sensitive information or loss of audit value |
| Runtime configuration | Integrity | Unsafe behavior or disabled protections |

## 4. Threat Actors

Potential threat actors include:

- An unauthenticated external caller.
- An authenticated user attempting to access another user's data.
- A malicious or faulty client sending malformed or repeated requests.
- A user attempting to exploit concurrency or timing behavior.
- A developer accidentally introducing unsafe configuration or financial logic.
- An attacker who obtains application or database credentials.

The MVP does not assume that all clients are trustworthy simply because they use the intended frontend.

## 5. Threat Assessment

Risk ratings are preliminary qualitative assessments for prioritization. They must be revisited after the deployment environment and exposure are defined.

| ID | Threat | Likelihood | Impact | Priority |
|---|---|---|---|---|
| T-01 | Sender identity spoofing | Medium | Critical | High |
| T-02 | Unauthorized access to another wallet | Medium | High | High |
| T-03 | Duplicate deposits or transfers | Medium | High | High |
| T-04 | Race condition causes negative balance | Medium | Critical | High |
| T-05 | Concurrent credits exceed maximum balance | Medium | High | High |
| T-06 | Partial transfer is committed | Low–Medium | Critical | High |
| T-07 | Invalid monetary precision or amount | Medium | High | High |
| T-08 | Development authentication enabled in Production | Low–Medium | Critical | High |
| T-09 | Sensitive details exposed through errors or logs | Medium | Medium–High | Medium–High |
| T-10 | Database compromise or destructive access | Low–Medium | Critical | High |
| T-11 | Resource enumeration through API responses | Medium | Medium | Medium |
| T-12 | Request flooding or resource exhaustion | Medium | Medium–High | Medium–High |

These ratings are initial engineering judgments, not measured probabilities. They should be updated when deployment details and operational controls become known.

## 6. Threat Scenarios and Mitigations

### T-01: Sender Identity Spoofing

**Scenario:** A caller submits a transfer request containing another user's identifier and attempts to spend that user's funds.

**Impact:** Unauthorized transfers and incorrect attribution.

**Mitigations:**
- Derive the sender exclusively from the validated authentication principal.
- Do not accept a sender ID as an authoritative request field.
- Resolve the sender's application user and wallet server-side.
- Test attempts to manipulate user identifiers.

**Verification:** An authenticated Alice cannot initiate a transfer as Bob by changing request fields or untrusted headers.

### T-02: Unauthorized Wallet or History Access

**Scenario:** A user changes a wallet or transfer identifier in a request to access another user's information.

**Impact:** Disclosure of balances, transaction history, or transfer details.

**Mitigations:**
- Apply authorization checks to every resource-access endpoint.
- Scope wallet and history queries to the authenticated user.
- Permit transfer-detail access only to the sender or recipient.
- Return a consistent `404 Not Found` response for nonexistent and inaccessible transfers when concealment is required.

**Verification:** Tests confirm that users cannot access other users' balances, histories, or unrelated transfers.

### T-03: Duplicate Financial Operations

**Scenario:** A client retries a request after a timeout, or two identical requests arrive concurrently, causing a deposit or transfer to be applied more than once.

**Impact:** Incorrect balances and duplicate ledger entries.

**Mitigations:**
- Require `Idempotency-Key` for deposits and transfers.
- Scope keys by authenticated user and operation type.
- Associate each key with a request fingerprint.
- Persist the successful operation, financial changes, and replayable response atomically.
- Enforce uniqueness in SQL Server.
- Handle concurrent requests with the same key safely.

**Verification:** Sequential and concurrent duplicate requests produce no additional financial effects.

### T-04: Negative Balance Through a Race Condition

**Scenario:** Alice has $100. Two concurrent transfers attempt to send $80 and $70. Both requests read the original balance before either debit is applied.

**Impact:** The wallet could become negative if the implementation uses an unsafe read-then-update sequence.

**Mitigations:**
- Use concurrency-safe SQL Server updates or suitable locking.
- Make the balance sufficiency condition part of the protected database operation.
- Check the affected-row count or equivalent result.
- Keep all transfer changes within one database transaction.

**Verification:** Under concurrent execution, at most one of the two example transfers succeeds.

### T-05: Maximum Balance Bypass

**Scenario:** Concurrent deposits or incoming transfers each observe that a wallet is below the maximum and both add funds, exceeding the permitted balance.

**Impact:** Violation of the wallet balance invariant.

**Mitigations:**
- Enforce the maximum balance during a concurrency-safe database update.
- Reject operations that would exceed $1,000,000.00.
- Roll back the entire operation when the credit cannot be applied.
- Add concurrency integration tests against SQL Server.

**Verification:** Concurrent credits cannot cause the balance to exceed the configured maximum.

### T-06: Partial Transfer

**Scenario:** The sender is debited, but the recipient is not credited, or only one ledger entry is written.

**Impact:** Financial inconsistency and loss of reliable accounting history.

**Mitigations:**
- Use a single database transaction for both wallet updates, the transfer record, both ledger entries, and the successful idempotency result.
- Ensure exceptions trigger rollback.
- Avoid external side effects inside the transaction that cannot be rolled back.
- Enforce suitable foreign keys and uniqueness constraints.

**Verification:** Fault-injection or integration tests confirm that failure at any required step leaves no partial financial effect.

### T-07: Monetary Input Manipulation

**Scenario:** A caller sends a negative amount, zero, an amount with excessive fractional digits, a very large number, or a JSON numeric value where the contract requires a string.

**Impact:** Incorrect arithmetic, limit bypasses, or inconsistent API behavior.

**Mitigations:**
- Represent monetary values as decimal strings in the API.
- Use C# `decimal` and SQL Server `decimal(19,2)`.
- Reject amounts with more than two fractional decimal digits.
- Do not silently round invalid amounts.
- Validate positivity and operation limits server-side.
- Use invariant culture and a documented decimal-point format.

**Verification:** Unit and integration tests cover boundary values, malformed values, excess precision, and values near the operation and balance limits.

### T-08: Development Authentication in Production

**Scenario:** A deployment accidentally enables a development authentication handler that lets callers select Alice or Bob without real authentication.

**Impact:** Complete compromise of wallet access and financial-operation attribution.

**Mitigations:**
- Register development authentication only in explicitly permitted environments.
- Reject unsafe Production configuration.
- Keep test identities and credentials out of production secrets.
- Add startup configuration tests and deployment checks.
- Do not rely solely on a frontend restriction or documentation warning.

**Verification:** The application cannot start with development authentication enabled in Production, or otherwise guarantees that the handler is unavailable there.

### T-09: Sensitive Error or Log Disclosure

**Scenario:** An exception response or log entry exposes SQL statements, connection strings, tokens, or stack traces.

**Impact:** Information disclosure that could assist further attacks.

**Mitigations:**
- Use the standardized Problem Details response format.
- Return generic messages for unexpected failures.
- Keep detailed diagnostics server-side with access controls.
- Redact secrets and avoid logging authentication headers.
- Use trace identifiers to correlate responses and logs.

**Verification:** Error-handling tests confirm that public responses contain no internal exception or database details.

### T-10: Database Compromise

**Scenario:** An attacker or unauthorized process obtains database credentials or destructive database access.

**Impact:** Disclosure, modification, or deletion of financial records.

**Mitigations:**
- Store credentials outside source control.
- Use least-privilege database identities.
- Restrict database network access where deployment supports it.
- Use controlled schema migrations.
- Preserve ledger immutability through application permissions and design.
- Establish backups and test restoration.

**Verification:** Configuration reviews verify least privilege and secret handling. Recovery tests demonstrate that a backup can be restored.

The precise infrastructure controls depend on the deployment model and are not fully specified by the MVP.

### T-11: User or Resource Enumeration

**Scenario:** Different response messages allow a caller to determine whether a username or transfer identifier exists.

**Impact:** Disclosure of user or transaction existence.

**Mitigations:**
- Avoid returning private wallet or transaction details.
- Use consistent responses where resource concealment is required.
- Apply request throttling if rate limiting is introduced.
- Review recipient lookup behavior as an explicit security decision.

**Verification:** Tests compare responses for nonexistent and inaccessible transfer identifiers.

### T-12: Request Flooding

**Scenario:** A caller sends large volumes of requests or expensive malformed payloads.

**Impact:** Increased resource consumption or service unavailability.

**Mitigations:**
- Apply reasonable request body limits and timeouts.
- Use database command timeouts.
- Consider rate limiting at the API or deployment layer.
- Monitor latency, error rates, and resource consumption.

**Verification:** Tests cover configured request-size limits. Rate-limit behavior must be tested if implemented.

## 7. Security Invariants

The following invariants must hold regardless of the threat scenario:

1. A wallet balance never becomes negative.
2. A wallet balance never exceeds $1,000,000.00.
3. A transfer is fully committed or has no financial effect.
4. A transfer has two ledger entries of equal magnitude and opposite sign.
5. A deposit has one positive ledger entry.
6. Retrying a successful idempotent operation does not duplicate its financial effect.
7. A user cannot perform operations as another user.
8. A user cannot read another user's wallet or history.
9. Development authentication is unavailable in Production.
10. Monetary arithmetic never relies on binary floating-point types.

These invariants are supported by domain rules, database constraints, transaction handling, authorization, and automated tests—not by a single control alone.

## 8. Security Testing Strategy

Security testing must include:

- Unit tests for validation and domain invariants.
- API integration tests for authentication and authorization.
- SQL Server integration tests for idempotency, atomicity, and concurrency.
- Configuration tests for environment-specific authentication.
- Error-response tests for sensitive-data disclosure.
- Migration and database-constraint tests.
- Recovery tests for backup restoration when recovery procedures are introduced.

Mocks alone are insufficient to verify transaction isolation, database uniqueness constraints, or real concurrency behavior.

## 9. Residual Risks

The following risks remain outside the initial MVP's fully defined controls:

- No complete production identity lifecycle.
- Deployment infrastructure and network security are not yet specified.
- Rate limiting and abuse detection may be deferred.
- Monitoring and alert thresholds remain to be defined.
- Backup retention and recovery objectives remain open.
- The API's recipient username enumeration policy is undecided.

These limitations must be documented rather than interpreted as proof that the application is production-ready.

## 10. Review Triggers

Review this threat model whenever:

- Authentication or authorization changes.
- New financial endpoints are introduced.
- The ledger or balance update strategy changes.
- Idempotency semantics change.
- The application is exposed to a new network or deployment environment.
- External payment or banking integrations are added.
