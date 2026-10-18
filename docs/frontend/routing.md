# Frontend Routing

**Status:** Draft v0.1  
**Last Updated:** 2026-10-18  
**Scope:** Out of MVP; future phase.

## 1. Purpose

This document defines the proposed routing and navigation model for the Wallet API frontend.

Routing maps browser URLs to application pages and provides predictable navigation between wallet-related features.

The frontend routing layer is responsible for navigation and presentation-level access handling. It is not an authorization boundary. The backend MUST independently authenticate and authorize every protected API request.

## 2. Routing Principles

The routing implementation SHOULD follow these principles:

- Use clear, predictable, resource-oriented paths.
- Keep URLs independent of internal component names.
- Support direct navigation to valid pages.
- Provide a consistent application layout for protected pages.
- Handle unknown routes explicitly.
- Preserve relevant navigation context when redirecting users.
- Avoid placing sensitive financial data in URL query parameters.
- Do not rely on client-side route guards to enforce backend authorization.
- Keep the routing configuration proportional to the MVP.

## 3. Proposed Route Map

The following routes are proposed for the initial frontend.

| Route | Page | Access |
|---|---|---|
| `/` | Application entry point or redirect | Depends on authentication |
| `/wallet` | Wallet overview and balance | Authenticated user |
| `/wallet/transactions` | Transaction history | Authenticated user |
| `/transfers` | Transfer form | Authenticated user |
| `/transfers/:id` | Transfer details | Authenticated user; resource access enforced by API |
| `*` | Not-found page | Public presentation |

These routes are illustrative and may be adjusted when the frontend framework is selected.

A dedicated deposit page is not required if the deposit form is integrated into the wallet overview. If deposits receive a separate page, the route may be `/wallet/deposits`.

## 4. Entry Route

The root route (`/`) SHOULD direct the user to an appropriate destination.

Proposed behavior:

- If the user is authenticated, navigate to `/wallet`.
- If the user is not authenticated and a sign-in page exists, navigate to the sign-in page.
- If authentication is supplied by a development-only mechanism, use the documented development flow.
- If authentication status cannot be determined, show an appropriate loading or error state instead of assuming the user is authenticated.

The exact behavior depends on the selected authentication mechanism.

## 5. Protected Routes

Wallet, transaction history, and transfer pages are intended for authenticated users.

The frontend MAY use a route guard to prevent unauthenticated users from navigating to protected pages.

A route guard SHOULD:

1. Determine whether authentication is available or still loading.
2. Render a loading state while authentication status is being resolved.
3. Allow navigation when the user is authenticated.
4. Redirect to the appropriate sign-in or development-authentication flow when authentication is absent.
5. Preserve the intended destination when safe and useful.

Route guards improve user experience but MUST NOT be treated as security controls.

If an API request returns `401 Unauthorized`, the frontend SHOULD follow the documented authentication recovery behavior.

A `403 Forbidden` response, if used by the API, MUST NOT be treated as proof that the user can access the resource by changing the URL.

## 6. Transfer Details Route

The route `/transfers/:id` displays information about a specific transfer.

The frontend SHOULD:

- Validate the route parameter's basic format before making a request.
- Request the transfer details from the API.
- Display a loading state while the request is pending.
- Render the returned transfer information when access is permitted.
- Handle `404 Not Found` and other documented errors consistently.

The route parameter is an identifier, not proof of ownership.

The backend MUST verify that the authenticated user is authorized to access the requested transfer. A user MUST NOT gain access to another user's transfer by modifying the URL.

The frontend SHOULD avoid distinguishing nonexistent resources from inaccessible resources if the API intentionally returns the same response for both cases.

## 7. Navigation Structure

The authenticated application SHOULD provide consistent navigation to the main features:

- Wallet overview.
- Transaction history.
- Make a transfer.

Deposits MAY be available directly from the wallet overview or through a dedicated route.

Navigation SHOULD:

- Clearly indicate the current section.
- Remain usable with a keyboard.
- Provide accessible labels.
- Avoid losing unsaved form input without warning when practical.
- Keep navigation consistent across authenticated pages.

The application SHOULD use a shared layout for the main authenticated area rather than duplicating navigation across pages.

## 8. Query Parameters

Query parameters SHOULD be used only when they represent safe, shareable, or navigational state.

For transaction history, the frontend may use query parameters for non-sensitive navigation state if the API and routing design support it.

The frontend MUST NOT put credentials, access tokens, secret keys, or other sensitive authentication material into URLs.

Pagination cursors SHOULD be treated as opaque values. The frontend MUST NOT infer or modify their internal structure.

The decision to synchronize pagination state with browser URLs remains open.

## 9. Browser History and Navigation

The routing implementation SHOULD support expected browser navigation behavior.

- Back and forward navigation should restore the appropriate page.
- Refreshing a valid page should work when the hosting configuration supports direct route access.
- Navigating away from a form with unsaved input SHOULD be handled predictably.
- A completed financial operation SHOULD NOT be resubmitted merely because the user refreshes or revisits a page.
- Revisiting a transfer details page SHOULD perform a read operation rather than initiating another transfer.

The application MUST NOT use route navigation as a substitute for idempotency protection.

## 10. Unknown Routes and Route Errors

Unknown routes SHOULD render a dedicated not-found page.

The page SHOULD:

- Clearly state that the requested page does not exist.
- Provide a link back to the wallet overview or application entry point.
- Avoid displaying internal routing details or stack traces.

Routing failures SHOULD be distinguished from API failures. For example, an unknown frontend path is different from an API returning `404 Not Found` for a transfer.

## 11. Redirect Safety

Redirect destinations originating from query parameters, external links, or user-controlled values MUST be validated.

The frontend MUST NOT blindly redirect to arbitrary external URLs supplied by a client.

When preserving an intended destination during authentication, the application SHOULD allow only safe internal paths and SHOULD reject malformed or external destinations.

## 12. Testing Requirements

Routing tests SHOULD verify:

- The root route behaves correctly for each supported authentication state.
- Protected pages handle unauthenticated navigation.
- Valid routes render the expected page.
- Unknown routes display the not-found page.
- Transfer details load using the correct identifier.
- API authorization errors are handled correctly.
- Invalid redirect destinations are rejected.
- Browser refresh works for supported routes.
- Back and forward navigation do not trigger unintended financial writes.

Backend authorization tests remain mandatory and cannot be replaced by routing tests.

## 13. Open Decisions

The following items are tracked. Their labels indicate status:

1. Which routing library or framework-native routing solution will be used?
2. Will sign-in have a dedicated frontend route?
3. Will deposits use a separate page or a wallet overview form?
4. Will transaction pagination state be represented in the URL?
5. What is the exact post-authentication redirect policy?
6. How will the frontend hosting environment support direct route access?
7. Which HTTP authorization errors will the backend expose for inaccessible resources?

## 14. Related Documents

- `docs/frontend/overview.md`
- `docs/frontend/architecture.md`
- `docs/frontend/state-management.md`
- `docs/frontend/authentication.md`
- `docs/frontend/api-integration.md`
- `docs/api/endpoints.md`
- `docs/api/error-model.md`
- `docs/security/security-model.md`
- `docs/project/open-questions.md`