# Commit FHL — Decision Log
> Decisions made here are final and must not be re-litigated by agents.
> Pending decisions block specific tasks — agents skip those tasks and work on others.

---

## Format

```
### D-NNN: Decision Title
**Status**: Pending / Made
**Decided by**: Human / Agent / Both
**Date**: YYYY-MM-DD
**Blocks tasks**: T-NNN, T-NNN
**Decision**: [the actual decision]
**Rationale**: [why]
```

---

## Decisions Log (All — includes Made and Pending)

### D-001: Friday Demo Success Metrics
**Status**: ✅ Made
**Decided by**: Human
**Date**: 2026-03-01
**Decision**: 4 metrics — all must be green at Friday 4PM demo:
1. **Extraction precision > 85%** — NLP correctly identifies commitments from real meeting transcripts
2. **End-to-end latency < 5 min** — meeting ends → commitment in Teams pane in under 5 minutes, shown live
3. **Cascade detection 100%** — every at-risk task surfaces downstream impact before anyone is blocked
4. **1-click approval** — every agent-drafted action (status update, calendar block, PR comment) completable in one click
**Rationale**: Human chose all 4. Together they prove the full pipeline: extraction → display → risk → action. Any one failing is a demo failure.

---

### D-002: Tech Stack Confirmation
**Status**: ✅ Made
**Decided by**: Human
**Date**: 2026-03-01
**Decision**:
- TypeScript + Node.js 22 (installed)
- Teams Toolkit CLI v5 (installed)
- **Azure OpenAI** — endpoint: commit-fhl.openai.azure.com, deployment: `gpt-5-chat`
- Azure Table Storage via **Azurite** local emulator (no Azure subscription needed for dev)
- Jest + Playwright for tests
**Rationale**: Human explicitly chose Azure OpenAI over OpenAI API. Azurite for local dev keeps storage free and offline. Node 22 already installed on dev machine.

---

### D-003: Azure Credentials and App Registration
**Status**: ✅ Done
**Decided by**: Human
**Date**: 2026-03-01 (setup-env.sh); completed 2026-03-03 (OpenAI keys confirmed)
**Blocks tasks**: T-005, T-007, T-008 — all unblocked

**All credentials configured in src/api/.env and live on Container App:**
```
TENANT_ID=91b9767c-6b0a-4b0b-bd4d-e08a6383426c        ✅
CLIENT_ID=07b0afff-85b6-4be1-98ba-d26d566bd14a        ✅
CLIENT_SECRET=<set>                                    ✅
AZURE_STORAGE_CONN=(Azurite local / Azure prod)        ✅
AZURE_OPENAI_ENDPOINT=https://agentteams.openai.azure.com/  ✅ (reused from Commit-FHL)
AZURE_OPENAI_KEY=<set>                                 ✅
AZURE_OPENAI_DEPLOYMENT=OpenAICreate-20260221152626    ✅
```

---

### D-004: NLP Precision/Recall Threshold
**Status**: ⏳ Pending — demo proceeded without explicit threshold change
**Decided by**: Human (deferred)
**Blocks tasks**: None (default 0.75 in use; demo uses seeded data, not live extraction)
**Question**: After reviewing 10 real extraction examples, should threshold change? Deferred to post-demo pilot.
**Default**: 0.75 in production; demo scenario uses seed data

---

### D-005: Cascade Impact Score Weights
**Status**: ⏳ Pending — default weights validated sufficient for demo
**Decided by**: Human (deferred)
**Blocks tasks**: None (defaults used in demo: people×10, cal hrs×5, exec vis×20, days×-2)
**Demo validation**: Score 78 on SEVAL card confirms reasonable range. Post-demo tuning deferred to pilot.

---

### D-006: Agent Communication Tone and Templates
**Status**: ⏳ Pending — default drafts used in demo
**Decided by**: Human (deferred)
**Blocks tasks**: None (default templates working in demo scenario)
**Question**: Post-demo: review real-user drafts and approve standard templates for pilot rollout.

---

### D-007: Demo Script and Scenario
**Status**: ✅ Made
**Decided by**: Human + Agent
**Date**: 2026-03-02
**Decision**: Two demo scripts written:
- `docs/demo-script-fhl.md` — FHL judges: 4-min punchy script, 6 beats, technical talking points, Q&A prep
- `docs/demo-script-leadership.md` — Leadership/mixed: 4-5 min, plain-language 3-team narrative, emotional arc
**Scenario**: Reschedule Crew (Alex lead) builds skill; Scheduling Skill (Sarah) already delivered; BizChat Platform (Marcus) owns Q1 plugin slot. SEVAL + Foundry both cascade to Marcus's team. Two-cascade story with cross-org risk visible as purple pills.
**Blocks tasks**: T-036 ✅ unblocked and complete (24 commitments seeded, 3 cascade chains)
**UI changes**: Team labels (blue/green/purple) visible in CommitPane cards + CascadeView chain items

---

### D-008: Real-User Demo Tenant Setup
**Status**: ⏳ Pending — human action required (tenant sign-in)
**Decided by**: Human (2026-03-02)
**Question**: For the demo to show real user identities and live commitment arrivals, we need:
1. Real AAD user OIDs from the 7k2cc2 tenant (to replace demo-* OIDs in seed data)
2. Admin consent in 7k2cc2 for the Graph API scopes (already configured per D-003)
3. Actual meeting/chat/email activity in the tenant that extractors can pick up
**Proposed approach**:
- Map the 6 demo personas to real 7k2cc2 tenant users (or use actual team members)
- Re-seed with real OIDs so Graph profile calls return real names + photos
- Optionally: run a live extraction during the demo to show a new commitment arriving
**Blocks tasks**: T-041 (new task — real user OIDs seed update), T-042 (live commitment arrival demo)
**Human action needed**: Sign into 7k2cc2 tenant + provide real user OIDs or confirm persona-to-person mapping

---

## Decisions Made

| Decision | Date | Summary |
|----------|------|---------|
| D-001 | 2026-03-01 | 4 demo success metrics: extraction >85%, latency <5min, cascade 100%, 1-click approval |
| D-002 | 2026-03-01 | Tech stack: TypeScript frontend + Azure OpenAI (superseded for backend by DA-005) |
| D-003 | 2026-03-03 | All Azure creds + OpenAI keys confirmed (reused Commit-FHL keys, set on Container App) |
| D-007 | 2026-03-02 | Two demo scripts written: FHL judges (4 min) + Leadership (4-5 min, 3-team narrative) |
| DA-001 | 2026-03-01 | Azure Table Storage over SQL |
| DA-002 | 2026-03-01 | Polling + webhooks hybrid |
| DA-003 | 2026-03-01 | Teams tab (not message extension) |
| DA-004 | 2026-03-01 | Adaptive Cards for approval (not custom UX) |
| DA-005 | 2026-03-01 | C# ASP.NET Core Minimal API backend (supersedes D-002 Node.js) |

---

## Architectural Decisions (Agent-Made, Human-Ratified)

These were made during the planning session and are locked unless human overrides:

### DA-001: Azure Table Storage over SQL
**Date**: 2026-03-01
**Decision**: Use Azure Table Storage, not Azure SQL or Cosmos DB
**Rationale**: Zero schema migration overhead during rapid FHL iteration. Partitioned by userId. Good enough for single-tenant demo scale. Can migrate to Cosmos post-FHL.

### DA-002: Polling + Webhooks hybrid
**Date**: 2026-03-01
**Decision**: Use Graph change notifications (webhooks) for real-time, supplemented by 15-min polling for risk detection
**Rationale**: Webhooks give < 30s latency for new commitments. Polling handles the "no activity detected" risk signal which webhooks cannot express.

### DA-003: One Teams tab, not a message extension
**Date**: 2026-03-01
**Decision**: Build as a Teams tab (sidebar pane), not a message extension or bot
**Rationale**: Tab gives persistent surface, not ephemeral. Users expect it to always be there. Bot interactions are too interruptive. Message extension is too narrow.

### DA-004: Adaptive Cards for approval, not custom UX
**Date**: 2026-03-01
**Decision**: Use Adaptive Cards for all agent approval interactions
**Rationale**: Cards work in Teams, Outlook, and Teams mobile. Zero additional UX code. Teams Toolkit has native support. Keeps approval experience in the message stream, not a new tab.

### DA-005: C# ASP.NET Core Minimal API for backend (supersedes D-002 Node.js)
**Date**: 2026-03-01
**Decided by**: Human
**Decision**:
- **Backend language**: C# (.NET 9)
- **Backend framework**: ASP.NET Core Minimal API
- **Backend test framework**: xUnit
- **NuGet packages**: Microsoft.Graph, Azure.Data.Tables, Azure.Identity, Azure.AI.OpenAI, Azure.Extensions.AspNetCore.Configuration.Secrets, Azure.Data.AppConfiguration, Microsoft.ApplicationInsights.AspNetCore, Microsoft.Identity.Web, Swashbuckle.AspNetCore
- **Frontend stays unchanged**: TypeScript + React (as in D-002)
- **TypeScript types**: Moved to `src/app/src/types/api.ts` — frontend-only API contract types
- **C# models**: Live in `src/api/Models/` — mirror shape of TypeScript types
**Rationale**: Human explicitly chose C# over Node.js. C# is Microsoft-native, better type safety at scale, Production-grade Graph/Azure SDK support, and the team has C# expertise. ASP.NET Core Minimal API gives clean, low-ceremony route handlers. xUnit is the standard .NET test framework.
**Impact**: All previously created TypeScript backend files (commitmentStore.ts, FeatureFlagService.ts, AppInsightsClient.ts, PiiScrubber.ts) were deleted and reimplemented in C#. The Node.js package.json and tsconfig.json for api/ were removed.
