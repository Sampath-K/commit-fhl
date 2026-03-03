import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Text,
  Textarea,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import { animated, useSpring } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS } from '../../config/psychology.config';
import { API_BASE } from '../../config/api.config';
import type { AgentDraft, ApprovalDecision } from '../../types/api';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  card: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalM,
    border: `2px solid ${tokens.colorBrandStroke1}`,
    borderRadius: tokens.borderRadiusLarge,
    boxShadow: tokens.shadow16,
  },
  contextStrip: {
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  recipientsRow: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalXS,
  },
  draftBox: {
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalS,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    whiteSpace: 'pre-wrap',
    fontFamily: tokens.fontFamilyBase,
    fontSize: tokens.fontSizeBase300,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  approved: {
    color: tokens.colorPaletteGreenForeground1,
    fontWeight: tokens.fontWeightSemibold,
  },
  skipped: {
    color: tokens.colorNeutralForeground4,
  },
});

// ─── Types ────────────────────────────────────────────────────────────────────

interface ApprovalCardProps {
  commitmentId: string;
  userId: string;
  draft: AgentDraft;
  onDecision?: (decision: ApprovalDecision) => void;
  /** Teams SSO token — when present, sent as Authorization header so the backend
   *  can perform the OBO exchange and dispatch a real Teams message. */
  authToken?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ApprovalCard({
  commitmentId,
  userId,
  draft,
  onDecision,
  authToken,
}: ApprovalCardProps): JSX.Element {
  const { t } = useTranslation();
  const styles  = useStyles();
  const reduced = useReducedMotion();

  const [mode, setMode] = useState<'view' | 'edit' | 'done'>('view');
  const [editContent, setEditContent] = useState(draft.content);
  const [finalDecision, setFinalDecision] = useState<'approved' | 'edited' | 'skipped' | null>(null);

  const cardSpring = useSpring({
    from:   { opacity: 0, transform: 'scale(0.95)' },
    to:     { opacity: 1, transform: 'scale(1)' },
    config: SPRING_CONFIGS.bounce,
    immediate: reduced,
  });

  function postApproval(payload: ApprovalDecision): void {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
    void fetch(`${API_BASE}/api/v1/approvals?userId=${encodeURIComponent(userId)}`, {
      method:  'POST',
      headers,
      body:    JSON.stringify(payload),
    });
  }

  function handleDecision(decision: 'approve' | 'edit' | 'skip'): void {
    if (decision === 'edit') {
      setMode('edit');
      return;
    }

    const payload: ApprovalDecision = {
      draftId:          draft.draftId,
      commitmentId,
      decision,
      editedContent:    decision === 'skip' ? undefined : editContent,
      draftContent:     decision === 'skip' ? undefined : draft.content,
      draftActionType:  decision === 'skip' ? undefined : draft.actionType,
      draftRecipients:  decision === 'skip' ? undefined : draft.recipients,
    };

    setFinalDecision(decision === 'approve' ? 'approved' : 'skipped');
    setMode('done');
    onDecision?.(payload);
    postApproval(payload);
  }

  function handleEditApprove(): void {
    const payload: ApprovalDecision = {
      draftId:         draft.draftId,
      commitmentId,
      decision:        'edit',
      editedContent:   editContent,
      draftContent:    draft.content,
      draftActionType: draft.actionType,
      draftRecipients: draft.recipients,
    };
    setFinalDecision('edited');
    setMode('done');
    onDecision?.(payload);
    postApproval(payload);
  }

  const actionTypeLabel: Record<AgentDraft['actionType'], string> = {
    'send-message':          '📨 Teams Message',
    'create-calendar-event': '📅 Calendar Block',
    'post-pr-comment':       '💬 PR Comment',
    'send-email':            '✉️ Email',
  };

  return (
    <animated.div style={cardSpring}>
      <Card className={styles.card}>
        <CardHeader
          header={
            <Text size={400} weight="semibold">
              {actionTypeLabel[draft.actionType] ?? t('approvalCard.draft')}
            </Text>
          }
        />

        {/* Recipients row */}
        <div className={styles.recipientsRow}>
          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
            {t('approvalCard.to')}:
          </Text>
          {draft.recipients.map(name => (
            <Badge key={name} appearance="tint" color="informative" size="small">
              {name}
            </Badge>
          ))}
        </div>

        {/* Context strip */}
        <div className={styles.contextStrip}>
          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
            {t('approvalCard.context')}:
          </Text>
          <Text size={200}>{draft.contextSummary}</Text>
        </div>

        {/* Draft content or done state */}
        {mode === 'done' ? (
          <Text
            size={300}
            className={finalDecision === 'skipped' ? styles.skipped : styles.approved}>
            {finalDecision === 'approved'  && '✅ Sent'}
            {finalDecision === 'edited'    && '✅ Edited & Sent'}
            {finalDecision === 'skipped'   && '⏭ Skipped — handled manually'}
          </Text>
        ) : mode === 'edit' ? (
          <>
            <Textarea
              value={editContent}
              onChange={(_, d) => setEditContent(d.value)}
              resize="vertical"
              style={{ minHeight: '100px' }}
            />
            <div className={styles.actions}>
              <Button appearance="primary" onClick={handleEditApprove}>
                {t('approvalCard.approve')}
              </Button>
              <Button appearance="transparent" onClick={() => setMode('view')}>
                {t('actions.back')}
              </Button>
            </div>
          </>
        ) : (
          <>
            <div className={styles.draftBox}>{draft.content}</div>
            <div className={styles.actions}>
              <Button
                appearance="primary"
                onClick={() => handleDecision('approve')}>
                {t('approvalCard.approve')}
              </Button>
              <Button
                appearance="secondary"
                onClick={() => handleDecision('edit')}>
                {t('approvalCard.edit')}
              </Button>
              <Button
                appearance="transparent"
                onClick={() => handleDecision('skip')}>
                {t('approvalCard.skip')}
              </Button>
            </div>
          </>
        )}
      </Card>
    </animated.div>
  );
}

export default ApprovalCard;
