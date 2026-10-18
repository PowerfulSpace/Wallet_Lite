import type { Money } from '@/services/api/types'

// String-only monetary handling. No parseFloat/Number: monetary values are never computed
// on the client. The balance always comes from the server.
const MONEY_PATTERN = /^\d+(\.\d{1,2})?$/
const MAX_WHOLE = '1000000'
const MAX_FRACTION = '00'

export type MoneyParseResult = { ok: true; value: Money } | { ok: false; reason: string }

function isZero(whole: string, fraction: string): boolean {
  return /^0*$/.test(whole) && /^0*$/.test(fraction)
}

function exceedsMax(whole: string, fraction: string): boolean {
  const normalizedWhole = whole.replace(/^0+/, '') || '0'
  if (normalizedWhole.length !== MAX_WHOLE.length) {
    return normalizedWhole.length > MAX_WHOLE.length
  }
  if (normalizedWhole !== MAX_WHOLE) {
    return normalizedWhole > MAX_WHOLE
  }
  return fraction > MAX_FRACTION
}

// Validates a user-entered amount and normalizes it to "X.YY" (zero-padded, never rounded).
// Amounts with more than two fractional digits are rejected.
export function parseMoney(input: string): MoneyParseResult {
  const trimmed = input.trim()

  if (!MONEY_PATTERN.test(trimmed)) {
    return {
      ok: false,
      reason: 'Amount must be a positive number with at most two decimal places.',
    }
  }

  const [whole = '', fraction = ''] = trimmed.split('.')

  if (isZero(whole, fraction)) {
    return { ok: false, reason: 'Amount must be greater than zero.' }
  }

  if (exceedsMax(whole, fraction)) {
    return { ok: false, reason: 'Amount must not exceed 1000000.00.' }
  }

  const normalizedWhole = whole.replace(/^0+/, '') || '0'
  const normalizedFraction = fraction.padEnd(2, '0')

  return { ok: true, value: `${normalizedWhole}.${normalizedFraction}` }
}

// Formats a server-provided money string for display, e.g. "125.50" -> "$125.50".
// Negative values keep their sign: "-125.50" -> "-$125.50".
export function formatMoney(m: Money): string {
  if (m.startsWith('-')) {
    return `-$${m.slice(1)}`
  }
  return `$${m}`
}
