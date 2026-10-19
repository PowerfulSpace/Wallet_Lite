# PS.Wallet Frontend

Vue 3 + TypeScript + Vite frontend for the Wallet API.

## Status

MVP frontend — implemented for local development and demonstration against the
backend's Development environment.

## Implemented screens

- `/wallet` — wallet balance and deposit form.
- `/wallet/transactions` — transaction history with cursor pagination.
- `/transfers` — transfer form.
- `/transfers/:id` — transfer details (read-only).
- `/` and `/about` — placeholder pages.

## Development

Requirements:

- Node.js `^22.18.0 || >=24.12.0`
- npm
- Wallet API backend running on `http://localhost:5254` (see root `README.md` and
  `docs/development/setup.md`).

```bash
npm ci
npm run dev
```

Dev server: http://localhost:5173. Vite proxies /api to http://localhost:5254.

### Development authentication

The frontend uses the backend's Development-only `X-Test-User` header (alice or
bob). The user selector in the app header is a development convenience and
must not be exposed in any production build. Production authentication is out
of scope for the current phase (see `docs/frontend/authentication.md` and `OQ-021`
in `docs/project/open-questions.md`).

### Quality checks

```bash
npm run build     # type-check + production build
npm run lint      # oxlint + eslint (auto-fix)
npm run format    # prettier
```

## What the frontend does not do

- No production authentication, no user registration.
- No real money, no multi-currency, no bank or payment provider integration.
- No client-side recomputation of balances or amounts (server is the source of truth).
- No automatic retry of financial writes with a new idempotency key.

## Known limitations

- `npm audit` reports 4 high-severity advisories in transitive dependencies of the
  current toolchain. Not addressed in this phase; a future toolchain update will
  re-evaluate.
- Bundle size warning (>500 kB): naive-ui is imported globally. Code-splitting is
  deferred to a future phase.
- Idempotency keys are kept in-memory only. A page refresh during an uncertain
  operation loses the ability to safely retry (a new logical operation would be
  created). Persistent recovery is deferred (see
  `docs/frontend/state-management.md` §9.2).
