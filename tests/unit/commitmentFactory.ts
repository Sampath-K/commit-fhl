import { v4 as uuidv4 } from 'uuid';
// Frontend API contract types (C# backend mirrors these shapes)
import type { CommitmentRecord, GraphEdge, CommitmentSource } from '../../src/app/src/types/api';

export function makeCommitment(overrides?: Partial<CommitmentRecord>): CommitmentRecord {
  const userId = 'test-user-oid-' + uuidv4().slice(0, 8);
  return {
    id: uuidv4(),
    title: 'Test commitment \u2014 review API design document',
    owner: userId,
    watchers: [],
    source: makeSource(),
    committedAt: '2026-03-01T09:00:00Z',
    dueAt: '2026-03-05T17:00:00Z',
    status: 'pending',
    priority: 'urgent-important',
    blockedBy: [],
    blocks: [],
    impactScore: 0,
    burnoutContribution: 2,
    ...overrides,
  };
}

export function makeSource(overrides?: Partial<CommitmentSource>): CommitmentSource {
  return {
    type: 'meeting',
    url: 'https://teams.microsoft.com/l/meetup-join/test',
    timestamp: '2026-03-01T09:00:00Z',
    sourceId: 'meeting-id-' + uuidv4().slice(0, 8),
    ...overrides,
  };
}

export function makeGraphEdge(overrides?: Partial<GraphEdge>): GraphEdge {
  return {
    fromId: uuidv4(),
    toId: uuidv4(),
    edgeType: 'hard',
    confidence: 0.9,
    detectedBy: 'conversation-thread',
    ...overrides,
  };
}
