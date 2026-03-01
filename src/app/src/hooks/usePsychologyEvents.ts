import { useCallback, useState } from 'react';
import { MAX_TRIGGERS_PER_DAY } from '../config/psychology.config';

const storageKey = (): string => `psych_triggers_${new Date().toDateString()}`;

/**
 * Tracks psychology trigger count for the day (P-27: max 3/day).
 * State is persisted in sessionStorage so it survives React re-renders
 * but resets when the browser tab is closed.
 */
export function usePsychologyEvents() {
  const [triggersToday, setTriggersToday] = useState<number>(() =>
    parseInt(sessionStorage.getItem(storageKey()) ?? '0', 10)
  );

  const canTrigger = triggersToday < MAX_TRIGGERS_PER_DAY;

  const recordTrigger = useCallback((): boolean => {
    if (!canTrigger) return false;
    const next = triggersToday + 1;
    setTriggersToday(next);
    sessionStorage.setItem(storageKey(), String(next));
    return true;
  }, [canTrigger, triggersToday]);

  return { triggersToday, canTrigger, recordTrigger };
}
