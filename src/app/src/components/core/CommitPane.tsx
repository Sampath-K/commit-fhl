import { useState, useMemo } from 'react';
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
import { useSpring, animated } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS } from '../../config/psychology.config';
import { TEAM_BY_USER } from '../../config/teams.config';
import type { CommitmentRecord, EisenhowerQuadrant } from '../../types/api';

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

  const spring = useSpring({
    config: SPRING_CONFIGS.smooth,
    from: { opacity: 0, translateY: reducedMotion ? 0 : 8 },
    to:   { opacity: 1, translateY: 0 },
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
        {/* Resolution reason — the "aha moment" shown when the system auto-resolved */}
        {commitment.status === 'done' && commitment.resolutionReason && (
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
 * Main commitment pane — Eisenhower board with Morning Digest.
 * Supports two view modes: By Priority (Eisenhower quadrants) and By Project.
 * All strings from react-i18next (P-17). All colors from Fluent tokens (P-15).
 * Animations from @react-spring/web with reduced-motion support (P-27).
 */
export function CommitPane({ commitments, isLoading, currentUserId, onCommitmentClick }: CommitPaneProps): JSX.Element {
  const { t } = useTranslation();
  const styles = useStyles();
  const [viewMode, setViewMode] = useState<'priority' | 'project'>('priority');

  const quadrantLabels: Record<EisenhowerQuadrant, string> = {
    'urgent-important':        t('commitPane.quadrants.urgentImportant'),
    'not-urgent-important':    t('commitPane.quadrants.notUrgentImportant'),
    'urgent-not-important':    t('commitPane.quadrants.urgentNotImportant'),
    'not-urgent-not-important': t('commitPane.quadrants.notUrgentNotImportant'),
  };

  const grouped = QUADRANT_ORDER.reduce<Record<EisenhowerQuadrant, CommitmentRecord[]>>(
    (acc, q) => {
      acc[q] = commitments.filter(c => c.priority === q);
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
    commitments.forEach(c => {
      const key = c.projectContext ?? 'General';
      if (!groups[key]) groups[key] = [];
      groups[key].push(c);
    });
    // Sort alphabetically; 'General' always last
    return Object.entries(groups).sort(([a], [b]) =>
      a === 'General' ? 1 : b === 'General' ? -1 : a.localeCompare(b)
    );
  }, [commitments]);

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
          <Badge appearance="outline" color="informative" style={{ marginRight: '8px' }}>
            {commitments.length}
          </Badge>
          <Button
            size="small"
            appearance={viewMode === 'priority' ? 'primary' : 'subtle'}
            onClick={() => setViewMode('priority')}
          >
            Priority
          </Button>
          <Button
            size="small"
            appearance={viewMode === 'project' ? 'primary' : 'subtle'}
            onClick={() => setViewMode('project')}
          >
            Project
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

      {/* ── By Priority view (Eisenhower quadrants) ─────────────────────────── */}
      {viewMode === 'priority' && QUADRANT_ORDER.map(quadrant => {
        const items = grouped[quadrant];
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
    </div>
  );
}
