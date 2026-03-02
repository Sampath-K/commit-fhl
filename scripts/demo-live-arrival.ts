#!/usr/bin/env ts-node
/**
 * demo-live-arrival.ts — Injects a new commitment into the live system during the demo.
 *
 * Simulates a real extraction event: a new commitment arrives in the Teams pane
 * as if it was just extracted from a meeting that ended during the demo. Use this
 * to demonstrate D-001 metric 2: "End-to-end latency < 5 min".
 *
 * The script has two modes:
 *   --immediate   Inject right now (for testing)
 *   --delay <N>   Wait N seconds, then inject (default: 60s — use during demo)
 *
 * What gets injected:
 *   A new high-urgency commitment from Alex: "Address Q1 latency regression in plugin routing"
 *   Source: Teams meeting transcript (as if a standup just ended)
 *   Owner: Alex Chen (real OID if tenant is set up, else demo-alex-oid-001)
 *   Impact score: 72 (urgent + cross-team)
 *   Cascade trigger: links to BizChat Platform dependency chain
 *
 * Usage:
 *   # Test immediately (no delay)
 *   npx ts-node --project scripts/tsconfig.json scripts/demo-live-arrival.ts --immediate
 *
 *   # Queue for 60 seconds (default — use this during the demo)
 *   npx ts-node --project scripts/tsconfig.json scripts/demo-live-arrival.ts
 *
 *   # Custom delay
 *   npx ts-node --project scripts/tsconfig.json scripts/demo-live-arrival.ts --delay 90
 *
 *   # Full live scenario: queue and then show the card arriving
 *   API_BASE_URL=https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io \
 *     REAL_OID_ALEX=<real-oid> \
 *     npx ts-node --project scripts/tsconfig.json scripts/demo-live-arrival.ts --delay 45
 *
 * Decision: D-008 — Real-User Demo Tenant Setup
 * Task: T-042
 */

import { PERSONAS } from './personas/index';

const API_BASE = process.env['API_BASE_URL']  ?? 'http://localhost:5000';
const ALEX_OID = process.env['REAL_OID_ALEX'] ?? PERSONAS.find(p => p.id === 'alex')!.userId;

// ─── Parse arguments ────────────────────────────────────────────────────────
function parseArgs(): { delaySeconds: number } {
  const args = process.argv.slice(2);
  if (args.includes('--immediate')) return { delaySeconds: 0 };
  const delayIdx = args.indexOf('--delay');
  if (delayIdx >= 0 && args[delayIdx + 1]) {
    const n = parseInt(args[delayIdx + 1], 10);
    if (!isNaN(n) && n >= 0) return { delaySeconds: n };
  }
  return { delaySeconds: 60 };
}

// ─── The new commitment that arrives during the demo ────────────────────────
function buildLiveCommitment(ownerOid: string): object {
  const now        = new Date();
  const dueAt      = new Date(now.getTime() + 3 * 24 * 60 * 60 * 1000); // 3 days from now
  const meetingEnd = new Date(now.getTime() - 2 * 60 * 1000);            // 2 min ago (meeting just ended)

  return {
    id:     `rbs-live-${Date.now()}`,              // unique on every run
    owner:  ownerOid,
    title:  'Address Q1 latency regression in plugin routing — Alex to investigate and file ADO item before BizChat freeze',
    watchers: [
      PERSONAS.find(p => p.id === 'marcus')!.userId,  // BizChat Platform — affected
      PERSONAS.find(p => p.id === 'priya')!.userId,   // EM — tracking
    ],
    source: {
      type:      'meeting',
      url:       'https://teams.microsoft.com/l/meetup-join/live-standup',
      timestamp: meetingEnd.toISOString(),
      rawText:   'Alex committed to investigate the Q1 latency regression and file an ADO item before the BizChat freeze window.',
      sourceId:  `standup-${meetingEnd.toISOString().substring(0, 10)}`,
    },
    committedAt:   meetingEnd.toISOString(),
    dueAt:         dueAt.toISOString(),
    status:        'pending',
    priority:      'urgent-important',
    impactScore:   72,              // high — cross-team + close deadline
    blockedBy:     [],
    blocks:        ['rbs-bcp-003'], // blocks BizChat graph routing
    burnoutContribution: 3,
    lastActivity:  null,
    ownerDeliveryScoreAtCreation: 82,
  };
}

// ─── Countdown display ──────────────────────────────────────────────────────
function countdown(seconds: number): Promise<void> {
  return new Promise(resolve => {
    if (seconds === 0) { resolve(); return; }

    let remaining = seconds;
    process.stdout.write(`\n  Injecting in ${remaining}s... `);

    const interval = setInterval(() => {
      remaining--;
      if (remaining > 0) {
        if (remaining % 10 === 0 || remaining <= 5) {
          process.stdout.write(`${remaining}... `);
        }
      } else {
        clearInterval(interval);
        process.stdout.write('NOW!\n');
        resolve();
      }
    }, 1000);
  });
}

// ─── Main ───────────────────────────────────────────────────────────────────
async function demoLiveArrival(): Promise<void> {
  const { delaySeconds } = parseArgs();
  const commitment = buildLiveCommitment(ALEX_OID);

  console.log(`\n${'═'.repeat(70)}`);
  console.log('  DEMO LIVE ARRIVAL — New Commitment Injection');
  console.log(`  API:   ${API_BASE}`);
  console.log(`  Owner: ${ALEX_OID}`);
  console.log(`  Delay: ${delaySeconds === 0 ? 'immediate' : `${delaySeconds} seconds`}`);
  console.log(`${'═'.repeat(70)}`);
  console.log('\n  COMMITMENT TO INJECT:');
  console.log(`  Title:    "${(commitment as { title: string }).title.substring(0, 70)}..."`);
  console.log(`  Priority: urgent-important  |  Impact: 72  |  Due: 3 days`);
  console.log(`  Source:   Teams standup (meeting ended ~2 minutes ago)`);
  console.log(`  Blocks:   rbs-bcp-003 (BizChat graph routing) — cross-org impact`);

  if (delaySeconds > 0) {
    console.log('\n  ── DEMO TIMING ──────────────────────────────────────────────────────');
    console.log(`  Start this script now, then narrate the demo.`);
    console.log(`  At T+${delaySeconds}s: new card appears in the Commit pane automatically.`);
    console.log(`  Suggested: use --delay ${delaySeconds} then say:`);
    console.log(`    "I started an extraction in the background when this meeting started."`);
    console.log(`    "Watch the pane — a new commitment should arrive any moment now..."`);
  }

  await countdown(delaySeconds);

  console.log('\n  Posting commitment to API...');
  try {
    const res = await fetch(`${API_BASE}/api/v1/commitments`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify(commitment),
    });

    if (res.ok) {
      const body = await res.json() as { id: string };
      console.log(`\n  ✅ INJECTED — commitment ID: ${body.id}`);
      console.log(`\n  The new card should appear in the Commit pane within seconds.`);
      console.log(`  If the pane doesn't auto-refresh, hit the refresh button.`);
      console.log(`\n  Verify via API:`);
      console.log(`    ${API_BASE}/api/v1/commitments/${ALEX_OID}`);
      console.log(`\n  Expected demo moment:`);
      console.log(`    "There it is — extracted from the standup that ended 2 minutes ago."`);
      console.log(`    "Title, owner, due date, impact score — all populated by NLP."`);
      console.log(`    "It's already linked to Marcus's BizChat routing task as a blocker."`);
    } else {
      const text = await res.text();
      console.error(`\n  ✗ Injection failed: HTTP ${res.status}`);
      console.error(`  Response: ${text.substring(0, 200)}`);
      console.error('\n  Common causes:');
      console.error('  • API not running — start with: cd src/api && dotnet run');
      console.error(`  • Wrong API URL — current: ${API_BASE}`);
      process.exit(1);
    }
  } catch (err) {
    console.error(`\n  ✗ Network error: ${(err as Error).message}`);
    console.error('\n  Is the API running?');
    console.error(`  Local:  cd src/api && dotnet run`);
    console.error(`  Azure:  ${API_BASE}/api/v1/health`);
    process.exit(1);
  }

  console.log(`${'═'.repeat(70)}\n`);
}

demoLiveArrival().catch(console.error);
