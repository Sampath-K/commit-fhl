# Commit FHL — Session State
> **This is the first file any agent reads at the start of every session.**
> Updated at the end of every session and after every completed task.

---

## Standard Resume (Team Pattern)

**To resume any session:**
```
Read .agents/commit-fhl/SESSION.md — you are the lead agent on the Commit FHL project.
Resume from the current state. Check tasks.md for next task and decisions.md for any
pending human decisions before acting. Read your role card in .specify/memory/agent-roles/.
```

---

## Current State

| Field | Value |
|-------|-------|
| **Sprint Day** | Day 1 — Monday (active) |
| **Phase** | Building — sprint started |
| **Repo** | https://github.com/Sampath-K/commit-fhl (private) |
| **Local root** | `C:\Dev\commit-fhl\` |
| **Source root** | `C:\Dev\commit-fhl\src\` |
| **Last completed task** | Setup complete — repo, governance, psychology layer spec |
| **Next task** | T-C04 (Lens: Jest + Stryker + Playwright config) → T-C01 (Shield: Feature flags) → T-006 (Forge: types/index.ts) — run parallel |
| **Blockers** | D-003 partially done — Azure OpenAI key still needed in .env |
| **Human decisions needed** | D-003 (Azure OpenAI endpoint/key) before T-011 (NLP pipeline) |
| **Build status** | Scaffolded — no product code yet |
| **Last updated** | 2026-03-01 (repo setup + governance complete) |
| **Constitution version** | v1.1.0 (P-01 through P-27 including Psychology layer) |

---

## What Exists Right Now

| Artifact | Location | Status |
|----------|----------|--------|
| GitHub repo | https://github.com/Sampath-K/commit-fhl | ✅ Created (private) |
| Constitution | `.specify/memory/constitution.md` | ✅ v1.1.0 — P-01 through P-27 |
| UX Psychology spec | `.specify/memory/ux-psychology.md` | ✅ Complete |
| Agent role cards | `.specify/memory/agent-roles/` | ✅ All 6 done |
| Agent inbox | `.specify/memory/agent-inbox.md` | ✅ Ready |
| ADR template | `.specify/memory/adr-template.md` | ✅ Ready |
| Tech debt tracker | `.specify/memory/tech-debt.md` | ✅ Ready |
| Spec | `.agents/commit-fhl/spec.md` | ✅ Stable |
| Architecture plan | `.agents/commit-fhl/plan.md` | ✅ Done |
| Task list | `.agents/commit-fhl/tasks.md` | ✅ All tasks assigned [Agent: X], T-C01–C07 added |
| Decision log | `.agents/commit-fhl/decisions.md` | ✅ D-001, D-002 done; D-003 partial |
| Agent instructions | `.agents/commit-fhl/AGENT_INSTRUCTIONS.md` | ✅ Updated with multi-agent section |
| Directory structure | `src/api/`, `src/app/`, `tests/`, `scripts/`, `infra/` | ✅ Created |
| Source code | None yet | ⏳ Day 1 |

---

## Day 1 Parallel Dispatch Plan

```
SEQUENTIAL FIRST (types needed by all Forge tasks):
  T-006  [Forge]     → src/api/src/types/index.ts

PARALLEL BATCH 1 (all independent — launch simultaneously):
  T-C04  [Lens]      → Jest + Stryker + Playwright config
  T-C01  [Shield]    → Azure App Configuration + FeatureFlagService.ts
  T-C03  [Shield]    → Application Insights + PII scrubber middleware
  T-C02  [Canvas]    → react-i18next + ESLint i18n rule
  T-C05  [Seed]      → seed-demo.ts + flush-demo.ts scaffold

AFTER T-006 TYPES DONE (parallel):
  T-007  [Forge]     → commitmentStore.ts + 5 unit tests
  T-008  [Shield]    → webhookHandler.ts + HMAC signature validation
  T-009  [Canvas]    → CommitPane + Morning Digest skeleton

AFTER T-007 STORAGE DONE:
  T-004  [Shield]    → Teams Toolkit local dev server verification
  T-005  [Forge]     → MSAL OBO + graphClient.ts → /api/v1/health endpoint
```

---

## Day Completion Log

| Day | Status | Key output | Committed |
|-----|--------|-----------|-----------|
| Mon D1 | 🔵 In progress | Scaffold + auth + shell | — |
| Tue D2 | ⏳ Not started | Signal extraction live | — |
| Wed D3 | ⏳ Not started | Cascade engine live | — |
| Thu D4 | ⏳ Not started | Execution agents + psychology layer | — |
| Fri D5 | ⏳ Not started | Live demo | — |

---

## Agent Behaviour Rules (read every session)

1. **Update this file** after every completed task.
2. **Git commit** after every completed task (then push): `git add -A && git commit -m "feat(scope): T-NNN description" && git push`
3. **Never ask the human** about something already decided in `decisions.md`.
4. **Surface blockers** — post to `agent-inbox.md` with `[BLOCKING]`, add to `decisions.md`, move to next task.
5. **Run tests** before marking any task complete.
6. **Write session summary** to this file before stopping if context is getting long.
7. **Scope discipline**: Do not build features not in `tasks.md`. Add ideas to backlog section.
8. **Check agent-inbox.md** at start of every session before building anything.
