import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Text, tokens, makeStyles } from '@fluentui/react-components';
import { animated, useSpring } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SCORE_THRESHOLDS, SPRING_CONFIGS } from '../../config/psychology.config';
import type { UserMotivationState } from '../../types/api';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  root: {
    display:        'flex',
    flexDirection:  'column',
    alignItems:     'center',
    gap:            tokens.spacingVerticalXS,
  },
  svgWrap: {
    position: 'relative',
    cursor:   'pointer',
  },
  center: {
    position:       'absolute',
    inset:          0,
    display:        'flex',
    flexDirection:  'column',
    alignItems:     'center',
    justifyContent: 'center',
  },
  breakdown: {
    display:        'flex',
    flexDirection:  'column',
    gap:            tokens.spacingVerticalXS,
    marginTop:      tokens.spacingVerticalXS,
    padding:        `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius:   tokens.borderRadiusMedium,
    minWidth:       '160px',
  },
  breakdownRow: {
    display:        'flex',
    justifyContent: 'space-between',
    gap:            tokens.spacingHorizontalM,
  },
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

function scoreColor(score: number): string {
  if (score >= SCORE_THRESHOLDS.high)   return tokens.colorPaletteGreenForeground1;
  if (score >= SCORE_THRESHOLDS.medium) return tokens.colorPaletteTealForeground2;
  return tokens.colorBrandForeground1;
}

// ─── Component ────────────────────────────────────────────────────────────────

interface DeliveryScoreProps {
  state: UserMotivationState;
  size?: number;
}

export function DeliveryScore({ state, size = 80 }: DeliveryScoreProps): JSX.Element {
  const { t } = useTranslation('psychology');
  const styles  = useStyles();
  const reduced = useReducedMotion();
  const [showBreakdown, setShowBreakdown] = useState(false);

  // SVG donut geometry
  const r            = (size - 12) / 2;
  const cx           = size / 2;
  const circumference = 2 * Math.PI * r;
  const fraction     = Math.min(1, Math.max(0, state.deliveryScore / 100));

  const { dash } = useSpring({
    from:      { dash: 0 },
    to:        { dash: fraction * circumference },
    config:    SPRING_CONFIGS.smooth,
    immediate: reduced,
  });

  const trend = state.deliveryScore > state.deliveryScorePrevious ? 'up'
    : state.deliveryScore < state.deliveryScorePrevious ? 'down'
    : 'flat';

  return (
    <div className={styles.root}>
      {/* Donut — tap to explain */}
      <div
        className={styles.svgWrap}
        role="button"
        tabIndex={0}
        aria-label={t('deliveryScore.explainTitle')}
        onClick={() => setShowBreakdown(v => !v)}
        onKeyDown={(e) => { if (e.key === 'Enter') setShowBreakdown(v => !v); }}>
        <svg width={size} height={size}>
          {/* Track ring */}
          <circle
            cx={cx} cy={cx} r={r}
            fill="none"
            stroke={tokens.colorNeutralBackground4}
            strokeWidth={8}
          />
          {/* Animated progress ring */}
          <animated.circle
            cx={cx} cy={cx} r={r}
            fill="none"
            stroke={scoreColor(state.deliveryScore)}
            strokeWidth={8}
            strokeDasharray={String(circumference)}
            strokeDashoffset={dash.to(d => String(circumference - d))}
            strokeLinecap="round"
            transform={`rotate(-90 ${cx} ${cx})`}
          />
        </svg>
        <div className={styles.center}>
          <Text
            size={500}
            weight="bold"
            style={{ color: scoreColor(state.deliveryScore), lineHeight: '1' }}>
            {state.deliveryScore}
          </Text>
          <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
            {t(`deliveryScore.trending.${trend}`)}
          </Text>
        </div>
      </div>

      <Text size={200} weight="semibold">{t('deliveryScore.label')}</Text>

      {/* Tap-to-explain breakdown */}
      {showBreakdown && (
        <div className={styles.breakdown}>
          <Text size={100} weight="semibold">{t('deliveryScore.explainTitle')}</Text>
          <div className={styles.breakdownRow}>
            <Text size={100}>{t('deliveryScore.onTimeRate')}</Text>
            <Text size={100} weight="semibold">{Math.round(state.onTimeRate * 100)}%</Text>
          </div>
          <div className={styles.breakdownRow}>
            <Text size={100}>{t('deliveryScore.cascadeHealth')}</Text>
            <Text size={100} weight="semibold">{Math.round(state.cascadeHealthRate * 100)}%</Text>
          </div>
        </div>
      )}
    </div>
  );
}

export default DeliveryScore;
