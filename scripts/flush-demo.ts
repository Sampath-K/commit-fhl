#!/usr/bin/env ts-node
/**
 * Flush Demo — removes all seed data without touching real user data.
 * Safe: only deletes records with partitionKey starting with 'demo-'
 * Usage: npx ts-node scripts/flush-demo.ts
 */
const API_BASE = process.env.API_BASE_URL ?? 'http://localhost:3000';

async function flushDemo(): Promise<void> {
  console.log('\nFlushing demo data...');
  const res = await fetch(`${API_BASE}/api/v1/admin/flush-demo`, { method: 'DELETE' });
  if (res.ok) {
    const data = await res.json() as { deleted: number };
    console.log(`Removed ${data.deleted} demo records`);
  } else {
    console.error(`Flush failed: ${res.status}`);
    process.exit(1);
  }
}

flushDemo().catch(console.error);
