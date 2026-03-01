// Cascade A — The Classic Slip
// Alex's API Design blocks Marcus's Design Updates blocks Priya's Feature Ship
import type { CommitmentRecord, GraphEdge } from '../../src/api/src/types/index';
import { PERSONAS } from '../personas/index';

const alex = PERSONAS.find(p => p.id === 'alex')!;
const priya = PERSONAS.find(p => p.id === 'priya')!;
const marcus = PERSONAS.find(p => p.id === 'marcus')!;
const fatima = PERSONAS.find(p => p.id === 'fatima')!;
const david = PERSONAS.find(p => p.id === 'david')!;

export const CASCADE_A_COMMITMENT_IDS = {
  apiDesign: 'seed-alex-task-001',
  designUpdates: 'seed-marcus-task-001',
  featureShip: 'seed-priya-task-001',
};

export const CASCADE_A_COMMITMENTS: Partial<CommitmentRecord>[] = [
  {
    partitionKey: alex.userId,
    rowKey: CASCADE_A_COMMITMENT_IDS.apiDesign,
    title: 'Review API design doc and leave comments',
    owner: alex.userId,
    watchers: [priya.userId, marcus.userId],
    source: {
      type: 'meeting',
      url: 'https://teams.microsoft.com/l/meetup-join/demo-cascade-a',
      timestamp: new Date('2026-02-28T10:00:00Z'),
      rawText: 'I will review the API design doc by Monday',
      sourceId: 'demo-meeting-cascade-a',
    },
    committedAt: new Date('2026-02-28T10:00:00Z'),
    dueAt: new Date('2026-03-03T17:00:00Z'), // Was due Monday, now Wednesday — at risk
    status: 'pending',
    priority: 'urgent-important',
    blockedBy: [],
    blocks: [CASCADE_A_COMMITMENT_IDS.designUpdates],
    impactScore: 67,
    burnoutContribution: 3,
    lastActivity: new Date('2026-02-28T15:00:00Z'), // No activity in 2 days — at risk
  },
  {
    partitionKey: marcus.userId,
    rowKey: CASCADE_A_COMMITMENT_IDS.designUpdates,
    title: 'Update design system tokens for dark mode based on API design',
    owner: marcus.userId,
    watchers: [priya.userId, fatima.userId],
    source: {
      type: 'chat',
      url: 'https://teams.microsoft.com/l/chat/demo-marcus-priya',
      timestamp: new Date('2026-02-28T11:00:00Z'),
      rawText: 'I will start the design system updates Tuesday after Alex reviews the API design',
      sourceId: 'demo-chat-marcus-001',
    },
    committedAt: new Date('2026-02-28T11:00:00Z'),
    dueAt: new Date('2026-03-04T17:00:00Z'),
    status: 'pending',
    priority: 'urgent-important',
    blockedBy: [CASCADE_A_COMMITMENT_IDS.apiDesign],
    blocks: [CASCADE_A_COMMITMENT_IDS.featureShip],
    impactScore: 45,
    burnoutContribution: 4,
  },
  {
    partitionKey: priya.userId,
    rowKey: CASCADE_A_COMMITMENT_IDS.featureShip,
    title: 'Ship the team collaboration feature to production',
    owner: priya.userId,
    watchers: [fatima.userId, david.userId],
    source: {
      type: 'email',
      url: 'https://outlook.office.com/mail/demo-priya-001',
      timestamp: new Date('2026-02-27T14:00:00Z'),
      rawText: 'We will ship the feature Friday EOB',
      sourceId: 'demo-email-priya-001',
    },
    committedAt: new Date('2026-02-27T14:00:00Z'),
    dueAt: new Date('2026-03-06T17:00:00Z'),
    status: 'pending',
    priority: 'urgent-important',
    blockedBy: [CASCADE_A_COMMITMENT_IDS.designUpdates],
    blocks: [],
    impactScore: 82,
    burnoutContribution: 8,
  },
];

export const CASCADE_A_EDGES: GraphEdge[] = [
  {
    fromId: CASCADE_A_COMMITMENT_IDS.apiDesign,
    toId: CASCADE_A_COMMITMENT_IDS.designUpdates,
    edgeType: 'hard',
    confidence: 0.95,
    detectedBy: 'conversation-thread',
  },
  {
    fromId: CASCADE_A_COMMITMENT_IDS.designUpdates,
    toId: CASCADE_A_COMMITMENT_IDS.featureShip,
    edgeType: 'hard',
    confidence: 0.90,
    detectedBy: 'overlapping-people',
  },
];
