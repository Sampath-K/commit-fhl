#!/usr/bin/env ts-node
/**
 * Seed Demo — loads the Reschedule BizChat Skill project into the deployed API.
 * Idempotent: safe to run multiple times (uses upsert).
 *
 * Usage:
 *   npx ts-node --project scripts/tsconfig.json scripts/seed-demo.ts [--dry-run]
 *   API_BASE_URL=https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io \
 *     npx ts-node --project scripts/tsconfig.json scripts/seed-demo.ts
 */
import { PERSONAS } from './personas/index';
import { RESCHEDULE_SKILL_COMMITMENTS, RESCHEDULE_SKILL_EDGES, IDS } from './scenarios/rescheduleSkill';

const DRY_RUN  = process.argv.includes('--dry-run');
const API_BASE = process.env['API_BASE_URL'] ?? 'http://localhost:5000';

const RISK_LABELS: Record<string, string> = {
  [IDS.sevalFeedback]:       '🔴 AT RISK — main cascade root (SEVAL feedback, no activity 2d)',
  [IDS.foundryAccuracyGate]: '🔴 AT RISK — Foundry 71% vs 85% target (gate: Mar 7)',
  [IDS.bcpPluginManifest]:   '⚠️  IN PROGRESS — BizChat plugin manifest (due Mar 9)',
  [IDS.schedUnitTests]:      '⚠️  IN PROGRESS — unit tests (due Mar 5)',
};

async function seedDemo(): Promise<void> {
  console.log(`\n${'═'.repeat(70)}`);
  console.log(`  SEED: Reschedule BizChat Skill — Q1 Ship${DRY_RUN ? ' (DRY RUN)' : ''}`);
  console.log(`  API:  ${API_BASE}`);
  console.log(`${'═'.repeat(70)}\n`);

  // ─── Print persona summary ─────────────────────────────────────────────────
  console.log('PERSONAS:');
  for (const p of PERSONAS) {
    console.log(`  ${p.name.padEnd(20)} ${p.role.padEnd(22)} ${p.userId}`);
  }

  // ─── Print commitment summary grouped by track ─────────────────────────────
  const tracks: Record<string, string> = {
    'rbs-arch':    'A: Design & Architecture',
    'rbs-sched':   'B: Scheduling Skill',
    'rbs-bcp':     'C: BizChat Platform',
    'rbs-foundry': 'D: Foundry Test Runs',
    'rbs-seval':   'E: SEVALs',
    'rbs-score':   'F: Scorecards',
    'rbs-int':     'G: Integration & Ship',
  };

  console.log('\nCOMMITMENTS:');
  for (const [prefix, label] of Object.entries(tracks)) {
    const items = RESCHEDULE_SKILL_COMMITMENTS.filter(c => c.id.startsWith(prefix));
    console.log(`\n  Track ${label} (${items.length} items):`);
    for (const c of items) {
      const status    = c.status.padEnd(12);
      const due       = c.dueAt ? c.dueAt.substring(0, 10) : 'no date';
      const risk      = RISK_LABELS[c.id] ? `  ${RISK_LABELS[c.id]}` : '';
      const owner     = PERSONAS.find(p => p.userId === c.owner)?.name.split(' ')[0] ?? c.owner;
      console.log(`    [${c.id}] ${status} due:${due}  ${owner}  — ${c.title.substring(0, 55)}...${risk}`);
    }
  }

  console.log(`\n  TOTAL: ${RESCHEDULE_SKILL_COMMITMENTS.length} commitments, ${RESCHEDULE_SKILL_EDGES.length} edges`);

  // ─── Cascade call-out ─────────────────────────────────────────────────────
  console.log('\nCASCADE SCENARIO:');
  console.log('  Root: rbs-seval-002 (SEVAL feedback, 4d remaining, no progress)');
  console.log('  If slips 5 days → cascades to: seval-003 → seval-004 → int-001 → int-002 → int-003 → int-004 → int-005');
  console.log('  Impact: Reschedule Skill MISSES Q1 ship date (2026-03-30)');
  console.log('\n  Second risk: rbs-foundry-002 (71% accuracy, 14pp gap, gate 2026-03-07)');
  console.log('  If slips 7d → foundry-003 → bcp-004 → int-001 (second path to same Q1 miss)');

  if (DRY_RUN) {
    console.log('\n✔ Dry run complete — no data written.\n');
    return;
  }

  // ─── POST each commitment ─────────────────────────────────────────────────
  console.log('\nPosting commitments to API...');
  let ok = 0;
  let fail = 0;

  for (const commitment of RESCHEDULE_SKILL_COMMITMENTS) {
    try {
      const res = await fetch(`${API_BASE}/api/v1/commitments`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(commitment),
      });
      if (res.ok) {
        process.stdout.write('  ✔ ' + commitment.id + '\n');
        ok++;
      } else {
        const text = await res.text();
        console.warn(`  ✗ ${commitment.id}: HTTP ${res.status} — ${text.substring(0, 100)}`);
        fail++;
      }
    } catch (err) {
      console.warn(`  ✗ ${commitment.id}: network error — ${(err as Error).message}`);
      fail++;
    }
  }

  console.log(`\n${'─'.repeat(70)}`);
  console.log(`  Seeded: ${ok} / ${RESCHEDULE_SKILL_COMMITMENTS.length} commitments`);
  if (fail > 0) console.log(`  Failed: ${fail} commitments — check API logs`);
  console.log(`  Edges:  ${RESCHEDULE_SKILL_EDGES.length} (stored in-memory for cascade simulation)`);
  console.log(`\n  Verify: ${API_BASE}/api/v1/commitments/${PERSONAS[0].userId}`);
  console.log(`${'═'.repeat(70)}\n`);
}

seedDemo().catch(console.error);
