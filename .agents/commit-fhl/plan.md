# Commit — Architecture & Implementation Plan

---

## Tech Stack

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
