# Agent Inbox
> **Purpose**: Inter-agent communication. Post here when you need another agent's output before proceeding.
> **Router**: Checks this file at the start of every session. Routes messages, marks resolved.
> **Agents**: Check for messages addressed to you before starting each task.

---

## Message Format

```
### MSG-NNN
**From**: [agent name]
**To**: [agent name | Router | All]
**Re**: [task ID or topic]
**Status**: Open | Acknowledged | Resolved
**Priority**: Blocking | FYI

[message body — clear description of what is needed or communicated]

**Resolution**: [filled in when resolved]
```

---

## Active Messages

*(No messages yet — inbox is empty at sprint start)*

---

## Resolved Messages

*(Archive resolved messages here — never delete, for audit trail)*

---

## Rules

1. **Tag `[BLOCKING]`** if you cannot continue your current task without a response
2. **Router resolves** — only Router marks messages as Resolved (or the receiving agent if it's bilateral)
3. **Same-task coordination**: If two agents are working on the same task simultaneously, post here
4. **Never block silently**: If you need something from another agent and can't proceed, post here AND move to your next unblocked task
5. **Check before your first task each session** — do not start working without reading this file
