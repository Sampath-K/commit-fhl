# M365 Agent: Commit Tracker — Scheduled Task Extraction & Listing

## Overview

This document provides implementation instructions for creating a Microsoft 365 Agent (using the Teams AI Library / Bot Framework) that can be scheduled to extract tasks from the last N days and deliver them as rich Adaptive Cards in Teams.

**Key difference from the Teams tab app:**
The agent does not use change notification webhooks. Each scheduled run does a full look-back extraction over the last N days using the same Graph API signals. It produces rich output and can act on tasks (mark done, snooze, reply to stakeholders).

---

## Scope (First Cut)

This first cut covers **listing active tasks only**:
- Scheduled extraction from all 6 signal sources (Meetings, Chat, Email, ADO, Drive, Planner)
- Deduplicate and score extracted tasks
- Deliver a rich Adaptive Card summary to the user in a Teams 1:1 chat
- Respond to "show my tasks" / "what are my open commitments?" conversational prompts

---

## 1. M365 Agent Technology Stack

| Layer | Technology |
|-------|-----------|
| Agent runtime | Teams AI Library v2 (JavaScript/TypeScript) or Bot Framework SDK v4 (.NET) |
| Hosting | Azure Bot Service + Azure App Service / Azure Functions |
| Scheduling | Azure Logic Apps (HTTP trigger on a schedule) OR Teams Message Extension with background job |
| Auth | OAuth 2.0 SSO (OBO flow — same as the Teams tab) |
| Graph API | Microsoft Graph v1.0 (same endpoints as existing extractors) |
| Rich output | Adaptive Cards v1.5 |
| State | Azure Table Storage (re-use existing `commitments` table) |

---

## 2. Agent Registration

### 2.1 Azure Bot Registration

```json
// manifest.json (add to existing Teams app manifest or create new)
{
  "bots": [
    {
      "botId": "<BOT_APP_ID>",
      "scopes": ["personal"],
      "supportsFiles": false,
      "isNotificationOnly": false
    }
  ],
  "validDomains": ["<YOUR_BOT_DOMAIN>.azurewebsites.net"]
}
```

### 2.2 Required App Permissions (same OBO scopes as Teams tab)

```
Calendars.Read
Chat.Read
Mail.Read
Tasks.Read
Files.Read.All
offline_access
User.Read
```

Add for proactive messaging:
```
Chat.ReadWrite          (to open 1:1 chats)
ChatMessage.Send        (to send proactive messages)
```

---

## 3. Agent Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Azure Logic App (scheduled — every morning at 8am)     │
│  POST https://<bot-host>/api/scheduled-extraction        │
│    { userId: "<AAD-OID>", days: 1 }                     │
└────────────────────────┬────────────────────────────────┘
                         │ HTTP
┌────────────────────────▼────────────────────────────────┐
│  CommitBot (Azure Bot Service + App Service)             │
│                                                         │
│  /api/messages  ← Teams Bot Framework endpoint          │
│  /api/scheduled-extraction ← Logic App trigger          │
│                                                         │
│  ActivityHandler                                        │
│  ├── onMessage: natural language intent routing         │
│  │   ├── "show tasks" → ListTasksHandler               │
│  │   └── "extract now" → ExtractionHandler             │
│  └── ProactiveMessageSender                            │
│      └── Sends Adaptive Card to user's 1:1 chat        │
│                                                         │
│  ExtractionOrchestrator (re-used from CommitApi)        │
│  ├── All 6 extractors                                   │
│  ├── NLP pipeline                                       │
│  ├── Dedup + Eisenhower scorer                          │
│  └── CommitmentRepository (Azure Table Storage)         │
└─────────────────────────────────────────────────────────┘
```

---

## 4. Core Implementation

### 4.1 Project Setup (TypeScript)

```bash
# Use Teams Toolkit to scaffold
npm install -g @microsoft/teams-ai
npx @microsoft/teams-toolkit create --template bot-sso
```

Or add to existing repo:
```bash
cd C:/Dev/commit-fhl
mkdir src/agent
cd src/agent
npm init -y
npm install @microsoft/teams-ai botbuilder @azure/identity @microsoft/microsoft-graph-client
```

### 4.2 Bot Entry Point (`src/agent/src/index.ts`)

```typescript
import { BotFrameworkAdapter, TurnContext } from 'botbuilder';
import { CommitBot } from './CommitBot';
import express from 'express';

const adapter = new BotFrameworkAdapter({
  appId: process.env.BOT_APP_ID,
  appPassword: process.env.BOT_APP_PASSWORD,
});

const bot = new CommitBot();
const app = express();

// Teams Bot Framework messages endpoint
app.post('/api/messages', (req, res) => {
  adapter.processActivity(req, res, async (context) => {
    await bot.run(context);
  });
});

// Scheduled extraction trigger (called by Logic App)
app.post('/api/scheduled-extraction', async (req, res) => {
  const { userId, days = 1 } = req.body;
  if (!userId) { res.status(400).json({ error: 'userId required' }); return; }

  try {
    await bot.runScheduledExtraction(userId, days);
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: String(err) });
  }
});

app.listen(3978, () => console.log('CommitBot running on :3978'));
```

### 4.3 Bot Class (`src/agent/src/CommitBot.ts`)

```typescript
import { ActivityHandler, BotState, ConversationState, TurnContext, CardFactory } from 'botbuilder';
import { buildTaskListCard } from './cards/TaskListCard';
import { extractCommitments } from './services/ExtractionClient';
import { getStoredToken } from './services/TokenStore';

export class CommitBot extends ActivityHandler {
  constructor() {
    super();

    // Handle conversational messages
    this.onMessage(async (context: TurnContext, next) => {
      const text = context.activity.text?.toLowerCase().trim() ?? '';

      if (text.includes('show') || text.includes('tasks') || text.includes('commitments')) {
        await this.handleShowTasks(context);
      } else if (text.includes('extract') || text.includes('scan') || text.includes('refresh')) {
        await this.handleExtractNow(context);
      } else {
        await context.sendActivity(
          "I can help you track your commitments! Try:\n" +
          "• **show my tasks** — list your active commitments\n" +
          "• **extract now** — scan the last 7 days for new tasks"
        );
      }

      await next();
    });

    // Welcome message on first install
    this.onMembersAdded(async (context: TurnContext, next) => {
      for (const member of context.activity.membersAdded ?? []) {
        if (member.id !== context.activity.recipient.id) {
          await context.sendActivity(
            "👋 Hi! I'm **Commit Tracker**. I help you stay on top of commitments from your meetings, chats, and emails.\n\n" +
            "Say **show my tasks** to see your active commitments, or **extract now** to scan for new ones."
          );
        }
      }
      await next();
    });
  }

  /** Called by the scheduled Logic App trigger — extracts and proactively notifies the user. */
  async runScheduledExtraction(userId: string, days: number): Promise<void> {
    const token = await getStoredToken(userId);
    if (!token) {
      console.warn(`No stored token for user ${userId} — skipping scheduled extraction`);
      return;
    }

    // Run extraction via the Commit API backend
    const commitments = await extractCommitments(userId, token, days);

    if (commitments.length === 0) return; // Nothing new — no message

    // Build and send proactive Adaptive Card
    const card = buildTaskListCard(commitments, { days, isScheduled: true });
    await this.sendProactiveMessage(userId, card);
  }

  private async handleShowTasks(context: TurnContext): Promise<void> {
    const userId = context.activity.from.aadObjectId;
    if (!userId) {
      await context.sendActivity("Please sign in to see your tasks.");
      return;
    }

    // Fetch from Commit API
    const response = await fetch(`${process.env.COMMIT_API_URL}/api/v1/commitments/${userId}`, {
      headers: { 'Authorization': `Bearer ${await getStoredToken(userId)}` }
    });
    const data = await response.json();
    const commitments = data.data ?? [];
    const active = commitments.filter((c: any) => c.status === 'pending');

    if (active.length === 0) {
      await context.sendActivity("✅ You have no active commitments! Great work.");
      return;
    }

    const card = buildTaskListCard(active, { days: 7, isScheduled: false });
    await context.sendActivity({ attachments: [CardFactory.adaptiveCard(card)] });
  }

  private async handleExtractNow(context: TurnContext): Promise<void> {
    const userId = context.activity.from.aadObjectId;
    await context.sendActivity("🔄 Scanning your last 7 days... this takes about 10-15 seconds.");
    // Call extract endpoint — omitted for brevity, same as handleShowTasks but POST /extract first
    await this.handleShowTasks(context);
  }

  private async sendProactiveMessage(userId: string, card: object): Promise<void> {
    // Implementation: store conversation reference on bot install (onInstallationUpdate)
    // then use adapter.continueConversationAsync to send proactively
    // See: https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-howto-proactive-message
    console.log(`Proactive message queued for user ${userId}`);
  }
}
```

---

## 5. Adaptive Card: Task List (`TaskListCard.ts`)

This is the core rich output format.

```typescript
// src/agent/src/cards/TaskListCard.ts

interface Commitment {
  rowKey: string;
  title: string;
  sourceType: string;
  priority: string;
  dueAt?: string;
  status: string;
  projectContext?: string;
}

interface CardOptions {
  days: number;
  isScheduled: boolean;
}

export function buildTaskListCard(commitments: Commitment[], opts: CardOptions): object {
  const urgent   = commitments.filter(c => c.priority === 'urgent-important');
  const schedule = commitments.filter(c => c.priority === 'schedule');
  const other    = commitments.filter(c => !['urgent-important', 'schedule'].includes(c.priority));

  const priorityEmoji = (p: string) => ({
    'urgent-important': '🔴',
    'schedule':         '🟡',
    'delegate':         '🔵',
    'defer':            '⚪',
  }[p] ?? '⚪');

  const sourceEmoji = (s: string) => ({
    'Transcript': '🎙️',
    'Chat':       '💬',
    'Email':      '📧',
    'Ado':        '🔧',
    'Drive':      '📄',
    'Planner':    '✅',
  }[s] ?? '📌');

  const taskRow = (c: Commitment) => ({
    type: 'ColumnSet',
    columns: [
      {
        type: 'Column',
        width: 'auto',
        items: [{ type: 'TextBlock', text: priorityEmoji(c.priority), wrap: false }]
      },
      {
        type: 'Column',
        width: 'stretch',
        items: [
          {
            type: 'TextBlock',
            text: `${sourceEmoji(c.sourceType)} **${c.title}**`,
            wrap: true,
            size: 'Small'
          },
          ...(c.dueAt ? [{
            type: 'TextBlock',
            text: `Due ${new Date(c.dueAt).toLocaleDateString()}`,
            size: 'ExtraSmall',
            color: new Date(c.dueAt) < new Date() ? 'Attention' : 'Default',
            spacing: 'None'
          }] : []),
          ...(c.projectContext ? [{
            type: 'TextBlock',
            text: c.projectContext,
            size: 'ExtraSmall',
            color: 'Accent',
            spacing: 'None'
          }] : [])
        ]
      }
    ],
    spacing: 'Small'
  });

  const sectionBlock = (title: string, items: Commitment[]) => items.length === 0 ? [] : [
    { type: 'TextBlock', text: `**${title}** (${items.length})`, size: 'Small', weight: 'Bolder', spacing: 'Medium' },
    ...items.slice(0, 5).map(taskRow),
    ...(items.length > 5 ? [{
      type: 'TextBlock',
      text: `+${items.length - 5} more...`,
      size: 'ExtraSmall',
      color: 'Accent'
    }] : [])
  ];

  return {
    type: 'AdaptiveCard',
    version: '1.5',
    body: [
      {
        type: 'Container',
        style: 'emphasis',
        bleed: true,
        items: [
          {
            type: 'ColumnSet',
            columns: [
              {
                type: 'Column',
                width: 'stretch',
                items: [
                  {
                    type: 'TextBlock',
                    text: opts.isScheduled
                      ? `📋 Your Daily Commitment Summary`
                      : `📋 Active Commitments`,
                    weight: 'Bolder',
                    size: 'Medium'
                  },
                  {
                    type: 'TextBlock',
                    text: `${commitments.length} active · Last ${opts.days}d · ${new Date().toLocaleDateString()}`,
                    size: 'Small',
                    color: 'Accent',
                    spacing: 'None'
                  }
                ]
              }
            ]
          }
        ]
      },
      ...sectionBlock('🔴 Do First (Urgent & Important)', urgent),
      ...sectionBlock('🟡 Schedule (Important)', schedule),
      ...sectionBlock('Other', other),
    ],
    actions: [
      {
        type: 'Action.Submit',
        title: '🔄 Refresh',
        data: { action: 'extract' }
      },
      {
        type: 'Action.OpenUrl',
        title: '📊 Open Full View',
        url: process.env.TEAMS_TAB_URL ?? 'https://teams.microsoft.com'
      }
    ]
  };
}
```

---

## 6. Scheduling with Azure Logic Apps

### Logic App Definition (JSON)

```json
{
  "definition": {
    "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
    "triggers": {
      "Daily_8am": {
        "type": "Recurrence",
        "recurrence": {
          "frequency": "Day",
          "interval": 1,
          "schedule": { "hours": ["8"], "minutes": [0] },
          "timeZone": "UTC"
        }
      }
    },
    "actions": {
      "For_each_user": {
        "type": "Foreach",
        "foreach": "@variables('userIds')",
        "actions": {
          "Trigger_Extraction": {
            "type": "Http",
            "inputs": {
              "method": "POST",
              "uri": "https://<bot-host>/api/scheduled-extraction",
              "headers": {
                "Content-Type": "application/json",
                "X-Bot-Secret": "@variables('botSecret')"
              },
              "body": {
                "userId": "@item()",
                "days": 1
              }
            }
          }
        }
      }
    },
    "variables": {
      "userIds": {
        "type": "array",
        "value": []
      }
    }
  }
}
```

**User list management:** On bot install (`onInstallationUpdate`), register the user's AAD OID in an Azure Table `registered_users`. The Logic App queries this table before running.

---

## 7. Environment Variables

```bash
# Azure Bot
BOT_APP_ID=<bot-app-registration-client-id>
BOT_APP_PASSWORD=<bot-app-client-secret>

# Commit API backend (re-use existing)
COMMIT_API_URL=https://<your-commit-api>.azurewebsites.net

# Teams Tab URL (for deep links in cards)
TEAMS_TAB_URL=https://teams.microsoft.com/l/entity/<app-id>/<tab-id>

# Bot secret (validates Logic App calls)
BOT_SECRET=<generate-random-secret>

# Same Azure Table Storage as CommitApi
AZURE_STORAGE_CONN=<storage-connection-string>
```

---

## 8. Teams App Manifest Updates

Add the bot to the existing `manifest.json` in `src/app/`:

```json
{
  "bots": [
    {
      "botId": "${BOT_APP_ID}",
      "scopes": ["personal"],
      "commandLists": [
        {
          "scopes": ["personal"],
          "commands": [
            { "title": "show tasks",    "description": "List your active commitments" },
            { "title": "extract now",   "description": "Scan last 7 days for new tasks" },
            { "title": "help",          "description": "Show available commands" }
          ]
        }
      ]
    }
  ]
}
```

---

## 9. First Cut Implementation Checklist

- [ ] Create `src/agent/` directory with `package.json`, `tsconfig.json`
- [ ] Implement `CommitBot.ts` with `onMessage` handler and `handleShowTasks`
- [ ] Implement `TaskListCard.ts` Adaptive Card builder
- [ ] Add `ExtractionClient.ts` — calls `POST /api/v1/extract` then `GET /api/v1/commitments/{userId}`
- [ ] Add `TokenStore.ts` — reads stored OBO tokens from Azure Table Storage (same `tokenCache` table or new `botTokens` table)
- [ ] Register bot in Azure Portal and update `manifest.json`
- [ ] Deploy to Azure App Service (separate from CommitApi or co-hosted)
- [ ] Create Logic App for daily 8am trigger
- [ ] Test proactive message delivery end-to-end

---

## 10. Future Iterations

| Iteration | Feature |
|-----------|---------|
| v1.1 | Mark task done from Adaptive Card Action.Submit |
| v1.2 | Snooze task (add dueAt override) |
| v1.3 | Reply to stakeholder draft from card |
| v1.4 | Per-user configurable schedule (daily/weekly/on-demand) |
| v1.5 | Multi-user team rollup view (manager sees team's commitments) |
| v2.0 | Agentic loop: bot proactively surfaces at-risk tasks and suggests replans |
