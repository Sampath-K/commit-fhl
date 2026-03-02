// 6 demo personas from seed.md role card
export interface DemoPersona {
  id: string;  // used as partitionKey prefix: seed-{id}
  name: string;
  role: string;
  team: string;
  storyRole: string;
  userId: string;  // fake AAD OID for demo
}

export const PERSONAS: DemoPersona[] = [
  { id: 'alex',   name: 'Alex Chen',         role: 'Senior Engineer',              team: 'Reschedule Crew',   storyRole: 'Trigger of Cascade A',             userId: 'demo-alex-oid-001' },
  { id: 'priya',  name: 'Priya Sharma',      role: 'Engineering Manager',          team: 'Reschedule Crew',   storyRole: 'Tracking Cascade A+B',             userId: 'demo-priya-oid-002' },
  { id: 'marcus', name: 'Marcus Johnson',    role: 'Platform Engineer',            team: 'BizChat Platform',  storyRole: 'Blocked by Alex on 2 items',       userId: 'demo-marcus-oid-003' },
  { id: 'fatima', name: 'Fatima Al-Rashid',  role: 'PM',                           team: 'Reschedule Crew',   storyRole: 'Watcher, needs proactive updates', userId: 'demo-fatima-oid-004' },
  { id: 'david',  name: 'David Park',        role: 'Stakeholder/Director',         team: 'Reschedule Crew',   storyRole: 'Exec visibility watcher',          userId: 'demo-david-oid-005' },
  { id: 'sarah',  name: "Sarah O'Brien",     role: 'Scheduling Skill Engineer',    team: 'Scheduling Skill',  storyRole: 'Cross-team dependency',            userId: 'demo-sarah-oid-006' },
];
