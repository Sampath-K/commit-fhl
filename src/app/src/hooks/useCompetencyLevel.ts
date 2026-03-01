import { useDeliveryScore } from './useDeliveryScore';

/** XP required to START each level (index = level - 1). */
const XP_THRESHOLDS: readonly number[] = [0, 100, 300, 600, 1000];

/** Returns current level, XP progress within the current band, and XP to next level. */
export function useCompetencyLevel(userId: string) {
  const { data, ...rest } = useDeliveryScore(userId);
  const level      = data?.competencyLevel ?? 1;
  const totalXp    = data?.totalXp ?? 0;
  const floorXp    = XP_THRESHOLDS[level - 1] ?? 0;
  const ceilXp     = level < 5 ? (XP_THRESHOLDS[level] ?? Infinity) : Infinity;
  const xpToNext   = ceilXp === Infinity ? null : ceilXp - totalXp;
  const bandWidth  = ceilXp === Infinity ? 1 : ceilXp - floorXp;
  const progress   = bandWidth === 0 ? 1 : Math.min(1, Math.max(0, (totalXp - floorXp) / bandWidth));
  return { level, totalXp, xpToNext, progress, ...rest };
}
