import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Text, tokens, makeStyles } from '@fluentui/react-components';
import { animated, useSpring } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS } from '../../config/psychology.config';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  overlay: {
    position:       'fixed',
    inset:          0,
    zIndex:         100,
    backgroundColor: 'rgba(0, 0, 0, 0.45)',
    display:        'flex',
    alignItems:     'center',
    justifyContent: 'center',
    padding:        tokens.spacingVerticalM,
  },
  panel: {
    maxWidth:        '560px',
    width:           '100%',
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius:    tokens.borderRadiusLarge,
    padding:         tokens.spacingVerticalM,
    boxShadow:       tokens.shadow64,
  },
  meta: {
    display:       'flex',
    flexDirection: 'column',
    gap:           '2px',
    marginBottom:  tokens.spacingVerticalM,
  },
  exit: {
    marginTop:  tokens.spacingVerticalM,
    cursor:     'pointer',
    color:      tokens.colorNeutralForeground3,
    ':hover':   { color: tokens.colorNeutralForeground1 },
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

interface FocusModeProps {
  title:    string;
  children: ReactNode;
  onExit:   () => void;
}

export function FocusMode({ title, children, onExit }: FocusModeProps): JSX.Element {
  const { t } = useTranslation('psychology');
  const styles  = useStyles();
  const reduced = useReducedMotion();

  const spring = useSpring({
    from:      { opacity: 0, transform: 'scale(0.97)' },
    to:        { opacity: 1, transform: 'scale(1)' },
    config:    SPRING_CONFIGS.smooth,
    immediate: reduced,
  });

  return (
    <animated.div className={styles.overlay} style={reduced ? {} : spring}>
      <div className={styles.panel}>
        <div className={styles.meta}>
          <Text size={400} weight="semibold">{title}</Text>
          <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
            {t('focusMode.subtitle')}
          </Text>
        </div>

        {children}

        <Text
          size={200}
          className={styles.exit}
          role="button"
          tabIndex={0}
          onClick={onExit}
          onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') onExit(); }}>
          {t('focusMode.exit')}
        </Text>
      </div>
    </animated.div>
  );
}

export default FocusMode;
