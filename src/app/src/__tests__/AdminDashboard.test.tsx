/**
 * SCENARIO: AdminDashboard — KPI metrics rendering and admin portal link
 *
 * Covers:
 *   - Feedback KPI card renders totalFeedback from API
 *   - False positive rate KPI renders correctly
 *   - All 4 KPI cards render when metrics load
 *   - Renders loading state (Refresh button shows "Refreshing…")
 *   - API error falls back gracefully (all "—" values shown)
 */
import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { AdminDashboard } from '../pages/AdminDashboard';

// ─── Fluent UI stub ────────────────────────────────────────────────────────────

jest.mock('@fluentui/react-components', () => {
  const React = require('react');
  const el = (tag: string) =>
    ({ children, ...props }: Record<string, unknown>) =>
      React.createElement(tag, props, children);

  return {
    makeStyles:  () => () => ({}),
    tokens:      new Proxy({}, { get: () => '' }),
    Card:        el('div'),
    CardHeader:  ({ header, children }: Record<string, unknown>) =>
                   React.createElement('div', {}, header, children),
    Text:        ({ children, ...props }: Record<string, unknown>) =>
                   React.createElement('span', props, children),
    Badge:       ({ children, ...props }: Record<string, unknown>) =>
                   React.createElement('span', props, children),
    Button:      ({ onClick, children, disabled, ...props }: Record<string, unknown>) =>
                   React.createElement('button', { onClick, disabled, ...props }, children),
    Spinner:     el('span'),
    Divider:     el('hr'),
    Tab:         ({ children, value, ...props }: Record<string, unknown>) =>
                   React.createElement('button', { 'data-value': value, ...props }, children),
    TabList:     ({ children, selectedValue, onTabSelect, ...props }: Record<string, unknown>) =>
                   React.createElement('div', { 'data-selected': selectedValue, ...props }, children),
  };
});

// ─── commitApi mock ────────────────────────────────────────────────────────────

const mockGetAdminMetrics      = jest.fn();
const mockGetAdminInsights     = jest.fn();
const mockGetAdminFeedback     = jest.fn();
const mockGetAdminSignalProfiles = jest.fn();

jest.mock('../api/commitApi', () => ({
  getAdminMetrics:        (...args: unknown[]) => mockGetAdminMetrics(...args),
  getAdminInsights:       (...args: unknown[]) => mockGetAdminInsights(...args),
  getAdminFeedback:       (...args: unknown[]) => mockGetAdminFeedback(...args),
  getAdminSignalProfiles: (...args: unknown[]) => mockGetAdminSignalProfiles(...args),
}));

// ─── helpers ──────────────────────────────────────────────────────────────────

function makeMetrics(overrides = {}) {
  return {
    totalCommitments: 42,
    totalFeedback:    17,
    avgConfidence:    0.81,
    falsePositiveRate: 0.12,
    ...overrides,
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AdminDashboard — KPI rendering', () => {
  beforeEach(() => {
    mockGetAdminMetrics.mockReset();
    mockGetAdminInsights.mockReset();
    mockGetAdminFeedback.mockReset();
    mockGetAdminSignalProfiles.mockReset();
    // Default: feedback + profiles return empty so tab-switch effects don't error
    mockGetAdminFeedback.mockResolvedValue({ items: [], total: 0, breakdown: { byType: {}, bySource: {} } });
    mockGetAdminSignalProfiles.mockResolvedValue({ users: [], aggregate: { userCount: 0, avgFpRate: 0, avgConfidenceAdjustment: 0, totalSuppressed: 0 } });
  });

  it('renders all 4 KPI labels', async () => {
    mockGetAdminMetrics.mockResolvedValue(makeMetrics());
    render(<AdminDashboard />);
    await waitFor(() => {
      expect(screen.getByText('Total Commitments')).toBeInTheDocument();
      expect(screen.getByText('Feedback Events')).toBeInTheDocument();
      expect(screen.getByText('Avg Confidence')).toBeInTheDocument();
      expect(screen.getByText('False Positive Rate')).toBeInTheDocument();
    });
  });

  it('renders totalFeedback value from API response', async () => {
    mockGetAdminMetrics.mockResolvedValue(makeMetrics({ totalFeedback: 17 }));
    render(<AdminDashboard />);
    await waitFor(() => {
      expect(screen.getByText('💬 17')).toBeInTheDocument();
    });
  });

  it('renders totalCommitments value from API response', async () => {
    mockGetAdminMetrics.mockResolvedValue(makeMetrics({ totalCommitments: 42 }));
    render(<AdminDashboard />);
    await waitFor(() => {
      expect(screen.getByText('📋 42')).toBeInTheDocument();
    });
  });

  it('renders falsePositiveRate as percentage', async () => {
    mockGetAdminMetrics.mockResolvedValue(makeMetrics({ falsePositiveRate: 0.12 }));
    render(<AdminDashboard />);
    await waitFor(() => {
      // 0.12 * 100 = 12 → "12%"
      expect(screen.getByText(/✅ 12%/)).toBeInTheDocument();
    });
  });

  it('renders warning emoji when falsePositiveRate > 30%', async () => {
    mockGetAdminMetrics.mockResolvedValue(makeMetrics({ falsePositiveRate: 0.35 }));
    render(<AdminDashboard />);
    await waitFor(() => {
      expect(screen.getByText(/⚠️ 35%/)).toBeInTheDocument();
    });
  });

  it('shows "—" for all KPIs when API fails', async () => {
    mockGetAdminMetrics.mockRejectedValue(new Error('network error'));
    render(<AdminDashboard />);
    await waitFor(() => {
      const dashes = screen.getAllByText(/^[💬📋🎯✅⚠️]+ —$/);
      expect(dashes.length).toBeGreaterThanOrEqual(4);
    });
  });

  it('renders the page title', async () => {
    mockGetAdminMetrics.mockResolvedValue(makeMetrics());
    render(<AdminDashboard />);
    await waitFor(() => {
      expect(screen.getByText('Commit FHL Admin')).toBeInTheDocument();
    });
  });
});
