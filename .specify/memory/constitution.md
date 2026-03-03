# Commit FHL — Project Constitution
> **Version**: 1.6.0
> **Status**: Ratified
> **Last amended**: 2026-03-03
> All agents must read and follow every principle. Amendments require human approval + version bump.

---

## Versioning

| Version | Date | Change |
|---------|------|--------|
| 1.0.0 | 2026-03-01 | Initial constitution — P-01 through P-17 |
| 1.1.0 | 2026-03-01 | Added P-18 through P-27 (team structure, engineering standards, psychology layer) |
| 1.2.0 | 2026-03-01 | Backend changed to C# ASP.NET Core Minimal API (.NET 9) + xUnit; added P-28 (C# backend conventions) and P-29 (live reporting cadence); updated P-20, P-21, P-24 |
| 1.3.0 | 2026-03-02 | Added P-30 (deployment cadence — deployed to tenant is the definition of done) |
| 1.4.0 | 2026-03-02 | Added P-31 (Sentinel integrity protocol — non-skippable end-of-session verification) |
| 1.5.0 | 2026-03-03 | Added P-32 (Zero Human Input protocol), P-33 (Human-Agent Co-Team Contract), P-34 (Project Template Protocol); expanded agent roster to 9 (added Recon + Oracle) |
| 1.6.0 | 2026-03-03 | Added P-36 (Teams app package version bump required on every change) |

---

## Principles

### P-01 — Pilot-First Scale, Enterprise Architecture

Build for 50 pilot users but architect for enterprise from day 1.
- Multi-tenant data isolation baked in (partitionKey = userId OID)
- No hardcoded tenant-specific logic
- Feature flags control rollout from pilot → GA
- Performance targets must hold at 10× pilot load

### P-02 — Performance Standards

All performance targets are mandatory hard ceilings, not aspirations.

| Endpoint type | P95 target |
|--------------|-----------|
| Non-AI API endpoints | < 1 second |
| AI extraction pipeline | < 5 minutes end-to-end |
| Cascade simulation | < 10 seconds |
| Adaptive Card render | < 2 seconds |

Any task that degrades these targets must add a regression test before merging.

### P-03 — Availability SLA

99.9% uptime measured over rolling 30-day windows.
- Downtime budget: 43.8 minutes/month
- Health endpoint `/api/v1/health` must respond in < 500ms at P99
- All scheduled jobs must log success/failure; silence = alert

### P-04 — Data Residency

EU tenant data must not leave EU boundaries. GDPR Art. 44 compliance is mandatory.
- Azure region selection must respect tenant geography
- Storage and AI resources provisioned in matching regions
- Cross-region data transfer only with explicit legal basis

### P-05 — Compliance Framework

All code must adhere to GDPR/EUDB requirements and Microsoft SDL.
- Privacy-by-design: collect minimum data needed
- Right to erasure: DELETE /api/v1/users/{userId}/data must process within 30 days
- SDL: threat modelling required before any credential-handling code ships
- Security review gate before pilot deployment

### P-06 — Test Coverage

- **Unit tests**: ≥ 90% line coverage on all service and repository layers
- **Mutation testing**: Stryker score ≥ 80% (not just line coverage — tests must catch real bugs)
- **Functional tests**: real server, Azurite backend, all API routes exercised
- **E2E tests**: 5 critical user journeys × 4 viewports (Playwright)
- No merge that drops coverage below thresholds

### P-07 — Secrets Management

**Managed Identity ONLY in production and pilot environments.**
- No stored secrets (connection strings, API keys) in any environment above `dev`
- `.env` files only for local dev — never committed, never deployed
- Secret scan quality gate blocks merge if any secret pattern detected
- Key rotation policy: API keys rotated every 90 days

### P-08 — Quality Gates

All 4 gates are mandatory. Any gate failure blocks merge. No exceptions.

1. **Tests pass**: all unit + relevant functional tests green
2. **ESLint strict**: zero errors (warnings are informational only)
3. **Secret scan**: no hardcoded credentials, tokens, or connection strings
4. **npm audit**: zero HIGH or CRITICAL CVEs in production dependencies

### P-09 — Testing Strategy

Four test tiers, each with a distinct purpose:

| Tier | Tool | Scope | When runs |
|------|------|-------|----------|
| Unit | Jest + Stryker | Single function/class, mocked deps | Every commit |
| Functional API | Jest + Azurite | Real server, real storage emulator | Every PR |
| E2E | Playwright × 4 viewports | 5 critical journeys | Every PR |
| Demo data | seed-demo.ts | 6 personas, 3 cascade chains | On demand |

Demo data must be idempotent (safe to run twice) and flushable (flush-demo.ts removes seed data without affecting real data).

### P-10 — Feature Flags

All behavior changes must be gated by feature flags.
- Platform: Azure App Configuration
- Labels: `dev` (always on), `pilot` (opt-in), `ga` (default on)
- Flag names: `commit.feature.{featureName}` kebab-case
- Default: flags off unless explicitly enabled
- `FeatureFlagService.ts` is the ONLY place flags are evaluated — no direct SDK calls in business logic

### P-11 — CI/CD Pipeline

GitHub Actions drives all deployments.
- **Dev**: automatic deployment on merge to `main`
- **Pilot**: manual approval required in GitHub Actions workflow
- **Prod**: manual approval + security review sign-off
- Deployment artifacts are immutable — build once, promote the same artifact
- Rollback: redeploy previous artifact (not revert + rebuild)

### P-12 — Privacy & PII Protection

Four hard constraints, non-negotiable:

1. **No message body content in logs** — never log email/chat/transcript text
2. **No commitment title text in telemetry** — task names may contain PII
3. **User display names hashed** (SHA-256 + salt) in all telemetry events
4. **Right-to-erasure endpoint** — DELETE /api/v1/users/{userId}/data within 30 days

PII scrubber middleware runs on every telemetry event before emission.

### P-13 — Observability

Four telemetry types, all mandatory:

| Type | What it captures |
|------|-----------------|
| User actions | Feature used, approval decision, skip, edit |
| Error events | Exception type, source, recovery path (never PII) |
| Performance traces | Latency per pipeline stage, AI token usage |
| Business KPIs | Commitments extracted, cascades detected, approvals rate |

Four alert rules (Azure Monitor):

| Alert | Threshold |
|-------|---------|
| Error rate | > 1% over 5-min window |
| P95 latency | > 3× baseline |
| Availability | < 99% over 1h |
| AI extraction silence | Zero extractions > 30 minutes during business hours |

### P-14 — Accessibility

WCAG 2.1 Level AA compliance is mandatory, not optional.
- All interactive elements keyboard navigable
- All images have alt text
- Color contrast ratio ≥ 4.5:1 for normal text, ≥ 3:1 for large text
- No information conveyed by color alone
- Animations must respect `prefers-reduced-motion` media query (see P-27)
- Focus indicators always visible
- Screen reader testing with NVDA required before pilot

### P-15 — Design System

Fluent UI React v9 (Fluent 2) is the mandatory design system.
- No custom CSS for components that Fluent provides natively
- Fluent tokens for all color, spacing, and typography decisions
- Never hardcode hex colors — use Fluent semantic color tokens
- Component overrides via Fluent `makeStyles` only — no inline styles on shared components

### P-16 — Responsiveness

Four mandatory breakpoints, all tested in Playwright:

| Context | Viewport |
|---------|---------|
| Teams desktop tab | 320px wide |
| Web browser | 1024px+ |
| Teams mobile | 360px wide |
| Outlook add-in | 600px wide |

Layout must be usable (not just not-broken) at all four breakpoints.

### P-17 — Internationalization

Full localization from day 1.
- **Library**: react-i18next
- **Locale driver**: Teams locale setting (not browser locale)
- **Zero hardcoded strings**: every user-visible string in `src/locales/{locale}/translation.json`
- **ESLint rule**: `i18next/no-literal-string` enforced — build fails on hardcoded strings
- **String keys**: namespace.component.element pattern (e.g., `commitPane.header.title`)
- Right-to-left layout support built into Fluent v9 (no custom RTL CSS needed)

### P-18 — Multi-Agent Architecture

This project uses a 9-agent team (see P-35 for full roster and activation triggers).
Each agent owns a distinct slice of the codebase. One agent may play multiple roles in a session.

**Agent roster:**

| Agent | Role | Owns | Phase |
|-------|------|------|-------|
| **Router** | PM + Tech Lead | `.specify/`, `.agents/` governance | All |
| **Forge** | Backend Engineer | `src/api/**` | 4 |
| **Canvas** | Frontend Engineer | `src/app/**`, `src/locales/**` | 4 |
| **Shield** | Platform/DevOps | `infra/**`, `.github/**` | 3–4 |
| **Lens** | QA/SDET | `tests/**` | 4 |
| **Seed** | Demo/Data | `scripts/**` | 4 end |
| **Sentinel** | Integrity verifier | Governance review | End of session |
| **Recon** | Research analyst | `.specify/memory/research/` | 1–2 |
| **Oracle** | Analytics engineer | `scripts/analytics/`, dashboards | 4 live |

**Inter-agent communication**: `.specify/memory/agent-inbox.md`
- Post a message when you need another agent's output before proceeding
- Tag it `[BLOCKING]` if it blocks your current task
- Check inbox at start of every session

**Definition of Done** (Router checks all 4 before marking `[x]`):
1. All automated tests pass (unit + relevant functional)
2. Router reviewed output against constitution (all principles checked)
3. Acceptance criteria in tasks.md explicitly met
4. No unresolved agent-inbox.md messages for this task

### P-19 — Git Workflow

Trunk-based development. Short-lived branches. No long-running feature branches.

- **Main branch**: `main` — always deployable
- **Branch naming**: `feat/T-NNN-short-description` (e.g., `feat/T-006-type-definitions`)
- **Branch lifetime**: merge to main within 24 hours of creation
- **Commit format**: Conventional Commits — `type(scope): description`
  - Types: `feat`, `fix`, `chore`, `test`, `docs`, `refactor`, `perf`
  - Scopes: `forge`, `canvas`, `shield`, `lens`, `seed`, `infra`
  - Example: `feat(forge): add commitmentStore CRUD with unit tests`
- **PR scope**: one task per commit — atomic and independently reviewable
- **Merge strategy**: squash merge into main

### P-20 — Architecture Patterns

**3-layer strict layering** — applies to BOTH C# backend and TypeScript frontend. No layer skipping, no exceptions:

```
Endpoint / Route Handler  →  Service  →  Repository
(validate input)              (logic)     (storage only)
```

- **Endpoints/Routes**: validate input, call one service method, return typed response. NO business logic.
- **Services**: business logic, AI calls, orchestration. NO direct storage SDK calls.
- **Repositories**: storage interface only. NO business logic. Direct SDK calls here and nowhere else.

**C# error hierarchy** — all exceptions must be typed (see P-28 for C# conventions):
```csharp
CommitException (base)
  ├── ValidationException    (400 — bad input)
  ├── AuthException          (401/403 — auth failure)
  ├── GraphException         (502 — Graph API failure)
  ├── StorageException       (503 — Table Storage failure)
  └── AiException            (503 — Azure OpenAI failure)
```

**TypeScript error hierarchy** — for frontend API error handling:
```typescript
AppError (base)
  ├── ValidationError    (400)
  ├── AuthError          (401/403)
  ├── GraphError         (502)
  ├── StorageError       (503)
  └── AiError            (503)
```

**Repository pattern**: every storage operation goes through a typed repository interface (`ICommitmentRepository`, etc.). Direct SDK calls (`TableClient`, `GraphServiceClient`) only inside repository/client implementations.

### P-21 — TypeScript Frontend Conventions

- **`interface`** for object shapes (data structures, API payloads, repository contracts)
- **`type`** for unions, intersections, and computed types
- **Zero `any`** — ESLint `@typescript-eslint/no-explicit-any` is an error
- **Named exports only** — no default exports (improves refactoring safety)
- **JSDoc on all public APIs**: every exported function, class, and interface must have JSDoc
- **Strict mode**: `"strict": true` in tsconfig.json — no exceptions
- **No non-null assertions** (`!`) — handle null explicitly with guards

### P-22 — Frontend Architecture

**State management split:**
- **Server state** (API data): TanStack Query (React Query) — handles caching, refetching, loading/error states
- **UI state** (modals, selection, view mode): React Context — no external library needed
- **No Redux, no Zustand, no MobX** — unnecessary complexity for this app

**Component architecture:**
- Container components fetch data (hooks) and pass props down
- Presentational components are pure — props in, JSX out
- Psychology/animation components live in `components/psychology/` and `components/animations/`

**Import discipline:**
- Fluent UI from `@fluentui/react-components` (v9) only
- TanStack Query from `@tanstack/react-query`
- Animations from `@react-spring/web` (physics-based) — do NOT use raw CSS keyframes for UX animations

### P-22 — Frontend Architecture

**State management split:**
- **Server state**: TanStack Query — caching, refetching, loading/error states
- **UI state**: React Context — no external library
- **No Redux, no Zustand, no MobX**

**Component categories:**
- `components/core/` — functional app components (CommitPane, CascadeView, etc.)
- `components/psychology/` — motivation/progression components
- `components/animations/` — reusable animation hooks and wrappers
- `hooks/` — data fetching (useCommitments, useDeliveryScore, useStreak, etc.)

**Animation library**: `@react-spring/web` for physics-based animations.

### P-23 — Documentation Standards

Documentation is part of the task. A task is NOT done without:

1. **JSDoc** on all exported service methods and repository interfaces
2. **ADR** for any architectural choice with 2+ viable alternatives (use `.specify/memory/adr-template.md`)
3. **README.md** per major module: `api/`, `app/`, `scripts/`, `tests/`
4. **OpenAPI spec** kept in sync with every route change

### P-24 — Dependency Policy

Before adding any package (npm or NuGet), verify all three criteria:

**npm packages (frontend):**

| Criterion | Threshold | How to check |
|-----------|---------|--------------|
| Popularity | > 1M weekly downloads | npmjs.com |
| Maintenance | Updated within last 12 months | npmjs.com |
| Security | No HIGH or CRITICAL CVEs | `npm audit` |

**NuGet packages (backend):**

| Criterion | Threshold | How to check |
|-----------|---------|--------------|
| Publisher | Microsoft or verified publisher | nuget.org |
| Maintenance | Updated within last 12 months | nuget.org |
| Security | No HIGH or CRITICAL CVEs | `dotnet list package --vulnerable` |

Prefer official Microsoft Azure SDK packages (`Azure.*`, `Microsoft.*`). If criteria not met, build in-house or find an alternative. Document in an ADR.

### P-25 — Tech Debt Policy

Technical debt is tracked, not hidden.
- Every TODO/FIXME comment must reference a `.specify/memory/tech-debt.md` entry
- Format: `// TODO(T-debt-NNN): description`
- Tech debt file reviewed at end of each sprint day
- Priority: P1 (blocks demo), P2 (blocks pilot), P3 (nice-to-fix)
- Debt items created during a task do not block the task from being marked Done, but must be logged before closing

### P-26 — Definition of Done

A task is **done** when ALL four criteria are met. Meeting three of four is NOT done.

1. ✅ All automated tests pass (unit tests + relevant functional tests)
2. ✅ Router reviewed output against this constitution (all applicable principles checked)
3. ✅ All acceptance criteria stated in tasks.md are explicitly met
4. ✅ No open messages in agent-inbox.md addressed to this agent for this task

### P-27 — Psychology & Motivation Layer

The UI must be designed to intrinsically motivate users using evidence-based behavioral science. This is not decoration — it is a core product principle.

**Required psychological frameworks (all must be implemented):**

#### Self-Determination Theory (Deci & Ryan)
- **Autonomy**: Users choose their view, order, and which suggestions to act on. Never mandate. Always offer alternatives. Agent drafts are always editable.
- **Competence**: Surface a Delivery Score (0–100) that visibly improves as users manage commitments. Provide Competency Level progression (5 levels). Show trending arrows on all scores.
- **Relatedness**: Show social context ("Alice is also tracking 3 items from this meeting") — anonymized. Show team delivery health as a shared goal.

#### Progress Principle (Amabile & Kramer)
- Show small wins immediately and prominently. A checked task is a celebration, not just a state change.
- Daily progress bar: "3 of 7 commitments resolved today"
- Weekly streak: consecutive days with at least one commitment resolved
- Completion animations are mandatory — silent check marks are forbidden

#### Fogg Behavior Model (Motivation × Ability × Trigger)
- **Motivation**: Delivery Score, streak, level progression, team health
- **Ability**: Reduce friction to the minimum possible. One-click approval. Auto-filled drafts. Smart defaults.
- **Trigger**: Morning Digest card (daily cue). Risk alerts (contextual trigger). Streak protection nudge.

#### Habit Loop (Duhigg)
- **Cue**: Morning Digest surfaces at session start — "Here's your day"
- **Routine**: Review Eisenhower board → approve/skip suggestions
- **Reward**: Completion animation + score increment + streak count

#### Goal Gradient Effect
- As completion approaches 100%, show accelerating progress feedback
- "You're 80% there" message when 4 of 5 tasks are done
- Final task gets a "Last one!" animation variant

#### Variable Reward
- Occasional surprise insight cards: "You cleared your most complex dependency chain this week!"
- Random milestone celebrations for round numbers (10th commitment, 50th approval)
- Non-predictable so they don't become ignored

#### Loss Aversion (Kahneman & Tversky)
- Streak protection: "Don't break your 5-day streak — 1 task to close today"
- At-risk framing: "3 commitments at risk of missing their window" (not "you have 3 tasks")
- IMPORTANT: Use sparingly — anxiety is a bug, urgency is a feature

#### Peak-End Rule (Kahneman)
- End of day: satisfying "Day wrapped" summary with trophy animation
- End of week: celebration screen with weekly stats and a memorable insight
- First completion of the day: special "first win" micro-celebration

#### Commitment & Consistency (Cialdini)
- When users set a due date, remind them near the deadline: "You said you'd finish this by Thursday"
- Optional: "Share progress with team lead" — public commitment amplifies follow-through

#### Reciprocity (Cialdini)
- Show what the system has done for the user before asking them to act
- InsightCard: "Commit saved you 45 min of coordination this week" — give first, ask second
- Morning Digest leads with value, not tasks

**Required micro-animations (all mandatory, all must respect `prefers-reduced-motion`):**

| Animation | Trigger | Behavior |
|-----------|---------|---------|
| Task completion | Check button click | Check mark draws → particle burst → score increments (count-up) |
| First win of day | First task completed | Special confetti burst + "First win!" badge drops |
| New commitment detected | Webhook arrives | Card slides in from top with spring physics |
| At-risk pulse | Task has no activity > 24h AND due < 48h | Subtle amber glow pulse, 3s loop |
| Impact score change | Cascade simulation updates score | Number count-up/down with color morph |
| Cascade reveal | User clicks at-risk task | Downstream tasks stagger-reveal (40ms delay each) |
| Approval confirmed | Approve button pressed | Ripple outward → card slides out → success toast |
| Hover elevation | Mouse over task card | Card lifts 2px with shadow deepens (CSS transition 150ms) |
| Overcommit warning | Load > 90% detected | Card shake (3 oscillations) + red glow pulse |
| Streak milestone | Streak hits 3, 7, 14, 30 days | Badge drops with bounce + "🔥 N-day streak!" |
| Level up | Competency level increases | Full-screen overlay → level badge animates in → confetti |
| Morning Digest open | Session start | Cards stagger-reveal with spring physics (50ms between cards) |
| Focus mode activate | User selects focus task | All other tasks blur/dim, selected task expands |
| Day wrap summary | End-of-day trigger | Trophy drops, stats count up, 3-sentence narrative |
| Skeleton load | Data fetching | Shimmer animation (not spinners — never spinners) |
| Empty state | No commitments | Animated "All clear!" with subtle floating elements |

**Accessibility override**: ALL animations must respect `prefers-reduced-motion: reduce`:
- Replace motion with instant transitions + opacity changes only
- Progress feedback (score, streak) still appears — just without animation
- No animation may communicate information that isn't also communicated by text/color

**Required competency level system:**

| Level | Name | Criteria | Badge color |
|-------|------|---------|------------|
| 1 | Getting Started | < 5 commitments tracked | Gray |
| 2 | Consistent | 80%+ on-time, 2+ weeks | Blue |
| 3 | Reliable | 90%+ on-time, 4+ weeks, 0 cascade failures | Green |
| 4 | Trusted | Team delivery health improves while you use it | Gold |
| 5 | Multiplier | Dependencies managed proactively, no surprises | Platinum |

**Psychology components (Canvas owns):**

```
app/src/components/psychology/
├── DeliveryScore.tsx       ← animated donut chart 0–100, trend arrow
├── StreakBadge.tsx          ← fire icon, day count, milestone celebration
├── CompetencyLevel.tsx     ← level badge, XP bar, "X away from Level N"
├── CelebrationLayer.tsx    ← full-screen overlay system for milestones
├── MorningDigest.tsx       ← daily cue card with stagger reveal
├── InsightCard.tsx         ← reciprocity: "Here's what Commit did for you"
├── FocusMode.tsx           ← single-task immersive view with blur
└── MotivationalNudge.tsx   ← well-timed encouraging messages

app/src/hooks/
├── useDeliveryScore.ts     ← computed health score (0–100)
├── useStreak.ts            ← daily streak tracking with storage
├── useCompetencyLevel.ts   ← level computation + XP
└── usePsychologyEvents.ts  ← tracks behavior patterns for triggers

app/src/config/
└── psychology.config.ts    ← all thresholds, messages, milestone values
```

**Message tone**: Supportive, not surveillant. Encouraging, not pressuring. The system works FOR the user, not watches OVER them.

---

### P-28 — C# Backend Conventions

The backend is C# (.NET 9) with ASP.NET Core Minimal API. All agents writing backend code MUST follow these conventions.

**Project structure:**
```
src/api/
├── CommitApi.csproj          ← .NET 9 SDK-style project
├── Program.cs                ← app entry point, DI registration, route mapping
├── Models/                   ← record types for request/response shapes (DTOs)
├── Entities/                 ← domain entities stored in Table Storage
├── Repositories/             ← IRepository interfaces + implementations
│   └── CommitmentRepository.cs
├── Services/                 ← business logic layer
├── Extractors/               ← transcript, chat, email, ADO extractors
├── Graph/                    ← dependency linker, cascade simulator, impact scorer
├── Agents/                   ← status drafter, calendar blocker, PR reviewer
├── Auth/                     ← MSAL OBO, Graph client factory
├── Webhooks/                 ← subscription manager, HMAC validator
├── Config/                   ← FeatureFlagService, AppInsightsExtensions, PiiScrubber
├── Exceptions/               ← CommitException hierarchy
└── Tests/                    ← xUnit test project (separate .csproj)
    CommitApi.Tests/
    └── CommitApi.Tests.csproj
```

**Language conventions:**
- **Nullable reference types**: enabled — `<Nullable>enable</Nullable>`. Handle null explicitly; no `!` null-forgiving operators except at system boundaries.
- **Record types** for immutable DTOs: `public record CommitmentRequest(string Title, string OwnerId);`
- **Interfaces first**: define `ICommitmentRepository` before `CommitmentRepository` — never call concrete type from service layer
- **PascalCase** for all public identifiers; `_camelCase` for private fields
- **`async`/`await` throughout** — no `.Result` or `.Wait()` blocking calls
- **Dependency injection**: register all services in `Program.cs`. No `new` for injected dependencies anywhere in service/repo layer.
- **Exception handling**: catch `Exception` only at endpoint boundary (global middleware). Let typed exceptions propagate; map to HTTP status in middleware.
- **JSDoc equivalent**: XML doc comments (`/// <summary>`) on all public methods, interfaces, and records.

**Testing (xUnit):**
- Test project: `src/api/CommitApi.Tests/CommitApi.Tests.csproj`
- Use `Moq` for mocking interfaces
- Test class naming: `CommitmentRepositoryTests`, `CommitmentServiceTests`
- Method naming: `MethodName_Scenario_ExpectedResult` (e.g., `UpsertAsync_NewRecord_ReturnsCreated`)
- Minimum 90% line coverage; mutation testing with Stryker.NET (score ≥ 80%)

**NuGet packages (approved list):**
```xml
<PackageReference Include="Microsoft.Graph" Version="5.*" />
<PackageReference Include="Azure.Data.Tables" Version="12.*" />
<PackageReference Include="Azure.Identity" Version="1.*" />
<PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
<PackageReference Include="Azure.Data.AppConfiguration" Version="1.*" />
<PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.*" />
<PackageReference Include="Microsoft.Identity.Web" Version="3.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="coverlet.collector" Version="6.*" />
```

### P-29 — Live Reporting Cadence

The build story, sprint reports, and dashboard MUST always reflect the last 5 minutes of work. Stale documentation is a defect.

**Mandatory update triggers** — update immediately on any of these events:
- A task is marked `[x]` done in `tasks.md`
- A new `[~]` task is started
- A blocker is discovered or resolved
- A human decision is made or a decision is unblocked
- A git commit is pushed

**Files that must stay current (≤ 5 min lag):**

| File | What to update |
|------|----------------|
| `.agents/commit-fhl/SESSION.md` | `lastCompletedTask`, `nextTask`, `blockers`, `confidence` |
| `.agents/commit-fhl/tasks.md` | Task status (`[ ]` → `[~]` → `[x]`) |
| `docs/Commit_Day{N}_Report.html` | Task row class, timestamp, timeline entry |
| `docs/Commit_Sprint_Dashboard.html` | Task counts, activity feed (newest entry at top), overall stats |

**Timeline entry format** (for day reports and dashboard):
```html
<div class="tl-item agent">
  <div class="tl-time">2026-03-0X HH:MM</div>
  <div class="tl-text">T-NNN complete — [one-line description]
    <span class="tl-tag">Agent</span>
  </div>
  <div class="tl-detail">[key files created, tests passing, acceptance criteria met]</div>
</div>
```

**Non-negotiable**: An agent session MUST NOT end without updating SESSION.md and the active day report. If you are about to run out of context, update the reports FIRST before doing any other cleanup.

---

### P-30 — Deployment Cadence

After every scenario-completing feature (a new end-to-end user flow verified in tests),
the system MUST be deployed to the target environment. "Runs on a laptop" is not done.
Done means users can access it. Deployment is part of the Definition of Done for all
scenario-level tasks. The target environment for Commit FHL is the E5 tenant
(7k2cc2.onmicrosoft.com) on Azure Container Apps + Static Web Apps.

- Deploy after: any task that completes a full user-visible scenario
- Target: `commit-fhl-rg` in Azure, East US region
- API: Azure Container Apps (consumption tier), port 8080
- Frontend: Azure Static Web Apps (Free tier)
- Teams: published to 7k2cc2 org catalog via manifest zip upload

---

### P-31 — Sentinel Integrity Protocol

An independent Sentinel verification MUST be run at the end of every agent session and at the
start of any session where the previous session did not run Sentinel.

**Role definition**: `.specify/memory/agent-roles/sentinel.md`
**Violation log**: `.specify/memory/sentinel-log.md`

**Non-negotiable requirements:**

1. SESSION.md must have a `Sentinel sign-off` field updated by Sentinel before session close
2. No session is complete without Sentinel sign-off — Router cannot approve session close
3. Sentinel findings override Router's session-close approval
4. Sentinel MUST fix violations it finds before signing off (or create blocking tasks if fix requires human input)
5. Stale live reports (P-29 violations) are CRITICAL severity — fix immediately, before all other work

**Sentinel authority:**
- Sentinel may add blocking tasks to `tasks.md` without Router approval
- Sentinel may update any governance document (SESSION.md, reports, tasks.md, decisions.md)
- Sentinel may escalate to human by adding `[BLOCKING]` items to `decisions.md`

**This is a high-stakes project.** A stale report read by a judge or VP during demo is a defect,
not an oversight. Sentinel exists to ensure that trust in these documents is earned, not assumed.

---

### P-32 — Zero Human Input (ZHIN) Protocol

The agent team's primary operating target is **zero human inputs** after the initial spec is approved.
Every blocker, ambiguity, and implementation decision is the agent's responsibility to resolve first.

**Agent self-resolution order** (exhausted before escalating):

1. **Check governance files**: Is it already decided in `decisions.md`? Does a constitution principle answer it?
2. **Try the direct path**: Can scripting, an API call, or a known tool pattern resolve it?
3. **Try an alternative approach**: Can a different implementation achieve the same outcome without the blocker?
4. **Check environment state**: Is the resource there but discovered differently? (e.g., list resources rather than guessing the name)
5. **Escalate only if all above fail** — and when escalating, surface a concrete solution for the human to choose from, not an open question.

**Human interaction is reserved for:**
- Business/product strategy decisions (what to build, not how)
- Ethics, security, and legal review gates
- External system credentials that cannot be scripted
- Physical actions (tenant admin console clicks, hardware, legal signatures)

**Measurement**: Track the ratio of human-escalated vs. self-resolved blockers in SESSION.md.
Target: ≥ 90% self-resolved within a sprint day.

---

### P-33 — Human-Agent Co-Team Contract

This project runs as a **human-agent co-team**, not as a human supervising an agent tool.
The contract below governs who owns what. Crossing these boundaries requires explicit negotiation.

**Human half of the team owns:**

| Domain | Examples |
|--------|---------|
| Strategic vision | What problem to solve, what success looks like, go/no-go decisions |
| Stakeholder relationships | Executive alignment, demo audience, customer conversations |
| Business/ethics gates | Privacy decisions, legal review, communication tone approval |
| Credentials & secrets | AAD app registrations, API keys, tenant admin actions |
| Final demo and presentation | Demo narrative, live talking points, live Q&A |

**Agent half of the team owns:**

| Domain | Examples |
|--------|---------|
| Architecture and implementation | All source code, infrastructure-as-code, build scripts |
| Quality and testing | Test authorship, coverage, mutation testing, E2E scripts |
| Deployment | Build + push + container update + frontend deploy |
| Governance and reporting | SESSION.md, tasks.md, dashboards, reports, Sentinel verification |
| Unblocking autonomously | Scripting credential retrieval, resource discovery, self-healing |

**The contract is NOT delegation.** It is co-ownership.
The human is an **active team member** who contributes domain expertise, decisions, and validation —
not a passive approver of agent output.

**Weekly cadence for human participation:**
- Daily: Read SESSION.md (5 min) — know what was shipped
- Mid-sprint: Review demo readiness (30 min) — course-correct if needed
- End-of-sprint: Live demo + retrospective (1h) — celebrate + extract learnings

---

### P-34 — Project Template Protocol

Commit FHL is the **reference implementation** for the human-agent co-team pattern.
Any new project built using this pattern must replicate the following governance kit:

**Minimum governance kit** (copy from `.specify/` and `.agents/` in this repo):

| File | Purpose |
|------|---------|
| `.specify/memory/constitution.md` | Engineering principles — adapt P-01 through P-27 to project; keep P-32/P-33/P-34 |
| `.specify/memory/agent-roles/` | All 9 role cards — customize file ownership per project |
| `.specify/memory/agent-inbox.md` | Inter-agent communication channel |
| `.specify/memory/tech-debt.md` | Tech debt tracker |
| `.agents/{project}/SESSION.md` | Current state — every session reads this first |
| `.agents/{project}/spec.md` | What we're building (human-authored) |
| `.agents/{project}/plan.md` | Architecture (agent-authored after spec is approved) |
| `.agents/{project}/tasks.md` | Full task list with `[x]` / `[~]` / `[ ]` / `[H]` status |
| `.agents/{project}/decisions.md` | Human decisions (answered) + pending (blocking) |
| `.agents/{project}/AGENT_INSTRUCTIONS.md` | Project-specific boot instructions for agents |

**4-phase project lifecycle** (proven on Commit FHL):

```
Phase 1 — SPECIFY  (1-2h):  Human describes the product. /speckit.specify + /speckit.clarify
Phase 2 — PLAN     (2-4h):  Agent architects. /speckit.plan → plan.md + decisions.md
Phase 3 — TASKS    (1h):    Agent breaks down work. /speckit.tasks → tasks.md (all [H] tagged)
Phase 4 — IMPLEMENT (days): Agent builds. /speckit.implement — resumes across sessions.
```

**Human time investment target**: ≤ 2h/day for a 5-day sprint (spec, pivots, demo).
**Agent time investment**: All remaining work.

**What makes this template portable:**
- Agent roles are domain-agnostic — swap file ownership, keep the governance model
- Constitution principles P-01 through P-15 are infrastructure-neutral — adapt to any stack
- speckit workflow is stack-neutral — runs on any project with a natural language spec
- ZHIN protocol (P-32) keeps humans out of the implementation loop without losing oversight

**Template repo**: `https://github.com/Sampath-K/commit-fhl` is the reference.
When starting a new project: fork the `.specify/` and `.agents/` directory structure,
update the spec, re-run speckit.plan and speckit.tasks, then /speckit.implement.

---

### P-35 — Expanded Agent Roster

The baseline agent roster has expanded from 7 to 9 to cover the full project lifecycle:

| Agent | Role | Activates |
|-------|------|----------|
| **Router** | PM + Tech Lead | Every phase — coordinates, routes, reviews |
| **Forge** | Backend Engineer | Phase 4 (implement): all server-side code |
| **Canvas** | Frontend Engineer | Phase 4 (implement): all UI code |
| **Shield** | Platform/DevOps | Phase 3+4: infra, CI/CD, deployment |
| **Lens** | QA/SDET | Phase 4: tests, coverage, E2E |
| **Seed** | Demo/Data | Phase 4 end: seed scripts, demo content |
| **Sentinel** | Integrity verifier | End of every session (P-31) |
| **Recon** | Research analyst | Phase 1+2: market research, competitive analysis, user research |
| **Oracle** | Analytics engineer | Phase 4+ (live): telemetry, KPIs, usage dashboards, A/B tests |

**Recon responsibilities:**
- Competitive landscape analysis for any new feature or product direction
- User research synthesis (interviews → insights → spec input)
- Technology research (evaluate libraries, patterns, third-party APIs before Forge commits)
- Input to `spec.md` and `plan.md` via `.specify/memory/agent-inbox.md`
- Owns: `.specify/memory/research/` directory

**Oracle responsibilities:**
- Telemetry dashboard implementation (`docs/analytics-dashboard.html`)
- KPI tracking: commitments captured, resolution rate, cascade detection accuracy, approval rate
- A/B test configuration and result analysis
- Weekly sprint analytics report (appended to day reports)
- Owns: `scripts/analytics/`, `docs/analytics-dashboard.html`

**When to activate each agent:**
- **Recon**: Activate before any major spec change, new feature area, or competitor concern
- **Oracle**: Activate when the system is live in pilot and usage data is available
- All other agents: same triggers as before (see P-18)

**Single-agent sessions**: One agent playing all roles is valid. Use role cards as checklists,
not as hard parallelism requirements. The governance model works whether there is 1 agent or 9.

---

### P-36 — Teams App Package Version Bump

Every change to `appPackage/manifest.json` — regardless of how small — **must** be accompanied
by a version bump in the `"version"` field before rebuilding `commitai.zip`. No exceptions.

**Rule**: `manifest.json version` must increase on every commit that touches anything in `appPackage/`.

**Why**: Teams Admin Center rejects uploads where the version matches an already-installed version,
causing the upload to silently fail or error. The version is the only signal Teams uses to detect
that an update is available.

**Version format**: Semantic versioning `MAJOR.MINOR.PATCH` — use PATCH increments for fixes,
MINOR increments for new capabilities (new tabs, connectors, scopes).

**Required sequence** — agents must follow this order on every `appPackage/` change:

1. Edit `appPackage/manifest.json` — make the intended change
2. Bump `"version"` in the same edit (e.g., `1.0.3` → `1.0.4`)
3. Delete and recreate `appPackage/commitai.zip`:
   ```powershell
   Remove-Item appPackage/commitai.zip -Force
   Compress-Archive -Path appPackage/manifest.json,appPackage/color.png,appPackage/outline.png `
     -DestinationPath appPackage/commitai.zip -Force
   ```
4. Commit both `manifest.json` and `commitai.zip` together in the same commit
5. Notify the human that a new zip is ready to upload to Teams Admin Center

**Sentinel check**: Sentinel must verify that the version in `manifest.json` is strictly greater
than the version in the previous commit whenever `appPackage/` files are modified.
A version that was not bumped is a **CRITICAL** violation (blocks demo upload).

---

*This constitution governs all agents. Amendments require human approval and a version bump.*
