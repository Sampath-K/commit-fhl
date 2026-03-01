import { useTranslation } from 'react-i18next';
import { Text, tokens, makeStyles } from '@fluentui/react-components';
import { animated, useSpring } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS, STREAK_MILESTONES } from '../../config/psychology.config';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  badge: {
    display:         'inline-flex',
    alignItems:      'center',
    gap:             tokens.spacingHorizontalXS,
    padding:         `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    backgroundColor: tokens.colorPaletteMarigoldBackground2,
    borderRadius:    tokens.borderRadiusMedium,
  },
  milestone: {
    display:    'block',
    marginTop:  tokens.spacingVerticalXS,
    color:      tokens.colorPaletteMarigoldForeground1,
  },
  root: {
    display:       'flex',
    flexDirection: 'column',
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

interface StreakBadgeProps {
  streak: number;
}

export function StreakBadge({ streak }: StreakBadgeProps): JSX.Element {
  const { t } = useTranslation('psychology');
  const styles  = useStyles();
  const reduced = useReducedMotion();

  const isMilestone = (STREAK_MILESTONES as readonly number[]).includes(streak) && streak > 0;

  const spring = useSpring({
    from:      { scale: 0.8, opacity: 0 },
    to:        { scale: 1,   opacity: 1 },
    config:    SPRING_CONFIGS.bounce,
    immediate: reduced,
  });

  return (
    <div className={styles.root}>
      <animated.div style={reduced ? {} : spring}>
        <div className={styles.badge}>
          <span role="img" aria-label="fire">🔥</span>
          <Text size={200} weight="semibold">
            {t('streak.label', { count: streak })}
          </Text>
        </div>
      </animated.div>
      {isMilestone && (
        <Text size={100} className={styles.milestone}>
          {t(`streak.milestones.${streak}`)}
        </Text>
      )}
      {streak === 0 && (
        <Text size={100} style={{ color: tokens.colorNeutralForeground3, marginTop: tokens.spacingVerticalXS }}>
          {t('streak.new')}
        </Text>
      )}
    </div>
  );
}

export default StreakBadge;
