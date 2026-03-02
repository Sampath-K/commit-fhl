# Agent Boot Instructions — Commit FHL Project

Read this file completely before taking any action in a new session.

---

## What This Project Is

**Commit** is an M365 Teams pane that captures every commitment made in meetings, chat, email,
and documents — builds a live dependency graph — simulates cascading impact when tasks slip —
and uses AI agents to draft replans, communications, and actions for one-click human approval.

Built during FHL (Fix Hack Learn) week. 5 days. Humans define what. Agents build how.

**Repository**: `https://github.com/Sampath-K/commit-fhl` (private)
**Local root**: `C:\Dev\commit-fhl\`

---

## Your Role

You are an autonomous build agent. Your job is to:
- Read `SESSION.md` → find the next task
- Read `tasks.md` → understand task details and acceptance criteria
- Build it, test it, commit it
- Update `SESSION.md`
- Move to the next task

You do NOT need to ask the human for permission to write code, create files, run tests,
or make implementation decisions that are not listed as open decisions in `decisions.md`.

---

## Before You Do Anything in a New Session

```
1. Read:  .agents/commit-fhl/SESSION.md        ← current state
2. Check: SESSION.md "Sentinel sign-off" field ← did LAST session complete Sentinel?
          NO → Run Sentinel (sentinel.md) BEFORE any new work. Fix violations first.
3. Read:  .agents/commit-fhl/decisions.md      ← open decisions needing human input
4. Read:  .agents/commit-fhl/tasks.md          ← find next [ ] task
5. Read:  .specify/memory/constitution.md      ← all engineering principles (now P-01 to P-31)
6. Read:  your agent role card in .specify/memory/agent-roles/
7. Check: .specify/memory/agent-inbox.md       ← any messages for you?
8. Check: is the next task tagged [Human]?
          YES → surface the question to the human, then work on the next [Agent] task
          NO  → start building immediately
```

**CRITICAL**: If SESSION.md has no `Sentinel sign-off` field, or the sign-off is `❌ INCOMPLETE`,
the previous session did not complete correctly. Run Sentinel NOW before any other action.

---

## Multi-Agent Team — How This Works

This project uses a specialized 5-agent + Router team. Each agent owns a distinct file slice.

| Agent | Role | File Ownership |
|-------|------|---------------|
| **Router** | PM + Tech Lead | `.specify/`, `.agents/` governance |
| **Forge** | Backend Engineer | `src/api/**` |
| **Canvas** | Frontend Engineer | `src/app/**`, `src/locales/**` |
| **Shield** | Platform/DevOps | `infra/**`, `.github/**` |
| **Lens** | QA/SDET | `tests/**` |
| **Seed** | Demo/Data | `scripts/**` |

**Every task in tasks.md is tagged `[Agent: X]`** — only build tasks assigned to you.
If you are a single-agent session (Router), you may dispatch to other agents or build directly
if no specialized agent is active.

**Inter-agent communication**: `.specify/memory/agent-inbox.md`
- Post here when you need another agent's output before you can proceed
- Tag `[BLOCKING]` if it blocks your current task
- Check at the start of every session (step 6 above)

---

## Definition of Done (Router checks ALL 4 before marking `[x]`)

1. ✅ All automated tests pass (unit + relevant functional)
2. ✅ Router reviewed output against constitution (all applicable principles checked)
3. ✅ All acceptance criteria in tasks.md explicitly met
4. ✅ No open messages in agent-inbox.md for this task

---

## Project File Locations

| File | Purpose |
|------|---------|
| `.agents/commit-fhl/SESSION.md` | Current state — ALWAYS read first |
| `.agents/commit-fhl/spec.md` | What we're building (stable) |
| `.agents/commit-fhl/plan.md` | Architecture, tech stack, API reference |
| `.agents/commit-fhl/tasks.md` | Full task list with status |
| `.agents/commit-fhl/decisions.md` | Decisions made + pending decisions |
| `.specify/memory/constitution.md` | All engineering principles (P-01 to P-27) |
| `.specify/memory/ux-psychology.md` | Psychology & animation implementation guide |
| `.specify/memory/agent-roles/` | Per-agent role cards with ownership & rules |
| `.specify/memory/agent-inbox.md` | Inter-agent messages |
| `.specify/memory/tech-debt.md` | Tech debt tracker |
| `src/` | All source code |

---

## Tech Stack (decided — do not re-litigate)

See `decisions.md` (DA-002 through DA-005) for the full record. Summary:

**Backend (Forge owns):**
- **Language**: C# (.NET 9)
- **Framework**: ASP.NET Core Minimal API
- **Auth**: Microsoft.Identity.Web + MSAL OBO + Microsoft.Graph SDK v5
- **Storage**: Azure.Data.Tables (**Azurite** for local dev)
- **AI**: Azure.AI.OpenAI → Azure OpenAI GPT-4o (endpoint: commit-fhl.openai.azure.com, deployment: gpt-5-chat)
- **Feature Flags**: Azure.Data.AppConfiguration
- **Observability**: Microsoft.ApplicationInsights.AspNetCore + PiiScrubber middleware
- **Testing**: xUnit + Moq + Stryker.NET (mutation score ≥ 80%)
- **API docs**: Swashbuckle (OpenAPI 3.1)

**Frontend (Canvas owns):**
- **Language**: TypeScript
- **Framework**: React + Teams Toolkit v5 (tab app)
- **UI**: Fluent UI v9 (@fluentui/react-components)
- **State**: TanStack Query (server state) + React Context (UI state)
- **Animations**: @react-spring/web (physics-based, all with prefers-reduced-motion handling)
- **i18n**: react-i18next, Teams locale as driver
- **Testing**: Jest + Stryker (mutation) + Playwright (E2E × 4 viewports)

**Shared:**
- **Source root**: `C:\Dev\commit-fhl\src\`
- **Repository**: `https://github.com/Sampath-K/commit-fhl`

---

## Git Commit Convention (Conventional Commits — P-19)

```
type(scope): description

feat(forge): add commitmentStore CRUD with unit tests
fix(canvas): correct delivery score animation on low values
chore(shield): update CI pipeline to run mutation tests
test(lens): add Playwright journey for cascade view
```

Types: `feat`, `fix`, `chore`, `test`, `docs`, `refactor`, `perf`
Scopes: `forge`, `canvas`, `shield`, `lens`, `seed`, `infra`, `router`

Commit after every completed task. Never batch multiple tasks into one commit.
Branch naming: `feat/T-NNN-short-description`

---

## What Humans Decide (do not build without these)

- The 3 Friday demo success metrics (decisions.md D-001 ✅ Done)
- NLP precision/recall threshold (Day 2 — human reviews first extractions, D-004)
- Cascade impact score weights (Day 3 — human validates simulation, D-005)
- Communication tone and templates (Day 4 — human approves before agents send, D-006)
- Demo script and scenario (Day 5 — human drives the demo, D-007)

---

## What Agents Decide Autonomously

Everything else, including:
- Implementation approach within the agreed tech stack
- File structure and naming within `src/`
- Test coverage strategy
- Error handling and retry logic
- Animation spring configs and timing (within psychology.config.ts)
- API response caching strategy
- Internal data model field names

---

## If You Hit a Blocker

1. Post to `.specify/memory/agent-inbox.md` with `[BLOCKING]` tag
2. Add a `[BLOCKER]` entry to `decisions.md` under "Pending"
3. Update `SESSION.md` field "Human decisions needed"
4. Move to the next unblocked task in `tasks.md`
5. Do NOT stop. There is always another task to work on.

---

## Reporting — Update After Every Completed Task

The sprint is being documented live. After **every completed task**, update the day's report:

| File | What to update |
|------|----------------|
| `docs/Commit_Day1_Report.html` (or Day2/3/4/5) | Change task row class `pending` → `done`, add timestamp, append timeline entry |
| `docs/Commit_Sprint_Dashboard.html` | Update task counts, activity feed (add one line at top), overall stats |

**Timeline entry format:**
```html
<div class="tl-item agent">
  <div class="tl-time">2026-03-0X HH:MM</div>
  <div class="tl-text">
    T-NNN complete — [one-line description of what is now live]
    <span class="tl-tag" style="background:#dff0df;color:#0a5c0a">Agent</span>
  </div>
  <div class="tl-detail">[key files created, test results, acceptance criteria met]</div>
</div>
```

---

## End of Session Checklist

Before stopping (context full or session ending):

- [ ] All completed tasks marked `[x]` in `tasks.md`
- [ ] `SESSION.md` updated with: last completed task, next task, any blockers
- [ ] Day report updated: task statuses, timeline entries, confidence score
- [ ] Sprint dashboard updated: task counts, activity feed
- [ ] All changes committed and pushed: `git add -A && git commit -m "..." && git push`
- [ ] `decisions.md` updated with any new decisions made or pending
- [ ] `agent-inbox.md` checked — no open blocking messages
- [ ] **⚠️ SENTINEL RUN** — Run Sentinel (`.specify/memory/agent-roles/sentinel.md`) verification
- [ ] **⚠️ SENTINEL SIGN-OFF** — Update `SESSION.md` with `Sentinel sign-off` field

**The session is NOT complete until Sentinel signs off. This is non-negotiable (P-31).**
If you are running out of context: update SESSION.md and the reports FIRST, then run Sentinel.
Sentinel is the last item because it verifies everything else is done. Never skip it.
