# API Endpoints

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Base Path:** `/api/v1`

## 1. Purpose

This document defines the HTTP endpoints, request and response contracts, authorization requirements, and expected behavior of the Wallet API MVP.

General HTTP conventions, error responses, money representation, idempotency, and pagination rules are defined in [`api-guidelines.md`](./api-guidelines.md).

## 2. Endpoint Summary

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/v1/wallet` | Get the authenticated user's wallet and current balance |
| GET | `/api/v1/wallet/transactions` | Get the authenticated user's transaction history |
| POST | `/api/v1/wallet/deposits` | Add virtual funds to the authenticated user's wallet |
| POST | `/api/v1/transfers` | Transfer funds to another user |
| GET | `/api/v1/transfers/{id}` | Get transfer details if the user is its sender or recipient |

All endpoints except development-only authentication mechanisms require an authenticated user. No endpoint accepts a client-supplied sender ID as the source of authorization.

## 3. GET /api/v1/wallet

### Purpose

Return the current user's wallet and available balance.

### Authentication

Required.

The wallet is resolved using the authenticated user's identity. The client cannot request another user's wallet by supplying a user or wallet ID.

### Request

No request body or query parameters.

### Success Response — `200 OK`

```json
{
  "walletId": "7a0e6f6b-5b4c-4d7c-9c5b-6b1c2c5e9f10",
  "currency": "USD",
  "balance": "125.50",
  "createdAtUtc": "2026-10-18T10:15:30Z"
}
```

The identifiers and timestamp above are illustrative.

| Field | Type | Description |
|---|---|---|
| `walletId` | UUID string | Wallet identifier |
| `currency` | string | Always `USD` in the MVP |
| `balance` | decimal string | Current balance with exactly two fractional digits |
| `createdAtUtc` | ISO 8601 timestamp | Wallet creation time in UTC |

### Error Responses

- `401 Unauthorized` — authentication is missing or invalid.
- `500 Internal Server Error` — unexpected server failure.

## 4. GET /api/v1/wallet/transactions

### Purpose

Return a paginated history of the authenticated user's wallet transactions.

The history includes deposits and both sides of transfers from the current wallet owner's perspective.

### Authentication

Required.

Users can access only their own wallet history.

### Request

Query parameters:

| Parameter | Required | Description |
|---|---|---|
| `cursor` | No | Opaque cursor returned by a previous response |
| `limit` | No | Maximum number of items; default `CONST-007` (20), maximum `CONST-008` (100) |

Filtering by transaction type or date range is outside the MVP scope.

### Success Response — `200 OK`

```json
{
  "items": [
    {
      "transactionId": "b9c0a2e2-9e9f-4d6a-a9f2-2e5a6e4d8a11",
      "type": "transfer_out",
      "amount": "-25.00",
      "currency": "USD",
      "description": "Transfer to bob",
      "relatedOperationId": "6b13bfc7-08e8-4e4e-8d16-5b1c7c9f0001",
      "createdAtUtc": "2026-10-18T10:20:00Z"
    },
    {
      "transactionId": "d2f1a6e3-1a77-4c5e-b3e4-2d9b0a2e1234",
      "type": "deposit",
      "amount": "50.00",
      "currency": "USD",
      "description": "Virtual deposit",
      "relatedOperationId": "3c8a1f52-1e2b-4a77-9c4b-8b2d5e100002",
      "createdAtUtc": "2026-10-18T10:15:00Z"
    }
  ],
  "nextCursor": "opaque-cursor-value",
  "hasMore": true
}
```

All IDs, amounts, timestamps, and cursor values are illustrative.

### Fields

| Field | Type | Description |
|---|---|---|
| `items` | array | Transaction entries for the current wallet |
| `transactionId` | UUID string | Ledger entry identifier |
| `type` | string | `deposit`, `transfer_in`, or `transfer_out` |
| `amount` | decimal string | Signed amount from the wallet owner's perspective |
| `currency` | string | Always `USD` |
| `description` | string | Human-readable summary |
| `relatedOperationId` | UUID string | Deposit or transfer identifier |
| `createdAtUtc` | ISO 8601 timestamp | Entry creation time in UTC |
| `nextCursor` | string or `null` | Cursor for the next page |
| `hasMore` | boolean | Whether more entries are available |

### Ordering and Pagination

- Entries are ordered newest first.
- Pagination uses a stable composite ordering based on `(CreatedAtUtc, Id)`.
- The cursor is opaque to clients and must be treated as an unmodified string.
- Cursor encoding: `base64(JSON { createdAtUtc: ISO8601, id: GUID })` (`CONST-010`). The JSON uses invariant culture and UTC.
- The cursor must be validated before use. A malformed or undecodable cursor returns `400 VALIDATION_ERROR` with code `INVALID_CURSOR`.
- The ordering tuple is `(CreatedAtUtc DESC, Id DESC)`. The cursor encodes the last item of the previous page, and the query uses `WHERE (CreatedAtUtc, Id) < (cursor.CreatedAtUtc, cursor.Id)`.
- New entries created between page requests appear as additional items in earlier pages (newest-first order); they do not shift already-returned items.

### Error Responses

- `400 Bad Request` — invalid cursor or invalid pagination parameters.
- `401 Unauthorized` — authentication is missing or invalid.
- `500 Internal Server Error` — unexpected server failure.

## 5. POST /api/v1/wallet/deposits

### Purpose

Add virtual funds to the authenticated user's wallet.

A deposit is an internal wallet top-up. It does not represent a real payment, bank transaction, or external payment-provider operation.

### Authentication

Required.

The target wallet is determined from the authenticated user's identity.

### Request Headers

| Header | Required | Description |
|---|---|---|
| `Idempotency-Key` | Yes | Unique key identifying this logical deposit request. Format: `^[A-Za-z0-9_-]{8,128}$` (CONST-005). |
| `Content-Type` | Yes | `application/json` |

### Request Body

```json
{
  "amount": "50.00"
}
```

### Validation

- `amount` must be a JSON string representing a positive decimal value.
- The amount must have no more than two fractional decimal digits.
- The amount must not exceed `1,000,000.00`.
- The request must not cause the wallet balance to exceed `1,000,000.00`.
- Invalid precision must be rejected; the server must not silently round.
- The authenticated user must have an existing wallet.

### Success Response — `201 Created`

```json
{
  "depositId": "3c8a1f52-1e2b-4a77-9c4b-8b2d5e100002",
  "walletId": "7a0e6f6b-5b4c-4d7c-9c5b-6b1c2c5e9f10",
  "amount": "50.00",
  "currency": "USD",
  "balanceAfter": "175.50",
  "createdAtUtc": "2026-10-18T10:15:00Z"
}
```

The values are illustrative.

### Atomicity Requirements

The following changes must commit together in one database transaction:

1. The wallet balance update.
2. The deposit operation record.
3. The positive ledger entry.
4. The successful idempotency record and replayable response.

If any required step fails, the financial changes must be rolled back.

### Idempotency Behavior

- The same key and equivalent request must replay the original successful response without creating another deposit.
- Reusing the same key with a different request payload must return `409 Conflict`.
- Concurrent requests using the same key must not produce duplicate deposits.
- A business rejection (`400`, `409`) does **not** permanently block the key: the same key and same payload may be retried later and will be re-validated against the current state.
- An unexpected failure (`500`) leaves the attempt in `InProgress`; retry with the same key follows the bounded-wait policy and may recover via on-demand lease recovery.

### Error Responses

- `400 Bad Request` — malformed JSON or invalid amount.
- `401 Unauthorized` — authentication is missing or invalid.
- `409 Conflict` — balance limit exceeded or idempotency key reused with a different payload.
- `500 Internal Server Error` — unexpected server failure.

## 6. POST /api/v1/transfers

### Purpose

Transfer virtual funds from the authenticated user's wallet to another user's wallet.

Transfers are synchronous. A successful response means the transfer has been committed; the API does not expose artificial pending or processing states.

### Authentication

Required.

The sender is always derived from the authenticated principal. A sender ID in the request body is not accepted.

### Request Headers

| Header | Required | Description |
|---|---|---|
| `Idempotency-Key` | Yes | Unique key identifying this logical transfer request. Format: `^[A-Za-z0-9_-]{8,128}$` (CONST-005). |
| `Content-Type` | Yes | `application/json` |

### Request Body

```json
{
  "recipientUsername": "bob",
  "amount": "25.00"
}
```

### Validation

- `recipientUsername` must match `^[A-Za-z0-9_]{3,32}$` after trimming; it is canonicalized to lowercase before lookup and fingerprinting.
- `recipientUsername` must identify an existing user.
- `amount` must be a positive decimal value represented as a JSON string.
- The amount must have no more than two fractional decimal digits.
- The amount must not exceed `1,000,000.00`.
- The sender and recipient must be different users. Violation returns `409 SELF_TRANSFER`.
- The sender must have sufficient funds.
- The recipient's resulting balance must not exceed `1,000,000.00`.
- The server must not silently round amounts with excessive precision.

### Success Response — `201 Created`

```json
{
  "transferId": "6b13bfc7-08e8-4e4e-8d16-5b1c7c9f0001",
  "senderUsername": "alice",
  "recipientUsername": "bob",
  "amount": "25.00",
  "currency": "USD",
  "createdAtUtc": "2026-10-18T10:20:00Z"
}
```

The values are illustrative.

The response does not expose the sender's or recipient's current balance. Clients can retrieve their own wallet balance separately.

### Atomicity Requirements

The following changes must commit together in one database transaction:

1. Debit the sender's wallet.
2. Credit the recipient's wallet.
3. Create one transfer operation record.
4. Create two ledger entries:
   - A negative entry for the sender.
   - A positive entry of equal magnitude for the recipient.
5. Persist the successful idempotency record and replayable response.

The transfer must either complete fully or have no financial effect.

### Concurrency Requirements

- Concurrent transfers must not cause a wallet balance to become negative.
- Concurrent credits must not cause a wallet balance to exceed the maximum.
- Concurrent requests using the same idempotency key must not create duplicate transfers.
- If multiple wallets must be locked or updated, the implementation must use a consistent lock ordering where applicable and handle SQL Server deadlocks according to the documented retry policy.

### Idempotency Behavior

- Repeating the same logical request with the same key replays the original successful response.
- Reusing a key with a different request payload returns `409 Conflict`.
- A successful retry must not create additional ledger entries or change either wallet balance again.
- A business rejection (`400`, `409`) does not permanently block the key; the same key and same payload may be retried later and re-validated.
- An unexpected failure (`500`) leaves the attempt in `InProgress`; retry with the same key follows the bounded-wait policy.

### Error Responses

- `400 Bad Request` — malformed JSON or invalid input.
- `401 Unauthorized` — authentication is missing or invalid.
- `404 Not Found` — recipient does not exist (`RESOURCE_NOT_FOUND`). The response is indistinguishable from a nonexistent transfer, to avoid user enumeration (`CONST-022`).
- `409 Conflict` — insufficient funds, self-transfer, recipient balance limit exceeded, or idempotency key reused with a different payload.
- `500 Internal Server Error` — unexpected server failure.

The API must not reveal information about unrelated users or their wallets.

## 7. GET /api/v1/transfers/{id}

### Purpose

Return details of a transfer that involves the authenticated user.

### Authentication

Required.

Access is allowed only when the authenticated user is the sender or recipient of the requested transfer.

### Request

Path parameter:

| Parameter | Type | Description |
|---|---|---|
| `id` | UUID | Transfer identifier |

### Success Response — `200 OK`

```json
{
  "transferId": "6b13bfc7-08e8-4e4e-8d16-5b1c7c9f0001",
  "senderUsername": "alice",
  "recipientUsername": "bob",
  "amount": "25.00",
  "currency": "USD",
  "createdAtUtc": "2026-10-18T10:20:00Z"
}
```

### Error Responses

- `400 Bad Request` — invalid transfer ID format.
- `401 Unauthorized` — authentication is missing or invalid.
- `404 Not Found` — transfer does not exist or the current user is not authorized to view it.
- `500 Internal Server Error` — unexpected server failure.

Returning the same `404` response for nonexistent and inaccessible transfers reduces the risk of exposing transfer existence to unauthorized users.

## 8. Cross-Endpoint Requirements

All endpoints must:

- Use the common error response format defined in `error-model.md`.
- Return only information the authenticated user is authorized to access.
- Treat SQL Server as the source of truth for persisted financial state.
- Avoid logging credentials, authentication tokens, or sensitive request data.
- Respect request cancellation where safe.
- Preserve the atomicity and idempotency guarantees defined in the domain and architecture documents.

## 9. Open Decisions

The following items must be resolved before treating this contract as final:

1. The default and maximum page size for transaction history. (tracked in OQ-013)
2. Whether a missing recipient returns `404` or a generic `400`/`409` response to reduce user enumeration. (tracked in OQ-015)
3. The exact canonicalization rules for usernames. (resolved in data-model.md §4 Username Normalization Policy; OQ-002)
4. The exact idempotency request fingerprint and which validation failures are persisted. (resolved in ADR-005 §5 and §10; OQ-017, OQ-009)
5. Whether successful creation responses use a `Location` header and, if so, its precise value. (tracked in OQ-018)
6. The exact fields exposed in deposit and transfer responses. (tracked in OQ-018)
7. The cursor encoding and invalid-cursor error details. (tracked in OQ-014)
8. Whether to standardize all successful financial responses on `201 Created` or use a different status for some operations. (unique to this document)
