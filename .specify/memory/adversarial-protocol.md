# Adversarial Review Protocol
> **Version**: 1.0.0 — 2026-03-06
> **Authority**: P-37 (Adversarial Review Protocol) — ratified in Constitution v1.7.0
> **Principle**: Every agent has a challenger. No task is done until the challenger signs off.

---

## Why This Exists

The original agent team is optimised for **building**. Every agent is motivated to complete
their task and mark it done. This creates a systematic blind spot: no agent is constitutionally
incentivised to say "this is wrong" or "we should have done this differently."

Decisions go unexamined against the spec. Assumptions are never stress-tested. Quality
standards get interpreted loosely under time pressure. The Sentinel catches compliance
violations, but only after the work is done — it cannot challenge the *approach*.

The adversarial agents fix this by introducing structured opposition at the task level,
before Router can mark `[x]`.

---

## The 9 Adversarial Agents

| Challenger | Challenges | Core attack surface |
|------------|-----------|---------------------|
| **Veto** | Router | Coordination decisions, task assignments, sequencing |
| **Crucible** | Forge | Backend correctness, error paths, spec compliance |
| **Friction** | Canvas | UX assumptions, psychology effectiveness, accessibility |
| **Breach** | Shield | Security gaps, over-permissioning, infra assumptions |
| **Blind** | Lens | Test quality vs. quantity, false coverage, missing edge cases |
| **Wilt** | Seed | Demo realism, scenario believability, timing gaps |
| **Mirage** | Recon | Research bias, stale sources, unexamined counterarguments |
| **Noise** | Oracle | Vanity metrics, measurement gaps, KPI validity |
| **Shadow** | Sentinel | Verification completeness, rubber-stamping, protocol gaps |

---

## Activation: When Challengers Run

Challengers activate at **three points**, not just at task completion:

### Gate 1 — Design Review (before implementation)

```
Agent produces design note [DESIGN] in agent-inbox.md
  → Challenger reviews within 10-minute time-box
  → Challenger posts [DESIGN-APPROVED] or [DESIGN-CHALLENGE]
  → If DESIGN-APPROVED: implementation may begin
  → If DESIGN-CHALLENGE: agent revises design — implementation BLOCKED until approved
  → CRITICAL design challenge: must be resolved before any code is written
```

This gate is **mandatory for every task** that creates or modifies a component, service,
repository, or infrastructure resource. It cannot be skipped. (See P-38)

### Gate 2 — Mid-Task Self-Referral (mandatory, not optional)

```
Agent hits a mid-task decision trigger (see list below)
  → Agent MUST post [DESIGN-REVIEW] to agent-inbox.md (cannot continue without this)
  → Challenger reviews the specific decision within 10-minute time-box
  → Challenger posts [APPROVED] or [CHALLENGE]
  → If CHALLENGE: agent resolves before proceeding
```

**Mandatory self-referral triggers** (agent MUST stop and refer — not optional):
- An architectural pattern chosen that was not in the approved design
- A new dependency (npm/NuGet) added that was not in the original task
- A layering boundary crossed (business logic in wrong layer)
- A data model or API contract changed from the specification
- A security permission or OAuth scope added mid-task
- A new component or service created that wasn't in the design phase
- Implementation line count reaches 500 — must stop and split into sub-tasks (P-38)

Skipping a mandatory self-referral is a **P-38 violation** (CRITICAL severity if it resulted
in a layering or security decision; HIGH severity otherwise). Sentinel checks for this.

### Gate 3 — Task Completion Review (before Router marks [x])

```
Agent announces: "Task T-NNN complete"
  → Challenger activates (same session, 10-minute time-box per party)
  → Challenger posts PASS or CHALLENGE to agent-inbox.md
  → If PASS: Router can mark [x] (P-26 criterion 6 satisfied)
  → If CHALLENGE: originating agent must respond (fix or rebuttal)
  → If rebuttal: Challenger evaluates and either accepts or ESCALATES
  → If ESCALATE: Sentinel arbitrates (hard cap: 1 round)
  → If Sentinel cannot resolve: surfaces to human
```

**Exchange-limit rules (the enforceable constraint — Gap D closed):**

The primary constraint is **one substantive exchange per party** — not a wall-clock.
"10 minutes" is the spirit of the rule; "one exchange per party" is the enforceable letter.

- Challenger posts CHALLENGE → agent posts one rebuttal → challenger posts final verdict
  (accept or ESCALATE). That is the complete protocol. No further rounds.
- A "substantive exchange" requires evidence: spec citation, constitution principle, or
  concrete code reference. A one-line acknowledgement without substance is not an exchange
  and must be followed up before the exchange count advances.
- CRITICAL challenges are not cut off by the exchange limit. They proceed through the full
  rebuttal round. The limit prevents *debates*; it does not truncate *resolution work*.
- The 10-minute framing applies as a guideline in human-supervised sessions where wall-clock
  time is meaningful. In AI-agent sessions the exchange limit is the operative rule.

---

## The Two Verdicts

### PASS

A PASS is only valid if it documents evidence. A PASS without documentation is invalid
and Router must reject it, requiring the challenger to repost with evidence.

**Minimum evidence requirement for a valid PASS:**
- At least 3 spec sections or constitution principles listed as checked
- At least one concrete item confirmed correct (not just "nothing found wrong")
- A PASS on a task that changed >5 files must list each file reviewed

```markdown
## Adversarial Review — [Challenger] — Task T-NNN — PASS
**Agent reviewed**: [Forge/Canvas/etc.]
**Spec sections checked**: [list — minimum 1]
**Constitution principles checked**: [list P-NN, P-NN — minimum 2]
**Files reviewed**: [list of files checked]
**Evidence of correctness**: [at least 1 concrete item — e.g., "error path for 429 tested at line 47"]
**Challenges raised**: [none | N raised, all resolved]
**Verdict**: ✅ PASS — Router may mark [x]
```

If Router sees a pattern of undocumented PASSes from a challenger, Router escalates to Sentinel.
Sentinel may retroactively invalidate PASSes that lack evidence and reopen those tasks.

### CHALLENGE
```markdown
## Adversarial Review — [Challenger] — Task T-NNN — CHALLENGE
**Agent reviewed**: [Forge/Canvas/etc.]
**Spec sections checked**: [list]
**Constitution principles checked**: [list]

### Challenge 1 — [CRITICAL | HIGH | MEDIUM | LOW]
**Claim**: [what the challenger asserts is wrong]
**Evidence**: [spec section, constitution principle, or concrete test case]
**Required action**: [fix | justify | document as known gap]

### Challenge 2 — ...

**Verdict**: ❌ CHALLENGE — task is BLOCKED pending response
```

---

## Challenge Severity

| Severity | Definition | Blocks [x]? |
|----------|-----------|-------------|
| **CRITICAL** | Spec requirement not met; constitution principle violated; security vulnerability | Yes — must fix |
| **HIGH** | Significant quality gap; missing error path; assumption invalidated by evidence | Yes — fix or accepted rebuttal required |
| **MEDIUM** | Improvement opportunity; better approach exists; documentation gap | No — agent must acknowledge and either fix or log as tech debt |
| **LOW** | Minor concern; style preference; hypothetical edge case | No — logged for awareness |

---

## The Rebuttal Protocol

An agent may push back on a CHALLENGE with a rebuttal. A valid rebuttal must:

1. **Cite evidence** — spec, constitution, or empirical data. Opinion is not a rebuttal.
2. **Acknowledge the concern** — "I understand you're raising X because of Y"
3. **Explain the tradeoff** — why the current approach was chosen despite the concern
4. **Propose a resolution** — either fix, document as tech debt (with T-debt reference), or
   accept as known limitation with a rationale entry in `decisions.md`

A rebuttal that does none of these is invalid. Challenger escalates immediately.

---

## Design Review Formats

### Design Approval
```markdown
## Design Review — [Challenger] — Task T-NNN — DESIGN-APPROVED
**Contract reviewed**: [yes/no — interface correct?]
**Error paths reviewed**: [yes/no — all failure modes named?]
**Layering reviewed**: [yes/no — no P-20 violations?]
**State ownership reviewed**: [yes/no — P-22 compliant?]
**Size estimate validated**: [N lines — sub-task rule triggered? yes/no]
**Verdict**: ✅ DESIGN-APPROVED — implementation may begin
```

### Design Challenge
```markdown
## Design Review — [Challenger] — Task T-NNN — DESIGN-CHALLENGE
**Section challenged**: [Contract | Error paths | Layering | State | Size]
**Challenge**: [what is wrong with the design]
**Evidence**: [spec section, constitution principle, or concrete failure scenario]
**Required action**: [revise and repost design | split into sub-tasks | justify the decision]
**Verdict**: ❌ DESIGN-CHALLENGE — implementation BLOCKED
```

---

## Single-Agent Session Rules (Gap A — adversarial integrity in solo mode)

When one agent instance plays both a production role and its challenger, the following
are mandatory. Without them, adversarial review collapses into self-review.

| Rule | What it requires |
|------|-----------------|
| **Role declaration** | Agent posts `[ROLE: switching from X to Y for T-NNN review]` to `agent-inbox.md` before performing the review |
| **Reasoning separation** | The challenger block is written as a distinct, labelled section — not blended into the build narrative |
| **No forward knowledge** | Challenger reasoning is written as if encountering the implementation for the first time. "I chose this earlier" is not a valid PASS justification. |
| **Sentinel elevated audit** | Sentinel reviews at least one PASS per challenger issued in single-agent mode; a PASS with no documented reasoning is CRITICAL, not just invalid |
| **Shadow elevated scrutiny** | Shadow treats any "zero violations" Sentinel sign-off from a single-agent session as requiring additional evidence before accepting it |

A single-agent PASS that does not meet these rules is treated as no PASS at all —
the task remains blocked at P-26 criterion 6.

---

## What Challengers Check (Universal Across All)

Every challenger checks these regardless of domain:

1. **Spec compliance** — does the output match what `spec.md` says should exist?
2. **Constitution compliance** — which principles apply? Are they met?
3. **Assumption validity** — what assumptions were made? Are they documented? Are they sound?
4. **Error/edge case coverage** — what happens when inputs are invalid, empty, null, or adversarial?
5. **Improvement opportunity** — is there a materially better approach that was overlooked?

---

## What Challengers Do NOT Do

- **Do not rebuild** — challengers review and challenge, they do not rewrite the agent's work
- **Do not nitpick style** — only raise challenges with material impact
- **Do not obstruct** — if a challenge cannot be articulated with evidence, it is not raised
- **Do not challenge FHL scope decisions** — if the spec says it, the spec wins
- **Do not re-litigate closed decisions** — check `decisions.md` before raising anything in it

---

## Integration with Existing Roles

**Router** — must wait for challenger PASS before marking any task `[x]`. If no challenger
exists for a task type (e.g., infra-only task), Router self-certifies and notes it.

**Sentinel** — arbitrates escalated challenges. Sentinel's ruling is final within a session.
If Sentinel and a challenger disagree, the human is the tiebreaker.

**Recon** — challengers may request Recon investigate a factual dispute (e.g., "is this
Graph API actually rate-limited at this level?"). Recon provides evidence; challenger decides.

---

## Anti-patterns to Avoid

| Anti-pattern | Description | Correct behaviour |
|-------------|-------------|------------------|
| Rubber-stamp reviews | Challenger always PASSes without substance | Challenger must document what was checked |
| Scope creep challenges | Challenger raises concerns outside the task | Restrict to what the task changed |
| Circular escalation | Agent and challenger loop without resolution | Hard cap: 1 exchange each, then Sentinel |
| Challenge theatre | Challenger raises LOW issues as CRITICAL | Severity must be justified with evidence |
| Approval anxiety | Agent pre-emptively rewrites everything to avoid challenges | Fix only what challengers actually flag |
