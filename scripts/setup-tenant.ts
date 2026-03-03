#!/usr/bin/env ts-node
/**
 * setup-tenant.ts — Fully automated, idempotent tenant setup for the Commit FHL demo.
 *
 * What this script does (every run is safe to repeat):
 *   1. Authenticates as a tenant admin via Azure CLI
 *   2. Creates 6 demo persona users in AAD (skips if already exist)
 *   3. Gets the real OIDs for all 6 users
 *   4. Flushes all previous demo data from the API (demo-* OIDs + any prior real-OID seeds)
 *   5. Re-seeds the full scenario with real OIDs
 *   6. Verifies health and prints a summary
 *
 * Prerequisites:
 *   - Azure CLI installed: `az --version`
 *   - Logged in as tenant admin: `az login --tenant <TENANT_ID>`
 *     (needs User.ReadWrite.All permission — typically a Global Admin or User Administrator)
 *   - API must be running: set API_BASE_URL below or in environment
 *
 * Usage:
 *   # Dry run — see what would be created, no changes made
 *   npx ts-node --project scripts/tsconfig.json scripts/setup-tenant.ts --dry-run
 *
 *   # Full setup (creates users + seeds data)
 *   npx ts-node --project scripts/tsconfig.json scripts/setup-tenant.ts
 *
 *   # Point at live Azure deployment
 *   API_BASE_URL=https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io \
 *     npx ts-node --project scripts/tsconfig.json scripts/setup-tenant.ts
 *
 *   # Reset: flush everything and re-seed from scratch
 *   npx ts-node --project scripts/tsconfig.json scripts/setup-tenant.ts --reset
 *
 * Environment variables:
 *   TENANT_ID         — AAD Tenant ID (default: 91b9767c-6b0a-4b0b-bd4d-e08a6383426c)
 *   TENANT_DOMAIN     — onmicrosoft.com domain (default: auto-detected from tenant metadata)
 *   API_BASE_URL      — Commit API base URL (default: http://localhost:5000)
 *   DEMO_PASSWORD     — Password for created demo users (default: Commit@FHL2026!)
 *
 * Decision: D-008 — Real-User Demo Tenant Setup
 */

import { execSync } from 'child_process';
import { PERSONAS } from './personas/index';
import { RESCHEDULE_SKILL_COMMITMENTS, RESCHEDULE_SKILL_EDGES } from './scenarios/rescheduleSkill';
import type { CommitmentRecord } from '../src/app/src/types/api';

// ─── Configuration ─────────────────────────────────────────────────────────
const DRY_RUN      = process.argv.includes('--dry-run');
const RESET_MODE   = process.argv.includes('--reset');
const TENANT_ID    = process.env['TENANT_ID']     ?? '91b9767c-6b0a-4b0b-bd4d-e08a6383426c';
const API_BASE     = process.env['API_BASE_URL']  ?? 'http://localhost:5000';
const DEMO_PASSWORD = process.env['DEMO_PASSWORD'] ?? 'Commit@FHL2026!';

// Demo OIDs — these are flushed before re-seeding with real OIDs
const DEMO_OIDS = PERSONAS.map(p => p.userId);

// ─── Persona → AAD user definition ─────────────────────────────────────────
interface AadUserSpec {
  personaId:   string;
  displayName: string;
  givenName:   string;
  surname:     string;
  mailNickname: string;
  jobTitle:    string;
  department:  string;
  demoOid:     string;    // the demo-* OID this replaces
  realOid?:    string;    // filled in after lookup/create
  upn?:        string;    // filled in after domain detection
}

const USER_SPECS: AadUserSpec[] = [
  { personaId: 'alex',   displayName: 'Alex Chen',        givenName: 'Alex',   surname: 'Chen',       mailNickname: 'alex.chen',    jobTitle: 'Senior Engineer',           department: 'Reschedule Crew',   demoOid: PERSONAS.find(p => p.id === 'alex')!.userId },
  { personaId: 'priya',  displayName: 'Priya Sharma',     givenName: 'Priya',  surname: 'Sharma',     mailNickname: 'priya.sharma',  jobTitle: 'Engineering Manager',       department: 'Reschedule Crew',   demoOid: PERSONAS.find(p => p.id === 'priya')!.userId },
  { personaId: 'marcus', displayName: 'Marcus Johnson',   givenName: 'Marcus', surname: 'Johnson',    mailNickname: 'marcus.johnson',jobTitle: 'Platform Engineer',         department: 'BizChat Platform',  demoOid: PERSONAS.find(p => p.id === 'marcus')!.userId },
  { personaId: 'fatima', displayName: 'Fatima Al-Rashid', givenName: 'Fatima', surname: 'Al-Rashid',  mailNickname: 'fatima.alrashid',jobTitle: 'Program Manager',          department: 'Reschedule Crew',   demoOid: PERSONAS.find(p => p.id === 'fatima')!.userId },
  { personaId: 'david',  displayName: 'David Park',       givenName: 'David',  surname: 'Park',       mailNickname: 'david.park',    jobTitle: 'Director',                 department: 'Reschedule Crew',   demoOid: PERSONAS.find(p => p.id === 'david')!.userId },
  { personaId: 'sarah',  displayName: "Sarah O'Brien",    givenName: 'Sarah',  surname: "O'Brien",    mailNickname: 'sarah.obrien',  jobTitle: 'Scheduling Skill Engineer', department: 'Scheduling Skill',  demoOid: PERSONAS.find(p => p.id === 'sarah')!.userId },
];

// ─── Step 1: Get admin token via Azure CLI ──────────────────────────────────
function getAdminToken(): string {
  console.log('  Getting admin token via Azure CLI...');
  try {
    const output = execSync(
      `az account get-access-token --resource https://graph.microsoft.com --tenant ${TENANT_ID} --output json`,
      { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }
    );
    const parsed = JSON.parse(output) as { accessToken: string; expiresOn: string };
    console.log(`  ✅ Admin token obtained (expires: ${parsed.expiresOn})`);
    return parsed.accessToken;
  } catch (err) {
    const msg = (err as Error).message;
    if (msg.includes('az: command not found') || msg.includes('not recognized')) {
      throw new Error('Azure CLI not found. Install from https://aka.ms/installazurecli');
    }
    if (msg.includes('AADSTS') || msg.includes('not logged')) {
      throw new Error(
        `Not logged in to Azure CLI as a tenant admin.\n` +
        `Run: az login --tenant ${TENANT_ID}\n` +
        `Then retry this script.`
      );
    }
    throw err;
  }
}

// ─── Step 2: Detect tenant domain ──────────────────────────────────────────
async function getTenantDomain(token: string): Promise<string> {
  if (process.env['TENANT_DOMAIN']) {
    console.log(`  Using TENANT_DOMAIN env var: ${process.env['TENANT_DOMAIN']}`);
    return process.env['TENANT_DOMAIN'];
  }
  const res = await graphGet<{ value: { id: string; verifiedDomains: { name: string; isDefault: boolean }[] }[] }>(
    token, 'v1.0/organization?$select=id,verifiedDomains'
  );
  const domains = res.value?.[0]?.verifiedDomains ?? [];
  const defaultDomain = domains.find((d: { name: string; isDefault: boolean }) => d.isDefault)?.name
    ?? domains[0]?.name;
  if (!defaultDomain) throw new Error('Could not detect tenant domain from organization metadata');
  console.log(`  ✅ Tenant domain detected: ${defaultDomain}`);
  return defaultDomain;
}

// ─── Graph API helpers ──────────────────────────────────────────────────────
async function graphGet<T>(token: string, path: string): Promise<T> {
  const res = await fetch(`https://graph.microsoft.com/${path}`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }
  });
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`Graph GET /${path} → ${res.status}: ${body.substring(0, 200)}`);
  }
  return res.json() as Promise<T>;
}

async function graphPost<T>(token: string, path: string, body: object): Promise<T> {
  const res = await fetch(`https://graph.microsoft.com/${path}`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Graph POST /${path} → ${res.status}: ${text.substring(0, 200)}`);
  }
  return res.json() as Promise<T>;
}

// ─── Step 3: Create or get each user ───────────────────────────────────────
async function ensureUser(token: string, spec: AadUserSpec): Promise<string> {
  // Check if user already exists
  try {
    const existing = await graphGet<{ id: string; displayName: string }>(
      token, `v1.0/users/${encodeURIComponent(spec.upn!)}`
    );
    console.log(`  ✅ EXISTS  ${spec.displayName.padEnd(22)} → ${existing.id} (${spec.upn})`);
    return existing.id;
  } catch {
    // 404 — user does not exist, create it
  }

  if (DRY_RUN) {
    const fakeOid = `dry-run-oid-${spec.personaId}`;
    console.log(`  🔵 DRY RUN  ${spec.displayName.padEnd(22)} → would create ${spec.upn}`);
    return fakeOid;
  }

  const created = await graphPost<{ id: string }>(token, 'v1.0/users', {
    accountEnabled:    true,
    displayName:       spec.displayName,
    givenName:         spec.givenName,
    surname:           spec.surname,
    mailNickname:      spec.mailNickname,
    userPrincipalName: spec.upn,
    jobTitle:          spec.jobTitle,
    department:        spec.department,
    usageLocation:     'US',
    passwordProfile: {
      forceChangePasswordNextSignIn: false,
      password: DEMO_PASSWORD,
    },
  });

  console.log(`  ✅ CREATED  ${spec.displayName.padEnd(22)} → ${created.id} (${spec.upn})`);
  return created.id;
}

// ─── Step 4: Flush existing data (demo-* OIDs + any prior real seeds) ──────
async function flushAll(allOids: string[]): Promise<void> {
  console.log(`\n${'─'.repeat(70)}`);
  console.log('STEP 4: Flush existing data');
  console.log(`  Flushing ${allOids.length} OIDs: demo-* seeds + any prior real-OID seeds`);

  for (const oid of allOids) {
    if (DRY_RUN) {
      console.log(`  🔵 DRY RUN  would DELETE /api/v1/users/${oid}/data`);
      continue;
    }
    try {
      const res = await fetch(`${API_BASE}/api/v1/users/${encodeURIComponent(oid)}/data`, {
        method: 'DELETE',
      });
      if (res.ok || res.status === 404) {
        console.log(`  ✅ Flushed  ${oid}`);
      } else {
        console.warn(`  ⚠️  Flush returned ${res.status} for ${oid} — continuing`);
      }
    } catch (err) {
      console.warn(`  ⚠️  Flush failed for ${oid}: ${(err as Error).message} — continuing`);
    }
  }
}

// ─── Step 5: Re-seed with real OIDs ────────────────────────────────────────
function remapCommitments(oidMap: Record<string, string>): CommitmentRecord[] {
  return RESCHEDULE_SKILL_COMMITMENTS.map(c => ({
    ...c,
    owner:    oidMap[c.owner]   ?? c.owner,
    watchers: c.watchers.map(w => oidMap[w] ?? w),
  }));
}

async function seedCommitments(commitments: CommitmentRecord[]): Promise<void> {
  console.log(`\n${'─'.repeat(70)}`);
  console.log(`STEP 5: Seed ${commitments.length} commitments with real OIDs`);

  let ok = 0, fail = 0;
  for (const c of commitments) {
    if (DRY_RUN) {
      process.stdout.write(`  🔵 DRY RUN  ${c.id} (owner: ${c.owner})\n`);
      ok++;
      continue;
    }
    try {
      const res = await fetch(`${API_BASE}/api/v1/commitments`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(c),
      });
      if (res.ok) {
        process.stdout.write(`  ✅ ${c.id}\n`);
        ok++;
      } else {
        const text = await res.text();
        console.warn(`  ✗ ${c.id}: HTTP ${res.status} — ${text.substring(0, 80)}`);
        fail++;
      }
    } catch (err) {
      console.warn(`  ✗ ${c.id}: ${(err as Error).message}`);
      fail++;
    }
  }
  console.log(`\n  Seeded ${ok}/${commitments.length} commitments${fail > 0 ? ` (${fail} failed)` : ''}`);
}

// ─── Step 6: Health check ───────────────────────────────────────────────────
async function verifyHealth(primaryOid: string): Promise<void> {
  console.log(`\n${'─'.repeat(70)}`);
  console.log('STEP 6: Verify');

  const checks: { label: string; url: string; expectField: string }[] = [
    { label: 'API health',          url: `${API_BASE}/api/v1/health`,                        expectField: 'status' },
    { label: 'Commitments for Alex', url: `${API_BASE}/api/v1/commitments/${primaryOid}`,     expectField: 'data' },
  ];

  for (const check of checks) {
    if (DRY_RUN) {
      console.log(`  🔵 DRY RUN  ${check.label}: ${check.url}`);
      continue;
    }
    try {
      const res  = await fetch(check.url);
      const body = await res.json() as Record<string, unknown>;
      if (res.ok && check.expectField in body) {
        const val = check.expectField === 'data'
          ? `${(body['data'] as unknown[]).length} commitments`
          : String(body[check.expectField]);
        console.log(`  ✅ ${check.label.padEnd(28)} ${val}`);
      } else {
        console.warn(`  ⚠️  ${check.label}: unexpected response (${res.status})`);
      }
    } catch (err) {
      console.warn(`  ⚠️  ${check.label}: ${(err as Error).message}`);
    }
  }
}

// ─── Main ───────────────────────────────────────────────────────────────────
async function setupTenant(): Promise<void> {
  console.log(`\n${'═'.repeat(70)}`);
  console.log(`  SETUP TENANT — Commit FHL Demo${DRY_RUN ? ' (DRY RUN)' : ''}${RESET_MODE ? ' (RESET)' : ''}`);
  console.log(`  Tenant: ${TENANT_ID}`);
  console.log(`  API:    ${API_BASE}`);
  console.log(`${'═'.repeat(70)}\n`);

  // ── Step 1: Admin token ────────────────────────────────────────────────────
  console.log('STEP 1: Authenticate');
  const token = DRY_RUN ? 'dry-run-token' : getAdminToken();

  // ── Step 2: Detect domain ──────────────────────────────────────────────────
  console.log('\nSTEP 2: Detect tenant domain');
  const domain = DRY_RUN ? 'dryrun.onmicrosoft.com' : await getTenantDomain(token);

  // Assign UPNs
  for (const spec of USER_SPECS) {
    spec.upn = `${spec.mailNickname}@${domain}`;
  }

  // ── Step 3: Create/verify users ────────────────────────────────────────────
  console.log('\nSTEP 3: Create or verify AAD users');
  console.log(`  Domain: ${domain}  |  Password: ${DEMO_PASSWORD}  |  Users: ${USER_SPECS.length}`);
  console.log(`  (Idempotent: skips creation if user already exists)\n`);

  const oidMap: Record<string, string> = {};  // demoOid → realOid
  for (const spec of USER_SPECS) {
    const realOid = await ensureUser(token, spec);
    spec.realOid  = realOid;
    oidMap[spec.demoOid] = realOid;
  }

  console.log('\n  OID MAPPING:');
  for (const spec of USER_SPECS) {
    console.log(`  ${spec.displayName.padEnd(22)} ${spec.demoOid.padEnd(26)} → ${spec.realOid}`);
  }

  // ── Step 4: Flush ─────────────────────────────────────────────────────────
  // Flush both old demo OIDs and any real OIDs from previous runs
  const allOidsToFlush = [
    ...DEMO_OIDS,
    ...USER_SPECS.map(s => s.realOid!).filter(o => !o.startsWith('dry-run')),
  ];
  await flushAll(allOidsToFlush);

  // ── Step 5: Re-seed ────────────────────────────────────────────────────────
  const realCommitments = remapCommitments(oidMap);
  await seedCommitments(realCommitments);

  // ── Step 6: Verify ────────────────────────────────────────────────────────
  const alexRealOid = USER_SPECS.find(s => s.personaId === 'alex')!.realOid!;
  await verifyHealth(alexRealOid);

  // ── Summary ────────────────────────────────────────────────────────────────
  console.log(`\n${'═'.repeat(70)}`);
  console.log('  SETUP COMPLETE ✅');
  console.log(`${'─'.repeat(70)}`);
  console.log('  NEXT STEPS:');
  console.log(`  1. Open Teams as:  ${USER_SPECS.find(s => s.personaId === 'alex')!.upn}  (pw: ${DEMO_PASSWORD})`);
  console.log(`  2. Install app:    Upload commit-fhl.zip to Teams admin catalog → pin for all users`);
  console.log(`  3. Admin consent:  portal.azure.com → Enterprise Apps → ${process.env['CLIENT_ID'] ?? '07b0afff...'} → Permissions → Grant consent`);
  console.log(`  4. Verify pane:    Open Commit tab in Teams — should show ${RESCHEDULE_SKILL_COMMITMENTS.length} commitments with real names`);
  console.log(`  5. Live arrival:   npx ts-node scripts/demo-live-arrival.ts  (injects new commitment after 60s)`);
  console.log(`\n  User accounts created in ${domain}:`);
  for (const spec of USER_SPECS) {
    console.log(`    ${spec.upn!.padEnd(45)} pw: ${DEMO_PASSWORD}`);
  }
  if (DRY_RUN) {
    console.log('\n  ⚠️  DRY RUN — no actual changes were made. Remove --dry-run to execute.');
  }
  console.log(`${'═'.repeat(70)}\n`);
}

setupTenant().catch(err => {
  console.error(`\n❌ SETUP FAILED: ${(err as Error).message}`);
  console.error('\nCommon fixes:');
  console.error('  • az login --tenant <TENANT_ID>  (if not logged in)');
  console.error('  • az account set --subscription <ID>  (if wrong subscription)');
  console.error('  • Add User.ReadWrite.All to the app registration (if permission error)');
  console.error('  • Start the API: cd src/api && dotnet run  (if API not running)');
  process.exit(1);
});
