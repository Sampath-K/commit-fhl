import { useQuery } from '@tanstack/react-query';
import { CommitPane } from './components/core/CommitPane';
import type { ApiResponse, CommitmentRecord } from './types/api';
import { API_BASE } from './config/api.config';

/** Fetch commitments for the current user from /api/v1/commitments/:userId */
async function fetchCommitments(userId: string): Promise<CommitmentRecord[]> {
  const res = await fetch(`${API_BASE}/api/v1/commitments/${userId}`);
  if (!res.ok) throw new Error(`Failed to load commitments: ${res.status}`);
  const body = (await res.json()) as ApiResponse<CommitmentRecord[]>;
  return body.data ?? [];
}

// TODO(T-005): Replace with real userId from Teams context / OBO token
const DEMO_USER_ID = 'demo-user';

/**
 * Root application component.
 * Wires TanStack Query → CommitPane.
 * Auth context injected in T-005.
 */
export function App(): JSX.Element {
  const { data: commitments = [], isLoading } = useQuery({
    queryKey: ['commitments', DEMO_USER_ID],
    queryFn:  () => fetchCommitments(DEMO_USER_ID),
  });

  return (
    <CommitPane
      commitments={commitments}
      isLoading={isLoading}
    />
  );
}
