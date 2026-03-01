import { STREAK_MILESTONES } from '../config/psychology.config';
import { useDeliveryScore } from './useDeliveryScore';

/** Returns current streak and whether today is a milestone day. */
export function useStreak(userId: string) {
  const { data, ...rest } = useDeliveryScore(userId);
  const streak = data?.streakDays ?? 0;
  const milestone = (STREAK_MILESTONES as readonly number[]).includes(streak)
    ? (streak as typeof STREAK_MILESTONES[number])
    : null;
  return { streak, milestone, ...rest };
}
