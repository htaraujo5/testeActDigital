import { ArrowDownLeft, ArrowUpRight } from 'lucide-react'

type Props = {
  onCredit: () => void
  onDebit: () => void
  disabled?: boolean
}

export function QuickActions({ onCredit, onDebit, disabled }: Props) {
  return (
    <div className="quick-actions">
      <button type="button" className="action-card action-credit" onClick={onCredit} disabled={disabled}>
        <span className="action-icon">
          <ArrowDownLeft size={22} strokeWidth={2} />
        </span>
        <span>
          <strong>Adicionar entrada</strong>
          <small>Registrar crédito</small>
        </span>
      </button>
      <button type="button" className="action-card action-debit" onClick={onDebit} disabled={disabled}>
        <span className="action-icon">
          <ArrowUpRight size={22} strokeWidth={2} />
        </span>
        <span>
          <strong>Registrar saída</strong>
          <small>Registrar débito</small>
        </span>
      </button>
    </div>
  )
}
