/**
 * Commit FHL — Team affiliation mappings (frontend-only, no API changes)
 * Maps demo user IDs and task ID prefixes to display team info.
 * Demo IDs are hardcoded and never change across sessions.
 */

export interface TeamInfo {
  label: string;
  color: string;
}

/** Maps user IDs → display team info (demo OIDs + real tenant OIDs from setup-tenant.ts) */
export const TEAM_BY_USER: Record<string, TeamInfo> = {
  // Demo OIDs (local dev / Azurite)
  'demo-alex-oid-001':   { label: 'Reschedule Crew',  color: '#0078D4' },
  'demo-priya-oid-002':  { label: 'Reschedule Crew',  color: '#0078D4' },
  'demo-fatima-oid-004': { label: 'Reschedule Crew',  color: '#0078D4' },
  'demo-david-oid-005':  { label: 'Reschedule Crew',  color: '#0078D4' },
  'demo-sarah-oid-006':  { label: 'Scheduling Skill', color: '#107C10' },
  'demo-marcus-oid-003': { label: 'BizChat Platform', color: '#881798' },
  // Real tenant OIDs (7k2cc2.onmicrosoft.com — created by setup-tenant.ts)
  'f7a02de7-e195-4894-bc23-f7f74b696cbd': { label: 'Reschedule Crew',  color: '#0078D4' }, // Alex
  '8d0832a0-c586-4c41-b6ad-02a76c5b326c': { label: 'Reschedule Crew',  color: '#0078D4' }, // Priya
  '78a8c66f-2928-4edc-9230-d6a209e72f85': { label: 'Reschedule Crew',  color: '#0078D4' }, // Fatima
  '6a638a9c-cad1-429d-8bc0-d435bf552e5a': { label: 'Reschedule Crew',  color: '#0078D4' }, // David
  '5659f687-9ea8-4dfe-95c7-2990356288af': { label: 'Scheduling Skill', color: '#107C10' }, // Sarah
  'c1c0037d-1b8c-4c34-bede-f6dadc38a8c6': { label: 'BizChat Platform', color: '#881798' }, // Marcus
};

/**
 * Maps task ID prefix → team info (for cascade chain items).
 * rbs-sched-* → Scheduling Skill (green)
 * rbs-bcp-*   → BizChat Platform (purple)
 * rbs-*       → Reschedule Crew (blue, catch-all)
 */
export function teamFromTaskId(taskId: string): TeamInfo | null {
  if (taskId.startsWith('rbs-sched-')) return { label: 'Scheduling Skill', color: '#107C10' };
  if (taskId.startsWith('rbs-bcp-'))   return { label: 'BizChat Platform', color: '#881798' };
  if (taskId.startsWith('rbs-'))       return { label: 'Reschedule Crew',  color: '#0078D4' };
  return null;
}
