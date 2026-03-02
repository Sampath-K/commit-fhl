import { useQuery } from '@tanstack/react-query';
import type { UserMotivationState } from '../types/api';
import { API_BASE } from '../config/api.config';

/**
 * Fetches the user's motivation state (delivery score, streak, XP, level).
 * Cached for 5 minutes — this data changes slowly.
 */
export function useDeliveryScore(userId: string) {
  return useQuery<UserMotivationState>({
    queryKey: ['motivationState', userId],
    queryFn: async () => {
      const res = await fetch(`${API_BASE}/api/v1/users/${userId}/motivation`);
      return res.json() as Promise<UserMotivationState>;
    },
    staleTime: 5 * 60 * 1000,
    enabled:   Boolean(userId),
  });
}
