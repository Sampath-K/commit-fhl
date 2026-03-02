# Commit — Architecture & Implementation Plan

> **AS-BUILT NOTE (2026-03-01 — DA-005)**: The original plan specified TypeScript/Node.js.
> Human decision DA-005 changed the backend to **C# .NET 9 ASP.NET Core Minimal API**.
> The frontend remains TypeScript + React. All sections below that describe backend code
> refer to the TypeScript design intent — the actual implementation is in `src/api/` (C#).
> See "As-Built Divergences" section at the end for a complete mapping.

---

## Tech Stack

### Original Plan (TypeScript — superseded by DA-005 for backend)

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| Teams app framework | Teams Toolkit v5 (tab) | Official scaffold, handles auth, local dev tunnel |
| Language | TypeScript (Node.js 20 LTS) | Best Graph SDK support, fastest agent iteration |
| Auth | MSAL Node + On-Behalf-Of flow | Required for delegated Graph permissions |
| Graph API client | `@microsoft/microsoft-graph-client` v3 | Official SDK, typed, handles retry |
| AI / NLP | Azure OpenAI GPT-4o via `openai` SDK | Commitment extraction + draft generation |
| Storage | Azure Table Storage (`@azure/data-tables`) | Zero-schema, fast, cheap, good for graph edges |
| Notifications | Power Automate + Adaptive Cards | Low-code triggers for Teams Adaptive Card delivery |
| Testing | Jest (unit) + Playwright (E2E) | Standard, agent-friendly |
| Dev tunnel | Teams Toolkit built-in | Teams app local dev requirement |
| Source root | `src/commit/` (relative to repo root) | Isolated from Substrate C# sources |

### Actual Implementation (DA-005 — C# backend)

| Layer | Technology | Notes |
|-------|-----------|-------|
| **Backend language** | **C# (.NET 9)** | Human choice — DA-005 |
| **Backend framework** | **ASP.NET Core Minimal API** | `src/api/CommitApi.csproj` |
| **Backend auth** | **MSAL C# OBO** (`Microsoft.Identity.Web`) | `src/api/Auth/GraphClientFactory.cs` |
| **Graph client** | **Microsoft.Graph SDK** (C#) | `src/api/Auth/GraphClientFactory.cs` |
| **AI / NLP** | **Azure.AI.OpenAI** (C# SDK) | `src/api/Services/NlpPipeline.cs` |
| **Storage** | **Azure.Data.Tables** (C#) | `src/api/Repositories/CommitmentRepository.cs` |
| **Notifications** | **AdaptiveCardBuilder.cs** (direct, no Power Automate) | `src/api/Agents/AdaptiveCardBuilder.cs` |
| **Backend testing** | **xUnit** | `src/api/CommitApi.Tests/` — 66 tests |
| **Frontend language** | **TypeScript + React** | `src/app/` — unchanged |
| **Frontend testing** | **Jest + Playwright** | `src/app/` |
| **Source root** | `src/api/` (backend) + `src/app/` (frontend) | Not `src/commit/` |
| **Deployment** | Azure Container Apps (API) + Static Web Apps (frontend) | `infra/main.bicep` |

---

## Directory Structure

```
src/commit/
├── app/                        ← Teams tab React frontend
│   ├── src/
│   │   ├── components/
│   │   │   ├── CommitPane.tsx       ← main pane
│   │   │   ├── CommitItem.tsx       ← single task card
│   │   │   ├── CascadeView.tsx      ← dependency cascade
│   │   │   ├── LoadBar.tsx          ← capacity indicator
│   │   │   └── ApprovalCard.tsx     ← one-click approval UI
│   │   ├── hooks/
│   │   │   ├── useCommitments.ts    ← data fetching
│   │   │   └── useCascade.ts        ← cascade simulation
│   │   └── App.tsx
│   └── package.json
│
├── api/                        ← Node.js backend (Azure Functions or Express)
│   ├── src/
│   │   ├── auth/
│   │   │   ├── msalClient.ts        ← MSAL OBO token provider
│   │   │   └── graphClient.ts       ← typed Graph client factory
│   │   │
│   │   ├── extractors/              ← F1: Commitment Discovery
│   │   │   ├── transcriptExtractor.ts   ← Teams meeting transcripts
│   │   │   ├── chatExtractor.ts         ← Teams DM + channels
│   │   │   ├── emailExtractor.ts        ← Outlook inbox
│   │   │   ├── adoExtractor.ts          ← ADO PR threads
│   │   │   └── nlpPipeline.ts           ← Azure OpenAI NLP
│   │   │
│   │   ├── graph/                   ← F3: Dependency Graph
│   │   │   ├── commitmentStore.ts       ← Azure Table Storage CRUD
│   │   │   ├── dependencyLinker.ts      ← link related tasks
│   │   │   ├── cascadeSimulator.ts      ← impact propagation
│   │   │   └── impactScorer.ts          ← business impact score
│   │   │
│   │   ├── replan/                  ← F4: Replan Engine
│   │   │   ├── replanGenerator.ts       ← 3-option replan
│   │   │   └── commsdrafter.ts          ← per-recipient comms
│   │   │
│   │   ├── agents/                  ← F5: Execution Agents
│   │   │   ├── prReviewDrafter.ts
│   │   │   ├── statusUpdateDrafter.ts
│   │   │   ├── calendarBlocker.ts
│   │   │   └── overcommitFirewall.ts
│   │   │
│   │   ├── capacity/                ← F6: Wellbeing Layer
│   │   │   ├── vivaInsightsClient.ts
│   │   │   └── burnoutIndex.ts
│   │   │
│   │   ├── webhooks/                ← Real-time change notifications
│   │   │   ├── subscriptionManager.ts
│   │   │   └── webhookHandler.ts
│   │   │
│   │   └── routes/                  ← REST API surface
│   │       ├── commitments.ts
│   │       ├── cascade.ts
│   │       ├── approvals.ts
│   │       └── health.ts
│   │
│   └── package.json
│
├── tests/
│   ├── unit/
│   ├── integration/
│   └── e2e/
│
├── .env.example                ← required env vars (no secrets)
├── teams-manifest/             ← Teams app manifest
└── README.md
```

---

## Data Model

### CommitmentRecord (Azure Table Storage)

```typescript
interface CommitmentRecord {
  partitionKey: string;        // userId (OID from AAD)
  rowKey: string;              // commitmentId (UUID)
  title: string;               // normalized task title
  owner: string;               // userId
  watchers: string[];          // userIds watching this task
  source: CommitmentSource;    // { type, url, timestamp, rawText }
  committedAt: Date;           // when commitment was made
  dueAt?: Date;                // inferred or explicit ETA
  status: 'pending' | 'done' | 'deferred' | 'delegated';
  priority: EisenhowerQuadrant; // 'urgent-important' | 'schedule' | 'delegate' | 'defer'
  blockedBy: string[];         // commitmentIds that block this
  blocks: string[];            // commitmentIds this blocks
  impactScore: number;         // 0-100, computed by cascade engine
  burnoutContribution: number; // hours this adds to owner's load
  lastActivity?: Date;         // last signal of progress
  agentDraft?: AgentDraft;     // pending draft awaiting approval
}
```

### GraphEdge (dependency links)
```typescript
interface GraphEdge {
  fromId: string;    // commitment blocking
  toId: string;      // commitment blocked
  edgeType: 'hard' | 'soft' | 'date' | 'capacity';
  confidence: number; // 0-1, how certain the link is
}
```

---

## Key Graph API Calls

```typescript
// Meeting transcripts
GET /me/onlineMeetings?$filter=startDateTime gt {date}
GET /me/onlineMeetings/{meetingId}/transcripts
GET /me/onlineMeetings/{meetingId}/transcripts/{transcriptId}/content

// Teams chat
GET /me/chats?$expand=members&$filter=lastUpdatedDateTime gt {date}
GET /chats/{chatId}/messages?$filter=createdDateTime gt {date}
GET /teams/{teamId}/channels/{channelId}/messages?$filter=createdDateTime gt {date}

// Outlook
GET /me/messages?$filter=receivedDateTime gt {date} and isRead eq false
GET /me/messages?$filter=flag/flagStatus eq 'flagged'

// Calendar (for capacity)
GET /me/calendarView?startDateTime={start}&endDateTime={end}

// Viva Insights
GET /me/analytics/activityStatistics?$filter=startDateTime gt {date}

// Change notifications (webhooks)
POST /subscriptions  { changeType: 'created,updated', resource: '/me/chats/getAllMessages' }
```

---

## NLP Prompt Strategy

```
System: You extract work commitments from conversation text.
        A commitment is any statement where a person agrees to do something by some time.
        Extract: owner (who committed), task (what they will do), deadline (when, if stated),
        watchers (who they committed to or who was present).
        Return JSON array. If no commitments, return [].
        Confidence threshold: only include if >0.75 confidence.

User:   [raw transcript / message text]
        Speaker mapping: [name → userId]
```

---

## Cascade Simulation Algorithm

```
function simulateCascade(rootTaskId, slipDays):
  visited = {}
  queue = [{ taskId: rootTaskId, cumulativeSlip: slipDays }]

  while queue not empty:
    { taskId, cumulativeSlip } = queue.pop()
    if visited[taskId]: continue
    visited[taskId] = cumulativeSlip

    task = getTask(taskId)
    newEta = task.dueAt + cumulativeSlip days

    // Check owner calendar capacity for new eta
    ownerCapacity = getCalendarPressure(task.owner, newEta)
    if ownerCapacity > 0.8:
      cumulativeSlip += ownerCapacity * 1 day  // capacity pressure adds slip

    // Propagate to dependents
    for each blockedTask in task.blocks:
      if blockedTask.dueAt < newEta:
        additionalSlip = newEta - blockedTask.dueAt
        queue.push({ taskId: blockedTask.id, cumulativeSlip: additionalSlip })

  return { affectedTasks: visited, totalSlip, impactScore }
```

---

## Adaptive Card — One-Click Approval Pattern

Every agent draft surfaces as an Adaptive Card in Teams with:
- **Context strip**: source, who's affected, why it matters
- **Draft content**: the actual text/action the agent prepared
- **3 buttons**: `✅ Approve & Send` | `✏️ Edit First` | `❌ Skip`
- Approve → Power Automate flow executes the action
- Edit → Opens the draft in a Teams compose box
- Skip → Task marked as human-handled, no agent action

---

## Microsoft Graph Permissions Required

```
Delegated (user context — requires user consent):
- Chat.Read
- Chat.ReadWrite (for sending approved messages)
- ChannelMessage.Read.All
- Mail.Read
- Mail.Send (for approved email actions)
- Calendars.Read
- Calendars.ReadWrite (for calendar blocking agent)
- OnlineMeetings.Read
- Tasks.ReadWrite
- User.Read
- Analytics.Read (Viva Insights)
```

---

## As-Built Divergences (from original TypeScript plan → actual C# implementation)

> This section is the canonical reconciliation between the original TypeScript plan and the
> actual implementation. Added 2026-03-02. Maintained by Sentinel (P-31).

| Original Plan | As Built | Decision |
|---------------|----------|---------|
| `src/commit/` source root | `src/api/` (C# backend) + `src/app/` (frontend) | DA-005 |
| TypeScript Node.js backend | C# .NET 9 ASP.NET Core Minimal API | DA-005 |
| `api/src/types/index.ts` backend types | `src/api/Models/` C# records; `src/app/src/types/api.ts` frontend-only | DA-005 |
| `commitmentStore.ts` | `CommitmentRepository.cs` (Azure.Data.Tables C# SDK) | DA-005 |
| `nlpPipeline.ts` | `NlpPipeline.cs` (Azure.AI.OpenAI C# SDK) | DA-005 |
| `graphClient.ts` | `GraphClientFactory.cs` (Microsoft.Identity.Web + Microsoft.Graph C# SDK) | DA-005 |
| Power Automate for card delivery | `AdaptiveCardBuilder.cs` direct card construction | DA-005 |
| Jest for backend tests | xUnit in `src/api/CommitApi.Tests/` | DA-005 |
| No psychology layer in original plan | 8 psychology components + hooks (UX spec P-27) | DA-005 day 4 |
| No governance system | Sentinel P-31: 4-phase verification, sentinel-log.md | 2026-03-02 |
| No team labels in UI | `teams.config.ts` + CommitPane badges + CascadeView labels | 2026-03-02 |
| No demo scripts | `docs/demo-script-fhl.md` + `docs/demo-script-leadership.md` | D-007 |
| No real-user demo support | T-041 (seed-real-users.ts) + T-042 (demo-live-arrival.ts) | D-008 |
| Deployed to `src/commit/` folder | Deployed to Azure: Container Apps + Static Web Apps | infra/main.bicep |

### Actual Directory Structure (as built)

```
C:\Dev\commit-fhl\
├── src/
│   ├── api/                        ← C# .NET 9 ASP.NET Core Minimal API
│   │   ├── CommitApi.csproj
│   │   ├── Program.cs              ← all routes wired
│   │   ├── Auth/                   ← MSAL OBO + Graph client factory
│   │   ├── Agents/                 ← AdaptiveCardBuilder, CalendarBlocker, OvercommitFirewall,
│   │   │                              PrReviewDrafter, RiskDetector, StatusUpdateDrafter
│   │   ├── Capacity/               ← VivaInsightsClient
│   │   ├── Config/                 ← FeatureFlagService, PiiScrubber, AppInsightsExtensions
│   │   ├── Entities/               ← CommitmentEntity
│   │   ├── Exceptions/             ← CommitException hierarchy
│   │   ├── Extractors/             ← AdoExtractor, ChatExtractor, EmailExtractor, TranscriptExtractor
│   │   ├── Graph/                  ← CascadeSimulator, DependencyLinker, ImpactScorer
│   │   ├── Models/                 ← C# API contract models (Agents/, Capacity/, Graph/)
│   │   ├── Replan/                 ← ReplanGenerator
│   │   ├── Repositories/           ← CommitmentRepository (Azure Table Storage)
│   │   ├── Services/               ← DeduplicationService, EisenhowerScorer, MotivationService, NlpPipeline
│   │   ├── Webhooks/               ← SubscriptionManager, WebhookHandler
│   │   ├── Dockerfile
│   │   └── CommitApi.Tests/        ← xUnit; 66 tests
│   │
│   └── app/                        ← TypeScript + React (unchanged from original plan)
│       └── src/
│           ├── components/
│           │   ├── core/           ← CommitPane.tsx, CascadeView.tsx, ApprovalCard.tsx
│           │   └── psychology/     ← 8 psychology components
│           ├── config/
│           │   └── teams.config.ts ← TEAM_BY_USER + teamFromTaskId (demo team labels)
│           ├── hooks/              ← useDeliveryScore, useStreak, useCompetencyLevel, usePsychologyEvents
│           └── types/
│               └── api.ts          ← Frontend-only TypeScript contract types
│
├── scripts/                        ← TypeScript seed/demo scripts
│   ├── personas/index.ts           ← 6 demo personas with team field
│   ├── scenarios/                  ← 3 cascade chain skeletons
│   ├── seed-demo.ts                ← seeds 24 commitments to live API
│   ├── flush-demo.ts               ← clears demo data
│   ├── verify-demo.ts              ← smoke test (6 checks)
│   ├── seed-real-users.ts          ← real AAD OID mapping (T-041)
│   └── demo-live-arrival.ts        ← live commitment injection (T-042)
│
├── docs/
│   ├── demo-script-fhl.md          ← FHL judges: 4-min, 6 beats (D-007)
│   ├── demo-script-leadership.md   ← Leadership: 4-5 min, 3-team narrative (D-007)
│   ├── real-user-setup.md          ← Guide for mapping personas to real tenant users (T-041)
│   ├── Commit_Sprint_Dashboard.html
│   ├── Commit_Day1_Report.html
│   └── Commit_Build_Story.html
│
├── infra/                          ← Bicep IaC
│   ├── main.bicep
│   └── parameters.json
│
├── appPackage/                     ← Teams manifest
│   ├── manifest.json
│   └── *.png
│
├── .agents/commit-fhl/             ← Speckit governance files
│   ├── SESSION.md, tasks.md, plan.md, spec.md, decisions.md, AGENT_INSTRUCTIONS.md
│
└── .specify/memory/                ← Constitutional governance
    ├── constitution.md             ← v1.4.0 — P-01 through P-31
    ├── agent-roles/                ← 7 role cards (Forge, Canvas, Shield, Lens, Seed, Router, Sentinel)
    └── sentinel-log.md
```
