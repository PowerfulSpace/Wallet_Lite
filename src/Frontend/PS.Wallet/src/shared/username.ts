// Backend canonicalization: trim, then match ^[A-Za-z0-9_]{3,32}$ (case-insensitive lookup).
// See docs/architecture/data-model.md §4 Username Normalization Policy.
const USERNAME_PATTERN = /^[A-Za-z0-9_]{3,32}$/

export type UsernameValidationResult =
  | { ok: true; canonical: string }
  | { ok: false; reason: string }

export function validateUsername(input: string): UsernameValidationResult {
  const trimmed = input.trim()
  if (trimmed === '') {
    return { ok: false, reason: 'Recipient username is required.' }
  }
  if (!USERNAME_PATTERN.test(trimmed)) {
    return { ok: false, reason: 'Recipient username is invalid.' }
  }
  return { ok: true, canonical: trimmed.toLowerCase() }
}
