import { useState, useMemo, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Card,
  CardHeader,
  Text,
  Badge,
  Button,
  Divider,
  Skeleton,
  SkeletonItem,
  Tooltip,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import { recordFeedback } from '../../api/commitApi';
import { useSpring, animated } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS } from '../../config/psychology.config';
import { TEAM_BY_USER } from '../../config/teams.config';
import { API_BASE } from '../../config/api.config';
import { useDeliveryScore } from '../../hooks/useDeliveryScore';
import { DeliveryScore } from '../psychology/DeliveryScore';
import { StreakBadge } from '../psychology/StreakBadge';
import { CompetencyLevel } from '../psychology/CompetencyLevel';
import type { CommitmentRecord, EisenhowerQuadrant } from '../../types/api';

// XP thresholds mirroring MotivationService.cs
const XP_THRESHOLDS = [0, 100, 300, 600, 1000] as const;

function computeLevelProgress(totalXp: number, level: number): number {
  const start = XP_THRESHOLDS[level - 1] ?? 0;
  const end   = XP_THRESHOLDS[level] ?? null;
  if (end === null) return 1;
  return Math.min(1, Math.max(0, (totalXp - start) / (end - start)));
}

function computeXpToNext(totalXp: number, level: number): number | null {
  const end = XP_THRESHOLDS[level] ?? null;
  return end !== null ? Math.max(0, end - totalXp) : null;
}

const useStyles = makeStyles({
  pane: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    backgroundColor: tokens.colorNeutralBackground1,
    padding: tokens.spacingVerticalM,
    gap: tokens.spacingVerticalS,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingBottom: tokens.spacingVerticalS,
  },
  quadrantLabel: {
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground3,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
    paddingTop: tokens.spacingVerticalS,
  },
  commitCard: {
    cursor: 'pointer',
    transition: `box-shadow ${tokens.durationFast} ease, transform ${tokens.durationFast} ease`,
    ':hover': {
      boxShadow: tokens.shadow8,
      transform: 'translateY(-2px)',
    },
  },
  atRiskBadge: {
    backgroundColor: tokens.colorStatusWarningBackground1,
    color: tokens.colorStatusWarningForeground1,
  },
  impactChip: {
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground2,
    fontSize: tokens.fontSizeBase100,
  },
  sourceLink: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorBrandForeground1,
    minWidth: 'unset',
    padding: '0 2px',
    height: 'auto',
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    height: '200px',
    gap: tokens.spacingVerticalS,
    color: tokens.colorNeutralForeground3,
  },
  sourceIcon: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground4,
  },
  morningDigest: {
    backgroundColor: tokens.colorBrandBackground2,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalS,
  },
  // ── Progress view ──────────────────────────────────────────────────────────
  psychologyPanel: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    alignItems: 'flex-start',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  weekStat: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalM}`,
    backgroundColor: tokens.colorStatusSuccessBackground1,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorStatusSuccessBorder1}`,
    gap: tokens.spacingVerticalXS,
  },
  weekStatNumber: {
    fontSize: '40px',
    fontWeight: tokens.fontWeightBold,
    lineHeight: '1',
    color: tokens.colorStatusSuccessForeground1,
  },
  progressTrack: {
    height: '6px',
    backgroundColor: tokens.colorNeutralBackground4,
    borderRadius: '3px',
    overflow: 'hidden',
    marginTop: tokens.spacingVerticalXS,
  },
  completionCard: {
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
    borderLeft: `3px solid ${tokens.colorStatusSuccessBorder1}`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: `0 ${tokens.borderRadiusSmall} ${tokens.borderRadiusSmall} 0`,
    marginBottom: tokens.spacingVerticalXS,
  },
  dayLabel: {
    fontSize: tokens.fontSizeBase100,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground3,
    textTransform: 'uppercase',
    letterSpacing: '0.06em',
    paddingTop: tokens.spacingVerticalS,
    paddingBottom: '2px',
  },
});

const QUADRANT_ORDER: EisenhowerQuadrant[] = [
  'urgent-important',
  'not-urgent-important',
  'urgent-not-important',
  'not-urgent-not-important',
];

const SOURCE_ICONS: Record<string, string> = {
  meeting:  '\u{1F4F9}',  // 📹
  chat:     '\u{1F4AC}',  // 💬
  email:    '\u{1F4E7}',  // 📧
  ado:      '\u{1F527}',  // 🔧
  drive:    '\u{1F4C4}',  // 📄
  planner:  '\u{1F4CB}',  // 📋
};

interface CommitPaneProps {
  commitments: CommitmentRecord[];
  isLoading: boolean;
  currentUserId?: string;
  onCommitmentClick?: (commitment: CommitmentRecord) => void;
}

/** Morning Digest skeleton while data loads */
function MorningDigestSkeleton(): JSX.Element {
  const styles = useStyles();
  return (
    <div className={styles.morningDigest} data-testid="morning-digest">
      <Skeleton>
        <SkeletonItem size={16} style={{ width: '60%', marginBottom: '8px' }} />
        <SkeletonItem size={12} style={{ width: '40%' }} />
      </Skeleton>
    </div>
  );
}

/** A single commitment card */
function CommitCard({
  commitment,
  onClick,
  delay,
  currentUserId,
}: {
  commitment: CommitmentRecord;
  onClick?: () => void;
  delay: number;
  currentUserId?: string;
}): JSX.Element {
  const { t } = useTranslation();
  const styles = useStyles();
  const reducedMotion = useReducedMotion();
  const isAtRisk = commitment.impactScore > 40 && commitment.status === 'pending';
  const ownerTeam = TEAM_BY_USER[commitment.owner];
  const [isDone, setIsDone] = useState(commitment.status === 'done');
  const [markingDone, setMarkingDone] = useState(false);
  const [feedbackState, setFeedbackState] = useState<'idle' | 'confirmed' | 'dismissed'>('idle');
  const [feedbackMsg, setFeedbackMsg] = useState('');

  const handleMarkDone = useCallback(async (e: React.MouseEvent) => {
    e.stopPropagation();
    setMarkingDone(true);
    try {
      await fetch(
        `${API_BASE}/api/v1/commitments/${encodeURIComponent(commitment.owner)}/${encodeURIComponent(commitment.id)}`,
        {
          method: 'PATCH',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ status: 'done', resolutionReason: 'Marked done by user' }),
        }
      );
      setIsDone(true);
    } finally {
      setMarkingDone(false);
    }
  }, [commitment.owner, commitment.id]);

  const handleFeedback = useCallback(async (type: 'Confirm' | 'FalsePositive', e: React.MouseEvent) => {
    e.stopPropagation();
    if (!currentUserId) return;
    try {
      await recordFeedback(currentUserId, commitment.id, type);
      if (type === 'Confirm') {
        setFeedbackState('confirmed');
        setFeedbackMsg('Marked as useful');
        setTimeout(() => setFeedbackState('idle'), 2500);
      } else {
        setFeedbackMsg("Won't show similar tasks");
        setFeedbackState('dismissed');
      }
    } catch {
      // Best-effort — silently ignore feedback errors
    }
  }, [currentUserId, commitment.id]);

  const spring = useSpring({
    config: SPRING_CONFIGS.smooth,
    from: { opacity: 0, translateY: reducedMotion ? 0 : 8 },
    to:   {
      opacity: (isDone || feedbackState === 'dismissed') ? 0 : 1,
      translateY: (isDone || feedbackState === 'dismissed') ? -8 : 0,
    },
    delay: reducedMotion ? 0 : delay,
  });

  const daysUntilDue = commitment.dueAt
    ? Math.ceil((new Date(commitment.dueAt).getTime() - Date.now()) / (1000 * 60 * 60 * 24))
    : null;

  return (
    <animated.div style={spring}>
      <Card
        className={styles.commitCard}
        onClick={onClick}
        data-testid={`commit-card-${commitment.id}`}
      >
        <CardHeader
          header={
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
              <Text weight="semibold" truncate>
                {commitment.title}
              </Text>
              {/* T-019: Impact score chip — visible on all cards */}
              {commitment.impactScore > 0 && (
                <Tooltip content={t('commitPane.card.impactScoreTooltip')} relationship="description">
                  <Badge
                    appearance="tint"
                    className={isAtRisk ? styles.atRiskBadge : styles.impactChip}
                  >
                    {t('commitPane.card.impactScore')}: {commitment.impactScore}
                  </Badge>
                </Tooltip>
              )}
              {/* Team affiliation pill — shown for cross-team owners only */}
              {ownerTeam && commitment.owner !== currentUserId && (
                <Badge
                  appearance="tint"
                  style={{ backgroundColor: ownerTeam.color + '22', color: ownerTeam.color }}
                >
                  {ownerTeam.label}
                </Badge>
              )}
            </div>
          }
          description={
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
                {/* T-019: Clickable source link */}
                <Tooltip content={t('commitPane.card.openSource')} relationship="description">
                  <Button
                    as="a"
                    href={commitment.source.url || '#'}
                    target="_blank"
                    rel="noopener noreferrer"
                    appearance="transparent"
                    className={styles.sourceLink}
                    aria-label={t('commitPane.card.openSource')}
                  >
                    {SOURCE_ICONS[commitment.source.type] ?? '\u{1F4CC}'}
                  </Button>
                </Tooltip>
                {/* Artifact name — shown when it adds context beyond the title */}
                {commitment.artifactName && commitment.artifactName !== commitment.title && (
                  <Text size={100} style={{ color: tokens.colorNeutralForeground3, fontStyle: 'italic' }}>
                    {commitment.artifactName}
                  </Text>
                )}
                {daysUntilDue !== null && (
                  <Text size={100} className={styles.sourceIcon}>
                    {daysUntilDue < 0
                      ? t('commitPane.card.overdue', { days: Math.abs(daysUntilDue) })
                      : daysUntilDue === 0
                      ? t('commitPane.card.dueToday')
                      : t('commitPane.card.dueIn', { days: daysUntilDue })}
                  </Text>
                )}
                {commitment.blocks.length > 0 && (
                  <Text size={100} style={{ color: tokens.colorStatusDangerForeground1 }}>
                    {t('commitPane.card.blocking', { count: commitment.blocks.length })}
                  </Text>
                )}
              </div>
            </div>
          }
        />
        {/* Action row: Done + Feedback buttons */}
        {!isDone && feedbackState !== 'dismissed' && (
          <div style={{ padding: '0 12px 10px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            {/* Thumbs feedback */}
            <div style={{ display: 'flex', gap: '2px', alignItems: 'center' }}>
              {feedbackState === 'confirmed' ? (
                <Text size={100} style={{ color: tokens.colorStatusSuccessForeground1 }}>{feedbackMsg}</Text>
              ) : (
                <>
                  <Tooltip content="Useful — keep tracking" relationship="description">
                    <Button
                      appearance="transparent"
                      size="small"
                      aria-label="Mark as useful"
                      onClick={(e) => handleFeedback('Confirm', e)}
                      style={{ minWidth: 'unset', padding: '0 4px' }}
                    >
                      👍
                    </Button>
                  </Tooltip>
                  <Tooltip content="Not a real task — won't show again" relationship="description">
                    <Button
                      appearance="transparent"
                      size="small"
                      aria-label="Not a real task"
                      onClick={(e) => handleFeedback('FalsePositive', e)}
                      style={{ minWidth: 'unset', padding: '0 4px' }}
                    >
                      👎
                    </Button>
                  </Tooltip>
                </>
              )}
            </div>
            <Button
              appearance="subtle"
              size="small"
              disabled={markingDone}
              onClick={handleMarkDone}
              style={{ color: tokens.colorStatusSuccessForeground1 }}
            >
              {markingDone ? '...' : '✓ Done'}
            </Button>
          </div>
        )}
        {/* Resolution reason — the "aha moment" shown when the system auto-resolved */}
        {isDone && commitment.resolutionReason && (
          <div style={{
            margin: '0 12px 10px',
            padding: '6px 10px',
            borderRadius: '6px',
            backgroundColor: tokens.colorStatusSuccessBackground1,
            border: `1px solid ${tokens.colorStatusSuccessBorder1}`,
            display: 'flex',
            alignItems: 'flex-start',
            gap: '6px',
          }}>
            <span style={{ fontSize: '13px', lineHeight: '18px' }}>✅</span>
            <Text
              size={100}
              style={{ color: tokens.colorStatusSuccessForeground1, lineHeight: '18px' }}
            >
              {commitment.resolutionReason}
            </Text>
          </div>
        )}
      </Card>
    </animated.div>
  );
}

/**
 * Main commitment pane — Eisenhower board + By Project + Progress (psychology layer).
 * All strings from react-i18next (P-17). All colors from Fluent tokens (P-15).
 * Animations from @react-spring/web with reduced-motion support (P-27).
 */
export function CommitPane({ commitments, isLoading, currentUserId, onCommitmentClick }: CommitPaneProps): JSX.Element {
  const { t } = useTranslation();
  const styles = useStyles();
  const [viewMode, setViewMode] = useState<'priority' | 'project' | 'progress'>('priority');

  // Motivation state — fetched only when Progress tab is active
  const { data: motivationState } = useDeliveryScore(currentUserId ?? '');

  const quadrantLabels: Record<EisenhowerQuadrant, string> = {
    'urgent-important':        t('commitPane.quadrants.urgentImportant'),
    'not-urgent-important':    t('commitPane.quadrants.notUrgentImportant'),
    'urgent-not-important':    t('commitPane.quadrants.urgentNotImportant'),
    'not-urgent-not-important': t('commitPane.quadrants.notUrgentNotImportant'),
  };

  const grouped = QUADRANT_ORDER.reduce<Record<EisenhowerQuadrant, CommitmentRecord[]>>(
    (acc, q) => {
      acc[q] = commitments.filter(c => c.priority === q && c.status !== 'done');
      return acc;
    },
    {
      'urgent-important': [],
      'not-urgent-important': [],
      'urgent-not-important': [],
      'not-urgent-not-important': [],
    }
  );

  // Group by projectContext for the By Project view
  const groupedByProject = useMemo(() => {
    const groups: Record<string, CommitmentRecord[]> = {};
    commitments.filter(c => c.status !== 'done').forEach(c => {
      const key = c.projectContext ?? 'General';
      if (!groups[key]) groups[key] = [];
      groups[key].push(c);
    });
    // Sort alphabetically; 'General' always last
    return Object.entries(groups).sort(([a], [b]) =>
      a === 'General' ? 1 : b === 'General' ? -1 : a.localeCompare(b)
    );
  }, [commitments]);

  // Completed items for the Progress view — sorted newest first
  const doneItems = useMemo(() =>
    commitments
      .filter(c => c.status === 'done' || c.itemKind === 'completion')
      .sort((a, b) => {
        const ta = a.lastActivity ?? a.committedAt;
        const tb = b.lastActivity ?? b.committedAt;
        return new Date(tb).getTime() - new Date(ta).getTime();
      }),
    [commitments]
  );

  const now = Date.now();
  const sevenDaysMs = 7 * 24 * 60 * 60 * 1000;
  const oneDayMs    = 24 * 60 * 60 * 1000;
  const doneThisWeek = doneItems.filter(c => {
    const t = new Date(c.lastActivity ?? c.committedAt).getTime();
    return now - t < sevenDaysMs;
  }).length;

  // Group completions by human-readable day bucket
  function dayBucket(isoDate: string): string {
    const ms = now - new Date(isoDate).getTime();
    if (ms < oneDayMs)        return 'Today';
    if (ms < 2 * oneDayMs)   return 'Yesterday';
    if (ms < sevenDaysMs)    return 'This week';
    return 'Earlier';
  }

  const groupedByDay = useMemo(() => {
    const groups: Record<string, CommitmentRecord[]> = {};
    doneItems.forEach(c => {
      const bucket = dayBucket(c.lastActivity ?? c.committedAt);
      if (!groups[bucket]) groups[bucket] = [];
      groups[bucket].push(c);
    });
    const ORDER = ['Today', 'Yesterday', 'This week', 'Earlier'];
    return ORDER.filter(k => k in groups).map(k => [k, groups[k]] as [string, CommitmentRecord[]]);
  }, [doneItems]); // eslint-disable-line react-hooks/exhaustive-deps

  // Progress bar: done this week / total open+done items
  const totalItems = commitments.length;
  const progressFraction = totalItems > 0 ? doneThisWeek / totalItems : 0;

  // Motivation / level helpers
  const level = motivationState?.competencyLevel ?? 1;
  const totalXp = motivationState?.totalXp ?? 0;

  if (isLoading) {
    return (
      <div className={styles.pane} data-testid="commit-pane">
        <MorningDigestSkeleton />
        <Skeleton>
          {[0, 1, 2].map(i => (
            <SkeletonItem key={i} size={48} style={{ marginBottom: '8px' }} />
          ))}
        </Skeleton>
      </div>
    );
  }

  const hasAny = commitments.length > 0;

  return (
    <div className={styles.pane} data-testid="commit-pane">
      <div className={styles.header}>
        <Text size={500} weight="semibold">{t('commitPane.header.title')}</Text>
        <div style={{ display: 'flex', gap: '4px', alignItems: 'center' }}>
          <Badge appearance="outline" color="informative" style={{ marginRight: '4px' }}>
            {commitments.filter(c => c.status !== 'done').length}
          </Badge>
          <Button size="small" appearance={viewMode === 'priority' ? 'primary' : 'subtle'} onClick={() => setViewMode('priority')}>
            Priority
          </Button>
          <Button size="small" appearance={viewMode === 'project' ? 'primary' : 'subtle'} onClick={() => setViewMode('project')}>
            Project
          </Button>
          <Button size="small" appearance={viewMode === 'progress' ? 'primary' : 'subtle'} onClick={() => setViewMode('progress')}>
            Progress {doneThisWeek > 0 && <Badge size="small" appearance="filled" color="success" style={{ marginLeft: '4px' }}>{doneThisWeek}</Badge>}
          </Button>
        </div>
      </div>

      <Divider />

      {!hasAny && (
        <div className={styles.emptyState} data-testid="empty-state">
          <Text size={700}>{'\u2728'}</Text>
          <Text weight="semibold">{t('commitPane.empty')}</Text>
          <Text size={200} className={styles.sourceIcon}>{t('commitPane.emptySubtitle')}</Text>
        </div>
      )}

      {/* ── By Priority view (Eisenhower quadrants) — active items only ───── */}
      {viewMode === 'priority' && QUADRANT_ORDER.map(quadrant => {
        const items = grouped[quadrant].filter(c => c.status !== 'done');
        if (items.length === 0) return null;
        return (
          <div key={quadrant}>
            <Text className={styles.quadrantLabel}>{quadrantLabels[quadrant]}</Text>
            {items.map((c, idx) => (
              <CommitCard
                key={c.id}
                commitment={c}
                onClick={() => onCommitmentClick?.(c)}
                delay={idx * 40}
                currentUserId={currentUserId}
              />
            ))}
          </div>
        );
      })}

      {/* ── By Project view ──────────────────────────────────────────────────── */}
      {viewMode === 'project' && groupedByProject.map(([project, items]) => (
        <div key={project}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', paddingTop: tokens.spacingVerticalS }}>
            <Text className={styles.quadrantLabel}>{project}</Text>
            <Badge appearance="filled" size="small" color="informative">
              {items.length}
            </Badge>
          </div>
          {items.map((c, idx) => (
            <CommitCard
              key={c.id}
              commitment={c}
              onClick={() => onCommitmentClick?.(c)}
              delay={idx * 40}
              currentUserId={currentUserId}
            />
          ))}
        </div>
      ))}

      {/* ── Progress view — psychology layer + completion timeline ──────────── */}
      {viewMode === 'progress' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM }}>

          {/* Psychology panel: DeliveryScore + Streak + CompetencyLevel */}
          {motivationState && (
            <div className={styles.psychologyPanel}>
              <DeliveryScore state={motivationState} size={80} />
              <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS, flex: 1, minWidth: 0 }}>
                <StreakBadge streak={motivationState.streakDays} />
                <CompetencyLevel
                  level={level as 1 | 2 | 3 | 4 | 5}
                  progress={computeLevelProgress(totalXp, level)}
                  xpToNext={computeXpToNext(totalXp, level)}
                />
              </div>
            </div>
          )}

          {/* Weekly completion stat */}
          <div className={styles.weekStat}>
            <span className={styles.weekStatNumber}>{doneThisWeek}</span>
            <Text size={200} weight="semibold" style={{ color: tokens.colorStatusSuccessForeground1 }}>
              {doneThisWeek === 1 ? 'item completed this week' : 'items completed this week'}
            </Text>
            {/* Progress bar: completions / total items */}
            <div style={{ width: '100%', marginTop: tokens.spacingVerticalXS }}>
              <div className={styles.progressTrack}>
                <div
                  className={styles.progressTrack}
                  style={{
                    width: `${Math.round(progressFraction * 100)}%`,
                    backgroundColor: tokens.colorStatusSuccessForeground1,
                    height: '6px',
                    borderRadius: '3px',
                    margin: 0,
                  }}
                />
              </div>
              <Text size={100} style={{ color: tokens.colorStatusSuccessForeground1 }}>
                {Math.round(progressFraction * 100)}% of {totalItems} tracked items
              </Text>
            </div>
          </div>

          {/* Empty state */}
          {doneItems.length === 0 && (
            <div className={styles.emptyState}>
              <Text size={700}>🎯</Text>
              <Text weight="semibold">No completions yet this week</Text>
              <Text size={200} className={styles.sourceIcon}>
                Mark commitments done or merge a PR — they'll appear here
              </Text>
            </div>
          )}

          {/* Completion timeline, grouped by day */}
          {groupedByDay.map(([day, items]) => (
            <div key={day}>
              <Text className={styles.dayLabel}>{day} · {items.length}</Text>
              {items.map((c) => (
                <div key={c.id} className={styles.completionCard}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '8px' }}>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <Text size={200} weight="semibold" truncate style={{ display: 'block' }}>
                        {c.title}
                      </Text>
                      {c.resolutionReason && (
                        <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
                          {c.resolutionReason}
                        </Text>
                      )}
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '2px', flexShrink: 0 }}>
                      <Badge
                        appearance="tint"
                        color={c.itemKind === 'completion' ? 'success' : 'informative'}
                        size="small"
                      >
                        {SOURCE_ICONS[c.source.type] ?? '📌'} {c.itemKind === 'completion' ? 'shipped' : 'done'}
                      </Badge>
                      {c.projectContext && (
                        <Text size={100} style={{ color: tokens.colorNeutralForeground4 }}>
                          {c.projectContext}
                        </Text>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
