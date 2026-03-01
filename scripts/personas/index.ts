// 6 demo personas from seed.md role card
export interface DemoPersona {
  id: string;  // used as partitionKey prefix: seed-{id}
  name: string;
  role: string;
  storyRole: string;
  userId: string;  // fake AAD OID for demo
}

export const PERSONAS: DemoPersona[] = [
  { id: 'alex',   name: 'Alex Chen',         role: 'Senior Engineer',      storyRole: 'Trigger of Cascade A',         userId: 'demo-alex-oid-001' },
  { id: 'priya',  name: 'Priya Sharma',      role: 'Engineering Manager',  storyRole: 'Tracking Cascade A+B',         userId: 'demo-priya-oid-002' },
  { id: 'marcus', name: 'Marcus Johnson',    role: 'Designer',             storyRole: 'Blocked by Alex on 2 items',   userId: 'demo-marcus-oid-003' },
  { id: 'fatima', name: 'Fatima Al-Rashid',  role: 'PM',                   storyRole: 'Watcher, needs proactive updates', userId: 'demo-fatima-oid-004' },
  { id: 'david',  name: 'David Park',        role: 'Stakeholder/Director', storyRole: 'Exec visibility watcher',       userId: 'demo-david-oid-005' },
  { id: 'sarah',  name: "Sarah O'Brien",     role: 'Peer Engineer',        storyRole: 'Cross-team dependency',         userId: 'demo-sarah-oid-006' },
];
