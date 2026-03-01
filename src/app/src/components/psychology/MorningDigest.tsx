import { useTranslation } from 'react-i18next';
import { Text, tokens, makeStyles } from '@fluentui/react-components';
import { animated, useTrail } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS, STAGGER_DELAYS } from '../../config/psychology.config';
import { InsightCard } from './InsightCard';
import type { UserMotivationState } from '../../types/api';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  root: {
    display:       'flex',
    flexDirection: 'column',
    gap:           tokens.spacingVerticalS,
  },
  greeting: {
    marginBottom: tokens.spacingVerticalXS,
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

interface InsightItem {
  icon:     string;
  headline: string;
  detail?:  string;
}

interface MorningDigestProps {
  state:            UserMotivationState;
  totalCommitments: number;
  atRiskCount:      number;
}

export function MorningDigest({ state, totalCommitments, atRiskCount }: MorningDigestProps): JSX.Element {
  const { t } = useTranslation('psychology');
  const styles  = useStyles();
  const reduced = useReducedMotion();

  const insights: InsightItem[] = [
    {
      icon:     '🏆',
      headline: t('morningDigest.score', { score: state.deliveryScore }),
    },
    {
      icon:     '🔥',
      headline: t('morningDigest.streak', { count: state.streakDays, tasks: atRiskCount }),
    },
  ];

  const trail = useTrail(insights.length, {
    from:   { opacity: 0, transform: 'translateY(8px)' },
    to:     { opacity: 1, transform: 'translateY(0px)' },
    config: { ...SPRING_CONFIGS.gentle },
    delay:  STAGGER_DELAYS.digestCards,
    immediate: reduced,
  });

  return (
    <div className={styles.root}>
      <Text size={300} weight="semibold" className={styles.greeting}>
        {t('morningDigest.greeting', { count: totalCommitments, atRisk: atRiskCount })}
      </Text>

      {insights.map((item, i) => (
        <animated.div key={i} style={reduced ? {} : trail[i]}>
          <InsightCard icon={item.icon} headline={item.headline} detail={item.detail} />
        </animated.div>
      ))}
    </div>
  );
}

export default MorningDigest;
