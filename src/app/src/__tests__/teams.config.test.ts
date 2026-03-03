/**
 * SCENARIO: teams.config — TEAM_BY_USER lookup and teamFromTaskId routing
 *
 * These are pure unit tests with no React rendering.
 * Tests cover both demo OIDs (local dev) and real tenant OIDs (production).
 */
import { TEAM_BY_USER, teamFromTaskId } from '../config/teams.config';
import { ALEX_OID, SARAH_OID, MARCUS_OID, PRIYA_OID } from './helpers';

// ─── TEAM_BY_USER ─────────────────────────────────────────────────────────────

describe('TEAM_BY_USER', () => {
  describe('real tenant OIDs (production demo)', () => {
    it('Alex OID → Reschedule Crew (blue)', () => {
      const team = TEAM_BY_USER[ALEX_OID];
      expect(team).toBeDefined();
      expect(team?.label).toBe('Reschedule Crew');
      expect(team?.color).toBe('#0078D4');
    });

    it('Priya OID → Reschedule Crew (same team as Alex)', () => {
      const team = TEAM_BY_USER[PRIYA_OID];
      expect(team?.label).toBe('Reschedule Crew');
      expect(team?.color).toBe('#0078D4');
    });

    it('Sarah OID → Scheduling Skill (green)', () => {
      const team = TEAM_BY_USER[SARAH_OID];
      expect(team).toBeDefined();
      expect(team?.label).toBe('Scheduling Skill');
      expect(team?.color).toBe('#107C10');
    });

    it('Marcus OID → BizChat Platform (purple)', () => {
      const team = TEAM_BY_USER[MARCUS_OID];
      expect(team).toBeDefined();
      expect(team?.label).toBe('BizChat Platform');
      expect(team?.color).toBe('#881798');
    });
  });

  describe('demo OIDs (local dev / Azurite)', () => {
    it('demo-alex-oid-001 → Reschedule Crew', () => {
      expect(TEAM_BY_USER['demo-alex-oid-001']?.label).toBe('Reschedule Crew');
    });

    it('demo-sarah-oid-006 → Scheduling Skill', () => {
      expect(TEAM_BY_USER['demo-sarah-oid-006']?.label).toBe('Scheduling Skill');
    });

    it('demo-marcus-oid-003 → BizChat Platform', () => {
      expect(TEAM_BY_USER['demo-marcus-oid-003']?.label).toBe('BizChat Platform');
    });
  });

  describe('unknown user ID', () => {
    it('returns undefined for an unrecognized OID', () => {
      expect(TEAM_BY_USER['unknown-oid-xyz']).toBeUndefined();
    });

    it('returns undefined for empty string', () => {
      expect(TEAM_BY_USER['']).toBeUndefined();
    });
  });
});

// ─── teamFromTaskId ───────────────────────────────────────────────────────────

describe('teamFromTaskId', () => {
  describe('Scheduling Skill prefix (rbs-sched-)', () => {
    it('rbs-sched-001 → Scheduling Skill green', () => {
      const result = teamFromTaskId('rbs-sched-001');
      expect(result).not.toBeNull();
      expect(result?.label).toBe('Scheduling Skill');
      expect(result?.color).toBe('#107C10');
    });

    it('rbs-sched-sdk-delivery → Scheduling Skill', () => {
      expect(teamFromTaskId('rbs-sched-sdk-delivery')?.label).toBe('Scheduling Skill');
    });
  });

  describe('BizChat Platform prefix (rbs-bcp-)', () => {
    it('rbs-bcp-004 → BizChat Platform purple', () => {
      const result = teamFromTaskId('rbs-bcp-004');
      expect(result).not.toBeNull();
      expect(result?.label).toBe('BizChat Platform');
      expect(result?.color).toBe('#881798');
    });

    it('rbs-bcp-slot-reservation → BizChat Platform', () => {
      expect(teamFromTaskId('rbs-bcp-slot-reservation')?.label).toBe('BizChat Platform');
    });
  });

  describe('Reschedule Crew catch-all prefix (rbs-)', () => {
    it('rbs-foundry-002 → Reschedule Crew blue (catch-all)', () => {
      const result = teamFromTaskId('rbs-foundry-002');
      expect(result).not.toBeNull();
      expect(result?.label).toBe('Reschedule Crew');
      expect(result?.color).toBe('#0078D4');
    });

    it('rbs-seval-002 → Reschedule Crew (not sched, not bcp)', () => {
      expect(teamFromTaskId('rbs-seval-002')?.label).toBe('Reschedule Crew');
    });

    it('rbs-int-001 → Reschedule Crew', () => {
      expect(teamFromTaskId('rbs-int-001')?.label).toBe('Reschedule Crew');
    });

    it('rbs-arch-001 → Reschedule Crew', () => {
      expect(teamFromTaskId('rbs-arch-001')?.label).toBe('Reschedule Crew');
    });
  });

  describe('priority: specific prefixes beat catch-all', () => {
    it('rbs-sched- is matched before rbs- catch-all', () => {
      // rbs-sched- starts with rbs- but the specific check runs first
      expect(teamFromTaskId('rbs-sched-001')?.label).toBe('Scheduling Skill');
      expect(teamFromTaskId('rbs-sched-001')?.label).not.toBe('Reschedule Crew');
    });

    it('rbs-bcp- is matched before rbs- catch-all', () => {
      expect(teamFromTaskId('rbs-bcp-004')?.label).toBe('BizChat Platform');
      expect(teamFromTaskId('rbs-bcp-004')?.label).not.toBe('Reschedule Crew');
    });
  });

  describe('unknown prefixes', () => {
    it('returns null for non-rbs task IDs', () => {
      expect(teamFromTaskId('ado-task-123')).toBeNull();
    });

    it('returns null for empty string', () => {
      expect(teamFromTaskId('')).toBeNull();
    });

    it('returns null for tasks with no matching prefix', () => {
      expect(teamFromTaskId('graph-edge-001')).toBeNull();
    });
  });
});
