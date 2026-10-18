# Demo Workflow

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Document Path:** `docs/project/demo.md`

## 1. Purpose

This document describes a reproducible end-to-end demonstration of the Wallet API MVP using the seeded development users (Alice and Bob).

## 2. Prerequisites

- SQL Server (local) or LocalDB.
- `.NET 10 SDK`.
- The solution built (`dotnet build`).
- The database migrated (`dotnet ef database update`).
- The API running (`dotnet run --project src/Backend/PS.Wallet/PS.Wallet.API`).
- Seed data applied (automatic in Development: `alice`, `bob`, wallets at `0.00`).

Base URL: `http://localhost:5254`. Development authentication uses the `X-Test-User` header (`alice` or `bob`).

Each `Idempotency-Key` below is a literal demo key. Reusing it in a later run replays or conflicts with the earlier result, so use a fresh database or new keys when repeating the whole walkthrough.

## 3. Scenario 1 — Alice deposits $100.00

```bash
curl -X POST http://localhost:5254/api/v1/wallet/deposits \
  -H "X-Test-User: alice" \
  -H "Idempotency-Key: demo-deposit-1" \
  -H "Content-Type: application/json" \
  -d '{"amount": "100.00"}'
```

Expected: `201 Created`, `Location: /api/v1/wallet/deposits/{id}`, body `{ depositId, walletId, amount: "100.00", currency: "USD", balanceAfter: "100.00", createdAtUtc }`.

Verify balance:

```bash
curl http://localhost:5254/api/v1/wallet -H "X-Test-User: alice"
```

Expected: `balance: "100.00"`.

## 4. Scenario 2 — Alice transfers $25.00 to Bob

```bash
curl -X POST http://localhost:5254/api/v1/transfers \
  -H "X-Test-User: alice" \
  -H "Idempotency-Key: demo-transfer-1" \
  -H "Content-Type: application/json" \
  -d '{"recipientUsername": "bob", "amount": "25.00"}'
```

Expected: `201 Created`, body `{ transferId, senderUsername: "alice", recipientUsername: "bob", amount: "25.00", currency: "USD", createdAtUtc }`.

Verify balances:

```bash
curl http://localhost:5254/api/v1/wallet -H "X-Test-User: alice"   # balance: "75.00"
curl http://localhost:5254/api/v1/wallet -H "X-Test-User: bob"     # balance: "25.00"
```

## 5. Scenario 3 — Idempotent retry

Repeat the transfer request with the same `Idempotency-Key: demo-transfer-1` and the same body.

Expected: `201 Created` with the same `transferId`. No new transfer is created. Alice remains at `75.00`, Bob at `25.00`.

## 6. Scenario 4 — Conflicting key reuse

Repeat the transfer request with `Idempotency-Key: demo-transfer-1` but `amount: "30.00"`.

Expected: `409 Conflict` with `code: "IDEMPOTENCY_KEY_REUSED"`. Balances unchanged.

## 7. Scenario 5 — Transaction history

```bash
curl "http://localhost:5254/api/v1/wallet/transactions?limit=10" -H "X-Test-User: alice"
```

Expected: `items` contains:

- `type: "transfer_out"`, `amount: "-25.00"` (to Bob).
- `type: "deposit"`, `amount: "100.00"`.

`nextCursor: null`, `hasMore: false`.

For Bob: `type: "transfer_in"`, `amount: "25.00"`.

## 8. Scenario 6 — Concurrent debit protection

Reset Alice's balance to $100.00 (deposit or fresh database). Then fire two transfers concurrently:

```bash
curl -X POST http://localhost:5254/api/v1/transfers \
  -H "X-Test-User: alice" -H "Idempotency-Key: race-a" -H "Content-Type: application/json" \
  -d '{"recipientUsername": "bob", "amount": "80.00"}' &

curl -X POST http://localhost:5254/api/v1/transfers \
  -H "X-Test-User: alice" -H "Idempotency-Key: race-b" -H "Content-Type: application/json" \
  -d '{"recipientUsername": "bob", "amount": "70.00"}' &

wait
```

Both requests must be in flight at the same time; the bash `&` background operator is used above. In PowerShell, send them from two terminals, or rely on `tests/PS.Wallet.IntegrationTests/Concurrency/ConcurrentDebitTests.cs`, which asserts the same invariant automatically.

Expected: at most one `201 Created`; the other `409` `INSUFFICIENT_FUNDS`. Alice's balance is `20.00` or `30.00` (never negative). Bob's balance increases by exactly one of `80.00` / `70.00`.

## 9. Scenario 7 — Balance limit

Bob is near `1,000,000.00` (`CONST-001`). Fund Bob to the limit and attempt a deposit that would exceed it:

```bash
curl -X POST http://localhost:5254/api/v1/wallet/deposits \
  -H "X-Test-User: bob" -H "Idempotency-Key: demo-limit-1" -H "Content-Type: application/json" \
  -d '{"amount": "999950.00"}'

curl -X POST http://localhost:5254/api/v1/wallet/deposits \
  -H "X-Test-User: bob" -H "Idempotency-Key: demo-limit-2" -H "Content-Type: application/json" \
  -d '{"amount": "100.00"}'
```

Expected: the second request returns `409 Conflict` with `code: "BALANCE_LIMIT_EXCEEDED"`. No balance change.

## 10. Scenario 8 — Unauthorized access

```bash
curl http://localhost:5254/api/v1/wallet
```

Expected: `401 Unauthorized`.

```bash
curl http://localhost:5254/api/v1/wallet -H "X-Test-User: unknown"
```

Expected: `401 Unauthorized`.

## 11. What the demo does not cover

- Real money movement (virtual only).
- Production authentication.
- Multi-currency.
- Frontend (out of MVP scope; scaffold exists at `src/Frontend/PS.Wallet/`).

See `docs/project/open-questions.md §12` for remaining non-blocking items and `docs/project/known-issues.md` for accepted deviations.
