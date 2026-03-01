#!/usr/bin/env ts-node
/**
 * Seed Demo — loads personas + cascade chains into the API.
 * Idempotent: safe to run multiple times (uses upsert).
 * Usage: npx ts-node --project scripts/tsconfig.json scripts/seed-demo.ts [--dry-run]
 */
import { PERSONAS } from './personas/index';
import { CASCADE_A_COMMITMENTS, CASCADE_A_EDGES } from './scenarios/cascadeA';

const DRY_RUN = process.argv.includes('--dry-run');
const API_BASE = process.env['API_BASE_URL'] ?? 'http://localhost:5000';

async function seedDemo(): Promise<void> {
  console.log(`\nSeeding demo data${DRY_RUN ? ' (DRY RUN)' : ''}...\n`);

  let totalCommitments = 0;
  let totalEdges = 0;

  // ─── Personas ───────────────────────────────────────────────────────────────
  console.log('Personas:');
  for (const persona of PERSONAS) {
    console.log(`  ${persona.name} (${persona.role}) -- ${persona.storyRole}`);
  }

  // ─── Cascade A ──────────────────────────────────────────────────────────────
  console.log('\nCascade A -- The Classic Slip:');
  for (const commitment of CASCADE_A_COMMITMENTS) {
    console.log(`  [${commitment.id}] ${commitment.title} [owner: ${commitment.owner}]`);
    totalCommitments++;
  }
  for (const edge of CASCADE_A_EDGES) {
    console.log(`  Edge: ${edge.fromId} -> ${edge.toId} (${edge.edgeType}, confidence: ${edge.confidence})`);
    totalEdges++;
  }

  if (!DRY_RUN) {
    console.log('\nPosting to API...');
    for (const commitment of CASCADE_A_COMMITMENTS) {
      const res = await fetch(`${API_BASE}/api/v1/commitments`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(commitment),
      });
      if (!res.ok) {
        console.warn(`  WARNING: Failed to seed commitment ${commitment.id}: ${res.status}`);
      }
    }
    console.log(`\nSeeded ${totalCommitments} commitments, ${totalEdges} edges`);
  } else {
    console.log(`\nDry run complete:`);
    console.log(`  Would seed ${totalCommitments} commitments, ${totalEdges} edges`);
    console.log(`  Personas: ${PERSONAS.length}`);
    console.log(`  Cascade chains: 1 (Cascade A)`);
  }
}

seedDemo().catch(console.error);
