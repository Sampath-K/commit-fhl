import { useEffect, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Text, tokens, makeStyles } from '@fluentui/react-components';
import { animated, useSpring } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { SPRING_CONFIGS, VARIABLE_REWARD_HISTORY_LENGTH, VARIABLE_REWARD_MIN_POOL } from '../../config/psychology.config';
import { usePsychologyEvents } from '../../hooks/usePsychologyEvents';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  nudge: {
    padding:    `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
    borderLeft: `3px solid ${tokens.colorBrandStroke1}`,
  },
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

const HISTORY_KEY = 'psych_nudge_history';

function pickPhrase(pool: string[]): string {
  const raw = sessionStorage.getItem(HISTORY_KEY) ?? '[]';
  let history: number[] = [];
  try { history = JSON.parse(raw) as number[]; } catch { /* malformed — reset */ }

  const allIndices  = pool.map((_, i) => i);
  const available   = allIndices.filter(i => !history.includes(i));
  const candidates  = available.length > 0 ? available : allIndices;
  const idx         = candidates[Math.floor(Math.random() * candidates.length)] ?? 0;
  const next        = [...history, idx].slice(-VARIABLE_REWARD_HISTORY_LENGTH);
  sessionStorage.setItem(HISTORY_KEY, JSON.stringify(next));
  return pool[idx] ?? pool[0] ?? '';
}

// ─── Component ────────────────────────────────────────────────────────────────

/**
 * Shows a single variable-reward completion affirmation.
 * Respects MAX_TRIGGERS_PER_DAY (P-27) and the 20-phrase minimum pool.
 * Shows at most once per React session mount.
 */
export function MotivationalNudge(): JSX.Element | null {
  const { t } = useTranslation('psychology');
  const styles  = useStyles();
  const reduced = useReducedMotion();
  const { canTrigger, recordTrigger } = usePsychologyEvents();
  const hasShown = useRef(false);

  const pool   = t('completionAffirmations', { returnObjects: true }) as string[];
  const phrase = useMemo((): string | null => {
    if (hasShown.current) return null;
    if (!canTrigger)       return null;
    if (pool.length < VARIABLE_REWARD_MIN_POOL) return null;
    hasShown.current = true;
    const granted = recordTrigger();
    return granted ? pickPhrase(pool) : null;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // pick once on mount

  const spring = useSpring({
    from:      { opacity: 0, transform: 'translateX(-4px)' },
    to:        { opacity: phrase ? 1 : 0, transform: 'translateX(0px)' },
    config:    SPRING_CONFIGS.gentle,
    immediate: reduced,
  });

  // Accessibility: announce phrase to screen readers once
  useEffect(() => {
    if (phrase && reduced) {
      // No visual, but phrase is still meaningful — rendered as text
    }
  }, [phrase, reduced]);

  if (!phrase) return null;

  return (
    <animated.div style={reduced ? {} : spring}>
      <div className={styles.nudge}>
        <Text size={200} italic style={{ color: tokens.colorNeutralForeground2 }}>
          {phrase}
        </Text>
      </div>
    </animated.div>
  );
}

export default MotivationalNudge;
