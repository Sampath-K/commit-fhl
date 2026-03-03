/**
 * Test data factories — build minimal valid CommitmentRecord / AgentDraft objects.
 * All required fields have defaults so callers only override what they care about.
 */
import type {
  AgentDraft,
  AffectedTask,
  CascadeApiResponse,
  CommitmentRecord,
  ReplanApiOption,
} from '../types/api';

export const ALEX_OID   = 'f7a02de7-e195-4894-bc23-f7f74b696cbd';
export const SARAH_OID  = '5659f687-9ea8-4dfe-95c7-2990356288af';
export const MARCUS_OID = 'c1c0037d-1b8c-4c34-bede-f6dadc38a8c6';
export const PRIYA_OID  = '8d0832a0-c586-4c41-b6ad-02a76c5b326c';

export function makeCommitment(overrides: Partial<CommitmentRecord> = {}): CommitmentRecord {
  return {
    id:            'rbs-test-001',
    title:         'Test Commitment',
    owner:         ALEX_OID,
    watchers:      [],
    source:        { type: 'meeting', url: 'https://teams.microsoft.com/l/meet/1', timestamp: '2026-03-01T09:00:00Z' },
    committedAt:   '2026-03-01T09:00:00Z',
    dueAt:         '2026-03-15T00:00:00Z',
    status:        'in-progress',
    priority:      'urgent-important',
    blockedBy:     [],
    blocks:        [],
    impactScore:   0,
    burnoutContribution: 0,
    ...overrides,
  };
}

export function makeDraft(overrides: Partial<AgentDraft> = {}): AgentDraft {
  return {
    draftId:        'draft-test-001',
    actionType:     'send-message',
    content:        'Hi team, quick update on the timeline.',
    contextSummary: 'Option C: Clean Slip — 3 task(s) affected',
    recipients:     ['Marcus Johnson', 'Priya Sharma'],
    createdAt:      '2026-03-01T10:00:00Z',
    status:         'pending',
    ...overrides,
  };
}

export function makeAffectedTask(overrides: Partial<AffectedTask> = {}): AffectedTask {
  return {
    taskId:             'rbs-test-001',
    title:              'Test Task',
    cumulativeSlipDays: 0,
    originalEta:        '2026-03-15T00:00:00Z',
    newEta:             '2026-03-16T00:00:00Z',
    calendarPressure:   0,
    ...overrides,
  };
}

export function makeCascadeResponse(overrides: Partial<CascadeApiResponse> = {}): CascadeApiResponse {
  return {
    rootTaskId:    'rbs-test-001',
    slipDays:      14,
    impactScore:   75,
    affectedCount: 3,
    affectedTasks: [
      makeAffectedTask({ taskId: 'rbs-foundry-002', title: 'Foundry accuracy gate', cumulativeSlipDays: 14 }),
      makeAffectedTask({ taskId: 'rbs-bcp-004',     title: 'BizChat slot reservation', cumulativeSlipDays: 3 }),
      makeAffectedTask({ taskId: 'rbs-int-001',     title: 'Integration complete',    cumulativeSlipDays: 4 }),
    ],
    ...overrides,
  };
}

export function makeReplanResponse(): { options: ReplanApiOption[] } {
  return {
    options: [
      {
        optionId:       'A',
        label:          'Resolve Fast',
        description:    'Pull in extra resources to meet original deadline.',
        confidence:     0.72,
        requiredActions: ['Add contractor for SEVAL cycle', 'Approve overtime budget'],
      },
      {
        optionId:       'B',
        label:          'Parallel Work',
        description:    'Start downstream work in parallel under risk.',
        confidence:     0.65,
        requiredActions: ['Kick off BizChat integration early', 'Accept rework risk'],
      },
      {
        optionId:       'C',
        label:          'Clean Slip + Auto-Comms',
        description:    'Accept the slip and notify all stakeholders now.',
        confidence:     0.88,
        requiredActions: ['Notify Marcus of +14d', 'Update Code Complete date to March 17'],
      },
    ],
  };
}
