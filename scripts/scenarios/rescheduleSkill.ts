/**
 * Scenario: Reschedule BizChat Skill — Q1 Ship
 *
 * Project story: The team is delivering a new BizChat skill that lets users
 * reschedule meetings via natural language in Microsoft Copilot. The project
 * has been running for ~30 days with a hard Q1 ship date of 2026-03-30.
 *
 * Dependencies (and current risk posture):
 *   Scheduling Skill SDK  — ✅ Integration done, unit tests in progress
 *   BizChat Platform      — ⚠️  Plugin manifest in progress, routing pending
 *   Foundry Test Runs     — 🔴 71% accuracy (need 85%), gate is 2026-03-07
 *   SEVALs                — 🔴 Reviewer feedback outstanding, due 2026-03-06 (main cascade root)
 *   Scorecards            — ✅  KPIs defined, pipeline pending
 *
 * Key cascade: rbs-seval-002 slips ≥5d → misses Q1 ship date (rbs-int-005).
 */
import type { CommitmentRecord, GraphEdge } from '../../src/app/src/types/api';
import { PERSONAS } from '../personas/index';

const alex   = PERSONAS.find(p => p.id === 'alex')!;   // Lead engineer on Reschedule Skill
const priya  = PERSONAS.find(p => p.id === 'priya')!;  // EM
const marcus = PERSONAS.find(p => p.id === 'marcus')!; // BizChat platform integration
const fatima = PERSONAS.find(p => p.id === 'fatima')!; // PM
const david  = PERSONAS.find(p => p.id === 'david')!;  // Director / exec sponsor
const sarah  = PERSONAS.find(p => p.id === 'sarah')!;  // Scheduling Skill team (cross-team)

// ─── Commitment IDs ─────────────────────────────────────────────────────────
export const IDS = {
  // A: Design & Architecture
  archDesignDoc:       'rbs-arch-001',
  archSignOff:         'rbs-arch-002',
  // B: Scheduling Skill
  schedApiExpose:      'rbs-sched-001',
  schedSdkIntegrate:   'rbs-sched-002',
  schedUnitTests:      'rbs-sched-003',
  // C: BizChat Platform
  bcpSlotAllocation:   'rbs-bcp-001',
  bcpPluginManifest:   'rbs-bcp-002',
  bcpGraphRouting:     'rbs-bcp-003',
  bcpE2eTest:          'rbs-bcp-004',
  // D: Foundry
  foundryBaseline:     'rbs-foundry-001',
  foundryAccuracyGate: 'rbs-foundry-002',  // AT RISK — 71% vs 85% target
  foundryProdRun:      'rbs-foundry-003',
  // E: SEVALs (main cascade root)
  sevalInitialSubmit:  'rbs-seval-001',
  sevalFeedback:       'rbs-seval-002',  // AT RISK — main cascade trigger
  sevalReReview:       'rbs-seval-003',
  sevalShipChecklist:  'rbs-seval-004',
  // F: Scorecards
  scoreKpis:           'rbs-score-001',
  scorePipeline:       'rbs-score-002',
  scoreReview:         'rbs-score-003',
  // G: Integration & Ship
  intCodeComplete:     'rbs-int-001',
  intShipChecklist:    'rbs-int-002',
  intDogfoodDeploy:    'rbs-int-003',
  intDogfoodMonitor:   'rbs-int-004',
  intShipDecision:     'rbs-int-005',
};

// ─── Date helpers ────────────────────────────────────────────────────────────
// Today = 2026-03-02
const past  = (daysAgo: number,  h = 10) => `2026-${pad(3 - Math.ceil(daysAgo / 31 + 0), daysAgo)}-${h}:00:00Z`;
const future = (daysOut: number, h = 17) => futureDate(daysOut, h);

function pad(m: number, d: number): string {
  // Build ISO date from base 2026-03-02
  const base = new Date('2026-03-02T00:00:00Z');
  base.setUTCDate(base.getUTCDate() - d);
  return base.toISOString().substring(0, 10);
}

function futureDate(daysOut: number, hour: number): string {
  const base = new Date('2026-03-02T00:00:00Z');
  base.setUTCDate(base.getUTCDate() + daysOut);
  return `${base.toISOString().substring(0, 10)}T${hour.toString().padStart(2, '0')}:00:00Z`;
}

const d = (daysAgo: number, hour = 10) => `${pad(0, daysAgo)}T${hour.toString().padStart(2, '0')}:00:00Z`;

export const RESCHEDULE_SKILL_COMMITMENTS: Omit<CommitmentRecord, 'agentDraft'>[] = [

  // ────────────────────────────────────────────────────────────────────────────
  // TRACK A: Design & Architecture (COMPLETED)
  // ────────────────────────────────────────────────────────────────────────────

  {
    id:    IDS.archDesignDoc,
    title: 'Write architecture design doc for Reschedule BizChat Skill v1.0',
    owner: alex.userId,
    watchers: [priya.userId, fatima.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-kickoff-2026',
      timestamp: d(30, 10),
      sourceId:  'rbs-kickoff-meeting',
    },
    committedAt: d(30, 10),
    dueAt:       d(23, 17),
    status:      'done',
    priority:    'urgent-important',
    blockedBy:   [],
    blocks:      [IDS.archSignOff],
    impactScore:         35,
    burnoutContribution: 3,
    lastActivity:        d(24, 16),
    ownerDeliveryScoreAtCreation: 72,
  },

  {
    id:    IDS.archSignOff,
    title: 'Review and sign off on Reschedule Skill architecture doc — approve to proceed',
    owner: priya.userId,
    watchers: [fatima.userId, david.userId],
    source: {
      type:      'email',
      url:       'https://outlook.office.com/mail/rbs-design-review',
      timestamp: d(24, 9),
      sourceId:  'rbs-design-review-email',
    },
    committedAt: d(24, 9),
    dueAt:       d(21, 17),
    status:      'done',
    priority:    'urgent-important',
    blockedBy:   [IDS.archDesignDoc],
    blocks:      [IDS.schedApiExpose, IDS.bcpSlotAllocation],
    impactScore:         40,
    burnoutContribution: 2,
    lastActivity:        d(21, 14),
    ownerDeliveryScoreAtCreation: 81,
  },

  // ────────────────────────────────────────────────────────────────────────────
  // TRACK B: Scheduling Skill Dependency
  // ────────────────────────────────────────────────────────────────────────────

  {
    id:    IDS.schedApiExpose,
    title: 'Expose RescheduleEvent() API in Scheduling Skill SDK v3.2 (cross-team commitment)',
    owner: sarah.userId,
    watchers: [alex.userId, priya.userId],
    source: {
      type:      'ado',
      url:       'https://dev.azure.com/msft/BizChat/_workitems/edit/89441',
      timestamp: d(21, 9),
      sourceId:  'ado-scheduling-skill-89441',
    },
    committedAt: d(21, 9),
    dueAt:       d(12, 17),
    status:      'done',
    priority:    'urgent-important',
    blockedBy:   [IDS.archSignOff],
    blocks:      [IDS.schedSdkIntegrate],
    impactScore:         72,
    burnoutContribution: 5,
    lastActivity:        d(12, 15),
    ownerDeliveryScoreAtCreation: 68,
  },

  {
    id:    IDS.schedSdkIntegrate,
    title: 'Integrate SchedulingSkill SDK v3.2 NuGet package into Reschedule Skill project',
    owner: alex.userId,
    watchers: [sarah.userId, priya.userId],
    source: {
      type:      'chat',
      url:       'https://teams.microsoft.com/l/chat/rbs-alex-sarah',
      timestamp: d(12, 16),
      sourceId:  'rbs-sdk-integration-chat',
    },
    committedAt: d(12, 16),
    dueAt:       d(6, 17),
    status:      'done',
    priority:    'urgent-important',
    blockedBy:   [IDS.schedApiExpose],
    blocks:      [IDS.schedUnitTests, IDS.foundryAccuracyGate],
    impactScore:         65,
    burnoutContribution: 4,
    lastActivity:        d(6, 11),
    ownerDeliveryScoreAtCreation: 74,
  },

  {
    id:    IDS.schedUnitTests,
    title: 'Write unit tests for RescheduleEvent integration — target 80% code coverage',
    owner: alex.userId,
    watchers: [priya.userId, sarah.userId],
    source: {
      type:      'ado',
      url:       'https://dev.azure.com/msft/BizChat/_workitems/edit/91203',
      timestamp: d(6, 9),
      sourceId:  'ado-rbs-unit-tests-91203',
    },
    committedAt: d(6, 9),
    dueAt:       future(3, 17),
    status:      'in-progress',
    priority:    'urgent-important',
    blockedBy:   [IDS.schedSdkIntegrate],
    blocks:      [IDS.intCodeComplete],
    impactScore:         55,
    burnoutContribution: 4,
    lastActivity:        d(1, 17),
    ownerDeliveryScoreAtCreation: 76,
  },

  // ────────────────────────────────────────────────────────────────────────────
  // TRACK C: BizChat Platform Integration
  // ────────────────────────────────────────────────────────────────────────────

  {
    id:    IDS.bcpSlotAllocation,
    title: 'Secure BizChat Platform team slot allocation for Reschedule plugin (Q1 capacity)',
    owner: fatima.userId,
    watchers: [priya.userId, david.userId],
    source: {
      type:      'email',
      url:       'https://outlook.office.com/mail/rbs-platform-capacity-request',
      timestamp: d(21, 14),
      sourceId:  'rbs-platform-capacity-email',
    },
    committedAt: d(21, 14),
    dueAt:       d(16, 17),
    status:      'done',
    priority:    'not-urgent-important',
    blockedBy:   [IDS.archSignOff],
    blocks:      [IDS.bcpPluginManifest],
    impactScore:         48,
    burnoutContribution: 2,
    lastActivity:        d(17, 10),
    ownerDeliveryScoreAtCreation: 83,
  },

  {
    id:    IDS.bcpPluginManifest,
    title: 'Implement BizChat plugin manifest for Reschedule Skill (actions, entities, card templates)',
    owner: marcus.userId,
    watchers: [alex.userId, fatima.userId],
    source: {
      type:      'ado',
      url:       'https://dev.azure.com/msft/BizChat/_workitems/edit/90117',
      timestamp: d(14, 9),
      sourceId:  'ado-bcp-plugin-manifest-90117',
    },
    committedAt: d(14, 9),
    dueAt:       future(7, 17),
    status:      'in-progress',
    priority:    'urgent-important',
    blockedBy:   [IDS.bcpSlotAllocation],
    blocks:      [IDS.bcpGraphRouting],
    impactScore:         60,
    burnoutContribution: 5,
    lastActivity:        d(2, 14),
    ownerDeliveryScoreAtCreation: 71,
  },

  {
    id:    IDS.bcpGraphRouting,
    title: 'Complete BizChat graph routing rules for Reschedule intent detection and disambiguation',
    owner: marcus.userId,
    watchers: [alex.userId, priya.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-bcp-weekly',
      timestamp: d(7, 10),
      sourceId:  'rbs-bcp-weekly-meeting',
    },
    committedAt: d(7, 10),
    dueAt:       future(12, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.bcpPluginManifest],
    blocks:      [IDS.bcpE2eTest],
    impactScore:         70,
    burnoutContribution: 5,
    lastActivity:        d(7, 10),
    ownerDeliveryScoreAtCreation: 69,
  },

  {
    id:    IDS.bcpE2eTest,
    title: 'End-to-end test Reschedule Skill in BizChat staging — all utterance types passing',
    owner: alex.userId,
    watchers: [marcus.userId, priya.userId, fatima.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-milestone-review',
      timestamp: d(4, 14),
      sourceId:  'rbs-milestone-review',
    },
    committedAt: d(4, 14),
    dueAt:       future(16, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.bcpGraphRouting, IDS.foundryProdRun],
    blocks:      [IDS.intCodeComplete],
    impactScore:         78,
    burnoutContribution: 6,
    lastActivity:        d(4, 14),
    ownerDeliveryScoreAtCreation: 74,
  },

  // ────────────────────────────────────────────────────────────────────────────
  // TRACK D: Foundry Test Runs  (AT RISK — 71% accuracy, need 85%)
  // ────────────────────────────────────────────────────────────────────────────

  {
    id:    IDS.foundryBaseline,
    title: 'Run Foundry baseline evaluation against SchedulingSkill mock (Prompt Flow dataset v1)',
    owner: sarah.userId,
    watchers: [alex.userId, priya.userId],
    source: {
      type:      'ado',
      url:       'https://dev.azure.com/msft/BizChat/_workitems/edit/91088',
      timestamp: d(12, 10),
      sourceId:  'ado-foundry-baseline-91088',
    },
    committedAt: d(12, 10),
    dueAt:       d(6, 17),
    status:      'done',
    priority:    'not-urgent-important',
    blockedBy:   [],
    blocks:      [IDS.foundryAccuracyGate],
    impactScore:         42,
    burnoutContribution: 3,
    lastActivity:        d(6, 9),
    ownerDeliveryScoreAtCreation: 70,
  },

  {
    id:    IDS.foundryAccuracyGate,
    // 🔴 AT RISK — currently 71%, need 85% by 2026-03-07
    title: 'Achieve ≥85% intent detection accuracy in Foundry eval (current: 71% — gap: 14pp)',
    owner: alex.userId,
    watchers: [sarah.userId, priya.userId, fatima.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-foundry-sync',
      timestamp: d(5, 10),
      sourceId:  'rbs-foundry-quality-sync',
    },
    committedAt: d(5, 10),
    dueAt:       future(5, 17),
    status:      'in-progress',
    priority:    'urgent-important',
    blockedBy:   [IDS.schedSdkIntegrate, IDS.foundryBaseline],
    blocks:      [IDS.foundryProdRun],
    impactScore:         85,
    burnoutContribution: 7,
    lastActivity:        d(1, 11),
    ownerDeliveryScoreAtCreation: 74,
  },

  {
    id:    IDS.foundryProdRun,
    title: 'Foundry production evaluation run — final quality gate sign-off (≥85% required)',
    owner: alex.userId,
    watchers: [priya.userId, david.userId],
    source: {
      type:      'ado',
      url:       'https://dev.azure.com/msft/BizChat/_workitems/edit/91312',
      timestamp: d(4, 9),
      sourceId:  'ado-foundry-prod-run-91312',
    },
    committedAt: d(4, 9),
    dueAt:       future(7, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.foundryAccuracyGate],
    blocks:      [IDS.bcpE2eTest],
    impactScore:         80,
    burnoutContribution: 5,
    lastActivity:        d(4, 9),
    ownerDeliveryScoreAtCreation: 74,
  },

  // ────────────────────────────────────────────────────────────────────────────
  // TRACK E: SEVALs — MAIN CASCADE ROOT (🔴 AT RISK)
  // ────────────────────────────────────────────────────────────────────────────

  {
    id:    IDS.sevalInitialSubmit,
    title: 'Submit Reschedule Skill SEVAL package — initial submission to Responsible AI team',
    owner: fatima.userId,
    watchers: [priya.userId, alex.userId],
    source: {
      type:      'email',
      url:       'https://outlook.office.com/mail/rbs-seval-submission-confirm',
      timestamp: d(10, 14),
      sourceId:  'rbs-seval-submission-email',
    },
    committedAt: d(10, 14),
    dueAt:       d(3, 17),
    status:      'done',
    priority:    'urgent-important',
    blockedBy:   [],
    blocks:      [IDS.sevalFeedback],
    impactScore:         58,
    burnoutContribution: 4,
    lastActivity:        d(4, 15),
    ownerDeliveryScoreAtCreation: 83,
  },

  {
    id:    IDS.sevalFeedback,
    // 🔴 MAIN CASCADE ROOT — no progress in 2 days, due 2026-03-06, cascades to Q1 ship
    title: 'Address SEVAL reviewer feedback: refusal case handling, prompt hardening, adversarial probes',
    owner: alex.userId,
    watchers: [fatima.userId, priya.userId, sarah.userId],
    source: {
      type:      'email',
      url:       'https://outlook.office.com/mail/rbs-seval-reviewer-feedback',
      timestamp: d(3, 10),
      sourceId:  'rbs-seval-feedback-email',
    },
    committedAt: d(3, 10),
    dueAt:       future(4, 17),
    status:      'in-progress',
    priority:    'urgent-important',
    blockedBy:   [IDS.sevalInitialSubmit],
    blocks:      [IDS.sevalReReview],
    impactScore:         88,
    burnoutContribution: 8,
    lastActivity:        d(2, 9),   // no activity in 2 days — at risk signal
    ownerDeliveryScoreAtCreation: 74,
  },

  {
    id:    IDS.sevalReReview,
    title: 'SEVAL re-review by Responsible AI team — await final approval decision',
    owner: fatima.userId,
    watchers: [priya.userId, david.userId, alex.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-seval-review',
      timestamp: d(2, 14),
      sourceId:  'rbs-seval-review-meeting',
    },
    committedAt: d(2, 14),
    dueAt:       future(9, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.sevalFeedback],
    blocks:      [IDS.sevalShipChecklist, IDS.intCodeComplete],
    impactScore:         76,
    burnoutContribution: 5,
    lastActivity:        d(2, 14),
    ownerDeliveryScoreAtCreation: 81,
  },

  {
    id:    IDS.sevalShipChecklist,
    title: 'Incorporate SEVAL approval certificate and RAI sign-off into Reschedule Skill ship checklist',
    owner: priya.userId,
    watchers: [fatima.userId, david.userId],
    source: {
      type:      'chat',
      url:       'https://teams.microsoft.com/l/chat/rbs-sprint-planning',
      timestamp: d(2, 11),
      sourceId:  'rbs-sprint-planning-chat',
    },
    committedAt: d(2, 11),
    dueAt:       future(11, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.sevalReReview],
    blocks:      [IDS.intShipChecklist],
    impactScore:         62,
    burnoutContribution: 3,
    lastActivity:        d(2, 11),
    ownerDeliveryScoreAtCreation: 81,
  },

  // ────────────────────────────────────────────────────────────────────────────
  // TRACK F: Scorecards
  // ────────────────────────────────────────────────────────────────────────────

  {
    id:    IDS.scoreKpis,
    title: 'Define Reschedule Skill success metrics: intent accuracy, reschedule completion rate, CSAT',
    owner: fatima.userId,
    watchers: [priya.userId, david.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-okr-alignment',
      timestamp: d(21, 14),
      sourceId:  'rbs-okr-alignment-meeting',
    },
    committedAt: d(21, 14),
    dueAt:       d(10, 17),
    status:      'done',
    priority:    'not-urgent-important',
    blockedBy:   [],
    blocks:      [IDS.scorePipeline],
    impactScore:         38,
    burnoutContribution: 2,
    lastActivity:        d(11, 16),
    ownerDeliveryScoreAtCreation: 83,
  },

  {
    id:    IDS.scorePipeline,
    title: 'Implement scorecard data pipeline for Reschedule Skill usage telemetry (Foundry → Kusto)',
    owner: sarah.userId,
    watchers: [fatima.userId, priya.userId],
    source: {
      type:      'ado',
      url:       'https://dev.azure.com/msft/BizChat/_workitems/edit/91540',
      timestamp: d(8, 10),
      sourceId:  'ado-scorecard-pipeline-91540',
    },
    committedAt: d(8, 10),
    dueAt:       future(14, 17),
    status:      'pending',
    priority:    'not-urgent-important',
    blockedBy:   [IDS.scoreKpis],
    blocks:      [IDS.scoreReview],
    impactScore:         45,
    burnoutContribution: 3,
    lastActivity:        d(3, 9),
    ownerDeliveryScoreAtCreation: 68,
  },

  {
    id:    IDS.scoreReview,
    title: 'Scorecard KPI review and sign-off: Priya + David confirm metrics meet ship bar',
    owner: priya.userId,
    watchers: [fatima.userId, david.userId, alex.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-milestone-review-w3',
      timestamp: d(2, 16),
      sourceId:  'rbs-milestone-review-w3',
    },
    committedAt: d(2, 16),
    dueAt:       future(21, 17),
    status:      'pending',
    priority:    'not-urgent-important',
    blockedBy:   [IDS.scorePipeline, IDS.sevalShipChecklist],
    blocks:      [IDS.intShipChecklist],
    impactScore:         50,
    burnoutContribution: 2,
    lastActivity:        d(2, 16),
    ownerDeliveryScoreAtCreation: 81,
  },

  // ────────────────────────────────────────────────────────────────────────────
  // TRACK G: Integration & Ship (Critical Path — all gates must pass)
  // ────────────────────────────────────────────────────────────────────────────

  {
    id:    IDS.intCodeComplete,
    title: 'Code complete — PR review and merge for Reschedule Skill core (all gates: tests ✓, SEVAL ✓)',
    owner: alex.userId,
    watchers: [priya.userId, marcus.userId, sarah.userId],
    source: {
      type:      'ado',
      url:       'https://dev.azure.com/msft/BizChat/_workitems/edit/91600',
      timestamp: d(2, 9),
      sourceId:  'ado-code-complete-91600',
    },
    committedAt: d(2, 9),
    dueAt:       future(15, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.schedUnitTests, IDS.bcpE2eTest, IDS.sevalReReview],
    blocks:      [IDS.intShipChecklist],
    impactScore:         90,
    burnoutContribution: 7,
    lastActivity:        d(2, 9),
    ownerDeliveryScoreAtCreation: 74,
  },

  {
    id:    IDS.intShipChecklist,
    title: 'Ship checklist complete and signed off — SEVAL ✓, Foundry ✓, Scorecard ✓, Perf ✓, Privacy ✓',
    owner: priya.userId,
    watchers: [fatima.userId, david.userId, alex.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-ship-decision-meeting',
      timestamp: d(2, 10),
      sourceId:  'rbs-ship-decision-meeting',
    },
    committedAt: d(2, 10),
    dueAt:       future(22, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.intCodeComplete, IDS.sevalShipChecklist, IDS.scoreReview],
    blocks:      [IDS.intDogfoodDeploy],
    impactScore:         92,
    burnoutContribution: 6,
    lastActivity:        d(2, 10),
    ownerDeliveryScoreAtCreation: 81,
  },

  {
    id:    IDS.intDogfoodDeploy,
    title: 'Deploy Reschedule Skill to BizChat dogfood ring (MSFT internal users)',
    owner: alex.userId,
    watchers: [priya.userId, marcus.userId, david.userId],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/rbs-go-no-go',
      timestamp: d(2, 14),
      sourceId:  'rbs-go-no-go-meeting',
    },
    committedAt: d(2, 14),
    dueAt:       future(24, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.intShipChecklist],
    blocks:      [IDS.intDogfoodMonitor],
    impactScore:         88,
    burnoutContribution: 5,
    lastActivity:        d(2, 14),
    ownerDeliveryScoreAtCreation: 74,
  },

  {
    id:    IDS.intDogfoodMonitor,
    title: 'Monitor dogfood ring for 3 days — send daily status email to David (key signals: accuracy, errors)',
    owner: priya.userId,
    watchers: [david.userId, fatima.userId, alex.userId],
    source: {
      type:      'chat',
      url:       'https://teams.microsoft.com/l/chat/rbs-weekly-status',
      timestamp: d(2, 15),
      sourceId:  'rbs-weekly-status-chat',
    },
    committedAt: d(2, 15),
    dueAt:       future(27, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.intDogfoodDeploy],
    blocks:      [IDS.intShipDecision],
    impactScore:         70,
    burnoutContribution: 4,
    lastActivity:        d(2, 15),
    ownerDeliveryScoreAtCreation: 81,
  },

  {
    id:    IDS.intShipDecision,
    title: 'Reschedule BizChat Skill — Q1 production ship decision (go/no-go to leadership)',
    owner: david.userId,
    watchers: [priya.userId, fatima.userId],
    source: {
      type:      'email',
      url:       'https://outlook.office.com/mail/rbs-q1-ship-commitment',
      timestamp: d(30, 9),
      sourceId:  'rbs-q1-ship-commitment-email',
    },
    committedAt: d(30, 9),
    dueAt:       future(28, 17),
    status:      'pending',
    priority:    'urgent-important',
    blockedBy:   [IDS.intDogfoodMonitor],
    blocks:      [],
    impactScore:         95,
    burnoutContribution: 3,
    lastActivity:        d(30, 9),
    ownerDeliveryScoreAtCreation: 88,
  },
];

// ─── Dependency Graph Edges ──────────────────────────────────────────────────
export const RESCHEDULE_SKILL_EDGES: GraphEdge[] = [
  // Architecture chain
  { fromId: IDS.archDesignDoc,       toId: IDS.archSignOff,         edgeType: 'hard', confidence: 0.98, detectedBy: 'conversation-thread' },
  { fromId: IDS.archSignOff,         toId: IDS.schedApiExpose,      edgeType: 'hard', confidence: 0.90, detectedBy: 'conversation-thread' },
  { fromId: IDS.archSignOff,         toId: IDS.bcpSlotAllocation,   edgeType: 'soft', confidence: 0.85, detectedBy: 'overlapping-people' },

  // Scheduling Skill chain
  { fromId: IDS.schedApiExpose,      toId: IDS.schedSdkIntegrate,   edgeType: 'hard', confidence: 0.97, detectedBy: 'conversation-thread' },
  { fromId: IDS.schedSdkIntegrate,   toId: IDS.schedUnitTests,      edgeType: 'hard', confidence: 0.95, detectedBy: 'nlp-similarity' },
  { fromId: IDS.schedSdkIntegrate,   toId: IDS.foundryAccuracyGate, edgeType: 'soft', confidence: 0.80, detectedBy: 'nlp-similarity' },
  { fromId: IDS.schedUnitTests,      toId: IDS.intCodeComplete,     edgeType: 'hard', confidence: 0.90, detectedBy: 'conversation-thread' },

  // BizChat Platform chain
  { fromId: IDS.bcpSlotAllocation,   toId: IDS.bcpPluginManifest,   edgeType: 'hard', confidence: 0.95, detectedBy: 'overlapping-people' },
  { fromId: IDS.bcpPluginManifest,   toId: IDS.bcpGraphRouting,     edgeType: 'hard', confidence: 0.96, detectedBy: 'conversation-thread' },
  { fromId: IDS.bcpGraphRouting,     toId: IDS.bcpE2eTest,          edgeType: 'hard', confidence: 0.94, detectedBy: 'conversation-thread' },
  { fromId: IDS.bcpE2eTest,          toId: IDS.intCodeComplete,     edgeType: 'hard', confidence: 0.92, detectedBy: 'overlapping-people' },

  // Foundry chain
  { fromId: IDS.foundryBaseline,     toId: IDS.foundryAccuracyGate, edgeType: 'soft', confidence: 0.88, detectedBy: 'nlp-similarity' },
  { fromId: IDS.foundryAccuracyGate, toId: IDS.foundryProdRun,      edgeType: 'hard', confidence: 0.95, detectedBy: 'conversation-thread' },
  { fromId: IDS.foundryProdRun,      toId: IDS.bcpE2eTest,          edgeType: 'soft', confidence: 0.75, detectedBy: 'nlp-similarity' },

  // SEVAL chain (main cascade)
  { fromId: IDS.sevalInitialSubmit,  toId: IDS.sevalFeedback,       edgeType: 'hard', confidence: 0.99, detectedBy: 'conversation-thread' },
  { fromId: IDS.sevalFeedback,       toId: IDS.sevalReReview,       edgeType: 'hard', confidence: 0.97, detectedBy: 'conversation-thread' },
  { fromId: IDS.sevalReReview,       toId: IDS.sevalShipChecklist,  edgeType: 'hard', confidence: 0.92, detectedBy: 'conversation-thread' },
  { fromId: IDS.sevalReReview,       toId: IDS.intCodeComplete,     edgeType: 'hard', confidence: 0.90, detectedBy: 'overlapping-people' },
  { fromId: IDS.sevalShipChecklist,  toId: IDS.intShipChecklist,    edgeType: 'hard', confidence: 0.95, detectedBy: 'conversation-thread' },

  // Scorecard chain
  { fromId: IDS.scoreKpis,           toId: IDS.scorePipeline,       edgeType: 'hard', confidence: 0.90, detectedBy: 'nlp-similarity' },
  { fromId: IDS.scorePipeline,       toId: IDS.scoreReview,         edgeType: 'hard', confidence: 0.93, detectedBy: 'conversation-thread' },
  { fromId: IDS.scoreReview,         toId: IDS.intShipChecklist,    edgeType: 'soft', confidence: 0.80, detectedBy: 'overlapping-people' },

  // Integration chain (critical path to Q1 ship)
  { fromId: IDS.intCodeComplete,     toId: IDS.intShipChecklist,    edgeType: 'hard', confidence: 0.98, detectedBy: 'conversation-thread' },
  { fromId: IDS.intShipChecklist,    toId: IDS.intDogfoodDeploy,    edgeType: 'hard', confidence: 0.97, detectedBy: 'conversation-thread' },
  { fromId: IDS.intDogfoodDeploy,    toId: IDS.intDogfoodMonitor,   edgeType: 'date', confidence: 0.95, detectedBy: 'conversation-thread' },
  { fromId: IDS.intDogfoodMonitor,   toId: IDS.intShipDecision,     edgeType: 'hard', confidence: 0.95, detectedBy: 'overlapping-people' },
];
