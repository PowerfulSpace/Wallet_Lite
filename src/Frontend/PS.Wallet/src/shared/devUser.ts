// Development-only identity.
//
// The Development backend authenticates solely through the `X-Test-User` header with the
// values `alice` or `bob`. This is NOT production authentication and must never be treated
// as authorization. Production builds must not rely on this mechanism.
export type DevUser = 'alice' | 'bob'

export const DEV_USERS: readonly DevUser[] = ['alice', 'bob']

export const DEV_USER_STORAGE_KEY = 'ps-wallet.devUser'

export const DEFAULT_DEV_USER: DevUser = 'alice'

export function isDevUser(value: unknown): value is DevUser {
  return value === 'alice' || value === 'bob'
}
