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
| **Sprint Day** | Day 5 — Demo Ready (T-040 pending) |
| **Phase** | LIVE on Azure · demo scripts written · team labels in UI · Sentinel governance active |
| **Repo** | https://github.com/Sampath-K/commit-fhl (private) |
| **Local root** | `C:\Dev\commit-fhl\` |
| **Source root** | `C:\Dev\commit-fhl\src\` |
| **Last completed task** | v10 deploy — DriveExtractor inline OOXML comments, PlannerExtractor plan titles, ChatExtractor channel ProjectContext, EmailExtractor ArtifactName, By Project view in CommitPane |
| **Next task** | Human: `az login --tenant 91b9767c-...` then `npx ts-node scripts/setup-tenant.ts` · Human: T-040 4PM demo |
| **Blockers** | None — all agent work done. Setup is automated; human just needs to run setup-tenant.ts after az login. |
| **Human decisions needed** | T-040: 4PM live demo · Run setup-tenant.ts to create real users (see docs/real-user-setup.md) |
| **Build status** | DEPLOYED v10 2026-03-03 · API v10 live · Frontend live with By Project view · Constitution v1.5.0 (ZHIN + H-A Contract + Template + Recon + Oracle) |
| **Last updated** | 2026-03-03 (v10 deployed · By Project view · extractor enrichment · constitution v1.5.0) |
| **Constitution version** | v1.5.0 (P-01 through P-35) — +P-32 ZHIN, +P-33 H-A Contract, +P-34 Template, +P-35 9-agent roster |
| **API URL** | https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io |
| **Frontend URL** | https://thankful-pond-0ba16370f.6.azurestaticapps.net |
| **Resource group** | commit-fhl-rg (East US) · subscription: Visual Studio Enterprise (6dbb6c34) |
| **Sentinel sign-off** | ✅ 2026-03-02 16:00 — 0 violations — speckit fully refreshed, real-user automation complete |

---

## What Exists Right Now

| Artifact | Location | Status |
|----------|----------|--------|
| GitHub repo | https://github.com/Sampath-K/commit-fhl | ✅ Created (private) |
| Constitution | `.specify/memory/constitution.md` | ✅ v1.4.0 — P-01 through P-31 (P-31 = Sentinel Integrity) |
| UX Psychology spec | `.specify/memory/ux-psychology.md` | ✅ Complete |
| Agent role cards | `.specify/memory/agent-roles/` | ✅ All 7: Forge/Canvas/Shield/Lens/Seed/Router + Sentinel (P-31) |
| Agent inbox | `.specify/memory/agent-inbox.md` | ✅ Ready |
| ADR template | `.specify/memory/adr-template.md` | ✅ Ready |
| Tech debt tracker | `.specify/memory/tech-debt.md` | ✅ Ready |
| Sentinel role | `.specify/memory/agent-roles/sentinel.md` | ✅ P-31 — 4-phase verification protocol |
| Sentinel log | `.specify/memory/sentinel-log.md` | ✅ First run 2026-03-02, 3 violations resolved |
| Spec | `.agents/commit-fhl/spec.md` | ✅ Stable (v1.0 — FHL scope) |
| Architecture plan | `.agents/commit-fhl/plan.md` | ✅ Done (DA-005 C# backend canonical) |
| Task list | `.agents/commit-fhl/tasks.md` | ✅ Days 1-5 all [x]; T-041[!] blocked on D-008; T-042/T-040 pending |
| Decision log | `.agents/commit-fhl/decisions.md` | ✅ D-001/D-002/D-007 ✅ Made; D-003 partial; D-008 ⏳ needs human action |
| Agent instructions | `.agents/commit-fhl/AGENT_INSTRUCTIONS.md` | ✅ Updated for C# + multi-agent + P-31 Sentinel |
| **C# API project** | `src/api/CommitApi.csproj` | ✅ .NET 9, all DI wired |
| **Exceptions** | `src/api/Exceptions/CommitException.cs` | ✅ Full hierarchy |
| **Repository** | `src/api/Repositories/CommitmentRepository.cs` | ✅ Azure Table Storage |
| **Feature flags** | `src/api/Config/FeatureFlagService.cs` | ✅ 5 flags, 60s cache |
| **PII scrubber** | `src/api/Config/PiiScrubber.cs` | ✅ SHA-256 hashing |
| **App Insights** | `src/api/Config/AppInsightsExtensions.cs` | ✅ 4 event types |
| **Graph auth** | `src/api/Auth/GraphClientFactory.cs` | ✅ MSAL OBO |
| **Webhooks** | `src/api/Webhooks/SubscriptionManager.cs` | ✅ Graph subscriptions |
| **Webhook handler** | `src/api/Webhooks/WebhookHandler.cs` | ✅ HMAC validation |
| **Program.cs** | `src/api/Program.cs` | ✅ All 5 routes wired |
| **xUnit tests** | `src/api/CommitApi.Tests/` | ✅ 15 tests across 3 suites |
| **Frontend types** | `src/app/src/types/api.ts` | ✅ Contract types |
| **i18n** | `src/app/src/i18n.ts` + locales/ | ✅ Teams locale driver |
| **ESLint i18n rule** | `src/app/eslint.config.js` | ✅ no-literal-string enforced |
| **CommitPane** | `src/app/src/components/core/CommitPane.tsx` | ✅ Fluent v9, i18n |
| **App.tsx / main.tsx** | `src/app/src/` | ✅ Teams SDK + TanStack Query |
| **Vite config** | `src/app/vite.config.ts` | ✅ Port 3000, API proxy |
| **Test infra** | `jest.config.ts`, `stryker.config.json`, `playwright.config.ts` | ✅ Frontend-only |
| **Seed scripts** | `scripts/seed-demo.ts`, `scripts/personas/`, `scripts/scenarios/` | ✅ Dry-run passes |
| **Dependency linker** | `src/api/Graph/DependencyLinker.cs` | ✅ 3 signals (thread, people, title ≥0.7) |
| **Cascade simulator** | `src/api/Graph/CascadeSimulator.cs` | ✅ BFS, calendar pressure, 4-test suite |
| **Impact scorer** | `src/api/Graph/ImpactScorer.cs` | ✅ Formula capped 0-100, 6 tests |
| **Viva Insights client** | `src/api/Capacity/VivaInsightsClient.cs` | ✅ loadIndex + burnoutTrend + free slots |
| **Risk detector** | `src/api/Agents/RiskDetector.cs` | ✅ BackgroundService, 15-min polling |
| **Replan generator** | `src/api/Replan/ReplanGenerator.cs` | ✅ Options A/B/C with confidence levels |
| **Graph routes** | `POST /graph/build`, `POST /graph/cascade`, `POST /graph/replan`, `GET /capacity` | ✅ Wired in Program.cs |
| **Graph models** | `src/api/Models/Graph/`, `src/api/Models/Capacity/` | ✅ GraphEdge, CascadeResult, ReplanOption, CapacitySnapshot |
| **Adaptive Card builder** | `src/api/Agents/AdaptiveCardBuilder.cs` | ✅ Draft card + info card |
| **Status update drafter** | `src/api/Agents/StatusUpdateDrafter.cs` | ✅ Per-watcher Teams messages for Option C |
| **Overcommit firewall** | `src/api/Agents/OvercommitFirewall.cs` | ✅ Load > 90% warning draft |
| **Calendar blocker** | `src/api/Agents/CalendarBlocker.cs` | ✅ Creates 2hr focus event via Graph |
| **PR review drafter** | `src/api/Agents/PrReviewDrafter.cs` | ✅ ADO diff + thread → structured review |
| **Motivation service** | `src/api/Services/MotivationService.cs` | ✅ Delivery score, streak, XP, level |
| **Agent models** | `src/api/Models/Agents/` | ✅ AgentDraft, ApprovalDecision |
| **Approval route** | `POST /api/v1/approvals` | ✅ approve/edit/skip + telemetry |
| **Motivation route** | `GET /api/v1/users/{userId}/motivation` | ✅ Full state for psychology layer |
| **Psychology hooks** | `src/app/src/hooks/` | ✅ useDeliveryScore, useStreak, useCompetencyLevel, usePsychologyEvents |
| **Psychology components** | `src/app/src/components/psychology/` | ✅ 8 components: DeliveryScore, StreakBadge, CompetencyLevel, CelebrationLayer, MorningDigest, InsightCard, FocusMode, MotivationalNudge |
| **CascadeView** | `src/app/src/components/core/CascadeView.tsx` | ✅ Stagger reveal, at-risk highlights, replan panel |
| **ApprovalCard** | `src/app/src/components/core/ApprovalCard.tsx` | ✅ Approve/Edit/Skip, fires /api/v1/approvals |
| **Constitution** | `.specify/memory/constitution.md` | ✅ v1.4.0 — P-01 through P-31 |
| **Sentinel role** | `.specify/memory/agent-roles/sentinel.md` | ✅ P-31 — full verification protocol |
| **Sentinel log** | `.specify/memory/sentinel-log.md` | ✅ First run complete, 3 violations resolved |
| **Timing logs** | `NlpPipeline.cs`, `CascadeSimulator.cs`, `AdaptiveCardBuilder.cs` | ✅ Stopwatch on all 3 hot paths |
| **X-Elapsed-Ms** | `Program.cs` /graph/cascade route | ✅ Response header for browser/curl verification |
| **Dockerfile** | `src/api/Dockerfile` | ✅ .NET 10 multi-stage, non-root user |
| **Bicep infra** | `infra/main.bicep` + `infra/parameters.json` | ✅ Storage + ACR + Container Apps + Static Web Apps |
| **Teams manifest** | `appPackage/manifest.json` + color.png + outline.png | ✅ Ready to zip + upload to 7k2cc2 catalog |
| **CI/CD scaffold** | `.github/workflows/deploy.yml` | ✅ Scaffold ready (activate post-demo: remove if:false) |
| **Smoke tests** | `scripts/verify-demo.ts` | ✅ 6 checks covering all feature areas |
| **Demo readiness** | `.agents/commit-fhl/demo-readiness.md` | ✅ All 6 green |
| **Tenant setup** | `scripts/setup-tenant.ts` | ✅ Fully automated, idempotent (az login → creates 6 users, flushes, re-seeds) |
| **OID override seed** | `scripts/seed-real-users.ts` | ✅ Accepts REAL_OID_* env vars, remaps scenario to real identities |
| **Live arrival** | `scripts/demo-live-arrival.ts` | ✅ T-042: injects commitment with 60s countdown during demo |
| **Real user guide** | `docs/real-user-setup.md` | ✅ 3-command TL;DR, admin consent, Teams install, troubleshooting |

---

## Day 2 Dispatch Plan

```
SEQUENTIAL (extractors need types — already done):

PARALLEL BATCH (all independent — launch simultaneously):
  T-010  [Forge]   → transcriptExtractor.cs (Graph meeting transcripts)
  T-012  [Forge]   → chatExtractor.cs (Teams DMs/channels)
  T-013  [Forge]   → emailExtractor.cs (Outlook inbox)
  T-014  [Forge]   → adoExtractor.cs (ADO PR threads)

AFTER EXTRACTORS DONE (sequential dependency):
  T-011  [Forge]   → nlpPipeline.cs (Azure OpenAI GPT-4o) ← BLOCKED on D-003
  T-015  [Forge]   → deduplication engine
  T-016  [Forge]   → Eisenhower priority scorer
  T-018  [Canvas]  → Wire real data into CommitPane
  T-019  [Canvas]  → Impact score chip + source links
```

**Note**: T-011 requires `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_KEY` in `.env`. Add these before running T-011.

---

## Day Completion Log

| Day | Status | Key output | Committed |
|-----|--------|-----------|-----------|
| Mon D1 | ✅ Complete | C# API, auth, storage, webhooks, React shell | feat: Day1 |
| Tue D2 | ✅ Complete | 4 extractors, NLP pipeline, dedup, Eisenhower scorer, CommitPane wired | feat: Day2 |
| Wed D3 | ✅ Complete | Cascade engine, impact scorer, Viva Insights, risk detector, replan engine | feat: Day3 |
| Thu D4 | ✅ Complete | Execution agents + psychology layer + approval loop | feat: Day4 |
| Fri D5 | ✅ Agent tasks done (T-040 live demo pending) | Demo scripts, team labels, Sentinel, perf pass, smoke tests, T-041/T-042 | feat(demo): demo scripts + team labels |

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
