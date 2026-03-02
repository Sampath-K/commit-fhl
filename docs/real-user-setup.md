# Real-User Demo Tenant Setup Guide

> **Decision**: D-008 — Real-User Demo Tenant Setup
> **Status**: Automated via `scripts/setup-tenant.ts`

---

## TL;DR — Full Setup in 3 Commands

```bash
# 1. Log in as tenant admin (one time)
az login --tenant 91b9767c-6b0a-4b0b-bd4d-e08a6383426c

# 2. Run the full automated setup (idempotent — safe to repeat)
API_BASE_URL=https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io \
  npx ts-node --project scripts/tsconfig.json scripts/setup-tenant.ts

# 3. During the demo — inject a live commitment arrival (60s delay)
API_BASE_URL=https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io \
  npx ts-node --project scripts/tsconfig.json scripts/demo-live-arrival.ts --delay 60
```

To reset everything and start fresh:

```bash
npx ts-node --project scripts/tsconfig.json scripts/setup-tenant.ts --reset
```

---

## What `setup-tenant.ts` Does

| Step | Action | Idempotent? |
|------|--------|-------------|
| 1 | Get admin token via Azure CLI | ✅ always |
| 2 | Detect tenant domain (verifiedDomains) | ✅ always |
| 3 | Create 6 AAD users (skip if already exist) | ✅ yes — checks UPN before creating |
| 4 | Flush existing data (demo-* OIDs + prior real OIDs) | ✅ yes — DELETE is idempotent |
| 5 | Re-seed 24 commitments with real OIDs | ✅ yes — API uses upsert |
| 6 | Verify API health + commitment count | ✅ always |

---

## The 6 Demo Personas

These user accounts are created in your tenant:

| Persona | UPN | Password | Role in Story |
|---------|-----|----------|---------------|
| **Alex Chen** | `alex.chen@<domain>` | `Commit@FHL2026!` | Lead engineer — opens the Teams pane during demo |
| **Priya Sharma** | `priya.sharma@<domain>` | `Commit@FHL2026!` | EM — tracks both cascade chains |
| **Marcus Johnson** | `marcus.johnson@<domain>` | `Commit@FHL2026!` | BizChat Platform — cross-org blocked dependency (purple) |
| **Fatima Al-Rashid** | `fatima.alrashid@<domain>` | `Commit@FHL2026!` | PM — watcher, receives proactive comms |
| **David Park** | `david.park@<domain>` | `Commit@FHL2026!` | Director — exec visibility watcher |
| **Sarah O'Brien** | `sarah.obrien@<domain>` | `Commit@FHL2026!` | Scheduling Skill — cross-team dependency (green) |

**`<domain>`** is auto-detected from your tenant's verified domains (e.g. `contoso.onmicrosoft.com`).

**During the demo**: Sign into Teams as `alex.chen@<domain>`. The Commit pane loads Alex's 9 commitments with real name and photo from AAD.

---

## Admin Consent for Graph Permissions

The app registration (CLIENT_ID: `07b0afff-85b6-4be1-98ba-d26d566bd14a`) needs admin consent for:

```
Chat.Read, Chat.ReadWrite, ChannelMessage.Read.All,
Mail.Read, Mail.Send,
Calendars.Read, Calendars.ReadWrite,
OnlineMeetings.Read, Tasks.ReadWrite,
User.Read, User.ReadWrite.All (for user creation),
Analytics.Read
```

**How to grant consent:**
1. Go to [portal.azure.com](https://portal.azure.com)
2. Azure Active Directory → Enterprise Applications → `07b0afff-85b6-4be1-98ba-d26d566bd14a`
3. Permissions → Grant admin consent for `<your tenant>`

Or via CLI:
```bash
az ad app permission admin-consent --id 07b0afff-85b6-4be1-98ba-d26d566bd14a
```

---

## Teams App Installation

1. Build the app package:
   ```bash
   cd appPackage && zip -r ../commit-fhl.zip manifest.json color.png outline.png
   ```

2. Upload to Teams Admin Center:
   - Go to [admin.teams.microsoft.com](https://admin.teams.microsoft.com)
   - Teams apps → Manage apps → Upload new app → select `commit-fhl.zip`
   - Set availability: All users (or specific demo accounts)

3. Pin the app for demo accounts:
   - Setup policies → Add app → Commit → Save

---

## Demo Day: Live Commitment Arrival (T-042)

During the demo, start the live arrival script in a separate terminal **before** you begin presenting:

```bash
# Terminal 2 (hidden from screen) — start just before the demo begins
API_BASE_URL=https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io \
  REAL_OID_ALEX=<real-oid> \                   # optional — falls back to alex.chen's real OID
  npx ts-node --project scripts/tsconfig.json scripts/demo-live-arrival.ts --delay 60
```

When you reach the "live extraction" beat in the demo script, say:
> "I started an extraction in the background when I opened this meeting.
> Watch the pane — a new commitment should arrive any moment now..."

Within 60 seconds, the new card appears: **"Address Q1 latency regression in plugin routing"** with:
- Source: Teams standup (2 minutes ago)
- Impact score: 72
- Already linked to Marcus's BizChat routing task

---

## Troubleshooting

| Error | Fix |
|-------|-----|
| `az: command not found` | Install Azure CLI: https://aka.ms/installazurecli |
| `AADSTS700016: not logged in` | Run `az login --tenant 91b9767c-...` |
| `403 Insufficient privileges` | App needs `User.ReadWrite.All` — grant admin consent (see above) |
| `API not running` | `cd src/api && dotnet run` (or check Azure Container Apps deployment) |
| User created but pane shows demo OID | Run `setup-tenant.ts` again — it flushes and re-seeds |
| Teams app not visible | Upload zip to Teams Admin Center (see above) |
