export type TokenResponse = {
  accessToken: string
  tokenType: string
  expiresInSeconds: number
  username: string
  role: string
}

export type MoneyResponse = {
  transactionId: string
  accountId: string
  operation: string
  amount: number
  balance: number
  outcome: string
  reasonCode: string | null
  idempotentReplay: boolean
}

export type BalanceResponse = {
  accountId: string
  balance: number
  asOfUtc: string
  source: string
}

export type TransactionItem = {
  id: string
  accountId: string
  type: string
  amount: number
  occurredAtUtc: string
  balanceAfter: number
}

export type TransactionListResponse = {
  accountId: string
  items: TransactionItem[]
  source: string
}

export type ApiResult<T> = {
  data: T
  headers: Headers
  instance: string
}

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

async function request<T>(path: string, init: RequestInit = {}): Promise<ApiResult<T>> {
  const response = await fetch(`${API_BASE}${path}`, init)
  const text = await response.text()
  const data = text ? JSON.parse(text) : null
  if (!response.ok) {
    const message = data?.message ?? data?.error ?? `HTTP ${response.status}`
    throw new Error(message)
  }
  return {
    data: data as T,
    headers: response.headers,
    instance: response.headers.get('X-Api-Instance') ?? '-',
  }
}

export const accountApi = {
  login(username: string, password: string) {
    return request<TokenResponse>('/api/v1/auth/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    })
  },

  credit(accountId: string, amount: number, token: string, idempotencyKey: string) {
    return request<MoneyResponse>(`/api/v1/accounts/${accountId}/credits`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
        'Idempotency-Key': idempotencyKey,
      },
      body: JSON.stringify({ amount }),
    })
  },

  debit(accountId: string, amount: number, token: string, idempotencyKey: string) {
    return request<MoneyResponse>(`/api/v1/accounts/${accountId}/debits`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
        'Idempotency-Key': idempotencyKey,
      },
      body: JSON.stringify({ amount }),
    })
  },

  getBalance(accountId: string, token: string) {
    return request<BalanceResponse>(`/api/v1/accounts/${accountId}/balance`, {
      headers: { Authorization: `Bearer ${token}` },
    })
  },

  getTransactions(accountId: string, token: string, take = 50) {
    return request<TransactionListResponse>(`/api/v1/accounts/${accountId}/transactions?take=${take}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
  },
}
