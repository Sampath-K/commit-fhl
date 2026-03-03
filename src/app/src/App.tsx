import { useState, useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CommitPane } from './components/core/CommitPane';
import { CascadeView } from './components/core/CascadeView';
import type { ApiResponse, CommitmentRecord } from './types/api';
import { API_BASE } from './config/api.config';

/** Fetch commitments for the current user from /api/v1/commitments/:userId */
async function fetchCommitments(userId: string): Promise<CommitmentRecord[]> {
  const res = await fetch(`${API_BASE}/api/v1/commitments/${userId}`);
  if (!res.ok) throw new Error(`Failed to load commitments: ${res.status}`);
  const body = (await res.json()) as ApiResponse<CommitmentRecord[]>;
  return body.data ?? [];
}

interface AppProps {
  /** AAD Object ID of the signed-in user — from Teams context or fallback for local dev. */
  userId: string;
  /** Teams SSO token — forwarded to ApprovalCard for real Teams message dispatch. */
  authToken?: string;
}

/**
 * Root application component.
 * Wires TanStack Query → CommitPane → CascadeView drill-in.
 * Clicking any in-progress or blocking commitment opens the cascade panel.
 */
export function App({ userId, authToken }: AppProps): JSX.Element {
  const [selected, setSelected] = useState<CommitmentRecord | null>(null);
  const queryClient = useQueryClient();

  const { data: commitments = [], isLoading } = useQuery({
    queryKey: ['commitments', userId],
    queryFn:  () => fetchCommitments(userId),
  });

  // SSE: receive push notifications from the backend when new commitments are stored.
  // On each event the query cache is invalidated → TanStack Query re-fetches automatically.
  // EventSource reconnects on its own if the connection drops.
  useEffect(() => {
    if (!userId) return;
    const es = new EventSource(
      `${API_BASE}/api/v1/events/stream?userId=${encodeURIComponent(userId)}`
    );
    es.onmessage = () => {
      void queryClient.invalidateQueries({ queryKey: ['commitments', userId] });
    };
    return () => es.close();
  }, [userId, queryClient]);

  if (selected) {
    return (
      <CascadeView
        commitment={selected}
        userId={userId}
        onClose={() => setSelected(null)}
        authToken={authToken}
      />
    );
  }

  return (
    <CommitPane
      commitments={commitments}
      isLoading={isLoading}
      currentUserId={userId}
      onCommitmentClick={(c) => {
        // Drill into cascade view for in-progress or items that block others
        if (c.status === 'in-progress' || c.blocks.length > 0) {
          setSelected(c);
        }
      }}
    />
  );
}
