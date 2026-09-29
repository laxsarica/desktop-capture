export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname;

    // CORS headers for DesktopViewer app
    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, DELETE, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, Authorization',
    };

    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: corsHeaders });
    }

    try {
      // ── Public endpoint: validate license key ──
      if (path === '/api/validate' && request.method === 'POST') {
        return await handleValidate(request, env, corsHeaders);
      }

      // ── Admin endpoints (require auth) ──
      if (path.startsWith('/api/licenses')) {
        const authError = checkAuth(request, env);
        if (authError) return authError;

        if (request.method === 'GET') {
          return await handleListLicenses(env, corsHeaders);
        }
        if (request.method === 'POST') {
          return await handleCreateLicense(request, env, corsHeaders);
        }
        if (request.method === 'DELETE') {
          const key = url.searchParams.get('key');
          return await handleDeleteLicense(key, env, corsHeaders);
        }
      }

      // ── Admin dashboard ──
      if (path === '/' || path === '') {
        return new Response(getDashboardHTML(), {
          headers: { 'Content-Type': 'text/html; charset=utf-8' },
        });
      }

      return json({ error: 'Not found' }, 404, corsHeaders);
    } catch (err) {
      return json({ error: err.message }, 500, corsHeaders);
    }
  },
};

// ─── Handlers ────────────────────────────────────────────────

async function handleValidate(request, env, headers) {
  const body = await request.json();
  const key = (body.key || '').trim();

  if (!key) {
    return json({ valid: false, message: 'No key provided' }, 400, headers);
  }

  const row = await env.DB.prepare(
    'SELECT license_key, expires_at FROM licenses WHERE license_key = ?'
  ).bind(key).first();

  if (!row) {
    return json({ valid: false, message: 'Invalid license key' }, 200, headers);
  }

  const now = new Date();
  const expires = new Date(row.expires_at);

  if (expires < now) {
    return json({ valid: false, message: 'License key has expired' }, 200, headers);
  }

  return json({ valid: true, message: 'License is active' }, 200, headers);
}

async function handleListLicenses(env, headers) {
  const { results } = await env.DB.prepare(
    'SELECT * FROM licenses ORDER BY created_at DESC'
  ).all();

  return json({ licenses: results || [] }, 200, headers);
}

async function handleCreateLicense(request, env, headers) {
  const body = await request.json();
  const { email, expires_at, license_key } = body;

  if (!email || !expires_at) {
    return json({ error: 'email and expires_at are required' }, 400, headers);
  }

  // Generate key if not provided
  const key = license_key || generateLicenseKey();
  const created_at = new Date().toISOString();

  try {
    await env.DB.prepare(
      'INSERT INTO licenses (license_key, email, expires_at, created_at) VALUES (?, ?, ?, ?)'
    ).bind(key, email, expires_at, created_at).run();

    return json({ success: true, license_key: key }, 201, headers);
  } catch (err) {
    if (err.message.includes('UNIQUE')) {
      return json({ error: 'License key already exists' }, 409, headers);
    }
    throw err;
  }
}

async function handleDeleteLicense(key, env, headers) {
  if (!key) {
    return json({ error: 'key parameter is required' }, 400, headers);
  }

  await env.DB.prepare('DELETE FROM licenses WHERE license_key = ?').bind(key).run();
  return json({ success: true }, 200, headers);
}

// ─── Helpers ─────────────────────────────────────────────────

function checkAuth(request, env) {
  const auth = request.headers.get('Authorization') || '';
  const token = auth.replace('Bearer ', '').trim();

  if (!env.ADMIN_TOKEN || token !== env.ADMIN_TOKEN) {
    return json({ error: 'Unauthorized' }, 401, {
      'Access-Control-Allow-Origin': '*',
    });
  }
  return null;
}

function json(data, status = 200, extraHeaders = {}) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json', ...extraHeaders },
  });
}

function generateLicenseKey() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const seg = () => Array.from({ length: 4 }, () => chars[Math.floor(Math.random() * chars.length)]).join('');
  return `LICE-${seg()}-${seg()}-${seg()}`;
}

// ─── Admin Dashboard HTML ────────────────────────────────────

function getDashboardHTML() {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>License Manager</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet">
<style>
  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

  :root {
    --bg: #0a0a0f;
    --surface: rgba(255,255,255,0.04);
    --surface-hover: rgba(255,255,255,0.07);
    --border: rgba(255,255,255,0.08);
    --text: #e4e4e7;
    --text-muted: #71717a;
    --accent: #818cf8;
    --accent-hover: #6366f1;
    --danger: #f87171;
    --danger-hover: #ef4444;
    --success: #34d399;
    --warning: #fbbf24;
    --radius: 12px;
  }

  body {
    font-family: 'Inter', -apple-system, sans-serif;
    background: var(--bg);
    color: var(--text);
    min-height: 100vh;
    line-height: 1.6;
  }

  /* ── Auth Screen ── */
  .auth-overlay {
    position: fixed; inset: 0;
    background: var(--bg);
    display: flex; align-items: center; justify-content: center;
    z-index: 1000;
    transition: opacity 0.3s, visibility 0.3s;
  }
  .auth-overlay.hidden { opacity: 0; visibility: hidden; pointer-events: none; }

  .auth-card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 16px;
    padding: 40px;
    width: 100%;
    max-width: 400px;
    backdrop-filter: blur(20px);
    text-align: center;
  }
  .auth-card h1 { font-size: 1.5rem; font-weight: 600; margin-bottom: 8px; }
  .auth-card p { color: var(--text-muted); font-size: 0.875rem; margin-bottom: 24px; }

  /* ── Layout ── */
  .container { max-width: 960px; margin: 0 auto; padding: 40px 24px; }

  header {
    display: flex; align-items: center; justify-content: space-between;
    margin-bottom: 32px;
  }
  header h1 { font-size: 1.5rem; font-weight: 700; }
  header h1 span { color: var(--accent); }

  .stats {
    display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px;
    margin-bottom: 32px;
  }
  .stat-card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    padding: 20px;
    text-align: center;
    transition: border-color 0.2s;
  }
  .stat-card:hover { border-color: rgba(255,255,255,0.15); }
  .stat-card .value { font-size: 2rem; font-weight: 700; }
  .stat-card .label { font-size: 0.75rem; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.05em; margin-top: 4px; }
  .stat-card.active .value { color: var(--success); }
  .stat-card.expired .value { color: var(--warning); }

  /* ── Form ── */
  .create-section {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    padding: 24px;
    margin-bottom: 32px;
  }
  .create-section h2 { font-size: 1rem; font-weight: 600; margin-bottom: 16px; }

  .form-row { display: flex; gap: 12px; flex-wrap: wrap; }
  .form-group { flex: 1; min-width: 180px; }
  .form-group label {
    display: block; font-size: 0.75rem; color: var(--text-muted);
    text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 6px;
  }

  input, select {
    width: 100%;
    background: rgba(0,0,0,0.3);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 10px 14px;
    color: var(--text);
    font-family: inherit;
    font-size: 0.875rem;
    outline: none;
    transition: border-color 0.2s;
  }
  input:focus { border-color: var(--accent); }
  input::placeholder { color: var(--text-muted); }

  /* ── Buttons ── */
  .btn {
    display: inline-flex; align-items: center; gap: 6px;
    padding: 10px 20px;
    border: none; border-radius: 8px;
    font-family: inherit; font-size: 0.875rem; font-weight: 500;
    cursor: pointer;
    transition: all 0.2s;
  }
  .btn-primary { background: var(--accent); color: white; }
  .btn-primary:hover { background: var(--accent-hover); transform: translateY(-1px); }
  .btn-danger { background: transparent; color: var(--danger); border: 1px solid rgba(248,113,113,0.2); padding: 6px 12px; font-size: 0.8rem; }
  .btn-danger:hover { background: rgba(248,113,113,0.1); }
  .btn-logout { background: transparent; color: var(--text-muted); border: 1px solid var(--border); padding: 8px 16px; font-size: 0.8rem; }
  .btn-logout:hover { color: var(--text); border-color: rgba(255,255,255,0.2); }
  .btn-copy { background: transparent; color: var(--text-muted); border: none; padding: 4px 8px; font-size: 0.8rem; cursor: pointer; }
  .btn-copy:hover { color: var(--accent); }

  /* ── Table ── */
  .table-section h2 { font-size: 1rem; font-weight: 600; margin-bottom: 16px; }

  .table-wrap {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    overflow: hidden;
  }

  table { width: 100%; border-collapse: collapse; }
  th {
    text-align: left; padding: 12px 16px;
    font-size: 0.7rem; font-weight: 600; color: var(--text-muted);
    text-transform: uppercase; letter-spacing: 0.05em;
    border-bottom: 1px solid var(--border);
    background: rgba(0,0,0,0.2);
  }
  td {
    padding: 14px 16px; font-size: 0.85rem;
    border-bottom: 1px solid var(--border);
    transition: background 0.15s;
  }
  tr:last-child td { border-bottom: none; }
  tr:hover td { background: var(--surface-hover); }

  .key-cell { font-family: 'Courier New', monospace; font-weight: 600; font-size: 0.82rem; letter-spacing: 0.02em; }

  .badge {
    display: inline-block; padding: 3px 10px;
    border-radius: 20px; font-size: 0.7rem; font-weight: 600;
    text-transform: uppercase; letter-spacing: 0.03em;
  }
  .badge-active { background: rgba(52,211,153,0.15); color: var(--success); }
  .badge-expired { background: rgba(251,191,36,0.15); color: var(--warning); }

  .empty-state { padding: 48px; text-align: center; color: var(--text-muted); }

  /* ── Toast ── */
  .toast {
    position: fixed; bottom: 24px; right: 24px;
    background: rgba(30,30,40,0.95); border: 1px solid var(--border);
    border-radius: 10px; padding: 14px 20px;
    font-size: 0.85rem; backdrop-filter: blur(12px);
    transform: translateY(100px); opacity: 0;
    transition: all 0.3s ease;
    z-index: 2000;
  }
  .toast.show { transform: translateY(0); opacity: 1; }
  .toast.success { border-color: rgba(52,211,153,0.3); }
  .toast.error { border-color: rgba(248,113,113,0.3); }

  /* ── Loading ── */
  .spinner {
    display: inline-block; width: 16px; height: 16px;
    border: 2px solid rgba(255,255,255,0.1);
    border-top-color: var(--accent);
    border-radius: 50%;
    animation: spin 0.6s linear infinite;
  }
  @keyframes spin { to { transform: rotate(360deg); } }

  @media (max-width: 640px) {
    .stats { grid-template-columns: 1fr; }
    .form-row { flex-direction: column; }
  }
</style>
</head>
<body>

<!-- Auth Screen -->
<div class="auth-overlay" id="authOverlay">
  <div class="auth-card">
    <h1>🔐 License Manager</h1>
    <p>Enter your admin token to continue</p>
    <input type="password" id="tokenInput" placeholder="Admin token..." style="margin-bottom:16px">
    <br>
    <button class="btn btn-primary" onclick="doLogin()" style="width:100%">Sign In</button>
  </div>
</div>

<!-- Main App -->
<div class="container" id="app" style="display:none">
  <header>
    <h1>🔑 License <span>Manager</span></h1>
    <button class="btn btn-logout" onclick="doLogout()">Logout</button>
  </header>

  <div class="stats" id="stats">
    <div class="stat-card"><div class="value" id="statTotal">-</div><div class="label">Total Keys</div></div>
    <div class="stat-card active"><div class="value" id="statActive">-</div><div class="label">Active</div></div>
    <div class="stat-card expired"><div class="value" id="statExpired">-</div><div class="label">Expired</div></div>
  </div>

  <div class="create-section">
    <h2>Create New License</h2>
    <div class="form-row">
      <div class="form-group">
        <label>Email</label>
        <input type="email" id="inputEmail" placeholder="user@example.com">
      </div>
      <div class="form-group">
        <label>Expires At</label>
        <input type="date" id="inputExpiry">
      </div>
      <div class="form-group" style="flex:0 0 auto; display:flex; align-items:flex-end;">
        <button class="btn btn-primary" id="createBtn" onclick="createLicense()">Create Key</button>
      </div>
    </div>
  </div>

  <div class="table-section">
    <h2>All Licenses</h2>
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>License Key</th>
            <th>Email</th>
            <th>Status</th>
            <th>Expires</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody id="licenseTable">
          <tr><td colspan="6" class="empty-state"><div class="spinner"></div> Loading...</td></tr>
        </tbody>
      </table>
    </div>
  </div>
</div>

<div class="toast" id="toast"></div>

<script>
const API = window.location.origin;
let TOKEN = localStorage.getItem('admin_token') || '';

// ── Auth ──
function doLogin() {
  TOKEN = document.getElementById('tokenInput').value.trim();
  if (!TOKEN) return;
  localStorage.setItem('admin_token', TOKEN);
  document.getElementById('authOverlay').classList.add('hidden');
  document.getElementById('app').style.display = '';
  loadLicenses();
}

function doLogout() {
  TOKEN = '';
  localStorage.removeItem('admin_token');
  document.getElementById('authOverlay').classList.remove('hidden');
  document.getElementById('app').style.display = 'none';
}

// Auto-login if token saved
if (TOKEN) {
  document.getElementById('authOverlay').classList.add('hidden');
  document.getElementById('app').style.display = '';
}

// ── API Helpers ──
async function api(path, method = 'GET', body = null) {
  const opts = {
    method,
    headers: { 'Authorization': 'Bearer ' + TOKEN, 'Content-Type': 'application/json' },
  };
  if (body) opts.body = JSON.stringify(body);
  const res = await fetch(API + path, opts);
  const data = await res.json();
  if (res.status === 401) { doLogout(); throw new Error('Unauthorized'); }
  if (!res.ok && data.error) throw new Error(data.error);
  return data;
}

// ── Load & Render ──
async function loadLicenses() {
  try {
    const data = await api('/api/licenses');
    renderTable(data.licenses);
    updateStats(data.licenses);
  } catch (err) {
    toast(err.message, 'error');
  }
}

function renderTable(licenses) {
  const tbody = document.getElementById('licenseTable');
  if (!licenses.length) {
    tbody.innerHTML = '<tr><td colspan="6" class="empty-state">No licenses yet. Create one above.</td></tr>';
    return;
  }

  const now = new Date();
  tbody.innerHTML = licenses.map(l => {
    const expired = new Date(l.expires_at) < now;
    const badge = expired
      ? '<span class="badge badge-expired">Expired</span>'
      : '<span class="badge badge-active">Active</span>';
    const expiryDate = new Date(l.expires_at).toLocaleDateString();
    const createdDate = new Date(l.created_at).toLocaleDateString();

    return \`<tr>
      <td class="key-cell">
        \${l.license_key}
        <button class="btn-copy" onclick="copyKey('\${l.license_key}')" title="Copy">📋</button>
      </td>
      <td>\${l.email}</td>
      <td>\${badge}</td>
      <td>\${expiryDate}</td>
      <td>\${createdDate}</td>
      <td><button class="btn btn-danger" onclick="deleteLicense('\${l.license_key}')">Delete</button></td>
    </tr>\`;
  }).join('');
}

function updateStats(licenses) {
  const now = new Date();
  const active = licenses.filter(l => new Date(l.expires_at) >= now).length;
  const expired = licenses.length - active;
  document.getElementById('statTotal').textContent = licenses.length;
  document.getElementById('statActive').textContent = active;
  document.getElementById('statExpired').textContent = expired;
}

// ── Actions ──
async function createLicense() {
  const email = document.getElementById('inputEmail').value.trim();
  const expires_at = document.getElementById('inputExpiry').value;

  if (!email || !expires_at) {
    toast('Please fill in email and expiry date', 'error');
    return;
  }

  const btn = document.getElementById('createBtn');
  btn.disabled = true;
  btn.innerHTML = '<span class="spinner"></span>';

  try {
    const data = await api('/api/licenses', 'POST', { email, expires_at });
    toast('Created: ' + data.license_key, 'success');
    document.getElementById('inputEmail').value = '';
    document.getElementById('inputExpiry').value = '';
    await loadLicenses();
  } catch (err) {
    toast(err.message, 'error');
  } finally {
    btn.disabled = false;
    btn.textContent = 'Create Key';
  }
}

async function deleteLicense(key) {
  if (!confirm('Delete license key?\\n' + key)) return;
  try {
    await api('/api/licenses?key=' + encodeURIComponent(key), 'DELETE');
    toast('Deleted', 'success');
    await loadLicenses();
  } catch (err) {
    toast(err.message, 'error');
  }
}

function copyKey(key) {
  navigator.clipboard.writeText(key);
  toast('Copied to clipboard', 'success');
}

// ── Toast ──
function toast(msg, type = 'success') {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.className = 'toast show ' + type;
  setTimeout(() => el.classList.remove('show'), 3000);
}

// ── Init ──
if (TOKEN) loadLicenses();
</script>
</body>
</html>`;
}
