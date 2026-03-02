/**
 * Commit FHL — Team affiliation mappings (frontend-only, no API changes)
 * Maps demo user IDs and task ID prefixes to display team info.
 * Demo IDs are hardcoded and never change across sessions.
 */

export interface TeamInfo {
  label: string;
  color: string;
}

/** Maps demo user IDs → display team info */
export const TEAM_BY_USER: Record<string, TeamInfo> = {
  'demo-alex-oid-001':   { label: 'Reschedule Crew',  color: '#0078D4' },  // Teams blue
  'demo-priya-oid-002':  { label: 'Reschedule Crew',  color: '#0078D4' },
  'demo-fatima-oid-004': { label: 'Reschedule Crew',  color: '#0078D4' },
  'demo-david-oid-005':  { label: 'Reschedule Crew',  color: '#0078D4' },
  'demo-sarah-oid-006':  { label: 'Scheduling Skill', color: '#107C10' },  // green
  'demo-marcus-oid-003': { label: 'BizChat Platform', color: '#881798' },  // purple
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
