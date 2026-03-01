import { useTranslation } from 'react-i18next';
import {
  Card,
  CardHeader,
  Text,
  Badge,
  Divider,
  Skeleton,
  SkeletonItem,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import { useSpring, animated } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS } from '../../config/psychology.config';
import type { CommitmentRecord, EisenhowerQuadrant } from '../../../../api/src/types/index';

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
  meeting: '\u{1F4F9}',
  chat: '\u{1F4AC}',
  email: '\u{1F4E7}',
  ado: '\u{1F527}',
};

interface CommitPaneProps {
  commitments: CommitmentRecord[];
  isLoading: boolean;
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
}: {
  commitment: CommitmentRecord;
  onClick?: () => void;
  delay: number;
}): JSX.Element {
  const { t } = useTranslation();
  const styles = useStyles();
  const reducedMotion = useReducedMotion();
  const isAtRisk = commitment.impactScore > 40 && commitment.status === 'pending';

  const spring = useSpring({
    config: SPRING_CONFIGS.smooth,
    from: { opacity: 0, translateY: reducedMotion ? 0 : 8 },
    to:   { opacity: 1, translateY: 0 },
    delay: reducedMotion ? 0 : delay,
  });

  const daysUntilDue = commitment.dueAt
    ? Math.ceil((commitment.dueAt.getTime() - Date.now()) / (1000 * 60 * 60 * 24))
    : null;

  return (
    <animated.div style={spring}>
      <Card
        className={styles.commitCard}
        onClick={onClick}
        data-testid={`commit-card-${commitment.rowKey}`}
      >
        <CardHeader
          header={
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
              <Text weight="semibold" truncate>
                {commitment.title}
              </Text>
              {isAtRisk && (
                <Badge appearance="filled" className={styles.atRiskBadge}>
                  {t('commitPane.card.impactScore')}: {commitment.impactScore}
                </Badge>
              )}
            </div>
          }
          description={
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <Text size={100} className={styles.sourceIcon}>
                {SOURCE_ICONS[commitment.source.type] ?? '\u{1F4CC}'}
              </Text>
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
          }
        />
      </Card>
    </animated.div>
  );
}

/**
 * Main commitment pane — Eisenhower board with Morning Digest.
 * All strings from react-i18next (P-17). All colors from Fluent tokens (P-15).
 * Animations from @react-spring/web with reduced-motion support (P-27).
 */
export function CommitPane({ commitments, isLoading, onCommitmentClick }: CommitPaneProps): JSX.Element {
  const { t } = useTranslation();
  const styles = useStyles();

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
        <Badge appearance="outline" color="informative">
          {commitments.length}
        </Badge>
      </div>

      <Divider />

      {!hasAny && (
        <div className={styles.emptyState} data-testid="empty-state">
          <Text size={700}>{'\u2728'}</Text>
          <Text weight="semibold">{t('commitPane.empty')}</Text>
          <Text size={200} className={styles.sourceIcon}>{t('commitPane.emptySubtitle')}</Text>
        </div>
      )}

      {QUADRANT_ORDER.map(quadrant => {
        const items = grouped[quadrant];
        if (items.length === 0) return null;
        return (
          <div key={quadrant}>
            <Text className={styles.quadrantLabel}>{quadrantLabels[quadrant]}</Text>
            {items.map((c, idx) => (
              <CommitCard
                key={c.rowKey}
                commitment={c}
                onClick={() => onCommitmentClick?.(c)}
                delay={idx * 40}
              />
            ))}
          </div>
        );
      })}
    </div>
  );
}
