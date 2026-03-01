# Commit FHL — UX Psychology & Motivation Design Guide
> **Owned by**: Canvas (frontend), with Router sign-off on psychology principle application
> **Version**: 1.0.0
> **Reference**: Constitution P-27

This document is the implementation reference for every behavioral science pattern in the Commit UX.
Canvas agents read this before building any motivational component.

---

## Philosophy: The Supportive Mirror

Commit is not a task manager. It is a **motivational mirror** — it reflects your professional
momentum back to you in a way that makes you want to protect and grow it.

Design principle: **The system works FOR the user, not watches OVER them.**

Every interaction must answer: *"Did this make the user feel more capable or more anxious?"*
If anxious → redesign. Capable → ship it.

---

## Behavioral Science Frameworks (Required)

### 1. Self-Determination Theory (Deci & Ryan, 1985)

Three core needs that drive intrinsic motivation. All three must be satisfied.

#### Autonomy
> "I am in control of my work"

**Implementations:**
- Users can reorder their Eisenhower board manually
- Every agent suggestion has Edit and Skip — never force acceptance
- Custom tags and groupings let users organize how they think
- No mandatory workflows — all paths are user-driven
- Settings for notification frequency, digest timing, score visibility

**Anti-pattern to avoid:** Mandatory steps before you can dismiss something. Lock-in.

#### Competence
> "I am getting better at managing my commitments"

**Implementations:**
- Delivery Score: animated 0–100 donut, updates live, shows 7-day trend
- Competency Level: 5-tier progression with named levels (see Level System below)
- Skill tags emerge from patterns: "You're excellent at shipping on time for design reviews"
- Weekly reliability report: "Your on-time rate this week: 94% (↑ from 87%)"
- After every completed cascade management: "You handled a 4-person cascade — that's advanced"

**Anti-pattern to avoid:** Score that goes down without explanation. Unexplained drops feel punitive.

#### Relatedness
> "My work connects to people I care about"

**Implementations:**
- "Alice is also tracking 3 items from this meeting" (anonymized social presence)
- Team delivery health score: shared goal everyone can see improving
- "You unblocked Priya — she can now complete her deployment" after approval
- Watcher count on tasks: "4 people are counting on this"
- End-of-week team highlight: "Your team hit 90% delivery health — best week yet"

**Anti-pattern to avoid:** Surveillance framing. Never show "who is watching you" — only "who you're helping."

---

### 2. Progress Principle (Amabile & Kramer, 2011)

> "Of all the things that can boost inner work life, the single most important is making progress
> in meaningful work — even if the progress is small."

**Implementations:**
- Daily progress bar: "X of Y commitments resolved today" — visible at all times
- Every task completion triggers an immediate celebration (not just a state change)
- "3 items cleared since 9 AM" running counter in the header
- Streak tracking: consecutive working days with at least 1 completion
- Progress animation on complex tasks: sub-tasks show fill as items check off
- Weekly retrospective highlights the magnitude of progress (even if incomplete)

**Anti-pattern to avoid:** Progress that disappears (e.g., new tasks added and score stays flat without explanation).

---

### 3. Fogg Behavior Model (Fogg, 2009)

> Behavior = Motivation × Ability × Trigger (all three must be present simultaneously)

**Motivation channels:**
- Delivery Score (competence signal)
- Streak (loss-aversion + achievement)
- Team health (relatedness + social proof)
- InsightCard ("Here's what Commit did for you this week")

**Ability enhancers (friction reduction):**
- One-click approval — the primary interaction takes exactly 1 click
- Auto-filled drafts — agent writes the message, user just approves
- Smart defaults — due dates suggested from calendar, not manually entered
- Progressive disclosure — only show 3 tasks at a time in focus mode, not 30

**Trigger architecture:**

| Trigger | When | What it says |
|---------|------|-------------|
| Morning Digest | Session start | "Here's your day — 5 commitments, 1 at risk" |
| Streak protection | EOD with no completions | "1 task to protect your N-day streak" |
| Risk alert | Task hits at-risk threshold | "This may block 3 people — here's what to do" |
| Quick win nudge | User has been idle 30+ min | "Here's your easiest win right now" |
| Celebration nudge | After 3+ completions in a session | "You're in a flow state — keep going!" |

**Anti-pattern to avoid:** Too many triggers. Users must feel triggered only when it's genuinely helpful.
Maximum 3 triggers per day (system-enforced).

---

### 4. Habit Loop (Duhigg, 2012)

> Cue → Routine → Reward (repeat = habit)

**Cue (Morning Digest):**
- Appears when Teams tab is opened for the first time each day
- Shows: today's commitment count, yesterday's streak status, one quick-win task
- Framed as "Here's your day" — not "You have N overdue tasks"
- Stagger animation: cards reveal one by one with spring physics

**Routine (Review → Approve → Done):**
- Default view is the Eisenhower board — exactly the mental model of prioritization
- One primary action per card: the most recommended action pre-selected
- Keyboard shortcuts available for power users: `A` approve, `E` edit, `S` skip

**Reward (Completion Celebration):**
- Immediate micro-animation on every completion (non-negotiable)
- Score increment — users see the number go up
- Streak counter increments with fire animation
- Verbal acknowledgment: "Done. 4 more to go" → "Done. Last one!"

---

### 5. Goal Gradient Effect (Hull, 1932; confirmed by Kivetz et al., 2006)

> Effort increases as goal completion approaches.

**Implementations:**
- Progress ring on daily goal fills faster-looking as it approaches 100%
- Messaging changes with completion percentage:
  - 0%: "Let's get started — here's your first task"
  - 40%: "Good progress — 3 of 7 done"
  - 70%: "Almost there — 2 remaining"
  - 90%: "One more! You're so close"
  - 100%: "All clear! Amazing day" + full celebration
- Final task of a group gets a "Last one!" visual treatment
- Progress bar color warms from blue → green as it fills

---

### 6. Variable Reward (Skinner, 1957; confirmed by Zeiler, 1977)

> Intermittent reinforcement creates stronger habit loops than predictable rewards.

**Implementations:**
- Occasional (not always) surprise insight: "You've cleared your most complex dependency chain this week!"
- Random bonus affirmations at milestone commitments (10th, 25th, 50th, 100th)
- "Achievement unlocked" moments for new behaviors (first cascade prevented, first team unblocked)
- Milestone messages are NOT predictable — users should occasionally be surprised

**Implementation constraint:** No more than 1 variable reward per session. Randomize from a message pool of ≥ 20 phrases. Never repeat within 5 sessions.

---

### 7. Loss Aversion (Kahneman & Tversky, 1979)

> Losses loom larger than gains of equivalent magnitude (~2× more impactful).

**Use sparingly and always with a solution path:**

| Use case | Message | Solution offered |
|----------|---------|-----------------|
| Streak at risk | "1 task to protect your 7-day streak" | Shows the easiest task to complete |
| At-risk commitment | "3 people may be blocked if this slips" | Shows cascade + replan options |
| Score declining | "Delivery score trending down — 2 tasks need attention" | Links to those 2 tasks only |

**Hard constraints on loss-aversion use:**
- NEVER show streak broken after the fact without an immediate "Start a new one" call to action
- NEVER show a raw "you missed N commitments" without also showing what was completed
- All loss messages must include a specific action to take. No dead-end warnings.
- Maximum 1 loss-framing message per session.

---

### 8. Peak-End Rule (Kahneman & Fredrickson, 1993)

> Experiences are remembered by their most intense moment (peak) and their final moment (end).

**Design the peaks:**
- First completion of the day: "First win!" special animation variant (peak)
- Cascade prevention: "You prevented a cascade before anyone was blocked" (peak)
- Level up: Full-screen celebration (peak)

**Design the ends:**
- End of daily session: "Day wrapped" summary with trophy animation
  - Shows: tasks completed, time saved estimate, streak status
  - 3-sentence narrative generated by AI (positive framing even on hard days)
- End of week: celebration screen with team impact
- These must be genuinely satisfying, not perfunctory

---

### 9. Commitment & Consistency (Cialdini, 1984)

> People act consistently with prior commitments once made.

**Implementations:**
- When user sets a due date: system treats it as a commitment, not just metadata
  - Reminders reference the user's own words: "You said you'd complete this by Thursday"
- Optional "Share with team lead" toggle — public commitment increases follow-through 3× (research-backed)
- When approving agent drafts: confirmation shows what was committed to ("You've committed to Priya that this will be done by Friday")

---

### 10. IKEA Effect (Norton et al., 2012)

> People value things more when they've invested effort in creating or configuring them.

**Implementations:**
- Onboarding: users choose which sources to monitor (meetings, email, chat, ADO)
- Users can rename and recategorize extracted commitments
- Custom priority rules: "Always mark items from [person] as urgent"
- These customizations should be surfaced prominently — "Your custom setup"

---

### 11. Reciprocity (Cialdini, 1984)

> People feel obligated to return favors.

**Implementation (the "give first" principle):**
- InsightCard appears at session start (give value before asking anything)
  - "Commit prevented 2 scheduling conflicts for you this week"
  - "3 people got early updates because of your proactive communication"
  - "You saved an estimated 40 minutes of coordination this week"
- Only AFTER the insight card does the system show tasks needing attention
- Never lead with asks. Always lead with what you've done for the user.

---

### 12. Chunking (Miller, 1956)

> Working memory handles ~7 ± 2 items. Break large tasks into digestible pieces.

**Implementations:**
- Default view shows maximum 5 tasks at a time (not the full list)
- Large commitments auto-suggest sub-tasks: "This looks complex — should I break it into 3 steps?"
- Focus mode: one task at a time, everything else blurred
- Weekly plan shows 3 key commitments for the week (not all 20)

---

### 13. Autonomy Bias

> People are more motivated when they choose rather than are told.

**Implementations:**
- All AI suggestions framed as options: "Here are 3 ways to handle this cascade"
- Never "You need to do X" — always "You could do X"
- Dismissing suggestions is as easy as approving them (no friction to opt out)
- Users can turn off any motivational layer in settings

---

### 14. Zeigarnik Effect (Zeigarnik, 1927)

> Uncompleted tasks are better remembered than completed ones.

**Productive use:**
- In-progress tasks show a subtle "in progress" indicator that draws the eye
- "Pick up where you left off" prompt at session start for unfinished items
- Tasks with recent activity (last 2 hours) shown at top with "active" glow

**Protective use (avoiding negative Zeigarnik):**
- Completed tasks are visually cleared — not just grayed out, but moved to "Done" tab
- The done list is satisfying to look at (not a guilt trip of completed items)

---

## Competency Level System

Five levels of mastery, each with clear criteria and a memorable identity.

### Level Design

| Level | Number | Name | Criteria | XP to next | Badge |
|-------|--------|------|---------|-----------|-------|
| 1 | ⚪ | Getting Started | First 5 commitments tracked | 20 XP | Gray ring |
| 2 | 🔵 | Consistent | 80%+ on-time for 2 consecutive weeks | 50 XP | Blue shield |
| 3 | 🟢 | Reliable | 90%+ on-time, 4+ weeks, 0 cascade failures | 100 XP | Green star |
| 4 | 🟡 | Trusted | Team delivery health improves by 5%+ | 200 XP | Gold crown |
| 5 | 🪙 | Multiplier | Dependencies managed proactively, 0 surprise escalations for 30 days | ∞ | Platinum ∞ |

### XP Events

| Event | XP | Notes |
|-------|-----|-------|
| Commitment resolved on time | +5 | Base reward |
| Commitment resolved early | +8 | Bonus for ahead-of-schedule |
| Cascade prevented | +15 | High-value behavior |
| Agent suggestion approved | +2 | Reinforces AI usage |
| Commitment resolved after slip | +1 | Still counts, reduced |
| Overcommit accepted (despite warning) | -3 | Soft deterrent, not punitive |
| Streak milestone (7 days) | +25 | Bonus reward |

### Level-Up Moment Design

Level up is the biggest celebration in the system. It must feel earned and memorable.

1. Full-screen overlay fades in (dark background, 60% opacity)
2. Level badge animates from center: scale 0 → 1.2 → 1.0 (spring physics)
3. Badge color pulses 3 times
4. Particle burst from badge (confetti in level color)
5. Text reveals: "Level [N] — [Level Name]" (stagger)
6. Sub-text: what unlocked this level ("90%+ on-time for 4 weeks")
7. "What's next" preview (criteria for next level)
8. Auto-dismisses after 4 seconds OR user taps to dismiss

---

## Delivery Score Algorithm

The Delivery Score is the top-level competence signal. It must always be accurate
and explainable.

```
DeliveryScore = (
  (onTimeRate × 40)           // 40% weight: did you deliver what you said?
  + (cascadeHealthRate × 25)  // 25% weight: no surprise downstream impact
  + (overcommitRate × 20)     // 20% weight: realistic commitments (inverse)
  + (consistencyBonus × 15)   // 15% weight: daily engagement streak
) / 100

where:
  onTimeRate        = completedOnTime / totalCompleted (rolling 14 days)
  cascadeHealthRate = tasksWithoutCascade / totalTasks (rolling 14 days)
  overcommitRate    = 1 - (overcommitWarningsAccepted / totalWarnings)
  consistencyBonus  = min(currentStreak / 10, 1.0) (capped at 10-day streak)
```

**Score display rules:**
- Show score as whole number (0–100)
- Show trend arrow: ↑ (up ≥ 3 points), → (±2 points), ↓ (down ≥ 3 points)
- Show 7-day sparkline next to score
- Score below 50: blue (needs attention) — NOT red (avoid shame)
- Score 50–79: blue/green gradient
- Score 80–89: green
- Score 90–100: green with gold shimmer

**Explainability rule:** Tapping the score shows a breakdown: "Your score is 84 because:
- On-time rate: 92% ↑ (+4 pts this week)
- Cascade health: 78% → (same as last week)
- Overcommit rate: 90% ↑ (you declined 2 overloaded weeks)
- Consistency: 7-day streak (+10 pts)"

---

## Animation System Specifications

All animations use `@react-spring/web`. No raw CSS keyframes for behavioral animations.

### Spring Configs (reuse these)

```typescript
// psychology.config.ts
export const SPRING_CONFIGS = {
  bounce:    { tension: 300, friction: 15 },  // badges, level-up
  smooth:    { tension: 200, friction: 25 },  // card transitions
  gentle:    { tension: 150, friction: 30 },  // digest reveal
  stiff:     { tension: 400, friction: 20 },  // overcommit shake
  wobbly:    { tension: 180, friction: 12 },  // confetti
} as const;

export const STAGGER_DELAYS = {
  digestCards:    50,   // ms between each Morning Digest card
  cascadeItems:   40,   // ms between each cascade reveal item
  statsCounters:  100,  // ms between stat count-ups in Day Wrap
} as const;

export const ANIMATION_DURATIONS = {
  microFeedback:  150,  // hover lift, button press
  stateChange:    300,  // task status change
  celebration:    600,  // completion burst
  levelUp:       4000,  // full level-up ceremony
  dayWrap:       3000,  // end-of-day summary
} as const;
```

### Reduced Motion Strategy

```typescript
// useReducedMotion.ts
import { useEffect, useState } from 'react';

export function useReducedMotion(): boolean {
  const [reduced, setReduced] = useState(
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
  useEffect(() => {
    const mq = window.matchMedia('(prefers-reduced-motion: reduce)');
    const handler = (e: MediaQueryListEvent) => setReduced(e.matches);
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  }, []);
  return reduced;
}

// Usage pattern in every animated component:
// const reducedMotion = useReducedMotion();
// const style = useSpring({
//   config: SPRING_CONFIGS.bounce,
//   scale: isVisible ? 1 : (reducedMotion ? 1 : 0),  // no scale animation when reduced
//   opacity: isVisible ? 1 : 0,                        // always animate opacity
// });
```

### Particle System (CelebrationLayer)

```typescript
// Particle types for different celebrations
type CelebrationType = 'taskComplete' | 'firstWin' | 'levelUp' | 'streak' | 'dayWrap';

const CELEBRATION_CONFIGS: Record<CelebrationType, CelebrationConfig> = {
  taskComplete: { particles: 12, colors: ['#0078d4', '#00b7c3'], duration: 600 },
  firstWin:     { particles: 30, colors: ['#0078d4', '#ffd700', '#00b7c3'], duration: 1200 },
  levelUp:      { particles: 80, colors: ['level-specific'], duration: 4000, fullScreen: true },
  streak:       { particles: 20, colors: ['#ff8c00', '#ff4500'], duration: 800 },
  dayWrap:      { particles: 50, colors: ['#0078d4', '#00b7c3', '#ffd700'], duration: 3000 },
};
```

---

## Message Library

All motivational text strings go in `src/locales/en/psychology.json` (fully i18n-compatible).

### Completion Affirmations (rotate randomly)
- "Done. Nice work."
- "That's one less thing standing between you and Friday."
- "Checked. Moving on."
- "Done — and Priya can move forward now."
- "That's clear."
- "Done. You made it easier for everyone today."
- "Checked. That one mattered."

### At-Risk (loss-aversion, always with action)
- "This may block {{n}} people — here's the easiest path forward"
- "No activity in {{hours}} hours. {{person}} is waiting on this."
- "Your {{streak}}-day streak ends today unless you close one more task"

### Insight Cards (reciprocity, give first)
- "Commit prevented {{n}} scheduling conflicts this week"
- "You gave {{n}} teammates early updates — no one was surprised"
- "You saved approximately {{minutes}} minutes of coordination overhead"
- "{{person}} was unblocked 2 days early because of your proactive communication"

### Milestone (variable reward, non-predictable)
- "You've prevented more cascades than 90% of users this week"
- "That was your 10th commitment closed on time in a row"
- "You managed a {{n}}-person cascade. That's genuinely impressive."
- "No surprises from your team in 3 weeks. That's rare."
- "Your delivery score improved {{n}} points this month"

### Morning Digest
- "Good morning. Here's your day — {{n}} commitments, {{k}} need attention today."
- "You're coming off a {{score}}-point delivery week. Let's keep it going."
- "{{streak}} days in a row. Today: {{n}} things to close."

---

## Component Implementation Checklist

For each psychology component, Canvas must verify before marking Done:

- [ ] `useReducedMotion()` hook applied — animation disabled/replaced when reduced
- [ ] All strings in `src/locales/en/psychology.json` — zero hardcoded text
- [ ] Fluent tokens used for all colors — no hardcoded hex values
- [ ] Component is keyboard navigable (Tab, Enter, Escape)
- [ ] Score/level explainability: tapping any number shows breakdown
- [ ] Loss-aversion messages always include a specific action link
- [ ] Variable reward messages come from a pool ≥ 20, no repeat within 5 sessions
- [ ] Maximum 3 trigger notifications per user per day (server-enforced)
- [ ] All animations tested at 4 breakpoints (P-16)
- [ ] Confetti/particles fall off screen and clean up from DOM after animation ends

---

*This guide is a living document. Canvas agents append to the Message Library as new messages are written.*
