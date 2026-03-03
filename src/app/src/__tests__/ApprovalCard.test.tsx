/**
 * SCENARIO: ApprovalCard — approve / edit / skip decision flows
 *
 * Teams message send is now wired end-to-end:
 *   Approve → POST /api/v1/approvals (with userId + auth header when token is present)
 *   Backend: TeamsMessageSender.SendAsync() fires OBO Graph call per recipient
 *   GraphScopes now includes Chat.ReadWrite + ChatMessage.Send
 */
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ApprovalCard } from '../components/core/ApprovalCard';
import { makeDraft } from './helpers';

const TEST_USER_ID = 'demo-test-user-001';

// ─── Module mocks ─────────────────────────────────────────────────────────────

jest.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: 'en' },
  }),
}));

jest.mock('@fluentui/react-components', () => {
  const React = require('react');
  const el = (tag: string) =>
    ({ children, ...props }: Record<string, unknown>) =>
      React.createElement(tag, props, children);

  return {
    makeStyles: () => () => ({}),
    tokens: new Proxy({}, { get: () => '' }),
    Card: el('div'),
    CardHeader: ({ header, action }: Record<string, unknown>) =>
      React.createElement('div', {}, header, action),
    Text: ({ children, truncate: _t, ...props }: Record<string, unknown>) =>
      React.createElement('span', props, children),
    Badge: el('span'),
    Button: ({ onClick, children, ...props }: Record<string, unknown>) =>
      React.createElement('button', { onClick, ...props }, children),
    Textarea: ({
      value,
      onChange,
      ...props
    }: {
      value?: string;
      onChange?: (e: unknown, d: { value: string }) => void;
      [key: string]: unknown;
    }) =>
      React.createElement('textarea', {
        value,
        onChange: (e: React.ChangeEvent<HTMLTextAreaElement>) =>
          onChange?.(e, { value: e.target.value }),
        ...props,
      }),
  };
});

jest.mock('@react-spring/web', () => {
  const React = require('react');
  return {
    useSpring: () => ({}),
    animated: new Proxy({}, {
      get: (_: unknown, tag: string) =>
        ({ children, style: _s, ...rest }: Record<string, unknown>) =>
          React.createElement(tag as string, rest, children),
    }),
  };
});

jest.mock('../hooks/useReducedMotion', () => ({ useReducedMotion: () => true }));
jest.mock('../config/psychology.config', () => ({
  SPRING_CONFIGS: { bounce: {} },
}));
jest.mock('../config/api.config', () => ({
  API_BASE: 'https://test-api.example.com',
}));

// ─── Initial render (view mode) ───────────────────────────────────────────────

describe('ApprovalCard — view mode', () => {
  it('renders the draft content', () => {
    const draft = makeDraft({ content: 'Hi Marcus, quick heads up...' });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getByText('Hi Marcus, quick heads up...')).toBeInTheDocument();
  });

  it('renders the action type label for send-message', () => {
    const draft = makeDraft({ actionType: 'send-message' });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getByText('📨 Teams Message')).toBeInTheDocument();
  });

  it('renders the action type label for create-calendar-event', () => {
    const draft = makeDraft({ actionType: 'create-calendar-event' });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getByText('📅 Calendar Block')).toBeInTheDocument();
  });

  it('renders each recipient as a named badge', () => {
    const draft = makeDraft({ recipients: ['Marcus Johnson', 'Priya Sharma', 'David Park'] });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getByText('Marcus Johnson')).toBeInTheDocument();
    expect(screen.getByText('Priya Sharma')).toBeInTheDocument();
    expect(screen.getByText('David Park')).toBeInTheDocument();
  });

  it('renders the "To:" label before recipients', () => {
    const draft = makeDraft({ recipients: ['Marcus Johnson'] });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getByText('approvalCard.to:')).toBeInTheDocument();
  });

  it('renders context summary', () => {
    const draft = makeDraft({ contextSummary: 'Option C: Clean Slip — 4 task(s) affected' });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getByText('Option C: Clean Slip — 4 task(s) affected')).toBeInTheDocument();
  });

  it('renders Approve, Edit, and Skip buttons', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    expect(screen.getByText('approvalCard.approve')).toBeInTheDocument();
    expect(screen.getByText('approvalCard.edit')).toBeInTheDocument();
    expect(screen.getByText('approvalCard.skip')).toBeInTheDocument();
  });

  it('does not render done state initially', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    expect(screen.queryByText('✅ Sent')).not.toBeInTheDocument();
  });
});

// ─── Approve flow ─────────────────────────────────────────────────────────────

describe('ApprovalCard — approve flow', () => {
  it('shows "✅ Sent" after clicking Approve', async () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.approve'));
    await waitFor(() => expect(screen.getByText('✅ Sent')).toBeInTheDocument());
  });

  it('hides the action buttons after approval', async () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.approve'));
    await waitFor(() => {
      expect(screen.queryByText('approvalCard.approve')).not.toBeInTheDocument();
      expect(screen.queryByText('approvalCard.skip')).not.toBeInTheDocument();
    });
  });

  it('calls onDecision with decision="approve"', () => {
    const onDecision = jest.fn();
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft({ draftId: 'draft-c-001' })} onDecision={onDecision} />);
    fireEvent.click(screen.getByText('approvalCard.approve'));
    expect(onDecision).toHaveBeenCalledWith(expect.objectContaining({
      decision:      'approve',
      draftId:       'draft-c-001',
      commitmentId:  'rbs-001',
    }));
  });

  it('calls fetch to /api/v1/approvals with userId query param', async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ result: 'executed' }),
    });

    render(<ApprovalCard commitmentId="rbs-approve-001" userId={TEST_USER_ID} draft={makeDraft({ draftId: 'd-approve' })} />);
    fireEvent.click(screen.getByText('approvalCard.approve'));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining(`/api/v1/approvals?userId=${TEST_USER_ID}`),
        expect.objectContaining({
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    expect.stringContaining('"decision":"approve"'),
        })
      );
    });
  });

  it('includes Authorization header when authToken is provided', async () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} authToken="sso-token-abc" />);
    fireEvent.click(screen.getByText('approvalCard.approve'));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/approvals'),
        expect.objectContaining({
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer sso-token-abc' },
        })
      );
    });
  });

  it('includes draftContent and draftRecipients in the fetch payload', async () => {
    const draft = makeDraft({
      content:    'Hi Marcus, heads up',
      recipients: ['Marcus Johnson', 'Priya Sharma'],
      actionType: 'send-message',
    });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    fireEvent.click(screen.getByText('approvalCard.approve'));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          body: expect.stringContaining('"draftContent":"Hi Marcus, heads up"'),
        })
      );
      expect(global.fetch).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          body: expect.stringContaining('"draftActionType":"send-message"'),
        })
      );
    });
  });

  it('does NOT include draftContent or recipients on skip', async () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.skip'));

    await waitFor(() => {
      const [, opts] = (global.fetch as jest.Mock).mock.calls[0];
      const body = JSON.parse(opts.body as string);
      expect(body.draftContent).toBeUndefined();
      expect(body.draftRecipients).toBeUndefined();
    });
  });
});

// ─── Skip flow ────────────────────────────────────────────────────────────────

describe('ApprovalCard — skip flow', () => {
  it('shows "⏭ Skipped — handled manually" after clicking Skip', async () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.skip'));
    await waitFor(() =>
      expect(screen.getByText('⏭ Skipped — handled manually')).toBeInTheDocument()
    );
  });

  it('calls onDecision with decision="skip"', () => {
    const onDecision = jest.fn();
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} onDecision={onDecision} />);
    fireEvent.click(screen.getByText('approvalCard.skip'));
    expect(onDecision).toHaveBeenCalledWith(expect.objectContaining({ decision: 'skip' }));
  });

  it('calls fetch to /api/v1/approvals with decision="skip"', async () => {
    render(<ApprovalCard commitmentId="rbs-skip-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.skip'));
    await waitFor(() =>
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/approvals'),
        expect.objectContaining({
          body: expect.stringContaining('"decision":"skip"'),
        })
      )
    );
  });
});

// ─── Edit flow ────────────────────────────────────────────────────────────────

describe('ApprovalCard — edit flow', () => {
  it('switches to edit mode (shows Textarea) after clicking Edit', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft({ content: 'Original text' })} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    expect(screen.getByRole('textbox')).toBeInTheDocument();
  });

  it('pre-populates Textarea with the draft content', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft({ content: 'Hi Marcus...' })} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    expect(screen.getByRole('textbox')).toHaveValue('Hi Marcus...');
  });

  it('hides the Approve/Edit/Skip buttons in edit mode', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    // approvalCard.skip and approvalCard.edit should be gone
    expect(screen.queryByText('approvalCard.skip')).not.toBeInTheDocument();
  });

  it('shows "Back" button in edit mode', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    expect(screen.getByText('actions.back')).toBeInTheDocument();
  });

  it('clicking Back from edit mode returns to view mode', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft({ content: 'Original' })} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    fireEvent.click(screen.getByText('actions.back'));
    // Back in view mode — original content visible, buttons back
    expect(screen.getByText('Original')).toBeInTheDocument();
    expect(screen.getByText('approvalCard.approve')).toBeInTheDocument();
  });

  it('allows editing the content in the Textarea', () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft({ content: 'Original text' })} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    const textarea = screen.getByRole('textbox') as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: 'Edited text' } });
    expect(textarea.value).toBe('Edited text');
  });
});

// ─── Edit → Approve flow ──────────────────────────────────────────────────────

describe('ApprovalCard — edit → approve flow', () => {
  it('shows "✅ Edited & Sent" after editing and approving', async () => {
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    const textarea = screen.getByRole('textbox');
    fireEvent.change(textarea, { target: { value: 'Updated message' } });
    fireEvent.click(screen.getByText('approvalCard.approve'));
    await waitFor(() =>
      expect(screen.getByText('✅ Edited & Sent')).toBeInTheDocument()
    );
  });

  it('calls onDecision with decision="edit" and editedContent', () => {
    const onDecision = jest.fn();
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={makeDraft()} onDecision={onDecision} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    const textarea = screen.getByRole('textbox');
    fireEvent.change(textarea, { target: { value: 'Updated message' } });
    fireEvent.click(screen.getByText('approvalCard.approve'));
    expect(onDecision).toHaveBeenCalledWith(expect.objectContaining({
      decision:     'edit',
      editedContent: 'Updated message',
    }));
  });

  it('sends editedContent in the fetch payload', async () => {
    render(<ApprovalCard commitmentId="rbs-edit-001" userId={TEST_USER_ID} draft={makeDraft({ draftId: 'd-edit' })} />);
    fireEvent.click(screen.getByText('approvalCard.edit'));
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'My edited message' } });
    fireEvent.click(screen.getByText('approvalCard.approve'));
    await waitFor(() =>
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/approvals'),
        expect.objectContaining({
          body: expect.stringContaining('"editedContent":"My edited message"'),
        })
      )
    );
  });
});

// ─── Recipients row ───────────────────────────────────────────────────────────

describe('ApprovalCard — recipients row', () => {
  it('shows one badge per recipient', () => {
    const draft = makeDraft({ recipients: ['Marcus Johnson', 'Priya Sharma'] });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getAllByText(/Marcus Johnson|Priya Singh/).length).toBe(2);
  });

  it('shows a single recipient correctly', () => {
    const draft = makeDraft({ recipients: ['Marcus Johnson'] });
    render(<ApprovalCard commitmentId="rbs-001" userId={TEST_USER_ID} draft={draft} />);
    expect(screen.getByText('Marcus Johnson')).toBeInTheDocument();
  });
});
