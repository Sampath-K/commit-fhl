# Architectural Decision Record (ADR) Template
> **See also**: Constitution P-23 — Documentation Standards
> **When to write an ADR**: Any architectural choice with 2+ viable alternatives.
> **File location**: `.specify/memory/adrs/ADR-NNN-short-title.md`
> **Numbering**: Sequential from ADR-001.

---

## Template

Copy this template to `.specify/memory/adrs/ADR-NNN-short-title.md`:

```markdown
# ADR-NNN: [Short Title]

| Field | Value |
|-------|-------|
| **Date** | YYYY-MM-DD |
| **Status** | Proposed / Accepted / Deprecated / Superseded |
| **Author** | [agent name] |
| **Supersedes** | ADR-NNN (if replacing a previous decision) |
| **Superseded by** | ADR-NNN (filled in if this ADR is later replaced) |

## Context

[What is the situation? What problem needs to be solved? What forces are at play?
Include relevant constraints from the constitution, tech stack, or timeline.]

## Decision

**We will [do X].**

[Describe the decision clearly and specifically. One paragraph. Use active voice.]

## Rationale

[Why this option over the alternatives? Reference specific constitution principles
or constraints that drove the decision.]

## Consequences

**Positive:**
- [What becomes easier or better?]

**Negative / Trade-offs:**
- [What becomes harder? What is accepted as a cost?]

**Neutral:**
- [Anything that changes but is neither better nor worse?]

## Alternatives Considered

### Option A: [Name]
- **Description**: [brief description]
- **Rejected because**: [specific reason]

### Option B: [Name]
- **Description**: [brief description]
- **Rejected because**: [specific reason]

## References

- [Link to relevant spec section, constitution principle, or external resource]
```

---

## Existing ADRs

| ID | Title | Status | Date |
|----|-------|--------|------|
| DA-001 | Azure Table Storage over SQL | Accepted | 2026-03-01 |
| DA-002 | Polling + Webhooks hybrid | Accepted | 2026-03-01 |
| DA-003 | One Teams tab, not a message extension | Accepted | 2026-03-01 |
| DA-004 | Adaptive Cards for approval UX | Accepted | 2026-03-01 |

*DA-001 through DA-004 are recorded in `decisions.md`. New ADRs from this point use the format above and go in `.specify/memory/adrs/`.*
