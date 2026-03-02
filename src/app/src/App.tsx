import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
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

// TODO(T-005): Replace with real userId from Teams context / OBO token
const DEMO_USER_ID = 'demo-alex-oid-001';

/**
 * Root application component.
 * Wires TanStack Query → CommitPane → CascadeView drill-in.
 * Clicking any in-progress or blocking commitment opens the cascade panel.
 */
export function App(): JSX.Element {
  const [selected, setSelected] = useState<CommitmentRecord | null>(null);

  const { data: commitments = [], isLoading } = useQuery({
    queryKey: ['commitments', DEMO_USER_ID],
    queryFn:  () => fetchCommitments(DEMO_USER_ID),
  });

  if (selected) {
    return (
      <CascadeView
        commitment={selected}
        userId={DEMO_USER_ID}
        onClose={() => setSelected(null)}
      />
    );
  }

  return (
    <CommitPane
      commitments={commitments}
      isLoading={isLoading}
      onCommitmentClick={(c) => {
        // Drill into cascade view for in-progress or items that block others
        if (c.status === 'in-progress' || c.blocks.length > 0) {
          setSelected(c);
        }
      }}
    />
  );
}
