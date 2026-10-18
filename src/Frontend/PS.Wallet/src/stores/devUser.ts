import { defineStore } from 'pinia'
import {
  DEFAULT_DEV_USER,
  DEV_USER_STORAGE_KEY,
  isDevUser,
  type DevUser,
} from '@/shared/devUser'

// Dev-only store. The Development backend has no real authentication and trusts the
// `X-Test-User` header (alice | bob). This store only drives that header and must never be
// treated as production authentication or authorization.
function readStoredUser(): DevUser {
  try {
    const stored = localStorage.getItem(DEV_USER_STORAGE_KEY)
    return isDevUser(stored) ? stored : DEFAULT_DEV_USER
  } catch {
    return DEFAULT_DEV_USER
  }
}

function persistUser(user: DevUser): void {
  try {
    localStorage.setItem(DEV_USER_STORAGE_KEY, user)
  } catch {
    // Ignore storage failures (e.g. disabled localStorage); the header still works in-session.
  }
}

export const useDevUserStore = defineStore('devUser', {
  state: () => ({
    currentUser: readStoredUser(),
  }),

  getters: {
    headerValue(state): Record<string, string> {
      return { 'X-Test-User': state.currentUser }
    },
  },

  actions: {
    setUser(user: DevUser) {
      this.currentUser = user
      persistUser(user)
    },

    toggle() {
      const next: DevUser = this.currentUser === 'alice' ? 'bob' : 'alice'
      this.setUser(next)
    },
  },
})
