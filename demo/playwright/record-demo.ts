/**
 * Commit FHL — Demo Recording Script
 *
 * Seeds the demo user with realistic commitments, then captures screenshots
 * at key moments for stitching into the Remotion video.
 *
 * Usage:
 *   cd demo/playwright
 *   npm install
 *   npx ts-node --project tsconfig.json record-demo.ts
 *
 * Output: ../remotion/public/screenshots/{01..04}-*.png
 */

import { chromium, Browser, Page, BrowserContext } from 'playwright';
import * as path from 'path';
import * as fs from 'fs';
import * as crypto from 'crypto';

// ─── Config ───────────────────────────────────────────────────────────────────

const API_BASE = 'https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io';
const APP_URL  = 'https://thankful-pond-0ba16370f.6.azurestaticapps.net';

/** Browser-mode fallback userId injected by main.tsx DEV_FALLBACK_USER_ID */
const DEMO_USER_ID = 'f7a02de7-e195-4894-bc23-f7f74b696cbd';

const SCREENSHOTS_DIR = path.resolve(__dirname, '../remotion/public/screenshots');

// Ensure output directory exists
fs.mkdirSync(SCREENSHOTS_DIR, { recursive: true });

// ─── Demo Scenario: "Reschedule BizChat Skill" ───────────────────────────────
//
// Priya Kumar is a PM driving the BizChat Copilot Skill GA delivery.
// She has made several cross-team commitments that are now at risk because
// a key dependency (the Latency Fix) slipped. The demo shows:
//   1. Priority board with real commitments surfaced from signals
//   2. Cascade view highlighting at-risk downstream items
//   3. Progress tab showing completions this week + psychology motivation layer
//   4. Replan panel with 3 AI-generated options

interface SeedCommitment {
  title: string;
  owner: string;
  watchers: string[];
  sourceType: 'meeting' | 'chat' | 'email' | 'ado' | 'planner';
  sourceUrl: string;
  status: 'pending' | 'in-progress' | 'done' | 'deferred' | 'delegated';
  priority: 'urgent-important' | 'not-urgent-important' | 'urgent-not-important' | 'not-urgent-not-important';
  dueAt?: string;
  impactScore: number;
  itemKind: 'commitment' | 'completion';
  resolutionReason?: string;
  projectContext?: string;
  artifactName?: string;
}

const DEMO_COMMITMENTS: SeedCommitment[] = [
  // ── At-risk commitments (blocker cascade) ──────────────────────────────────
  {
    title: 'Deliver BizChat Copilot Skill GA package to Substrate team by March 10',
    owner: DEMO_USER_ID,
    watchers: ['nia.james@contoso.com', 'alex.chen@contoso.com'],
    sourceType: 'meeting',
    sourceUrl: 'https://teams.microsoft.com/l/meetup-join/19:abc@thread.v2/1709200000000?context={}',
    status: 'in-progress',
    priority: 'urgent-important',
    dueAt: new Date(Date.now() + 5 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 92,
    itemKind: 'commitment',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'GA Delivery — All-Hands (recording)',
  },
  {
    title: 'Fix p99 latency regression in semantic ranking before GA',
    owner: DEMO_USER_ID,
    watchers: ['alex.chen@contoso.com'],
    sourceType: 'ado',
    sourceUrl: 'https://dev.azure.com/contoso/BizChat/_workitems/edit/18847',
    status: 'pending',
    priority: 'urgent-important',
    dueAt: new Date(Date.now() + 2 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 88,
    itemKind: 'commitment',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'ADO #18847 — Latency Regression',
  },
  {
    title: 'Send final API contract diff to SDK team (Omar) by EOD Friday',
    owner: DEMO_USER_ID,
    watchers: ['omar.hassan@contoso.com'],
    sourceType: 'chat',
    sourceUrl: 'https://teams.microsoft.com/l/chat/0/0?users=omar.hassan@contoso.com',
    status: 'pending',
    priority: 'urgent-important',
    dueAt: new Date(Date.now() + 1 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 79,
    itemKind: 'commitment',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'Teams DM — Omar Hassan',
  },
  {
    title: 'Unblock Nia\'s dashboard by sharing Viva Insights capacity data',
    owner: DEMO_USER_ID,
    watchers: ['nia.james@contoso.com'],
    sourceType: 'email',
    sourceUrl: 'https://outlook.office.com/mail/inbox/id/AAQkAGViN',
    status: 'pending',
    priority: 'not-urgent-important',
    dueAt: new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 65,
    itemKind: 'commitment',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'Email — Re: Dashboard data request',
  },
  {
    title: 'Review and approve Omar\'s SDK integration PR before Thursday standup',
    owner: DEMO_USER_ID,
    watchers: ['omar.hassan@contoso.com', 'alex.chen@contoso.com'],
    sourceType: 'ado',
    sourceUrl: 'https://dev.azure.com/contoso/BizChat/_git/BizChat/pullrequest/9201',
    status: 'pending',
    priority: 'not-urgent-important',
    dueAt: new Date(Date.now() + 2 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 58,
    itemKind: 'commitment',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'PR #9201 — SDK Integration',
  },
  {
    title: 'Update GA readiness checklist in SharePoint with latency test results',
    owner: DEMO_USER_ID,
    watchers: ['nia.james@contoso.com'],
    sourceType: 'meeting',
    sourceUrl: 'https://teams.microsoft.com/l/meetup-join/19:def@thread.v2/1709210000000?context={}',
    status: 'pending',
    priority: 'not-urgent-important',
    dueAt: new Date(Date.now() + 4 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 44,
    itemKind: 'commitment',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'Teams Meeting — GA Readiness Review',
  },

  // ── Completions (shipped this week — drive the Progress tab) ──────────────
  {
    title: 'Merged: Add streaming response support to BizChat plugin endpoint',
    owner: DEMO_USER_ID,
    watchers: [],
    sourceType: 'ado',
    sourceUrl: 'https://dev.azure.com/contoso/BizChat/_git/BizChat/pullrequest/9188',
    status: 'done',
    priority: 'not-urgent-not-important',
    dueAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 71,
    itemKind: 'completion',
    resolutionReason: 'PR merged — detected via ADO webhook',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'PR #9188 — Streaming Response',
  },
  {
    title: 'Completed: Q1 planning doc finalized and shared with leadership',
    owner: DEMO_USER_ID,
    watchers: [],
    sourceType: 'planner',
    sourceUrl: 'https://tasks.office.com/contoso/Home/Planner#/plantaskboard',
    status: 'done',
    priority: 'not-urgent-not-important',
    dueAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 55,
    itemKind: 'completion',
    resolutionReason: 'Planner task marked complete',
    projectContext: 'Q1 Planning',
    artifactName: 'Planner — Q1 Roadmap task',
  },
  {
    title: 'Merged: Telemetry instrumentation for skill latency tracking',
    owner: DEMO_USER_ID,
    watchers: [],
    sourceType: 'ado',
    sourceUrl: 'https://dev.azure.com/contoso/BizChat/_git/BizChat/pullrequest/9195',
    status: 'done',
    priority: 'not-urgent-not-important',
    dueAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
    impactScore: 62,
    itemKind: 'completion',
    resolutionReason: 'PR merged — detected via ADO webhook',
    projectContext: 'BizChat Copilot Skill GA',
    artifactName: 'PR #9195 — Latency Telemetry',
  },
];

// ─── API helpers ───────────────────────────────────────────────────────────────

async function warmUpApi(): Promise<void> {
  console.log('⏳ Warming up API (cold start may take ~15s)...');
  const start = Date.now();
  for (let attempt = 0; attempt < 10; attempt++) {
    try {
      const res = await fetch(`${API_BASE}/api/v1/health`, { signal: AbortSignal.timeout(20000) });
      if (res.ok) {
        console.log(`✅ API ready in ${((Date.now() - start) / 1000).toFixed(1)}s`);
        return;
      }
    } catch {
      // cold start — keep polling
    }
    await sleep(3000);
  }
  throw new Error('API did not become ready within 30s');
}

async function clearUserCommitments(): Promise<void> {
  console.log('🗑  Clearing existing demo user commitments...');
  try {
    const res = await fetch(`${API_BASE}/api/v1/commitments?userId=${DEMO_USER_ID}`, {
      signal: AbortSignal.timeout(15000),
    });
    if (!res.ok) return;
    const body = await res.json() as { success: boolean; data?: { id: string }[] };
    if (!body.success || !body.data) return;
    // Best-effort delete each
    await Promise.allSettled(
      body.data.map(c =>
        fetch(`${API_BASE}/api/v1/commitments/${c.id}`, {
          method: 'DELETE',
          signal: AbortSignal.timeout(5000),
        })
      )
    );
    console.log(`   Cleared ${body.data.length} existing items`);
  } catch {
    console.log('   (no existing items or delete not supported — continuing)');
  }
}

async function seedCommitments(): Promise<void> {
  console.log('🌱 Seeding demo commitments...');
  let seeded = 0;
  for (const c of DEMO_COMMITMENTS) {
    const payload = {
      id: crypto.randomUUID(),
      title: c.title,
      owner: c.owner,
      watchers: c.watchers,
      source: {
        type: c.sourceType,
        url: c.sourceUrl,
        timestamp: new Date(Date.now() - Math.random() * 7 * 24 * 60 * 60 * 1000).toISOString(),
      },
      committedAt: new Date().toISOString(),
      dueAt: c.dueAt,
      status: c.status,
      priority: c.priority,
      impactScore: c.impactScore,
      burnoutContribution: Math.round(c.impactScore * 0.3),
      itemKind: c.itemKind,
      resolutionReason: c.resolutionReason,
      projectContext: c.projectContext,
      artifactName: c.artifactName,
    };

    try {
      const res = await fetch(`${API_BASE}/api/v1/commitments`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
        signal: AbortSignal.timeout(10000),
      });
      if (res.ok) {
        seeded++;
        process.stdout.write('.');
      } else {
        const text = await res.text();
        console.warn(`\n   ⚠ Failed to seed "${c.title.slice(0, 50)}…": ${res.status} ${text.slice(0, 100)}`);
      }
    } catch (e) {
      console.warn(`\n   ⚠ Network error seeding "${c.title.slice(0, 50)}…": ${e}`);
    }
  }
  console.log(`\n✅ Seeded ${seeded}/${DEMO_COMMITMENTS.length} items`);
}

// ─── Screenshot helpers ───────────────────────────────────────────────────────

async function waitForCommitments(page: Page): Promise<void> {
  // Wait for at least one commitment card to appear (Fluent Card or custom card element)
  await page.waitForSelector('[data-testid="commitment-card"], .fui-Card, .commit-card', {
    timeout: 30000,
  }).catch(async () => {
    // Fallback: wait for some content beyond the loading spinner
    await page.waitForFunction(
      () => document.body.innerText.length > 500,
      { timeout: 30000 }
    );
  });
  // Extra settle time for animations
  await sleep(1500);
}

async function screenshot(page: Page, filename: string, description: string): Promise<void> {
  const dest = path.join(SCREENSHOTS_DIR, filename);
  await page.screenshot({ path: dest, fullPage: false });
  console.log(`📸 Saved: ${filename}  (${description})`);
}

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

// ─── Main ─────────────────────────────────────────────────────────────────────

async function main(): Promise<void> {
  console.log('\n🎬  Commit FHL — Demo Recording');
  console.log('================================\n');

  // Step 1 — Warm up API
  await warmUpApi();

  // Step 2 — Seed data
  await clearUserCommitments();
  await seedCommitments();

  // Step 3 — Launch browser
  console.log('\n🌐 Launching browser...');
  const browser: Browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox'],
  });

  const context: BrowserContext = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    deviceScaleFactor: 2, // Retina quality screenshots
  });

  const page: Page = await context.newPage();

  // Suppress console noise
  page.on('console', msg => {
    if (msg.type() === 'error') {
      console.log(`   [browser error] ${msg.text().slice(0, 120)}`);
    }
  });

  try {
    // ── Scene 01: Priority Board ─────────────────────────────────────────────
    console.log('\n📐 Scene 01 — Priority Board');
    await page.goto(APP_URL, { waitUntil: 'load', timeout: 60000 });
    await waitForCommitments(page);

    // Ensure Priority view is selected (default)
    const priorityBtn = page.getByRole('button', { name: /priority/i });
    if (await priorityBtn.isVisible()) {
      await priorityBtn.click();
      await sleep(800);
    }

    await screenshot(page, '01-priority-board.png', 'Priority board with all commitments');

    // ── Scene 02: Cascade View ────────────────────────────────────────────────
    console.log('\n📐 Scene 02 — Cascade View');
    // Click the first high-impact commitment to open cascade
    const firstCard = page.locator('[data-testid="commitment-card"], .fui-Card').first();
    if (await firstCard.isVisible()) {
      await firstCard.click();
      await sleep(1200);
    }

    // Try to trigger cascade analysis
    const cascadeBtn = page.getByRole('button', { name: /cascade|analyze|graph/i });
    if (await cascadeBtn.isVisible()) {
      await cascadeBtn.click();
      await sleep(2000);
    }

    await screenshot(page, '02-cascade-view.png', 'Cascade view showing at-risk downstream tasks');

    // ── Scene 03: Progress Tab ────────────────────────────────────────────────
    console.log('\n📐 Scene 03 — Progress Tab');
    // Navigate back to main pane if needed
    const backBtn = page.getByRole('button', { name: /back|close|×/i });
    if (await backBtn.isVisible()) {
      await backBtn.click();
      await sleep(600);
    }

    // Click Progress tab
    const progressBtn = page.getByRole('button', { name: /progress/i });
    if (await progressBtn.isVisible()) {
      await progressBtn.click();
      await sleep(1500); // Wait for psychology panel + completion timeline to render
    }

    await screenshot(page, '03-progress-tab.png', 'Progress tab with delivery score + completion timeline');

    // ── Scene 04: Replan Panel ────────────────────────────────────────────────
    console.log('\n📐 Scene 04 — Replan Panel');
    // Navigate to cascade view and trigger replan
    await page.goto(APP_URL, { waitUntil: 'load', timeout: 60000 });
    await waitForCommitments(page);
    await sleep(800);

    const firstCard2 = page.locator('[data-testid="commitment-card"], .fui-Card').first();
    if (await firstCard2.isVisible()) {
      await firstCard2.click();
      await sleep(1200);
    }

    const replanBtn = page.getByRole('button', { name: /replan|options|ai/i });
    if (await replanBtn.isVisible()) {
      await replanBtn.click();
      await sleep(2000);
    }

    await screenshot(page, '04-replan-panel.png', 'Replan panel with 3 AI-generated options');

    console.log('\n✅ All 4 screenshots captured successfully!');
    console.log(`📁 Output: ${SCREENSHOTS_DIR}\n`);

  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error('\n❌ Demo recording failed:', err);
  process.exit(1);
});
