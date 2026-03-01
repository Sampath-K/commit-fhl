import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Badge,
  Button,
  Card,
  Divider,
  Skeleton,
  SkeletonItem,
  Text,
  Tooltip,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import { animated, useSpring, useTrail } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS, STAGGER_DELAYS } from '../../config/psychology.config';
import type { CascadeApiResponse, CommitmentRecord, ReplanApiOption } from '../../types/api';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalM,
    maxHeight: '80vh',
    overflowY: 'auto',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  impactBadge: {
    fontSize: tokens.fontSizeBase400,
    fontWeight: tokens.fontWeightBold,
  },
  chainItem: {
    display: 'flex',
    flexDirection: 'column',
    borderLeft: `3px solid ${tokens.colorNeutralStroke1}`,
    paddingLeft: tokens.spacingHorizontalM,
    paddingTop: tokens.spacingVerticalXS,
    paddingBottom: tokens.spacingVerticalXS,
    gap: '2px',
  },
  atRisk: {
    borderLeftColor: tokens.colorPaletteRedBorder2,
    backgroundColor: tokens.colorPaletteRedBackground1,
    borderRadius: tokens.borderRadiusMedium,
    paddingRight: tokens.spacingHorizontalS,
  },
  etaRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
  strikethrough: {
    textDecoration: 'line-through',
    color: tokens.colorNeutralForeground4,
  },
  replanSection: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalM,
  },
  replanOption: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingVerticalS,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'pointer',
    ':hover': { backgroundColor: tokens.colorNeutralBackground2 },
  },
  optionHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
    flexWrap: 'wrap',
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

interface CascadeViewProps {
  commitment: CommitmentRecord;
  userId: string;
  onClose: () => void;
  onReplanSelected?: (option: ReplanApiOption) => void;
}

export function CascadeView({
  commitment,
  userId,
  onClose,
  onReplanSelected,
}: CascadeViewProps): JSX.Element {
  const { t } = useTranslation();
  const styles = useStyles();
  const reduced = useReducedMotion();

  const [loading, setLoading] = useState<boolean>(false);
  const [cascadeData, setCascadeData] = useState<CascadeApiResponse | null>(null);
  const [replanOptions, setReplanOptions] = useState<ReplanApiOption[]>([]);
  const [showReplan, setShowReplan] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // ── Slide-in for the whole panel ─────────────────────────────────────────
  const panelSpring = useSpring({
    from:   { opacity: 0, transform: 'translateY(12px)' },
    to:     { opacity: 1, transform: 'translateY(0px)' },
    config: SPRING_CONFIGS.gentle,
    immediate: reduced,
  });

  // ── Stagger for chain items ───────────────────────────────────────────────
  const chainItems = cascadeData?.affectedTasks ?? [];
  const trail = useTrail(chainItems.length, {
    from:   { opacity: 0, transform: 'translateX(-8px)' },
    to:     { opacity: 1, transform: 'translateX(0px)' },
    config: { ...SPRING_CONFIGS.gentle, delay: STAGGER_DELAYS.cascadeItems },
    immediate: reduced,
  });

  // ── Fetch cascade on mount ────────────────────────────────────────────────
  // Using useEffect-style fetch but kept inline to avoid extra deps
  if (!loading && !cascadeData && !error) {
    setLoading(true);
    void fetchCascade();
  }

  async function fetchCascade(): Promise<void> {
    try {
      const params = new URLSearchParams({
        rootTaskId: commitment.id,
        userId,
        slipDays: '1',
      });
      const res  = await fetch(`/api/v1/graph/cascade?${params}`, { method: 'POST' });
      const json = (await res.json()) as CascadeApiResponse;
      setCascadeData(json);
    } catch {
      setError(t('app.error.generic'));
    } finally {
      setLoading(false);
    }
  }

  async function fetchReplan(): Promise<void> {
    setShowReplan(true);
    if (replanOptions.length > 0) return;
    try {
      const params = new URLSearchParams({ rootTaskId: commitment.id, userId, slipDays: '1' });
      const res  = await fetch(`/api/v1/graph/replan?${params}`, { method: 'POST' });
      const json = (await res.json()) as { options: ReplanApiOption[] };
      setReplanOptions(json.options ?? []);
    } catch {
      /* replan fetch failed — show empty state */
    }
  }

  const impactColor = (score: number): string =>
    score >= 70 ? tokens.colorPaletteRedForeground1
    : score >= 40 ? tokens.colorPaletteMarigoldForeground1
    : tokens.colorNeutralForeground1;

  return (
    <animated.div style={panelSpring} className={styles.container}>
      {/* ── Header ── */}
      <div className={styles.header}>
        <Text size={500} weight="semibold">{t('cascadeView.title')}</Text>
        <Button appearance="transparent" onClick={onClose}>{t('actions.close')}</Button>
      </div>

      {/* ── Impact score ── */}
      {cascadeData && (
        <div className={styles.header}>
          <Text size={300}>{t('cascadeView.impactScore')}</Text>
          <Tooltip
            content={t('commitPane.card.impactScoreTooltip')}
            relationship="description">
            <Badge
              className={styles.impactBadge}
              color="danger"
              size="extra-large"
              style={{ color: impactColor(cascadeData.impactScore) }}>
              {cascadeData.impactScore}
            </Badge>
          </Tooltip>
        </div>
      )}

      {/* ── Affected task count ── */}
      {cascadeData && (
        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
          {t('cascadeView.affectedTasks', { count: cascadeData.affectedCount })}
        </Text>
      )}

      <Divider />

      {/* ── Loading state ── */}
      {loading && (
        <>
          <Text size={200}>{t('cascadeView.propagating')}</Text>
          <Skeleton>
            <SkeletonItem style={{ height: '48px' }} />
            <SkeletonItem style={{ height: '48px', marginTop: tokens.spacingVerticalS }} />
          </Skeleton>
        </>
      )}

      {/* ── Error ── */}
      {error && <Text style={{ color: tokens.colorPaletteRedForeground1 }}>{error}</Text>}

      {/* ── Cascade chain (stagger-revealed) ── */}
      {chainItems.map((task, i) => (
        <animated.div
          key={task.taskId}
          style={reduced ? {} : trail[i]}
          className={`${styles.chainItem} ${task.cumulativeSlipDays > 0 ? styles.atRisk : ''}`}>
          <Text size={300} weight={task.cumulativeSlipDays > 0 ? 'semibold' : 'regular'}>
            {task.title}
          </Text>
          {task.originalEta && (
            <div className={styles.etaRow}>
              <Text size={100} className={styles.strikethrough}>
                {new Date(task.originalEta).toLocaleDateString()}
              </Text>
              {task.newEta && (
                <Text size={100} style={{ color: tokens.colorPaletteRedForeground1 }}>
                  → {new Date(task.newEta).toLocaleDateString()}
                  {task.cumulativeSlipDays > 0 && ` (+${task.cumulativeSlipDays}d)`}
                </Text>
              )}
            </div>
          )}
        </animated.div>
      ))}

      {/* ── View replan options button ── */}
      {cascadeData && !showReplan && (
        <Button
          appearance="primary"
          style={{ marginTop: tokens.spacingVerticalM }}
          onClick={() => { void fetchReplan(); }}>
          {t('cascadeView.replanOptions')}
        </Button>
      )}

      {/* ── Replan options ── */}
      {showReplan && (
        <div className={styles.replanSection}>
          <Divider />
          <Text size={400} weight="semibold">{t('cascadeView.replanOptions')}</Text>
          {replanOptions.length === 0 && (
            <Skeleton><SkeletonItem style={{ height: '80px' }} /></Skeleton>
          )}
          {replanOptions.map(opt => (
            <Card
              key={opt.optionId}
              className={styles.replanOption}
              onClick={() => onReplanSelected?.(opt)}>
              <div className={styles.optionHeader}>
                <Text size={300} weight="semibold">
                  {opt.optionId}. {opt.label}
                </Text>
                <Badge
                  color={opt.confidence >= 0.80 ? 'success' : opt.confidence >= 0.65 ? 'warning' : 'informative'}
                  size="small">
                  {Math.round(opt.confidence * 100)}%
                </Badge>
              </div>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                {opt.description}
              </Text>
              {opt.requiredActions.length > 0 && (
                <ul style={{ margin: 0, paddingLeft: tokens.spacingHorizontalL }}>
                  {opt.requiredActions.slice(0, 2).map((action, ai) => (
                    <li key={ai}>
                      <Text size={100}>{action}</Text>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          ))}
          <div className={styles.actions}>
            <Button appearance="transparent" onClick={() => setShowReplan(false)}>
              {t('actions.back')}
            </Button>
          </div>
        </div>
      )}
    </animated.div>
  );
}

export default CascadeView;
