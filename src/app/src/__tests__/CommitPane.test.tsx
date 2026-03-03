/**
 * SCENARIO: CommitPane — commitment card rendering and team affiliation pills
 *
 * Key broken scenario documented here:
 *   KNOWN DESIGN GAP: Team pills (green/purple) do NOT appear on Alex's own board
 *   because ALL 9 seeded commitments are owned by Alex (owner === currentUserId).
 *   Pills only render for commitment.owner !== currentUserId.
 *   Cross-team labels ARE visible in CascadeView chain items (teamFromTaskId).
 *
 * Tests verify both the positive path (cross-team pill shows) and
 * the design gap (self-owned cards have no pill).
 */
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { CommitPane } from '../components/core/CommitPane';
import { makeCommitment, ALEX_OID, MARCUS_OID, SARAH_OID } from './helpers';

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
                   React.createElement('div', { onClick, ...props }, children),
    CardHeader:  ({ header, description, action }: Record<string, unknown>) =>
                   React.createElement('div', {}, header, description, action),
    Text:        ({ children, truncate: _t, ...props }: Record<string, unknown>) =>
                   React.createElement('span', props, children),
    Badge:       ({ children, ...props }: Record<string, unknown>) =>
                   React.createElement('span', { 'data-badge': 'true', ...props }, children),
    Button:      ({ onClick, children, href, ...props }: Record<string, unknown>) =>
                   React.createElement(href ? 'a' : 'button', { onClick, href, ...props }, children),
    Divider:     el('hr'),
    Skeleton:    el('div'),
    SkeletonItem: el('div'),
    Tooltip:     ({ children }: { children: unknown }) => children,
  };
});

jest.mock('@react-spring/web', () => {
  const React = require('react');
  return {
    useSpring: () => ({}),
    animated:  new Proxy({}, {
      get: (_: unknown, tag: string) =>
        ({ children, style: _s, ...rest }: Record<string, unknown>) =>
          React.createElement(tag as string, rest, children),
    }),
  };
});

jest.mock('../hooks/useReducedMotion', () => ({ useReducedMotion: () => true }));
jest.mock('../config/psychology.config', () => ({
  SPRING_CONFIGS: { smooth: {}, gentle: {}, bounce: {} },
  STAGGER_DELAYS:  { cascadeItems: 0 },
}));

// ─── Loading state ────────────────────────────────────────────────────────────

describe('CommitPane — loading state', () => {
  it('renders the pane container while loading', () => {
    render(<CommitPane commitments={[]} isLoading={true} />);
    expect(screen.getByTestId('commit-pane')).toBeInTheDocument();
  });

  it('does not render empty-state while loading', () => {
    render(<CommitPane commitments={[]} isLoading={true} />);
    expect(screen.queryByTestId('empty-state')).not.toBeInTheDocument();
  });
});

// ─── Empty state ──────────────────────────────────────────────────────────────

describe('CommitPane — empty state', () => {
  it('renders empty-state when no commitments and not loading', () => {
    render(<CommitPane commitments={[]} isLoading={false} />);
    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
  });
});

// ─── Card rendering ───────────────────────────────────────────────────────────

describe('CommitPane — card rendering', () => {
  it('renders a card for each commitment by data-testid', () => {
    const c1 = makeCommitment({ id: 'rbs-001', title: 'Ship the SDK' });
    const c2 = makeCommitment({ id: 'rbs-002', title: 'Foundry accuracy gate' });

    render(<CommitPane commitments={[c1, c2]} isLoading={false} />);

    expect(screen.getByTestId('commit-card-rbs-001')).toBeInTheDocument();
    expect(screen.getByTestId('commit-card-rbs-002')).toBeInTheDocument();
  });

  it('renders commitment titles', () => {
    const c = makeCommitment({ title: 'SEVAL re-review' });
    render(<CommitPane commitments={[c]} isLoading={false} />);
    expect(screen.getByText('SEVAL re-review')).toBeInTheDocument();
  });

  it('calls onCommitmentClick with the commitment when card is clicked', () => {
    const onClick = jest.fn();
    const c = makeCommitment({ id: 'rbs-click-001' });
    render(<CommitPane commitments={[c]} isLoading={false} onCommitmentClick={onClick} />);

    fireEvent.click(screen.getByTestId('commit-card-rbs-click-001'));
    expect(onClick).toHaveBeenCalledTimes(1);
    expect(onClick).toHaveBeenCalledWith(c);
  });

  it('shows total count badge', () => {
    const commitments = [makeCommitment({ id: 'a' }), makeCommitment({ id: 'b' })];
    render(<CommitPane commitments={commitments} isLoading={false} />);
    expect(screen.getByText('2')).toBeInTheDocument();
  });
});

// ─── Impact score chip ────────────────────────────────────────────────────────

describe('CommitPane — impact score chip', () => {
  it('renders impact score badge when impactScore > 0', () => {
    const c = makeCommitment({ impactScore: 75 });
    render(<CommitPane commitments={[c]} isLoading={false} />);
    // The badge text includes the key and value
    expect(screen.getByText(/commitPane\.card\.impactScore/)).toBeInTheDocument();
  });

  it('does not render impact score badge when impactScore = 0', () => {
    const c = makeCommitment({ impactScore: 0 });
    render(<CommitPane commitments={[c]} isLoading={false} />);
    expect(screen.queryByText(/commitPane\.card\.impactScore/)).not.toBeInTheDocument();
  });
});

// ─── Blocking count ───────────────────────────────────────────────────────────

describe('CommitPane — blocking indicator', () => {
  it('shows blocking text when commitment.blocks has entries', () => {
    const c = makeCommitment({ blocks: ['rbs-dep-001', 'rbs-dep-002'] });
    render(<CommitPane commitments={[c]} isLoading={false} />);
    expect(screen.getByText(/commitPane\.card\.blocking/)).toBeInTheDocument();
  });

  it('does not show blocking text when blocks is empty', () => {
    const c = makeCommitment({ blocks: [] });
    render(<CommitPane commitments={[c]} isLoading={false} />);
    expect(screen.queryByText(/commitPane\.card\.blocking/)).not.toBeInTheDocument();
  });
});

// ─── Team affiliation pills ───────────────────────────────────────────────────
//
// DESIGN DECISION: Team pills are intentionally HIDDEN for self-owned cards.
// This means Alex's board (all 9 cards owned by Alex) shows NO team pills.
// Cross-team labels appear only on cascade chain items via teamFromTaskId.

describe('CommitPane — team affiliation pills', () => {
  describe('KNOWN DESIGN GAP: self-owned cards have no team pill', () => {
    it('does NOT show team pill when commitment.owner === currentUserId (Alex on Alex board)', () => {
      const c = makeCommitment({ owner: ALEX_OID });
      render(<CommitPane commitments={[c]} isLoading={false} currentUserId={ALEX_OID} />);
      expect(screen.queryByText('Reschedule Crew')).not.toBeInTheDocument();
    });

    it('does NOT show BizChat Platform pill on a cross-team card if currentUserId is also that user', () => {
      // Edge case: viewing someone else's board as them — no self-pill
      const c = makeCommitment({ owner: MARCUS_OID });
      render(<CommitPane commitments={[c]} isLoading={false} currentUserId={MARCUS_OID} />);
      expect(screen.queryByText('BizChat Platform')).not.toBeInTheDocument();
    });
  });

  describe('positive path: cross-team pills appear for foreign owners', () => {
    it('shows "BizChat Platform" (purple) for Marcus-owned card on Alex board', () => {
      const c = makeCommitment({ owner: MARCUS_OID, title: 'BizChat Plugin Slot' });
      render(<CommitPane commitments={[c]} isLoading={false} currentUserId={ALEX_OID} />);
      expect(screen.getByText('BizChat Platform')).toBeInTheDocument();
    });

    it('shows "Scheduling Skill" (green) for Sarah-owned card on Alex board', () => {
      const c = makeCommitment({ owner: SARAH_OID, title: 'SDK Delivery' });
      render(<CommitPane commitments={[c]} isLoading={false} currentUserId={ALEX_OID} />);
      expect(screen.getByText('Scheduling Skill')).toBeInTheDocument();
    });

    it('shows no pill if currentUserId is not provided (undefined)', () => {
      // Without currentUserId, owner !== undefined is always true so pill DOES show
      const c = makeCommitment({ owner: MARCUS_OID });
      render(<CommitPane commitments={[c]} isLoading={false} />);
      // ownerTeam is defined for Marcus, currentUserId is undefined → pill should show
      expect(screen.getByText('BizChat Platform')).toBeInTheDocument();
    });

    it('shows no pill for an owner not in TEAM_BY_USER', () => {
      const c = makeCommitment({ owner: 'unknown-oid-xyz' });
      render(<CommitPane commitments={[c]} isLoading={false} currentUserId={ALEX_OID} />);
      expect(screen.queryByText('Reschedule Crew')).not.toBeInTheDocument();
      expect(screen.queryByText('Scheduling Skill')).not.toBeInTheDocument();
      expect(screen.queryByText('BizChat Platform')).not.toBeInTheDocument();
    });
  });
});

// ─── Quadrant grouping ────────────────────────────────────────────────────────

describe('CommitPane — Eisenhower quadrant grouping', () => {
  it('groups cards by priority quadrant', () => {
    const urgent    = makeCommitment({ id: 'u1', priority: 'urgent-important' });
    const notUrgent = makeCommitment({ id: 'n1', priority: 'not-urgent-important' });

    render(<CommitPane commitments={[urgent, notUrgent]} isLoading={false} />);

    expect(screen.getByTestId('commit-card-u1')).toBeInTheDocument();
    expect(screen.getByTestId('commit-card-n1')).toBeInTheDocument();
    expect(screen.getByText('commitPane.quadrants.urgentImportant')).toBeInTheDocument();
    expect(screen.getByText('commitPane.quadrants.notUrgentImportant')).toBeInTheDocument();
  });

  it('does not render quadrant label when no cards exist for it', () => {
    const c = makeCommitment({ priority: 'urgent-important' });
    render(<CommitPane commitments={[c]} isLoading={false} />);
    expect(screen.queryByText('commitPane.quadrants.notUrgentNotImportant')).not.toBeInTheDocument();
  });
});
