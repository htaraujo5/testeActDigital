import { useMemo, useState } from 'react'
import { formatBRL } from '../../lib/format'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { Modal } from '../ui/Modal'

type Mode = 'credit' | 'debit'

type Props = {
  open: boolean
  mode: Mode
  availableBalance: number
  busy: boolean
  onClose: () => void
  onConfirm: (amount: number) => Promise<void>
}

export function MoneyModal({ open, mode, availableBalance, busy, onClose, onConfirm }: Props) {
  const [raw, setRaw] = useState('')
  const amount = useMemo(() => {
    const normalized = raw.replace(/\./g, '').replace(',', '.')
    const value = Number(normalized)
    return Number.isFinite(value) ? value : NaN
  }, [raw])

  const error = useMemo(() => {
    if (!raw.trim()) return 'Informe um valor.'
    if (!(amount > 0)) return 'Valor deve ser maior que zero.'
    if (mode === 'debit' && amount > availableBalance) {
      return 'Saldo insuficiente para realizar esta operação.'
    }
    return ''
  }, [raw, amount, mode, availableBalance])

  const title = mode === 'credit' ? 'Adicionar entrada' : 'Registrar saída'
  const confirmLabel = mode === 'credit' ? 'Adicionar' : 'Confirmar saída'

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (error || busy) return
    await onConfirm(amount)
    setRaw('')
  }

  return (
    <Modal
      open={open}
      title={title}
      onClose={() => {
        if (!busy) {
          setRaw('')
          onClose()
        }
      }}
      footer={
        <>
          <Button variant="ghost" type="button" onClick={onClose} disabled={busy}>
            Cancelar
          </Button>
          <Button type="submit" form="money-form" disabled={Boolean(error) || busy}>
            {busy ? 'Processando...' : confirmLabel}
          </Button>
        </>
      }
    >
      <form id="money-form" className="stack gap-16" onSubmit={handleSubmit}>
        {mode === 'debit' ? (
          <p className="info-banner">Saldo disponível: {formatBRL(availableBalance)}</p>
        ) : null}
        <Input
          label="Valor"
          inputMode="decimal"
          placeholder="R$ 0,00"
          value={raw}
          onChange={(e) => setRaw(e.target.value)}
          error={raw ? error : undefined}
          autoFocus
        />
      </form>
    </Modal>
  )
}
