import { AlertCircle, Check } from 'lucide-react'

export type ToastTone = 'success' | 'error' | 'info'

export type ToastState = {
  tone: ToastTone
  title: string
  description?: string
} | null

type Props = {
  toast: ToastState
  onDismiss: () => void
}

export function Toast({ toast, onDismiss }: Props) {
  if (!toast) return null

  return (
    <div className={`toast toast-${toast.tone}`} role="status">
      <div className="toast-icon">
        {toast.tone === 'success' ? <Check size={18} strokeWidth={2} /> : <AlertCircle size={18} strokeWidth={2} />}
      </div>
      <div className="toast-copy">
        <strong>{toast.title}</strong>
        {toast.description ? <p>{toast.description}</p> : null}
      </div>
      <button type="button" className="toast-close" onClick={onDismiss} aria-label="Dispensar">
        ×
      </button>
    </div>
  )
}
