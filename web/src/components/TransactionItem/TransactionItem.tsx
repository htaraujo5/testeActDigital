import { ArrowDownLeft, ArrowUpRight } from 'lucide-react'
import type { TransactionItem } from '../../api/accountApi'
import { formatBRL, formatDateTime } from '../../lib/format'

type Props = {
  item: TransactionItem
}

export function TransactionItemRow({ item }: Props) {
  const isCredit = item.type.toLowerCase() === 'credit'
  return (
    <li className="tx-item">
      <div className={`tx-icon ${isCredit ? 'tx-credit' : 'tx-debit'}`}>
        {isCredit ? <ArrowDownLeft size={18} strokeWidth={2} /> : <ArrowUpRight size={18} strokeWidth={2} />}
      </div>
      <div className="tx-main">
        <strong>{isCredit ? 'Entrada' : 'Saída'}</strong>
        <span className="caption">{formatDateTime(item.occurredAtUtc)}</span>
      </div>
      <div className={`tx-amount ${isCredit ? 'tx-credit' : 'tx-debit'}`}>
        {isCredit ? '+' : '-'} {formatBRL(item.amount)}
      </div>
    </li>
  )
}
