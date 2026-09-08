import { LogOut, Wallet } from 'lucide-react'
import { initials } from '../../lib/format'
import { Button } from '../ui/Button'

type Props = {
  username: string
  role: string
  apiInstance: string
  onLogout: () => void
}

export function Header({ username, role, apiInstance, onLogout }: Props) {
  return (
    <header className="app-header">
      <div className="container header-inner">
        <div className="brand">
          <Wallet size={22} strokeWidth={2} />
          <span>Conta Digital</span>
        </div>
        <div className="header-right">
          <div className="header-meta">
            <span className="caption">API {apiInstance}</span>
            <span className="caption">{role}</span>
          </div>
          <div className="avatar" aria-label={username} title={username}>
            {initials(username)}
          </div>
          <Button variant="ghost" type="button" onClick={onLogout} aria-label="Sair">
            <LogOut size={18} strokeWidth={2} />
          </Button>
        </div>
      </div>
    </header>
  )
}
