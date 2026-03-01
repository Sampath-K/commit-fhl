# Tech Debt Tracker
> **See also**: Constitution P-25 — Tech Debt Policy
> **Format for code comments**: `// TODO(T-debt-NNN): description`
> **Review cadence**: End of each sprint day. Router prioritizes.

---

## Priority Definitions

| Priority | Meaning |
|----------|---------|
| **P1** | Blocks demo or causes data loss — fix same day |
| **P2** | Blocks pilot deployment — fix before pilot |
| **P3** | Nice to fix — backlog, address if time permits |

---

## Open Items

| ID | Location | Description | Priority | Added by | Sprint day |
|----|----------|-------------|----------|----------|-----------|
| | | | | | |

*No items yet — populated during sprint as TODO/FIXME comments are added.*

---

## Resolved Items

| ID | Location | Description | Priority | Resolved | Resolution |
|----|----------|-------------|----------|---------|------------|
| | | | | | |

---

## Adding an Entry

When writing `TODO(T-debt-NNN)` in code:

1. Assign the next sequential ID (check this file first)
2. Add the entry to the table above
3. The TODO comment in code must match the description here

**Example code comment:**
```typescript
// TODO(T-debt-001): Replace polling with Graph change notification for real-time at-risk detection
//   Current: 15-min poll. Better: subscription on task activity signals.
//   Unblocked when: Graph change notifications for custom table storage are GA.
const POLL_INTERVAL_MS = 15 * 60 * 1000;
```

**Corresponding table entry:**
```
| T-debt-001 | api/src/capacity/riskDetection.ts:42 | Replace polling with Graph change notification | P3 | Forge | Day 3 |
```
