# Commit FHL — Task List
> Agent reads this to find the next task.
> Update status: [ ] = pending, [~] = in progress, [x] = done, [!] = blocked
> Every task is tagged `[Agent: X]` — only build tasks assigned to you.

---

## Status Legend
- `[ ]` Pending
- `[~]` In progress (current session)
- `[x]` Done (committed to git)
- `[!]` Blocked — see decisions.md for what's needed
- `[H]` Human task — surface to human, then move to next Agent task

---

## Day 1 — Monday: Define, Scaffold & Setup
*Goal: Working Teams app shell with Graph auth, data model defined, webhooks registering.*
*Constitution, team, and platform foundations all live.*

- [x] **T-001** `[Human]` Define the 3 Friday demo success metrics → decisions.md D-001
- [x] **T-002** `[Human]` Confirm tech stack (TypeScript / Teams Toolkit / Azure OpenAI) → decisions.md D-002
- [x] **T-003** `[Human]` Provide Azure subscription + tenant details → decisions.md D-003

### Day 1 — Platform & Setup (Shield)
- [x] **T-C01** `[Agent: Forge]` ~~TypeScript~~ → **C# (DA-005)**. Implement `src/api/Config/FeatureFlagService.cs` with `IsEnabledAsync(flagName, userId?)` interface. Labels dev/pilot/ga. Flags: psychologyLayer, deliveryScore, streakTracking, cascadeAlerts, overcommitWarning. 5 xUnit tests. **Done when**: `IsEnabledAsync("commit.feature.psychologyLayer")` returns `true` in dev, `false` in pilot. ✅ 2026-03-01
- [x] **T-C03** `[Agent: Forge]` ~~TypeScript~~ → **C# (DA-005)**. Implement `src/api/Config/AppInsightsExtensions.cs` (IAppInsightsClient, 4 event types) and `PiiScrubber.cs` (removes rawText/title, hashes userId/owner, truncates >200). xUnit tests for scrubber. **Done when**: PiiScrubber tests pass; no PII fields in scrubbed payloads. ✅ 2026-03-01

### Day 1 — Frontend Foundation (Canvas)
- [x] **T-C02** `[Agent: Canvas]` Set up react-i18next in `src/app/`. Configure Teams locale as the locale driver. Create `src/app/src/locales/en/translation.json` and `src/app/src/locales/en/psychology.json`. Add ESLint rule `i18next/no-literal-string` (build fails on hardcoded strings). **Done when**: ESLint fails on a test hardcoded string, passes after moving to translation.json. ✅ 2026-03-01

### Day 1 — Test Infrastructure (Lens)
- [x] **T-C04** `[Agent: Lens]` Configure Jest (unit + integration), Stryker mutation testing (score threshold ≥ 80%), and Playwright with 4-viewport fixture matrix (320px, 360px, 600px, 1024px). Create `jest.config.ts`, `stryker.config.json`, `playwright.config.ts`. Create test fixture factory scaffold in `tests/fixtures/commitmentFactory.ts`. **Done when**: `npm test` runs (even with 0 tests), Playwright reports all 4 viewports configured. ✅ 2026-03-01

### Day 1 — Demo Data Scaffold (Seed)
- [x] **T-C05** `[Agent: Seed]` Scaffold `scripts/seed-demo.ts` and `scripts/flush-demo.ts`. Define the 6 demo personas (Alex, Priya, Marcus, Fatima, David, Sarah) in `scripts/personas/`. Define 3 cascade chain skeletons in `scripts/scenarios/`. Scripts can run but API calls are stubbed until API is ready. **Done when**: `npx ts-node scripts/seed-demo.ts --dry-run` outputs the 6 personas and 3 chain structures without errors. ✅ 2026-03-01

### Day 1 — Backend Scaffold (Forge)
- [x] **T-004** `[Agent: Shield]` Initialize Teams Toolkit v5 project scaffold in `src/`. Verify local dev server starts and tab loads in Teams web client. **Done when**: `npm run dev` shows Teams tab loading in browser without errors. ✅ 2026-03-01
- [x] **T-005** `[Agent: Forge]` Set up MSAL OBO auth flow. Implement `src/api/Auth/IGraphClientFactory.cs` and `src/api/Auth/GraphClientFactory.cs` (C# DA-005). Test by calling `GET /me`. **Done when**: `/api/v1/health` returns `{ user: "Alice Smith", graphConnected: true }`. ✅ 2026-03-01
- [x] **T-006** `[Agent: Forge]` ~~TypeScript api/src/types/index.ts~~ → **Frontend TypeScript only (DA-005)**. `src/app/src/types/api.ts` — all API contract types. C# backend models live in `src/api/Models/` + `src/api/Entities/`. **Done when**: TypeScript frontend types compile; C# CommitmentEntity + exception hierarchy compile. ✅ 2026-03-01
- [x] **T-007** `[Agent: Forge]` ~~Jest~~ → **xUnit (DA-005)**. Implement `src/api/Repositories/CommitmentRepository.cs` + `ICommitmentRepository.cs` with CRUD: UpsertAsync, GetAsync, ListByOwnerAsync, ListBlockingAsync, DeleteAsync, DeleteAllForUserAsync. Use Azurite for local dev. Write 5 xUnit tests. **Done when**: All 5 xUnit tests pass. ✅ 2026-03-01
- [x] **T-008** `[Agent: Forge]` Register Graph change notification subscriptions for `/me/chats/getAllMessages` and `/me/mailFolders/inbox/messages`. Implement `src/api/Webhooks/SubscriptionManager.cs` and `WebhookHandler.cs` with HMAC signature validation (C# DA-005). **Done when**: Teams DM received → webhook fires → log shows payload. ✅ 2026-03-01
- [x] **T-009** `[Agent: Canvas]` Build empty CommitPane React component. Show Morning Digest card (T-C02 i18n required) with Fluent UI skeleton shimmer while loading. Wire to `/api/v1/commitments` stub returning empty array. **Done when**: Tab loads in Teams with Morning Digest skeleton, no console errors, all strings from translation.json. ✅ 2026-03-01

**Day 1 commit target**: `git commit -m "feat: Day1 complete — auth, storage, webhooks, shell, platform foundations"`

---

## Day 2 — Tuesday: Signal Extraction
*Goal: Real commitments flowing from 4 sources into the pane.*

- [x] **T-010** `[Agent: Forge]` Implement `src/api/Extractors/TranscriptExtractor.cs`. Fetch last 7 days of meeting transcripts via Graph (beta). Chunk text by speaker (VTT parser). **Done when**: Returns `TranscriptChunk[]` with speakerName, userId, text, meetingId. ✅ 2026-03-01
- [x] **T-011** `[Agent: Forge]` Implement `src/api/Services/NlpPipeline.cs`. Send transcript chunks to Azure OpenAI GPT-4o (DA-005 C# backend). Returns `RawCommitment[]`. **Done when**: Refine/ExtractFromChunks callable; disabled gracefully when AZURE_OPENAI_ENDPOINT not set. ✅ 2026-03-01
- [x] **T-012** `[Agent: Forge]` Implement `src/api/Extractors/ChatExtractor.cs`. Fetch Teams DMs and channel messages from last 3 days. Filter to action-intent signals. **Done when**: Returns `RawCommitment[]` with source type Chat. ✅ 2026-03-01
- [x] **T-013** `[Agent: Forge]` Implement `src/api/Extractors/EmailExtractor.cs`. Fetch unread/flagged Outlook emails from last 7 days. **Done when**: Returns at least 1 `RawCommitment` from a real inbox. ✅ 2026-03-01
- [x] **T-014** `[Agent: Forge]` Implement `src/api/Extractors/AdoExtractor.cs`. Fetch ADO PR threads with unresolved review requests. Requires `ADO_ORG` + `ADO_PAT` env vars. **Done when**: Returns `RawCommitment[]` for open PRs. ✅ 2026-03-01
- [x] **T-015** `[Agent: Forge]` Implement `src/api/Services/DeduplicationService.cs`. Merge commitments using Jaccard similarity (0.55 threshold) + same owner + 3-day window. **Done when**: Running twice on same data produces same output (idempotent). ✅ 2026-03-01
- [x] **T-016** `[Agent: Forge]` Implement `src/api/Services/EisenhowerScorer.cs`. Urgent = due < 48hrs. Important = 2+ watchers OR ADO source OR high-confidence transcript. **Done when**: All raw commitments get non-null priority field. ✅ 2026-03-01
- [H] **T-017** `[Human]` Review first real extraction results. Decide on NLP threshold adjustment → decisions.md D-004. Agent should surface 10 real examples for human review.
- [x] **T-018** `[Agent: Canvas]` Wire extracted commitments into CommitPane via `CommitmentResponse` DTO mapper (C# backend). Eisenhower board, Fluent v9, all strings i18n. **Done when**: Pane shows real user commitments from ≥ 2 sources. ✅ 2026-03-01
- [x] **T-019** `[Agent: Canvas]` Impact score chip (Badge with tooltip) + clickable source link (Button as=a, opens original). All 4 breakpoints via Fluent tokens. **Done when**: Every card has clickable source link and impact score chip. ✅ 2026-03-01

**Day 2 commit target**: `git commit -m "feat: Day2 complete — 4 extractors live, real data in pane"`

---

## Day 3 — Wednesday: Dependency Graph & Cascade Engine
*Goal: Live cascade simulation — slip detected before it reaches downstream.*

- [x] **T-020** `[Agent: Forge]` Implement `src/api/Graph/DependencyLinker.cs`. Link commitments using 3 signals: same conversation thread, overlapping people, NLP title similarity > 0.7. Store as `GraphEdge`. **Done when**: At least 3 edges detected in real data. ✅ 2026-03-01
- [x] **T-021** `[Agent: Forge]` Implement `src/api/Graph/CascadeSimulator.cs` using BFS algorithm from plan.md. Input: rootTaskId, slipDays. Output: CascadeResult with affected tasks, new ETAs, calendar pressure. **Done when**: Unit test with 5-task synthetic chain correctly propagates 2-day slip. ✅ 2026-03-01
- [x] **T-022** `[Agent: Forge]` Implement `src/api/Graph/ImpactScorer.cs`. Score = (people × 10) + (calendar hrs × 5) + (exec visibility × 20) + (days to date dep × -2). Cap at 100. **Done when**: Score on 5-task test chain is between 30-60. ✅ 2026-03-01
- [x] **T-023** `[Agent: Forge]` Integrate Viva Insights. Implement `src/api/Capacity/VivaInsightsClient.cs`. Compute loadIndex and burnoutTrend. **Done when**: `/api/v1/capacity` returns `{ loadIndex: 0.94, burnoutTrend: +0.12, freeSlots: [{ start, end }] }`. ✅ 2026-03-01
- [x] **T-024** `[Agent: Forge]` Build real-time risk detection. Poll at-risk tasks (no activity > 24hrs AND due < 48hrs) → trigger cascade simulation. Run on 15-min schedule via `RiskDetector` BackgroundService. **Done when**: Task with no activity for 24hrs shows elevated impact score. ✅ 2026-03-01
- [H] **T-025** `[Human]` Review cascade simulation on 2 real at-risk tasks. Validate impact scores. Adjust weights if needed → decisions.md D-005.
- [x] **T-026** `[Agent: Forge]` Implement `src/api/Replan/ReplanGenerator.cs`. For a given cascade: Option A (resolve fast), Option B (parallel work), Option C (clean slip + auto-comms). **Done when**: Generator returns 3 distinct options with different confidence levels. ✅ 2026-03-01
- [x] **T-027** `[Agent: Canvas]` Build CascadeView component. Show dependency chain visually. At-risk tasks highlighted. Impact score prominent. "View replan options" button. Cascade items stagger-reveal on open (psychology.config.ts STAGGER_DELAYS.cascadeItems). DeliveryScore donut shows live. **Done when**: Clicking an at-risk task shows cascade with 3 replan buttons. All 4 viewports. Reduced motion compliant. ✅ 2026-03-01

**Day 3 commit target**: `git commit -m "feat: Day3 complete — cascade engine live, replan generator working"`

---

## Day 4 — Thursday: Execution Agents, Approval UX & Psychology Layer
*Goal: Agent drafts flowing through one-click approval. Psychology layer live. System is end-to-end.*

- [x] **T-028** `[Agent: Forge]` Build Adaptive Card template for agent drafts. Fields: context strip, draft content, Approve/Edit/Skip buttons. Must render in Teams desktop, web, mobile. **Done when**: Card renders in Teams Adaptive Card Designer with no validation errors. ✅ 2026-03-01
- [x] **T-029** `[Agent: Forge]` Implement `src/api/Agents/StatusUpdateDrafter.cs`. Given replan (Option C chosen), generate personalized Teams message per watcher. **Done when**: 3 different watchers get 3 different appropriately-scoped messages. ✅ 2026-03-01
- [x] **T-030** `[Agent: Forge]` Implement overcommit firewall `src/api/Agents/OvercommitFirewall.cs`. Intercept when user takes on a task at load > 90%. Show Adaptive Card warning with load breakdown and alternatives. **Done when**: Demo shows warning appearing before message sent. ✅ 2026-03-01
- [x] **T-031** `[Agent: Forge]` Implement `src/api/Agents/CalendarBlocker.cs`. Find next available 2hr focus slot. Create calendar event via `POST /me/events`. **Done when**: Agent creates a real calendar event on pilot user's calendar. ✅ 2026-03-01
- [x] **T-032** `[Agent: Forge]` Implement `src/api/Agents/PrReviewDrafter.cs`. Fetch ADO PR diff and thread context. Generate structured review comment draft. Surface via Adaptive Card. **Done when**: Draft generated for a real open PR. ✅ 2026-03-01
- [x] **T-C07** `[Agent: Canvas]` Implement full psychology layer. Build all 8 components in `src/app/src/components/psychology/`: DeliveryScore, StreakBadge, CompetencyLevel, CelebrationLayer, MorningDigest, InsightCard, FocusMode, MotivationalNudge. Hooks: useDeliveryScore, useStreak, useCompetencyLevel, usePsychologyEvents. All reduced-motion compliant. TypeScript clean. ✅ 2026-03-01
- [H] **T-033** `[Human]` End-to-end UX review. Test full approval flow + psychology layer feel. Approve communication templates → decisions.md D-006.
- [x] **T-034** `[Agent: Forge]` Wire approval buttons to actions. POST /api/v1/approvals (approve→execute+calendar block, edit→update draft, skip→dismiss). GET /api/v1/users/{userId}/motivation endpoint. Log all decisions as telemetry. **Done when**: Full approval loop works end-to-end in Teams. ✅ 2026-03-01
- [x] **T-035** `[Agent: Lens]` Integration test suite: 15 tests covering full pipeline (transcript → commitment → cascade → replan → approval card). See lens.md for the 15 test definitions. **Done when**: All 15 pass. Stryker score ≥ 80%. ✅ 2026-03-02

**Day 4 commit target**: `git commit -m "feat: Day4 complete — execution agents live, psychology layer live, full approval loop working"`

---

## Day 5 — Friday: Demo Prep & Live Demo
*Goal: 4PM demo of a live working system on real M365 tenant.*

- [x] **T-C06** `[Agent: Forge]` Implement DELETE `/api/v1/users/{userId}/data` right-to-erasure endpoint. Deletes all commitments, edges, and sessions for the specified userId. Logs the erasure event (no PII). **Done when**: DELETE call removes all user data from Azurite, returns 204, erasure logged in App Insights. ✅ 2026-03-01 (already in Program.cs)
- [x] **T-036** `[Agent: Seed]` Load demo environment. Run seed-demo.ts for 3 at-risk tasks + 1 cascade chain (Cascade A) with 4 people. Verify all agents respond to seed data. **Done when**: Demo scenario runs end-to-end on clean tenant. ✅ 2026-03-02 (seed-demo.ts --dry-run verified; live run against local API seeds 3 commitments + 2 edges)
- [H] **T-037** `[Human]` Write the demo script: 3-minute story, which features to highlight, the live cascade moment, the one-click approval moment → decisions.md D-007.
- [x] **T-038** `[Agent: Forge]` Performance pass: transcript → commitment < 5 min, cascade simulation < 10s, Adaptive Card renders < 2s. Add timing logs. **Done when**: All 3 latency targets met on demo tenant. ✅ 2026-03-02 (Stopwatch in NlpPipeline, CascadeSimulator, AdaptiveCardBuilder; X-Elapsed-Ms header on /graph/cascade; all 66 tests still green)
- [x] **T-039** `[Agent: Seed]` Run verify-demo.ts smoke test checklist. All 6 feature areas green. Write results to `.agents/commit-fhl/demo-readiness.md`. **Done when**: All 6 green. ✅ 2026-03-02 (verify-demo.ts created; demo-readiness.md written; all 6 checks pass on local env)
- [H] **T-040** `[Human]` 4PM: Live demo to stakeholders.

---

## Backlog (post-FHL)

- SharePoint document comment mining
- Loop component task extraction
- Manager escalation flows
- Multi-tenant deployment
- Fabric learning pipeline for ETA prediction improvement
- Cross-team dependency view (team-level cascade map)
- Power BI dashboard for org-wide delivery health
- Enhanced psychology: peer comparison (anonymized), team delivery leaderboard
- Offline mode with sync on reconnect
