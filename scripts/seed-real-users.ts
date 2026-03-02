#!/usr/bin/env ts-node
/**
 * Seed Real Users — re-seeds the demo scenario with real AAD user OIDs from the 7k2cc2 tenant.
 *
 * This script is used when you want the demo to show real user identities and photos
 * instead of the placeholder demo-* OIDs. The scenario (commitments + cascade chains)
 * is identical to seed-demo.ts — only the user OIDs change.
 *
 * Prerequisites:
 *   1. Sign into 7k2cc2 tenant (see docs/real-user-setup.md)
 *   2. Copy .env.real-users.example → .env.real-users and fill in the real OIDs
 *   3. Ensure the API is running and accessible via API_BASE_URL
 *
 * Usage:
 *   # Dry run — print what would be seeded without hitting the API
 *   npx ts-node --project scripts/tsconfig.json scripts/seed-real-users.ts --dry-run
 *
 *   # Live seed using env file
 *   set -a && source .env.real-users && set +a
 *   API_BASE_URL=https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io \
 *     npx ts-node --project scripts/tsconfig.json scripts/seed-real-users.ts
 *
 *   # Flush existing demo data first, then re-seed with real OIDs
 *   npx ts-node --project scripts/tsconfig.json scripts/flush-demo.ts
 *   npx ts-node --project scripts/tsconfig.json scripts/seed-real-users.ts
 *
 * Environment variables (all optional — falls back to demo-* OIDs if not set):
 *   REAL_OID_ALEX    — Real AAD OID for "Alex" persona (lead engineer)
 *   REAL_OID_PRIYA   — Real AAD OID for "Priya" persona (EM)
 *   REAL_OID_MARCUS  — Real AAD OID for "Marcus" persona (BizChat Platform engineer)
 *   REAL_OID_FATIMA  — Real AAD OID for "Fatima" persona (PM)
 *   REAL_OID_DAVID   — Real AAD OID for "David" persona (Director / exec sponsor)
 *   REAL_OID_SARAH   — Real AAD OID for "Sarah" persona (Scheduling Skill engineer)
 *
 * Decision: D-008 — Real-User Demo Tenant Setup
 */

import { PERSONAS } from './personas/index';
import { RESCHEDULE_SKILL_COMMITMENTS, RESCHEDULE_SKILL_EDGES } from './scenarios/rescheduleSkill';
import type { CommitmentRecord } from '../src/app/src/types/api';

const DRY_RUN  = process.argv.includes('--dry-run');
const API_BASE = process.env['API_BASE_URL'] ?? 'http://localhost:5000';

// ─── OID mapping — real from env, demo as fallback ───────────────────────────
const OID_MAP: Record<string, string> = {
  [PERSONAS.find(p => p.id === 'alex')!.userId]:   process.env['REAL_OID_ALEX']   ?? PERSONAS.find(p => p.id === 'alex')!.userId,
  [PERSONAS.find(p => p.id === 'priya')!.userId]:  process.env['REAL_OID_PRIYA']  ?? PERSONAS.find(p => p.id === 'priya')!.userId,
  [PERSONAS.find(p => p.id === 'marcus')!.userId]: process.env['REAL_OID_MARCUS'] ?? PERSONAS.find(p => p.id === 'marcus')!.userId,
  [PERSONAS.find(p => p.id === 'fatima')!.userId]: process.env['REAL_OID_FATIMA'] ?? PERSONAS.find(p => p.id === 'fatima')!.userId,
  [PERSONAS.find(p => p.id === 'david')!.userId]:  process.env['REAL_OID_DAVID']  ?? PERSONAS.find(p => p.id === 'david')!.userId,
  [PERSONAS.find(p => p.id === 'sarah')!.userId]:  process.env['REAL_OID_SARAH']  ?? PERSONAS.find(p => p.id === 'sarah')!.userId,
};

function remapOids(c: CommitmentRecord): CommitmentRecord {
  return {
    ...c,
    owner:    OID_MAP[c.owner]   ?? c.owner,
    watchers: c.watchers.map(w => OID_MAP[w] ?? w),
  };
}

const REAL_COMMITMENTS = RESCHEDULE_SKILL_COMMITMENTS.map(remapOids);

async function seedRealUsers(): Promise<void> {
  console.log(`\n${'═'.repeat(70)}`);
  console.log(`  SEED (REAL USERS): Reschedule BizChat Skill — Q1 Ship${DRY_RUN ? ' (DRY RUN)' : ''}`);
  console.log(`  API:  ${API_BASE}`);
  console.log(`${'═'.repeat(70)}\n`);

  // ─── Show OID mapping ─────────────────────────────────────────────────────
  console.log('OID MAPPING (persona → actual user):');
  const realCount = Object.values(OID_MAP).filter(v => !v.startsWith('demo-')).length;
  for (const [persona, p] of Object.entries(PERSONAS.reduce((m, p) => ({ ...m, [p.id]: p }), {} as Record<string, typeof PERSONAS[0]>))) {
    const demo = p.userId;
    const real = OID_MAP[demo];
    const status = real === demo ? '⚠️  DEMO OID (not mapped)' : '✅ Real OID';
    console.log(`  ${p.name.padEnd(20)} ${demo.padEnd(22)} → ${real.padEnd(36)} ${status}`);
  }

  if (realCount === 0) {
    console.log('\n⚠️  WARNING: No real OIDs provided. All personas will use demo OIDs.');
    console.log('   Set REAL_OID_ALEX etc. in your environment, or copy .env.real-users.example');
    console.log('   See docs/real-user-setup.md for instructions.\n');
  } else {
    console.log(`\n✅ ${realCount}/6 personas mapped to real OIDs\n`);
  }

  if (DRY_RUN) {
    console.log('  SAMPLE (first 3 commitments with remapped OIDs):');
    for (const c of REAL_COMMITMENTS.slice(0, 3)) {
      console.log(`  [${c.id}] owner:${c.owner} watchers:[${c.watchers.join(', ')}]`);
    }
    console.log('\n✔ Dry run complete — no data written.\n');
    return;
  }

  // ─── POST each commitment with remapped OIDs ──────────────────────────────
  console.log('Posting commitments to API...');
  let ok = 0, fail = 0;

  for (const commitment of REAL_COMMITMENTS) {
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
  console.log(`  Seeded: ${ok} / ${REAL_COMMITMENTS.length} commitments (real OIDs)`);
  if (fail > 0) console.log(`  Failed: ${fail} commitments — check API logs`);
  console.log(`  Edges:  ${RESCHEDULE_SKILL_EDGES.length} (stored in-memory for cascade simulation)`);
  if (realCount > 0) {
    const primaryOid = OID_MAP[PERSONAS.find(p => p.id === 'alex')!.userId];
    console.log(`\n  Verify: ${API_BASE}/api/v1/commitments/${primaryOid}`);
    console.log(`  Teams:  Open the Commit tab as the Alex persona user — real name/photo should appear`);
  }
  console.log(`${'═'.repeat(70)}\n`);
}

seedRealUsers().catch(console.error);
