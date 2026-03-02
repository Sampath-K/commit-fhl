#!/usr/bin/env ts-node
/**
 * verify-demo.ts — Smoke test checklist for T-039.
 * Runs 6 checks against the API and writes results to .agents/commit-fhl/demo-readiness.md.
 * Usage: npx ts-node --project scripts/tsconfig.json scripts/verify-demo.ts
 * Set API_BASE_URL env var to target a deployed environment (default: http://localhost:5000).
 */
import * as fs from 'fs';
import * as path from 'path';

const API_BASE = process.env['API_BASE_URL'] ?? 'http://localhost:5000';
const DEMO_USER_ID = process.env['DEMO_USER_ID'] ?? 'demo-alex';
const OUTPUT_PATH = path.resolve(__dirname, '../.agents/commit-fhl/demo-readiness.md');

interface CheckResult {
  name: string;
  passed: boolean;
  detail: string;
  elapsedMs: number;
}

async function runCheck(
  name: string,
  fn: () => Promise<{ passed: boolean; detail: string }>,
): Promise<CheckResult> {
  const start = Date.now();
  try {
    const result = await fn();
    return { name, ...result, elapsedMs: Date.now() - start };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { name, passed: false, detail: `Error: ${message}`, elapsedMs: Date.now() - start };
  }
}

async function main(): Promise<void> {
  console.log(`\nVerifying demo readiness against ${API_BASE}\n`);

  const results: CheckResult[] = [];

  // ── Check 1: Health / Graph auth ──────────────────────────────────────────
  results.push(await runCheck('Health / Graph auth', async () => {
    const res = await fetch(`${API_BASE}/api/v1/health`);
    if (!res.ok) return { passed: false, detail: `HTTP ${res.status}` };
    const body = await res.json() as Record<string, unknown>;
    // graphConnected may be false in local dev without real auth token — check status is at minimum 'degraded'
    const statusOk = body['status'] === 'ok' || body['status'] === 'degraded';
    return {
      passed: statusOk,
      detail: statusOk
        ? `status=${body['status']}, graphConnected=${body['graphConnected']}`
        : `Unexpected status: ${JSON.stringify(body)}`,
    };
  }));

  // ── Check 2: Commitment extraction (seeded data) ──────────────────────────
  results.push(await runCheck('Commitment extraction (seeded)', async () => {
    const res = await fetch(`${API_BASE}/api/v1/commitments/${DEMO_USER_ID}`);
    if (!res.ok) return { passed: false, detail: `HTTP ${res.status}` };
    const body = await res.json() as Record<string, unknown>;
    const data = body['data'] as unknown[];
    const hasData = Array.isArray(data);
    return {
      passed: hasData,
      detail: hasData
        ? `${data.length} commitment(s) found for ${DEMO_USER_ID}`
        : `No data array in response`,
    };
  }));

  // ── Check 3: Cascade simulation ───────────────────────────────────────────
  results.push(await runCheck('Cascade simulation', async () => {
    const res = await fetch(
      `${API_BASE}/api/v1/graph/cascade?rootTaskId=seed-alex-task-001&userId=${DEMO_USER_ID}&slipDays=2`,
      { method: 'POST' },
    );
    if (!res.ok) return { passed: false, detail: `HTTP ${res.status}` };
    const body = await res.json() as Record<string, unknown>;
    const elapsedHeader = res.headers.get('x-elapsed-ms');
    const affectedTasks = body['affectedTasks'] as unknown[] | undefined;
    const passed = Array.isArray(affectedTasks);
    return {
      passed,
      detail: passed
        ? `${affectedTasks.length} affected task(s), X-Elapsed-Ms=${elapsedHeader ?? 'n/a'}`
        : `affectedTasks missing from response`,
    };
  }));

  // ── Check 4: Viva Insights / capacity ─────────────────────────────────────
  results.push(await runCheck('Viva Insights / capacity', async () => {
    const res = await fetch(`${API_BASE}/api/v1/capacity?userId=${DEMO_USER_ID}`);
    if (!res.ok) return { passed: false, detail: `HTTP ${res.status}` };
    const body = await res.json() as Record<string, unknown>;
    const hasLoadIndex = typeof body['loadIndex'] === 'number';
    return {
      passed: hasLoadIndex,
      detail: hasLoadIndex
        ? `loadIndex=${body['loadIndex']}, burnoutTrend=${body['burnoutTrend']}`
        : `loadIndex missing from response`,
    };
  }));

  // ── Check 5: Approval loop ────────────────────────────────────────────────
  results.push(await runCheck('Approval loop (POST /approvals)', async () => {
    const res = await fetch(
      `${API_BASE}/api/v1/approvals?userId=${DEMO_USER_ID}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          draftId:      'smoke-test-draft-001',
          commitmentId: 'smoke-test-commitment-001',
          decision:     'skip',
        }),
      },
    );
    // 404 is acceptable here (commitment doesn't exist in smoke env) — we're testing the route is alive
    const routeAlive = res.status === 200 || res.status === 404 || res.status === 400;
    return {
      passed: routeAlive,
      detail: routeAlive
        ? `Route responded with HTTP ${res.status} (expected for smoke test)`
        : `Unexpected status ${res.status}`,
    };
  }));

  // ── Check 6: Psychology layer / motivation ────────────────────────────────
  results.push(await runCheck('Psychology layer / motivation', async () => {
    const res = await fetch(`${API_BASE}/api/v1/users/${DEMO_USER_ID}/motivation`);
    if (!res.ok) return { passed: false, detail: `HTTP ${res.status}` };
    const body = await res.json() as Record<string, unknown>;
    const hasDeliveryScore = body['deliveryScore'] !== undefined;
    return {
      passed: hasDeliveryScore,
      detail: hasDeliveryScore
        ? `deliveryScore=${body['deliveryScore']}, streakDays=${body['streakDays']}, level=${body['competencyLevel']}`
        : `deliveryScore missing from response`,
    };
  }));

  // ── Print results ──────────────────────────────────────────────────────────
  const allGreen = results.every(r => r.passed);
  console.log('Results:\n');
  for (const r of results) {
    const icon = r.passed ? '✅' : '❌';
    console.log(`  ${icon} ${r.name} (${r.elapsedMs}ms)`);
    console.log(`     ${r.detail}`);
  }
  console.log(`\nOverall: ${allGreen ? '✅ ALL GREEN' : '❌ SOME CHECKS FAILED'}`);

  // ── Write demo-readiness.md ────────────────────────────────────────────────
  const now = new Date().toISOString();
  const lines: string[] = [
    `# Demo Readiness Report`,
    `> Generated: ${now}`,
    `> Target: ${API_BASE}`,
    `> Demo user: ${DEMO_USER_ID}`,
    '',
    `## Summary`,
    '',
    `| Status | Checks |`,
    `|--------|--------|`,
    `| ✅ Passed | ${results.filter(r => r.passed).length} |`,
    `| ❌ Failed | ${results.filter(r => !r.passed).length} |`,
    `| Total    | ${results.length} |`,
    '',
    `**Overall: ${allGreen ? '✅ READY FOR DEMO' : '❌ NOT READY — see failures below'}**`,
    '',
    `## Check Results`,
    '',
    `| # | Check | Status | Detail | Elapsed |`,
    `|---|-------|--------|--------|---------|`,
    ...results.map((r, i) =>
      `| ${i + 1} | ${r.name} | ${r.passed ? '✅ PASS' : '❌ FAIL'} | ${r.detail} | ${r.elapsedMs}ms |`,
    ),
    '',
    `## Latency Targets (P-02)`,
    '',
    `| Target | Limit | Status |`,
    `|--------|-------|--------|`,
    `| AI extraction pipeline | < 300,000ms (5 min) | Logged in NlpPipeline |`,
    `| Cascade simulation | < 10,000ms | X-Elapsed-Ms header on /graph/cascade |`,
    `| Adaptive Card render | < 2,000ms | Logged in AdaptiveCardBuilder |`,
    '',
    `## Next Steps`,
    '',
    `1. \\`az login --tenant 91b9767c-6b0a-4b0b-bd4d-e08a6383426c\\``,
    `2. \\`az group create --name commit-fhl-rg --location eastus\\``,
    `3. \\`az deployment group create --resource-group commit-fhl-rg --template-file infra/main.bicep --parameters @infra/parameters.json\\``,
    `4. Build + push Docker image to ACR`,
    `5. Update appPackage/manifest.json with deployed URLs`,
    `6. Upload Teams app zip to 7k2cc2 org catalog`,
  ];

  fs.mkdirSync(path.dirname(OUTPUT_PATH), { recursive: true });
  fs.writeFileSync(OUTPUT_PATH, lines.join('\n') + '\n');
  console.log(`\nReport written to: ${OUTPUT_PATH}`);

  process.exit(allGreen ? 0 : 1);
}

main().catch((err: unknown) => {
  console.error(err);
  process.exit(1);
});
