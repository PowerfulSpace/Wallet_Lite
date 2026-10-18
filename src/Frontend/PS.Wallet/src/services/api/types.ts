// Money is always transported as a JSON string with exactly two fractional digits.
export type Money = string

// GET /api/v1/wallet
export interface WalletDto {
  walletId: string
  currency: 'USD'
  balance: Money
  createdAtUtc: string
}

// GET /api/v1/wallet/transactions
export type TransactionType = 'deposit' | 'transfer_in' | 'transfer_out'

export interface TransactionDto {
  transactionId: string
  type: TransactionType
  amount: Money
  currency: 'USD'
  description: string
  relatedOperationId: string
  createdAtUtc: string
}

export interface TransactionsPageDto {
  items: TransactionDto[]
  nextCursor: string | null
  hasMore: boolean
}

// POST /api/v1/wallet/deposits
export interface CreateDepositRequest {
  amount: Money
}

export interface DepositResponseDto {
  depositId: string
  walletId: string
  amount: Money
  currency: 'USD'
  balanceAfter: Money
  createdAtUtc: string
}

// POST /api/v1/transfers
export interface CreateTransferRequest {
  recipientUsername: string
  amount: Money
}

export interface TransferResponseDto {
  transferId: string
  senderUsername: string
  recipientUsername: string
  amount: Money
  currency: 'USD'
  createdAtUtc: string
}

// RFC 9457 Problem Details (see docs/api/error-model.md).
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}
