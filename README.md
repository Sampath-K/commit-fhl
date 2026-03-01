# Commit — AI-Powered Commitment Tracking for Microsoft Teams

> Built during FHL (Fix Hack Learn) week. 5 days. Humans define what. Agents build how.

## What It Does

**Commit** is a Teams pane that:

1. **Captures** every commitment automatically from meetings, chat, email, and ADO
2. **Graphs** them into a live dependency map with owners, ETAs, and watchers
3. **Simulates** cascade impact when any task is at risk — before it slips
4. **Agents** draft replans, stakeholder communications, and actions
5. **Humans** approve with one click; agents execute

The UX is built on evidence-based behavioral psychology — progress feedback, competency
progression, habit loops, and micro-animations — to make managing commitments intrinsically
motivating rather than stressful.

---

## Quick Start

```bash
# Clone
git clone https://github.com/Sampath-K/commit-fhl.git
cd commit-fhl

# Copy and fill in environment variables
cp .env.example .env
# Edit .env with your Azure credentials

# Install dependencies
cd src/api && npm install
cd ../app && npm install

# Start Azurite (local storage emulator)
npx azurite &

# Start the API
cd src/api && npm run dev

# Start the Teams tab
cd src/app && npm run dev
```

---

## Project Structure

```
commit-fhl/
├── src/
│   ├── api/          ← Node.js backend (Graph API, NLP, storage, webhooks)
│   └── app/          ← React frontend (Teams tab, psychology layer, animations)
├── tests/
│   ├── unit/         ← Jest unit tests (≥ 90% coverage + Stryker mutation)
│   ├── integration/  ← Functional API tests (Azurite backend)
│   └── e2e/          ← Playwright E2E (5 journeys × 4 viewports)
├── scripts/          ← Demo data: seed-demo.ts, flush-demo.ts, verify-demo.ts
├── infra/            ← Azure Bicep templates
├── .github/          ← GitHub Actions CI/CD
├── .agents/          ← Agent instructions, task list, session state
├── .specify/         ← Project constitution, agent role cards, ADRs
└── docs/             ← Sprint dashboards, HTML reports
```

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React + TypeScript + Fluent UI v9 |
| Backend | Node.js 22 + TypeScript + Express |
| Auth | MSAL Node + Microsoft Graph OBO flow |
| AI/NLP | Azure OpenAI GPT-4o |
| Storage | Azure Table Storage (Azurite for local dev) |
| Feature Flags | Azure App Configuration |
| Observability | Azure Application Insights |
| Testing | Jest + Stryker + Playwright |
| CI/CD | GitHub Actions |
| Animations | @react-spring/web (physics-based) |

---

## For Agents — Start Here

```
1. Read .agents/commit-fhl/SESSION.md     ← current state
2. Read .agents/commit-fhl/decisions.md   ← finalized decisions
3. Read .agents/commit-fhl/tasks.md       ← find your next task
4. Read .specify/memory/constitution.md   ← all engineering principles
5. Read your role card in .specify/memory/agent-roles/
```

---

## FHL Sprint Status

| Day | Status | Goal |
|-----|--------|------|
| Mon | ⏳ | Scaffold + auth + webhooks + shell UI |
| Tue | ⏳ | 4 extractors live, real data in pane |
| Wed | ⏳ | Cascade engine + replan generator |
| Thu | ⏳ | Execution agents + approval loop |
| Fri | ⏳ | Live demo at 4PM |

---

## License

MIT — built during FHL week, open for learning and remixing.
