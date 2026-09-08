import { History } from 'lucide-react'
import type { TransactionItem } from '../../api/accountApi'
import { Button } from '../ui/Button'
import { TransactionItemRow } from '../TransactionItem/TransactionItem'

type Props = {
  items: TransactionItem[]
  onSeeAll?: () => void
  emptyAction?: () => void
  title?: string
  limit?: number
}

export function TransactionList({
  items,
  onSeeAll,
  emptyAction,
  title = 'Movimentações recentes',
  limit = 5,
}: Props) {
  const visible = items.slice(0, limit)

  return (
    <section className="card tx-card">
      <div className="section-head">
        <h2>{title}</h2>
        {onSeeAll && items.length > 0 ? (
          <Button variant="ghost" type="button" onClick={onSeeAll}>
            Ver todas →
          </Button>
        ) : null}
      </div>

      {visible.length === 0 ? (
        <div className="empty-state">
          <History size={36} strokeWidth={1.75} />
          <h3>Nenhuma movimentação ainda</h3>
          <p>Suas entradas e saídas aparecerão aqui.</p>
          {emptyAction ? (
            <Button type="button" onClick={emptyAction}>
              Adicionar primeira entrada
            </Button>
          ) : null}
        </div>
      ) : (
        <ul className="tx-list">
          {visible.map((item) => (
            <TransactionItemRow key={item.id} item={item} />
          ))}
        </ul>
      )}
    </section>
  )
}
