import { useState } from 'react'
import { Wallet } from 'lucide-react'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'

type Props = {
  busy: boolean
  onSubmit: (username: string, password: string) => Promise<void>
}

export function LoginPage({ busy, onSubmit }: Props) {
  const [username, setUsername] = useState('admin')
  const [password, setPassword] = useState('Admin@123')

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-brand">
          <Wallet size={28} strokeWidth={2} />
          <h1>Conta Digital</h1>
          <p>Entre para ver saldo, entradas e saídas da conta empresarial.</p>
        </div>
        <form
          className="stack gap-16"
          onSubmit={(e) => {
            e.preventDefault()
            void onSubmit(username, password)
          }}
        >
          <Input
            label="Usuário"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
          />
          <Input
            label="Senha"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
          />
          <Button type="submit" fullWidth disabled={busy}>
            {busy ? 'Entrando...' : 'Entrar'}
          </Button>
        </form>
        <p className="caption center">Demo: admin / Admin@123 · operator / Operator@123</p>
      </div>
    </div>
  )
}
