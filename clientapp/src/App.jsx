import React, { useEffect, useState } from 'react'

// BFF endpoints (cùng origin). Token JWT do MiniSSO cấp, lưu localStorage, gửi Bearer.
const TOKEN_KEY = 'mm_token'

async function login(email, password) {
  const res = await fetch('/api/auth/login', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  })
  const data = await res.json()
  if (!res.ok || !data.access_token) throw new Error(data.error_description || data.error || 'Đăng nhập thất bại')
  return data.access_token
}
async function authGet(path, token) {
  const res = await fetch(path, { headers: { Authorization: `Bearer ${token}` } })
  if (res.status === 401) throw new Error('unauthorized')
  return res.json()
}

function Login({ onLogin }) {
  const [email, setEmail] = useState('admin@minisso.dev'); const [password, setPassword] = useState('Admin@123'); const [err, setErr] = useState(null); const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true); setErr(null)
    try { const t = await login(email.trim(), password); onLogin(t) } catch (e) { setErr(e.message) } finally { setBusy(false) }
  }
  return (
    <div className="phone">
      <div className="login">
        <h1>📱 MiniMobile</h1>
        <p style={{ textAlign: 'center', color: 'var(--muted)', marginBottom: 24 }}>Đăng nhập bằng tài khoản SSO</p>
        {err && <div className="err">{err}</div>}
        <input placeholder="Email" value={email} onChange={e => setEmail(e.target.value)} />
        <input placeholder="Mật khẩu" type="password" value={password} onChange={e => setPassword(e.target.value)} onKeyDown={e => e.key === 'Enter' && submit()} />
        <button onClick={submit} disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
        <p style={{ textAlign: 'center', color: 'var(--muted)', fontSize: 12, marginTop: 16 }}>Xác thực JWT RS256 qua MiniSSO (SSO chéo-app)</p>
      </div>
    </div>
  )
}

function Home({ token, onLogout }) {
  const [me, setMe] = useState(null); const [home, setHome] = useState(null); const [err, setErr] = useState(null)
  useEffect(() => {
    Promise.all([authGet('/api/me', token), authGet('/api/home', token)])
      .then(([m, h]) => { setMe(m); setHome(h) })
      .catch(e => { if (e.message === 'unauthorized') onLogout(); else setErr(e.message) })
  }, [token])
  if (err) return <div className="phone"><div className="body"><div className="err">{err}</div></div></div>
  if (!home) return <div className="phone"><div className="body"><p style={{ color: 'var(--muted)', padding: 40, textAlign: 'center' }}>Đang tải…</p></div></div>
  return (
    <div className="phone">
      <div className="topbar">
        <button className="logout" onClick={onLogout}>Đăng xuất</button>
        <div className="greet">{home.greeting}</div>
        <div className="sub">Tổ chức: {home.tenant || me?.tenant || '—'}</div>
        {me?.roles?.length > 0 && <div className="roles">{me.roles.map((r, i) => <span key={i} className="pill">{r}</span>)}</div>}
      </div>
      <div className="body">
        <div className="widgets">
          {home.widgets.map((w, i) => (
            <div className="widget" key={i}>
              <div className="v">{w.value}{w.live && <span className="live" title="dữ liệu trực tiếp" />}</div>
              <div className="t">{w.title}{w.unit ? ` (${w.unit})` : ''}</div>
            </div>))}
        </div>
        <div className="h">Ứng dụng</div>
        <div className="modules">
          {home.modules.map((m, i) => (
            <a className="mod" key={i} href={m.url} target="_blank" rel="noreferrer">
              <div className="ic">{iconFor(m.icon)}</div><div className="nm">{m.name}</div>
            </a>))}
        </div>
      </div>
    </div>
  )
}

function iconFor(bi) {
  const map = { cart: '🛒', 'hdd-stack': '🗄️', 'car-front': '🚗', tools: '🔧', 'shield-shaded': '🛡️', 'diagram-3': '🔗', 'box-seam': '📦', bell: '🔔' }
  return map[bi] || '📱'
}

export default function App() {
  const [token, setToken] = useState(() => localStorage.getItem(TOKEN_KEY))
  const onLogin = (t) => { localStorage.setItem(TOKEN_KEY, t); setToken(t) }
  const onLogout = () => { localStorage.removeItem(TOKEN_KEY); setToken(null) }
  return token ? <Home token={token} onLogout={onLogout} /> : <Login onLogin={onLogin} />
}
