import { useCallback, useEffect, useState } from 'react'
import { accountApi, type TransactionItem } from './api/accountApi'
import { MoneyModal } from './components/MoneyModal/MoneyModal'
import { Toast, type ToastState } from './components/ui/Toast'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import './styles/global.css'

type Session = {
  token: string
  username: string
  role: string
}

type View = 'home' | 'history'

function App() {
  const [session, setSession] = useState<Session | null>(null)
  const [accountId] = useState(() => {
    const key = 'conta-digital-account-id'
    const existing = localStorage.getItem(key)
    if (existing) return existing
    const id = crypto.randomUUID()
    localStorage.setItem(key, id)
    return id
  })
  const [balance, setBalance] = useState<number | null>(null)
  const [transactions, setTransactions] = useState<TransactionItem[]>([])
  const [apiInstance, setApiInstance] = useState('-')
  const [updatedLabel, setUpdatedLabel] = useState('—')
  const [busy, setBusy] = useState(false)
  const [loadingDashboard, setLoadingDashboard] = useState(false)
  const [balanceHidden, setBalanceHidden] = useState(false)
  const [view, setView] = useState<View>('home')
  const [modal, setModal] = useState<'credit' | 'debit' | null>(null)
  const [toast, setToast] = useState<ToastState>(null)

  const showToast = useCallback((next: ToastState) => {
    setToast(next)
    window.setTimeout(() => setToast(null), 3500)
  }, [])

  const refresh = useCallback(
    async (token: string) => {
      const [bal, tx] = await Promise.all([
        accountApi.getBalance(accountId, token),
        accountApi.getTransactions(accountId, token, 50),
      ])
      setBalance(bal.data.balance)
      setTransactions(tx.data.items)
      setApiInstance(bal.instance !== '-' ? bal.instance : tx.instance)
      setUpdatedLabel(
        bal.data.source === 'read' ? 'Atualizado agora · réplica' : 'Atualizado agora · write',
      )
    },
    [accountId],
  )

  useEffect(() => {
    if (!session) return
    setLoadingDashboard(true)
    void refresh(session.token)
      .catch((err) =>
        showToast({
          tone: 'error',
          title: 'Não foi possível carregar a conta',
          description: err instanceof Error ? err.message : 'Tente novamente.',
        }),
      )
      .finally(() => setLoadingDashboard(false))
  }, [session, refresh, showToast])

  async function handleLogin(username: string, password: string) {
    setBusy(true)
    try {
      const result = await accountApi.login(username, password)
      setSession({
        token: result.data.accessToken,
        username: result.data.username,
        role: result.data.role,
      })
      setApiInstance(result.instance)
      showToast({
        tone: 'success',
        title: 'Login realizado',
        description: `Olá, ${result.data.username}`,
      })
    } catch (err) {
      showToast({
        tone: 'error',
        title: 'Falha no login',
        description: err instanceof Error ? err.message : 'Credenciais inválidas',
      })
    } finally {
      setBusy(false)
    }
  }

  async function handleMoney(amount: number) {
    if (!session || !modal) return
    setBusy(true)
    try {
      const key = crypto.randomUUID()
      const result =
        modal === 'credit'
          ? await accountApi.credit(accountId, amount, session.token, key)
          : await accountApi.debit(accountId, amount, session.token, key)

      setApiInstance(result.instance)
      await refresh(session.token)
      setModal(null)
      showToast({
        tone: 'success',
        title: modal === 'credit' ? 'Entrada registrada' : 'Saída registrada',
        description: 'Seu saldo foi atualizado.',
      })
    } catch (err) {
      showToast({
        tone: 'error',
        title: 'Não foi possível realizar a operação',
        description: err instanceof Error ? err.message : 'Tente novamente.',
      })
    } finally {
      setBusy(false)
    }
  }

  if (!session) {
    return (
      <>
        <LoginPage busy={busy} onSubmit={handleLogin} />
        <Toast toast={toast} onDismiss={() => setToast(null)} />
      </>
    )
  }

  return (
    <>
      <DashboardPage
        username={session.username}
        role={session.role}
        apiInstance={apiInstance}
        balance={balance}
        balanceHidden={balanceHidden}
        updatedLabel={updatedLabel}
        transactions={transactions}
        loading={loadingDashboard || busy}
        onToggleHidden={() => setBalanceHidden((v) => !v)}
        onCredit={() => setModal('credit')}
        onDebit={() => setModal('debit')}
        onSeeAll={() => setView('history')}
        onHome={() => setView('home')}
        onLogout={() => {
          setSession(null)
          setBalance(null)
          setTransactions([])
          setView('home')
        }}
        showAll={view === 'history'}
      />
      <MoneyModal
        open={modal !== null}
        mode={modal ?? 'credit'}
        availableBalance={balance ?? 0}
        busy={busy}
        onClose={() => setModal(null)}
        onConfirm={handleMoney}
      />
      <Toast toast={toast} onDismiss={() => setToast(null)} />
    </>
  )
}

export default App
