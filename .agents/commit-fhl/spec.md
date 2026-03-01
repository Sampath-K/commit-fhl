# Commit — Product Specification
> Stable. Changes require human approval and an entry in decisions.md.

---

## Problem Statement

Knowledge workers make dozens of commitments per week in meetings, chat, email, and documents.
Nearly none are captured in any system. Cascading failures from missed commitments cause burnout,
missed deadlines, and constant coordination overhead. The signals to fix this already exist in M365
— they just haven't been connected.

---

## Solution

**Commit** is a Teams pane (and Outlook add-in) that:

1. **Captures** every commitment automatically from all M365 surfaces
2. **Graphs** them into a live dependency map with owners, ETAs, and watchers
3. **Simulates** the cascade impact when any task is at risk — before it slips
4. **Agents** draft replans, stakeholder communications, and actions
5. **Humans** approve with one click; agents execute

---

## User Personas

| Persona | Need |
|---------|------|
| **Engineer (Assignee)** | See what I owe, get help doing it, avoid overcommit |
| **Lead/Manager (Tracker)** | See team delivery risk without pinging anyone |
| **Stakeholder** | Get proactive updates without asking |
| **Anyone blocked** | See why and get an escalation path |

---

## Core Features (FHL Scope)

### F1 — Commitment Discovery (Day 2)
Extract commitments from:
- Teams meeting transcripts (`/onlineMeetings/{id}/transcripts`)
- Teams DM and channel messages (`/chats`, `/channels/messages`)
- Outlook email threads (`/me/messages`)
- ADO PR unresolved thread assignments

**Output per commitment**: title, owner, watcher(s), source link, timestamp, inferred ETA

### F2 — Commitment Pane in Teams (Day 2)
A Teams tab showing the authenticated user's commitments, categorized:
- Urgent + Important (act today)
- Schedule (block time)
- Delegate (route to someone else)
- Waiting On (blocked by someone else)

### F3 — Dependency Graph + Impact Simulation (Day 3)
- Link related commitments by owner, keywords, conversation context
- When any task shows risk signals → simulate full cascade
- Compute impact score (people affected, date risk, business visibility)
- Surface impact to the owner BEFORE the downstream task is blocked

### F4 — Replan Engine (Day 3)
Given a cascade, generate 3 replan options ranked by:
- Option A: most effort, safest outcome
- Option B: balanced risk
- Option C: slip accepted, all comms handled automatically

### F5 — Execution Agents with One-Click Approval (Day 4)
Agents draft (humans approve with one click via Adaptive Card):
- PR review comment summary
- Stakeholder status update (Teams channel post)
- Replan comms (individualized per recipient)
- Calendar block for focused work time
- Overcommit firewall warning (intercepts before Teams message sends)

### F6 — Capacity and Wellbeing Layer (Day 4)
- Integrate Viva Insights capacity signals
- Compute per-person load index (% of available hours committed)
- Burnout index trend (increasing → flag)
- Overcommit detected before commitment is verbalized

---

## Out of Scope for FHL

- SharePoint document comment mining (complexity; add in Phase 2)
- Loop components (API in preview; add in Phase 2)
- Manager escalation flows (need HR policy review)
- Multi-tenant deployment (FHL demo is single tenant)
- Mobile (Teams mobile pane is auto; no extra work needed)

---

## Success Metrics

> Defined by human on Day 1. Recorded in decisions.md.
> Measured at Friday 4PM demo.

See `decisions.md` → "D-001: Success Metrics"

---

## Non-Functional Requirements

| Requirement | Target |
|-------------|--------|
| Transcript → task latency | < 5 minutes from meeting end |
| Approval friction | 1 click maximum |
| NLP extraction precision | > 85% on labeled test set |
| Cascade simulation latency | < 10 seconds |
| Uptime during demo | 99.9% (single tenant, dev scale) |
| Privacy | Only process data of authenticated user; no cross-user reads |
