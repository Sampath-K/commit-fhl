import { useState, useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CommitPane } from './components/core/CommitPane';
import { CascadeView } from './components/core/CascadeView';
import { RescanModal } from './components/core/RescanModal';
import { MissedTaskReporter } from './components/core/MissedTaskReporter';
import { AdminDashboard } from './pages/AdminDashboard';
import type { ApiResponse, CommitmentRecord } from './types/api';
import { API_BASE } from './config/api.config';

/** Fetch commitments for the current user from /api/v1/commitments/:userId */
async function fetchCommitments(userId: string, authToken?: string): Promise<CommitmentRecord[]> {
  const headers: HeadersInit = authToken ? { Authorization: `Bearer ${authToken}` } : {};
  const res = await fetch(`${API_BASE}/api/v1/commitments/${userId}`, { headers });
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
  // Admin view: enabled via ?admin=true query param
  const isAdmin = new URLSearchParams(window.location.search).get('admin') === 'true';

  const { data: commitments = [], isLoading } = useQuery({
    queryKey: ['commitments', userId],
    queryFn:  () => fetchCommitments(userId, authToken),
  });

  // On mount: call POST /api/v1/extract to cache the token, trigger extraction,
  // and register Graph webhook subscriptions for real-time delivery.
  useEffect(() => {
    if (!userId || !authToken) return;
    const headers: HeadersInit = {
      'Content-Type':  'application/json',
      'Authorization': `Bearer ${authToken}`,
    };
    void fetch(`${API_BASE}/api/v1/extract?userId=${encodeURIComponent(userId)}`, {
      method: 'POST',
      headers,
    });
  }, [userId, authToken]);

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

  if (isAdmin) {
    return <AdminDashboard authToken={authToken} />;
  }

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
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Top action bar: Rescan + Admin link */}
      <div style={{ display: 'flex', justifyContent: 'flex-end', padding: '4px 12px 0', gap: '4px' }}>
        <RescanModal
          userId={userId}
          authToken={authToken}
          onRescanComplete={() => void queryClient.invalidateQueries({ queryKey: ['commitments', userId] })}
        />
        <a href="?admin=true" style={{ fontSize: '11px', color: '#666', textDecoration: 'none', alignSelf: 'center' }}>
          Admin
        </a>
      </div>
      <div style={{ flex: 1, overflow: 'auto' }}>
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
      </div>
      {/* Footer: Missed task reporter */}
      <div style={{ padding: '4px 12px 8px', display: 'flex', justifyContent: 'center', borderTop: '1px solid #eee' }}>
        <MissedTaskReporter
          userId={userId}
          authToken={authToken}
          onTaskAdded={() => void queryClient.invalidateQueries({ queryKey: ['commitments', userId] })}
        />
      </div>
    </div>
  );
}
