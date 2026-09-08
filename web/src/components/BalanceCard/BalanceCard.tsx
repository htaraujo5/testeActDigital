import { Eye, EyeOff } from 'lucide-react'
import { formatBRL } from '../../lib/format'
import { Button } from '../ui/Button'

type Props = {
  balance: number | null
  hidden: boolean
  onToggleHidden: () => void
  updatedLabel: string
  loading?: boolean
}

export function BalanceCard({ balance, hidden, onToggleHidden, updatedLabel, loading }: Props) {
  return (
    <section className="balance-card">
      <div className="balance-card-top">
        <p className="label">Saldo disponível</p>
        <Button variant="ghost" type="button" onClick={onToggleHidden} aria-label={hidden ? 'Mostrar saldo' : 'Ocultar saldo'}>
          {hidden ? <Eye size={18} strokeWidth={2} /> : <EyeOff size={18} strokeWidth={2} />}
        </Button>
      </div>
      <p className={`balance-value ${loading ? 'is-loading' : ''}`}>
        {balance === null ? '—' : hidden ? 'R$ ••••••' : formatBRL(balance)}
      </p>
      <p className="caption">{updatedLabel}</p>
    </section>
  )
}
