# Demo Script — Leadership / Mixed Audience
**Audience:** VPs, PMs, TPMs, non-engineers. May not know Foundry or SEVAL.
**Duration:** 4–5 minutes with deliberate pauses for audience reaction.

---

## Setup (before screen share)

- CommitPane loaded, Alex's board visible
- Cascade API warm — confirm sub-2s response before session
- Pause after the three-team setup — let the framing land before touching the screen

---

## Opening (0:00–0:30) — before touching screen

> "I want to show you something that happens on every shipping team, every quarter. A commitment
> slips. The person who made it knows. The people downstream don't — yet. The question is:
> how long does it take for the right people to find out, and does anyone have enough time
> to act when they do?"

> "Commit is a Teams pane that closes that gap. I'm going to show you three minutes of a story
> you'll recognize."

---

## Three-Team Setup (0:30–1:00) — say before touching screen

> "Alex's team is building the skill — they're in the middle of the story. Sarah's team at
> the Scheduling Skill group already delivered their piece — that's done, and we're grateful.
> Marcus's team at BizChat Platform owns the slot this skill needs to ship in Q1. If Marcus's
> team has to move to Q2, we miss Q1. That's the real pressure in this story."

*Pause. Let the framing settle.*

---

## Beat 1: Alex's board (1:00–1:30)

**Screen:** CommitPane, 9 cards, two high-impact cards at the top.

> "This is Alex's pane. Nine active commitments. Two are flagged — the yellow ones at the top."

**Point at team pills:**

> "The colored labels tell you who owns what. Blue is Alex's team — Reschedule Crew.
> Green is Sarah's team — Scheduling Skill. Purple is Marcus's team — BizChat Platform.
> No spreadsheet, no status meeting. The team graph is right here."

**Emotional beat: Recognition** — "This is legible. I can see my team's situation."

---

## Beat 2: Click SEVAL card → cascade (1:30–2:15)

**Action:** Click the SEVAL card.

> "SEVAL is the responsible AI review — a required quality gate before shipping anything in
> Copilot. Alex missed a feedback integration milestone. Let's see what that actually means."

**Screen:** CascadeView loads, chain items stagger in with team labels.

> "Watch this. The system is simulating: if this one task slips, what else moves?"

**Point at the chain animating in:**

> "Seven tasks. Think of this like a delayed flight — one slip at the gate causes missed
> connections downstream. Each item here is another person's week."

**Point at the purple BizChat Platform item:**

> "That purple item — Marcus's team. Cross-org. If Commit hadn't surfaced this, Alex would
> have found out when Marcus's team missed the dependency. Not today."

**Emotional beat: Tension** — "Watch the chain — each item is another person's week."

---

## Beat 3: Replan options (2:15–2:45)

**Action:** Click "View Replan Options".

> "Commit gives Alex three options. Not two. Not one."

**Read the options aloud:**

> "Option A: accelerate the testing schedule — adds effort, keeps the date. Option B:
> run the Foundry accuracy tests in parallel — split the risk. Option C: accept the slip
> and communicate proactively."

> "Each option has a confidence score. Option C is 88%. The system is telling Alex:
> 'This slip is real. The best move is honesty, now, before Marcus's team has to
> reorganize around a surprise.'"

**Emotional beat: Options** — "There are choices, with confidence levels."

---

## Beat 4: Approve — one click (2:45–3:10)

**Action:** Click Option C → ApprovalCard appears.

> "Alex clicks Option C. Commit has already drafted the message to Marcus. Alex reads it —
> it explains the slip, the reason, and the new ETA."

**Action:** Click Approve.

> "One click. Marcus already knows. He didn't get a call, he didn't get a long email thread.
> He got a clear message, in time to act."

**Emotional beat: Relief** — "One click — Marcus already knows."

---

## Beat 5: Click Foundry card → second cascade (3:10–3:50)

**Action:** Back to CommitPane → click Foundry card.

> "Foundry is automated accuracy testing — the team needs 85% of user phrases understood
> correctly before they can ship. There's a second risk on the board."

**Screen:** Second cascade chain loads.

> "Same convergence point. Code Complete — March 17. That's the point where nothing can change —
> the merge window that determines the ship date. Two independent risks. Both routes lead to
> Marcus's team."

**Point at the two purple BizChat Platform items across both cascades:**

> "Alex didn't know both cascades converged on the same person. The graph did.
> Five days to act — right now."

**Emotional beat: Dread → Agency** — "Second risk, same destination — but now we have 5 days to act."

---

## Closing (3:50–4:15)

> "This is what proactive risk management looks like when it's automatic. Alex didn't have to
> run a status report. The system surfaced it. Alex reviewed a draft. One click."

> "The teams that ship on time aren't the ones with better engineers. They're the ones where
> commitments are visible, and slips get communicated before they become surprises."

*Pause.*

> "This is Commit."

---

## Q&A Prompts (4:15–5:00)

Natural questions and plain-language answers:

- **"Where does it get the commitments?"** — From meeting notes, chat messages, and email; Alex's team was demo-seeded but in production it's automatic extraction from Teams
- **"Does it send messages without asking?"** — Never. Every action requires human approval. Commit drafts, Alex decides.
- **"What if the confidence is wrong?"** — Human is always in the loop. Confidence scores tell Alex how much to trust the option, not whether to skip the review.
- **"Could this work for my team?"** — The system is agnostic to team size. The demo uses 6 people across 3 teams because that's a recognizable Q1 shipping story.

---

## If Demo Breaks

> "The cascade API is running in Azure Container Apps in East US — normally sub-2 seconds.
> While it loads: here's what we're about to see..."

[Narrate verbally:]
> "Seven tasks affected by the SEVAL slip. Three owners — two on Alex's team, one cross-org at
> BizChat Platform, which is Marcus. The system will show that connection with a purple label.
> That's the moment the story turns — when the audience sees the cross-org risk appear in the chain."
