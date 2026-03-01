#!/usr/bin/env ts-node
/**
 * Seed Demo — loads 6 personas + 3 cascade chains into the API.
 * Idempotent: safe to run multiple times (uses upsert).
 * Usage: npx ts-node scripts/seed-demo.ts [--dry-run]
 */
import { PERSONAS } from './personas/index';
import { CASCADE_A_COMMITMENTS, CASCADE_A_EDGES } from './scenarios/cascadeA';

const DRY_RUN = process.argv.includes('--dry-run');
const API_BASE = process.env.API_BASE_URL ?? 'http://localhost:3000';

async function seedDemo(): Promise<void> {
  console.log(`\nSeeding demo data${DRY_RUN ? ' (DRY RUN)' : ''}...\n`);

  let totalCommitments = 0;
  let totalEdges = 0;

  // Show personas
  console.log('Personas:');
  for (const persona of PERSONAS) {
    console.log(`  ${persona.name} (${persona.role}) -- ${persona.storyRole}`);
  }

  // Show Cascade A
  console.log('\nCascade A -- The Classic Slip:');
  for (const commitment of CASCADE_A_COMMITMENTS) {
    console.log(`  ${commitment.title} [owner: ${commitment.partitionKey}]`);
    totalCommitments++;
  }
  for (const edge of CASCADE_A_EDGES) {
    console.log(`  Edge: ${edge.fromId} -> ${edge.toId} (${edge.edgeType})`);
    totalEdges++;
  }

  if (!DRY_RUN) {
    // POST to API
    for (const commitment of CASCADE_A_COMMITMENTS) {
      const res = await fetch(`${API_BASE}/api/v1/commitments`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(commitment),
      });
      if (!res.ok) console.warn(`  WARNING: Failed to seed commitment ${commitment.rowKey}: ${res.status}`);
    }
    console.log(`\nSeeded ${totalCommitments} commitments, ${totalEdges} edges`);
  } else {
    console.log(`\nDry run complete: would seed ${totalCommitments} commitments, ${totalEdges} edges`);
  }
}

seedDemo().catch(console.error);
