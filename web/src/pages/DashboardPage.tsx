import { BalanceCard } from '../components/BalanceCard/BalanceCard'
import { Header } from '../components/Header/Header'
import { QuickActions } from '../components/QuickActions/QuickActions'
import { TransactionList } from '../components/TransactionList/TransactionList'
import { DashboardSkeleton } from '../components/ui/Skeleton'
import type { TransactionItem } from '../api/accountApi'

type Props = {
  username: string
  role: string
  apiInstance: string
  balance: number | null
  balanceHidden: boolean
  updatedLabel: string
  transactions: TransactionItem[]
  loading: boolean
  onToggleHidden: () => void
  onCredit: () => void
  onDebit: () => void
  onSeeAll: () => void
  onHome: () => void
  onLogout: () => void
  showAll: boolean
}

export function DashboardPage({
  username,
  role,
  apiInstance,
  balance,
  balanceHidden,
  updatedLabel,
  transactions,
  loading,
  onToggleHidden,
  onCredit,
  onDebit,
  onSeeAll,
  onHome,
  onLogout,
  showAll,
}: Props) {
  return (
    <div className="app-shell">
      <Header username={username} role={role} apiInstance={apiInstance} onLogout={onLogout} />
      <main className="container main">
        <p className="greeting">Olá, {username}</p>
        {loading && balance === null ? (
          <DashboardSkeleton />
        ) : (
          <div className="stack gap-24">
            {!showAll ? (
              <>
                <BalanceCard
                  balance={balance}
                  hidden={balanceHidden}
                  onToggleHidden={onToggleHidden}
                  updatedLabel={updatedLabel}
                  loading={loading}
                />
                <QuickActions onCredit={onCredit} onDebit={onDebit} disabled={loading} />
              </>
            ) : null}
            <TransactionList
              items={transactions}
              limit={showAll ? 50 : 5}
              title={showAll ? 'Histórico de movimentações' : 'Movimentações recentes'}
              onSeeAll={showAll ? undefined : onSeeAll}
              emptyAction={onCredit}
            />
          </div>
        )}
      </main>
      <nav className="bottom-nav" aria-label="Navegação principal">
        <button type="button" className={!showAll ? 'active' : ''} onClick={onHome}>
          Início
        </button>
        <button type="button" className={showAll ? 'active' : ''} onClick={onSeeAll}>
          Movimentações
        </button>
      </nav>
    </div>
  )
}
