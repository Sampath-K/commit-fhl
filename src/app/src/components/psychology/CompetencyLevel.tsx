import { useTranslation } from 'react-i18next';
import { Text, tokens, makeStyles } from '@fluentui/react-components';
import { animated, useSpring } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS } from '../../config/psychology.config';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  root: {
    display:       'flex',
    flexDirection: 'column',
    gap:           tokens.spacingVerticalXS,
  },
  header: {
    display:    'flex',
    alignItems: 'center',
    gap:        tokens.spacingHorizontalS,
  },
  badge: {
    width:           '28px',
    height:          '28px',
    borderRadius:    '50%',
    display:         'flex',
    alignItems:      'center',
    justifyContent:  'center',
    backgroundColor: tokens.colorBrandBackground,
    color:           tokens.colorNeutralForegroundOnBrand,
    fontWeight:      tokens.fontWeightBold,
    fontSize:        tokens.fontSizeBase300,
    flexShrink:      '0',
  },
  nameBlock: {
    display:       'flex',
    flexDirection: 'column',
  },
  track: {
    height:          '4px',
    backgroundColor: tokens.colorNeutralBackground4,
    borderRadius:    '2px',
    overflow:        'hidden',
  },
  bar: {
    height:          '100%',
    backgroundColor: tokens.colorBrandBackground,
    borderRadius:    '2px',
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

interface CompetencyLevelProps {
  level:    1 | 2 | 3 | 4 | 5;
  progress: number;       // 0–1
  xpToNext: number | null;
}

export function CompetencyLevel({ level, progress, xpToNext }: CompetencyLevelProps): JSX.Element {
  const { t } = useTranslation('psychology');
  const styles  = useStyles();
  const reduced = useReducedMotion();

  const { width } = useSpring({
    from:      { width: '0%' },
    to:        { width: `${Math.round(progress * 100)}%` },
    config:    SPRING_CONFIGS.smooth,
    immediate: reduced,
  });

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.badge}>{level}</div>
        <div className={styles.nameBlock}>
          <Text size={200} weight="semibold">{t('level.label', { level })}</Text>
          <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
            {t(`level.names.${level}` as const)}
          </Text>
        </div>
      </div>
      <div className={styles.track}>
        <animated.div className={styles.bar} style={{ width }} />
      </div>
      <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
        {xpToNext !== null
          ? t('level.xpToNext', { xp: xpToNext, next: level + 1 })
          : t('level.maxLevel')}
      </Text>
    </div>
  );
}

export default CompetencyLevel;
