# Demo Script — FHL Judges
**Audience:** FHL competition judges — technical, seen many demos. Value concision and insight.
**Duration:** 4 minutes. No hand-holding. No jargon definitions.

---

## Setup (before screen share)

- CommitPane loaded, Alex's board visible (9 commitments, 2 red-flag cards in the urgent-important quadrant)
- Cascade API warm — confirm sub-2s response before session

---

## Opening Hook (0:00–0:15) — screen dark or on CommitPane

> "Every team runs on commitments between people. Most of those commitments live in chat threads,
> meeting notes, and people's heads. Commit makes them visible — and simulates what happens
> when one slips."

---

## Beat 1: Alex's board — 9 commitments, 2 red flags (0:15–0:45)

**Screen:** CommitPane, all cards loaded, two cards with red impact badges in the top quadrant.

> "This is Alex's commitment pane. 9 active commitments. Two are in the danger zone — impact
> score above 40, status still pending. The yellow one is the SEVAL gate. The other is Foundry
> accuracy baseline. Both are on the critical path."

**Point out:** Green "Scheduling Skill" pills on Sarah's cards — cross-team ownership is visible
without a legend. Purple "BizChat Platform" pill on Marcus's card.

> "The colored pills tell you which team owns what — without looking at a spreadsheet."

---

## Beat 2: Click SEVAL card → cascade reveals, 7 tasks, Q1 at risk (0:45–1:30)

**Action:** Click the SEVAL feedback integration card.

**Screen:** CascadeView animates in, stagger trail reveals chain items with team labels.

> "Seven tasks affected. Three owners. Four weeks of risk — surfaced in under 2 seconds."

**Point at the chain:** Reschedule Crew items in blue. BizChat Platform item in purple — visually
distinct as it staggers in.

> "Watch the purple item appear. That's Marcus's team at BizChat Platform — cross-org. If that
> slips, we miss the plugin slot. That's Q1 gone."

**Show the impact score badge:** e.g., 78/100

> "Impact score 78. Anything above 70 triggers an automatic replan prompt."

---

## Beat 3: Replan options A/B/C, confidence scores, required actions (1:30–2:00)

**Action:** Click "View Replan Options".

**Screen:** Three options appear — A, B, C with confidence % badges.

> "Option A: accelerate testing — 72% confidence, adds 20 hours. Option B: parallel-path with
> Foundry — 79%. Option C: accept the slip, auto-draft comms to 6 watchers. 88% confidence."

> "Option C is often the right answer. The system knows the slip is real. The question is
> whether you tell people before or after they find out themselves."

---

## Beat 4: Approve — agent drafts 3 stakeholder messages, one click (2:00–2:30)

**Action:** Click Option C → approval card appears.

**Screen:** ApprovalCard with pre-drafted message to Marcus.

> "Alex reviews the message Commit wrote for Marcus. Cross-org notification without a call.
> Marcus gets the slip notice, the reason, and the new ETA — in one message, before he's
> blocked."

**Action:** Click Approve.

> "One click. Three messages queued. The system tracked that Alex approved — not ignored."

---

## Beat 5: Click Foundry card → second cascade, cross-org impact (2:30–3:15)

**Action:** Navigate back → click the Foundry accuracy card.

**Screen:** Second cascade chain loads. Different set of tasks, same BizChat Platform dependency visible.

> "Second risk. Different card. Same convergence point — Code Complete, March 17. One chokepoint.
> Two independent paths that both run through Marcus's team."

> "This is what the system knows that humans miss: when two independent slips have the same
> downstream victim. Alex didn't know this. The dependency graph did."

**Show the two purple BizChat Platform badges side by side in the chain.**

---

## Beat 6: Closing (3:15–3:45)

> "Commit doesn't manage tasks. It manages the commitments between people. The moment a
> commitment is at risk, the right people know — with enough time to act."

> "The system isn't asking Alex to do more. It's asking Alex to approve one message.
> That's the whole interface."

---

## Q&A Buffer (3:45–4:00)

Likely questions to prep for:
- **"How does it extract commitments?"** — NLP from meeting transcripts + chat; Teams context API
- **"What's the backend?"** — Azure Container Apps, Cosmos DB, Graph API for calendar pressure
- **"What if the confidence is wrong?"** — Human approves every action; system never sends autonomously

---

## If Demo Breaks

> "The cascade API is running in Azure Container Apps in East US — normally sub-2 seconds.
> While it loads: here's what we're about to see..."

[Narrate verbally: 7 tasks affected, 3 owners, purple BizChat Platform cross-org item, impact 78]
