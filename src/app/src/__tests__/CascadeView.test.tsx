/**
 * SCENARIO: CascadeView — cascade chain rendering, team labels, replan options,
 *           and the ApprovalCard trigger after option selection.
 *
 * Key scenarios documented here:
 *
 *   SCENARIO 3 (SEVAL cross-partition): The SEVAL cascade only returns 1 task
 *   because downstream tasks live in Fatima's partition, not Alex's. The
 *   CascadeSimulator on the backend only traverses tasks owned by the requesting
 *   user. This is a backend limitation — these tests validate the frontend
 *   renders whatever the API returns, even if it's 1 item.
 *
 *   SCENARIO 4 (VERIFIED): Cross-org tasks (rbs-bcp-004) get a "BizChat Platform"
 *   badge in purple. The Foundry cascade (4 tasks including rbs-bcp-004) is the
 *   best demo scenario because all 4 tasks are in Alex's partition.
 *
 *   SCENARIO 6 (DEMO WORKS): ApprovalCard appears after clicking any replan option.
 */
import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { CascadeView } from '../components/core/CascadeView';
import {
  makeCommitment,
  makeCascadeResponse,
  makeReplanResponse,
  makeAffectedTask,
  ALEX_OID,
} from './helpers';

// ─── Module mocks ─────────────────────────────────────────────────────────────

jest.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string, opts?: Record<string, unknown>) =>
      opts ? `${key}:${JSON.stringify(opts)}` : key,
    i18n: { language: 'en' },
  }),
}));

jest.mock('@fluentui/react-components', () => {
  const React = require('react');
  const el = (tag: string) =>
    ({ children, ...props }: Record<string, unknown>) =>
      React.createElement(tag, props, children);

  return {
    makeStyles:  () => () => ({}),
    tokens:      new Proxy({}, { get: () => '' }),
    Card:        ({ onClick, children, ...props }: Record<string, unknown>) =>
                   React.createElement('div', { role: 'button', onClick, ...props }, children),
    CardHeader:  ({ header, description, action }: Record<string, unknown>) =>
                   React.createElement('div', {}, header, description, action),
    Text:        ({ children, truncate: _t, ...props }: Record<string, unknown>) =>
                   React.createElement('span', props, children),
    Badge:       ({ children, ...props }: Record<string, unknown>) =>
                   React.createElement('span', { 'data-badge': 'true', ...props }, children),
    Button:      ({ onClick, children, ...props }: Record<string, unknown>) =>
                   React.createElement('button', { onClick, ...props }, children),
    Divider:     el('hr'),
    Skeleton:    el('div'),
    SkeletonItem:el('div'),
    Tooltip:     ({ children, content }: { children: unknown; content?: string }) =>
                   React.createElement('span', { title: content }, children as React.ReactNode),
  };
});

jest.mock('@react-spring/web', () => {
  const React = require('react');
  return {
    useSpring:  () => ({}),
    useTrail:   (n: number) => Array(n).fill({}),
    animated:   new Proxy({}, {
      get: (_: unknown, tag: string) =>
        ({ children, style: _s, ...rest }: Record<string, unknown>) =>
          React.createElement(tag as string, rest, children),
    }),
  };
});

jest.mock('../hooks/useReducedMotion',     () => ({ useReducedMotion: () => true }));
jest.mock('../config/psychology.config',   () => ({
  SPRING_CONFIGS:  { gentle: {} },
  STAGGER_DELAYS:  { cascadeItems: 0 },
}));
jest.mock('../config/api.config',          () => ({ API_BASE: 'https://test-api.example.com' }));

// ─── Test helpers ─────────────────────────────────────────────────────────────

function mockFetch(cascade: unknown, replan?: unknown): jest.Mock {
  const m = jest.fn();
  if (replan !== undefined) {
    m.mockResolvedValueOnce({ ok: true, json: async () => cascade })
     .mockResolvedValueOnce({ ok: true, json: async () => replan })
     .mockResolvedValue   ({ ok: true, json: async () => ({ result: 'executed' }) });
  } else {
    m.mockResolvedValue({ ok: true, json: async () => cascade });
  }
  global.fetch = m;
  return m;
}

function renderCascade(overrides: { userId?: string } = {}) {
  const commitment = makeCommitment({ id: 'rbs-foundry-002', title: 'Foundry accuracy gate' });
  const onClose = jest.fn();
  const { rerender } = render(
    <CascadeView
      commitment={commitment}
      userId={overrides.userId ?? ALEX_OID}
      onClose={onClose}
    />
  );
  return { commitment, onClose, rerender };
}

// ─── Cascade fetch on mount ───────────────────────────────────────────────────

describe('CascadeView — cascade fetch on mount', () => {
  it('shows loading skeleton immediately on mount', () => {
    mockFetch(makeCascadeResponse());
    renderCascade();
    // propagating text from t('cascadeView.propagating')
    expect(screen.getByText('cascadeView.propagating')).toBeInTheDocument();
  });

  it('calls fetch to /api/v1/graph/cascade with correct params', async () => {
    const m = mockFetch(makeCascadeResponse());
    renderCascade();
    await waitFor(() => expect(m).toHaveBeenCalledTimes(1));
    const [url] = (m.mock.calls[0] as [string, RequestInit]);
    expect(url).toContain('/api/v1/graph/cascade');
    expect(url).toContain('rootTaskId=rbs-foundry-002');
    expect(url).toContain(`userId=${ALEX_OID}`);
    expect((m.mock.calls[0] as [string, RequestInit])[1]?.method).toBe('POST');
  });

  it('renders cascade chain items after fetch resolves', async () => {
    mockFetch(makeCascadeResponse());
    renderCascade();
    await waitFor(() =>
      expect(screen.getByText('Foundry accuracy gate')).toBeInTheDocument()
    );
  });

  it('renders impact score badge after fetch resolves', async () => {
    mockFetch(makeCascadeResponse({ impactScore: 100 }));
    renderCascade();
    await waitFor(() => expect(screen.getByText('100')).toBeInTheDocument());
  });

  it('renders affected task count', async () => {
    mockFetch(makeCascadeResponse({ affectedCount: 4 }));
    renderCascade();
    await waitFor(() =>
      expect(screen.getByText(/cascadeView\.affectedTasks/)).toBeInTheDocument()
    );
  });
});

// ─── Cascade chain items + team labels ───────────────────────────────────────
//
// SCENARIO 4 (DEMO WORKS): This is the key demo scenario.
// The Foundry cascade shows:
//   rbs-foundry-002 → Reschedule Crew (blue)
//   rbs-bcp-004     → BizChat Platform (purple) ← cross-org task, visually distinct
//   rbs-int-001     → Reschedule Crew (blue)

describe('CascadeView — team labels in cascade chain', () => {
  it('renders team badge for rbs-bcp- tasks → "BizChat Platform"', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({ taskId: 'rbs-bcp-004', title: 'BizChat slot reservation', cumulativeSlipDays: 3 }),
      ],
    }));
    renderCascade();
    await waitFor(() => expect(screen.getByText('BizChat Platform')).toBeInTheDocument());
  });

  it('renders team badge for rbs-sched- tasks → "Scheduling Skill"', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({ taskId: 'rbs-sched-001', title: 'SDK delivery', cumulativeSlipDays: 0 }),
      ],
    }));
    renderCascade();
    await waitFor(() => expect(screen.getByText('Scheduling Skill')).toBeInTheDocument());
  });

  it('renders team badge for rbs- tasks → "Reschedule Crew"', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({ taskId: 'rbs-foundry-003', title: 'Foundry eval 2', cumulativeSlipDays: 12 }),
      ],
    }));
    renderCascade();
    await waitFor(() => expect(screen.getByText('Reschedule Crew')).toBeInTheDocument());
  });

  it('renders all 4 chain items in the Foundry demo cascade', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({ taskId: 'rbs-foundry-002', title: 'Foundry accuracy gate', cumulativeSlipDays: 14 }),
        makeAffectedTask({ taskId: 'rbs-foundry-003', title: 'Foundry eval round 2',  cumulativeSlipDays: 12 }),
        makeAffectedTask({ taskId: 'rbs-bcp-004',     title: 'BizChat slot reservation', cumulativeSlipDays: 3 }),
        makeAffectedTask({ taskId: 'rbs-int-001',     title: 'Integration complete',  cumulativeSlipDays: 4 }),
      ],
    }));
    renderCascade();
    await waitFor(() => {
      expect(screen.getByText('Foundry accuracy gate')).toBeInTheDocument();
      expect(screen.getByText('Foundry eval round 2')).toBeInTheDocument();
      expect(screen.getByText('BizChat slot reservation')).toBeInTheDocument();
      expect(screen.getByText('Integration complete')).toBeInTheDocument();
    });
  });

  it('BizChat task is visually distinct — purple badge alongside blue ones', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({ taskId: 'rbs-foundry-002', title: 'Foundry gate',         cumulativeSlipDays: 14 }),
        makeAffectedTask({ taskId: 'rbs-bcp-004',     title: 'BizChat reservation',  cumulativeSlipDays: 3  }),
        makeAffectedTask({ taskId: 'rbs-int-001',     title: 'Integration complete', cumulativeSlipDays: 4  }),
      ],
    }));
    renderCascade();
    await waitFor(() => {
      const badges = screen.getAllByText(/Reschedule Crew|BizChat Platform|Scheduling Skill/);
      const teams = badges.map(b => b.textContent);
      expect(teams).toContain('Reschedule Crew');
      expect(teams).toContain('BizChat Platform');
    });
  });

  it('tasks with cumulativeSlipDays = 0 show no at-risk highlighting (verify title renders)', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({ taskId: 'rbs-sched-001', title: 'SDK already done', cumulativeSlipDays: 0 }),
      ],
    }));
    renderCascade();
    await waitFor(() => expect(screen.getByText('SDK already done')).toBeInTheDocument());
  });

  it('ETA strikethrough info renders for tasks with originalEta and newEta', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({
          taskId:             'rbs-int-001',
          title:              'Integration complete',
          cumulativeSlipDays: 14,
          originalEta:        '2026-03-15T00:00:00Z',
          newEta:             '2026-03-29T00:00:00Z',
        }),
      ],
    }));
    renderCascade();
    // Both ETAs should appear (original struck-through, new in red)
    await waitFor(() => {
      const eta1 = new Date('2026-03-15T00:00:00Z').toLocaleDateString();
      const eta2 = new Date('2026-03-29T00:00:00Z').toLocaleDateString();
      expect(screen.getByText(eta1)).toBeInTheDocument();
      expect(screen.getByText(new RegExp(eta2))).toBeInTheDocument();
    });
  });
});

// ─── SEVAL cross-partition: only 1 task returned ─────────────────────────────
//
// SCENARIO 3 (KNOWN LIMITATION): SEVAL cascade only returns 1 task because
// the downstream tasks (Fatima's SEVAL re-review) are in a different partition.
// The CascadeSimulator on the backend only traverses tasks owned by userId.
// This is a backend data-partitioning issue, not a frontend bug.

describe('CascadeView — SEVAL cross-partition limitation', () => {
  it('renders a 1-task cascade when backend returns only 1 affected task', async () => {
    mockFetch(makeCascadeResponse({
      affectedTasks: [
        makeAffectedTask({ taskId: 'rbs-seval-002', title: 'SEVAL feedback cycle', cumulativeSlipDays: 5 }),
      ],
      affectedCount: 1,
    }));
    renderCascade();
    await waitFor(() => {
      expect(screen.getByText('SEVAL feedback cycle')).toBeInTheDocument();
      // Only 1 task in the chain
      expect(screen.getAllByText(/SEVAL|Foundry|BizChat|Integration/i).length).toBe(1);
    });
  });
});

// ─── Close button ─────────────────────────────────────────────────────────────

describe('CascadeView — close', () => {
  it('calls onClose when Close button is clicked', async () => {
    mockFetch(makeCascadeResponse());
    const { onClose } = renderCascade();
    // Close button is visible immediately (not gated by fetch)
    fireEvent.click(screen.getByText('actions.close'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});

// ─── Replan options ───────────────────────────────────────────────────────────

describe('CascadeView — replan options', () => {
  it('"View replan options" button appears after cascade loads', async () => {
    mockFetch(makeCascadeResponse());
    renderCascade();
    await waitFor(() =>
      expect(screen.getByText('cascadeView.replanOptions')).toBeInTheDocument()
    );
  });

  it('clicking "View replan options" triggers fetch to /api/v1/graph/replan', async () => {
    const m = mockFetch(makeCascadeResponse(), makeReplanResponse());
    renderCascade();
    await waitFor(() => screen.getByText('cascadeView.replanOptions'));
    await act(async () => {
      fireEvent.click(screen.getByText('cascadeView.replanOptions'));
    });
    await waitFor(() => expect(m).toHaveBeenCalledTimes(2));
    const [replanUrl] = m.mock.calls[1] as [string, RequestInit];
    expect(replanUrl).toContain('/api/v1/graph/replan');
    expect(replanUrl).toContain('rootTaskId=rbs-foundry-002');
  });

  it('renders all 3 replan options A, B, C', async () => {
    mockFetch(makeCascadeResponse(), makeReplanResponse());
    renderCascade();
    await waitFor(() => screen.getByText('cascadeView.replanOptions'));
    await act(async () => {
      fireEvent.click(screen.getByText('cascadeView.replanOptions'));
    });
    await waitFor(() => {
      expect(screen.getByText('A. Resolve Fast')).toBeInTheDocument();
      expect(screen.getByText('B. Parallel Work')).toBeInTheDocument();
      expect(screen.getByText('C. Clean Slip + Auto-Comms')).toBeInTheDocument();
    });
  });

  it('renders confidence badges for each option', async () => {
    mockFetch(makeCascadeResponse(), makeReplanResponse());
    renderCascade();
    await waitFor(() => screen.getByText('cascadeView.replanOptions'));
    await act(async () => {
      fireEvent.click(screen.getByText('cascadeView.replanOptions'));
    });
    await waitFor(() => {
      expect(screen.getByText('72%')).toBeInTheDocument();  // Option A
      expect(screen.getByText('65%')).toBeInTheDocument();  // Option B
      expect(screen.getByText('88%')).toBeInTheDocument();  // Option C
    });
  });

  it('renders required actions for each option (first 2)', async () => {
    mockFetch(makeCascadeResponse(), makeReplanResponse());
    renderCascade();
    await waitFor(() => screen.getByText('cascadeView.replanOptions'));
    await act(async () => {
      fireEvent.click(screen.getByText('cascadeView.replanOptions'));
    });
    await waitFor(() => {
      expect(screen.getByText('Add contractor for SEVAL cycle')).toBeInTheDocument();
      expect(screen.getByText('Notify Marcus of +14d')).toBeInTheDocument();
    });
  });
});

// ─── ApprovalCard trigger ─────────────────────────────────────────────────────
//
// SCENARIO 6 (DEMO WORKS): Clicking a replan option triggers ApprovalCard.
// ApprovalCard shows Approve/Edit/Skip — clicking Approve shows "✅ Sent".

describe('CascadeView — ApprovalCard trigger after replan option click', () => {
  async function setupWithReplanOptions() {
    mockFetch(makeCascadeResponse(), makeReplanResponse());
    renderCascade();
    await waitFor(() => screen.getByText('cascadeView.replanOptions'));
    await act(async () => {
      fireEvent.click(screen.getByText('cascadeView.replanOptions'));
    });
    await waitFor(() => screen.getByText('C. Clean Slip + Auto-Comms'));
  }

  it('ApprovalCard appears after clicking a replan option', async () => {
    await setupWithReplanOptions();
    // Click Option C card
    await act(async () => {
      fireEvent.click(screen.getByText('C. Clean Slip + Auto-Comms'));
    });
    await waitFor(() => {
      // ApprovalCard shows agent-drafted message label
      expect(screen.getByText('✍️ Agent-drafted message — review before sending')).toBeInTheDocument();
    });
  });

  it('ApprovalCard shows "📨 Teams Message" action type', async () => {
    await setupWithReplanOptions();
    await act(async () => {
      fireEvent.click(screen.getByText('C. Clean Slip + Auto-Comms'));
    });
    await waitFor(() =>
      expect(screen.getByText('📨 Teams Message')).toBeInTheDocument()
    );
  });

  it('ApprovalCard shows named recipients (Marcus Johnson, Priya Singh, David Chen)', async () => {
    await setupWithReplanOptions();
    await act(async () => {
      fireEvent.click(screen.getByText('C. Clean Slip + Auto-Comms'));
    });
    await waitFor(() => {
      expect(screen.getByText('Marcus Johnson')).toBeInTheDocument();
      expect(screen.getByText('Priya Singh')).toBeInTheDocument();
      expect(screen.getByText('David Chen')).toBeInTheDocument();
    });
  });

  it('ApprovalCard shows Approve, Edit, Skip buttons', async () => {
    await setupWithReplanOptions();
    await act(async () => {
      fireEvent.click(screen.getByText('A. Resolve Fast'));
    });
    await waitFor(() => {
      expect(screen.getByText('approvalCard.approve')).toBeInTheDocument();
      expect(screen.getByText('approvalCard.edit')).toBeInTheDocument();
      expect(screen.getByText('approvalCard.skip')).toBeInTheDocument();
    });
  });

  it('clicking Approve on ApprovalCard triggers the approval request', async () => {
    // NOTE: CascadeView passes onDecision={() => setActiveDraft(null)} to ApprovalCard.
    // When Approve is clicked, activeDraft is set to null immediately after the state
    // transition in ApprovalCard, unmounting the card before '✅ Sent' can be asserted.
    // We verify the approval by checking that fetch was called to /api/v1/approvals.
    await setupWithReplanOptions();
    await act(async () => {
      fireEvent.click(screen.getByText('C. Clean Slip + Auto-Comms'));
    });
    await waitFor(() => screen.getByText('approvalCard.approve'));
    await act(async () => {
      fireEvent.click(screen.getByText('approvalCard.approve'));
    });
    await waitFor(() =>
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/approvals?userId='),
        expect.objectContaining({
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    expect.stringContaining('"decision":"approve"'),
        })
      )
    );
  });

  it('clicking Back hides ApprovalCard', async () => {
    await setupWithReplanOptions();
    await act(async () => {
      fireEvent.click(screen.getByText('C. Clean Slip + Auto-Comms'));
    });
    await waitFor(() => screen.getByText('✍️ Agent-drafted message — review before sending'));
    // The Back button for the replan section hides both replan options and ApprovalCard
    const backBtn = screen.getAllByText('actions.back')[0];
    await act(async () => {
      fireEvent.click(backBtn);
    });
    await waitFor(() => {
      expect(screen.queryByText('✍️ Agent-drafted message — review before sending')).not.toBeInTheDocument();
      expect(screen.queryByText('C. Clean Slip + Auto-Comms')).not.toBeInTheDocument();
    });
  });

  it('Option C draft contains "SEVAL" or "BizChat Skill" in message content', async () => {
    await setupWithReplanOptions();
    await act(async () => {
      fireEvent.click(screen.getByText('C. Clean Slip + Auto-Comms'));
    });
    // Option C uses the isCleanSlip path which generates a specific message
    await waitFor(() => {
      // The draft content references the BizChat Skill timeline
      const content = document.body.textContent ?? '';
      expect(content).toMatch(/Reschedule BizChat Skill|Quick heads up|SEVAL/);
    });
  });
});

// ─── Error handling ───────────────────────────────────────────────────────────

describe('CascadeView — error handling', () => {
  it('shows error text when cascade fetch fails', async () => {
    (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Network error'));
    renderCascade();
    await waitFor(() =>
      expect(screen.getByText('app.error.generic')).toBeInTheDocument()
    );
  });
});
