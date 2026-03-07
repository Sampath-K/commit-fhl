/**
 * Commit FHL — API client functions
 * @module api/commitApi
 *
 * Centralised fetch helpers for all API calls that don't live inline in components.
 * All functions forward the authToken as an Authorization header when provided.
 */

import { API_BASE } from '../config/api.config';
import type { ApiResponse } from '../types/api';

// ─── Feedback ─────────────────────────────────────────────────────────────────

export type FeedbackType = 'Confirm' | 'FalsePositive' | 'WrongOwner' | 'Duplicate';

/**
 * Record thumbs-up / thumbs-down feedback for an extracted commitment.
 * POST /api/v1/commitments/{id}/feedback?userId=X
 */
export async function recordFeedback(
  userId: string,
  commitmentId: string,
  type: FeedbackType,
  authToken?: string
): Promise<void> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
  await fetch(
    `${API_BASE}/api/v1/commitments/${encodeURIComponent(commitmentId)}/feedback?userId=${encodeURIComponent(userId)}`,
    {
      method: 'POST',
      headers,
      body: JSON.stringify({ commitmentId, type }),
    }
  );
}

// ─── Rescan / Preview Extraction ──────────────────────────────────────────────

export interface RescanResult {
  newCount: number;
  updatedCount: number;
}

/**
 * Trigger a preview extraction scan for the given user.
 * POST /api/v1/extract/preview?userId=X
 */
export async function runRescan(
  userId: string,
  lookBackDays: number,
  sources: string[],
  authToken?: string
): Promise<RescanResult> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
  const res = await fetch(
    `${API_BASE}/api/v1/extract/preview?userId=${encodeURIComponent(userId)}`,
    {
      method: 'POST',
      headers,
      body: JSON.stringify({ lookBackDays, sources }),
    }
  );
  if (!res.ok) throw new Error(`Rescan failed: ${res.status}`);
  const body = (await res.json()) as ApiResponse<RescanResult>;
  return body.data ?? { newCount: 0, updatedCount: 0 };
}

// ─── Missed Task Reporter ──────────────────────────────────────────────────────

export interface MissedExtractionResult {
  found: boolean;
  taskTitle?: string;
}

/**
 * Report a missed task extraction.
 * POST /api/v1/missed-extraction?userId=X
 */
export async function reportMissedExtraction(
  userId: string,
  text: string,
  sourceType: string,
  authToken?: string
): Promise<MissedExtractionResult> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
  const res = await fetch(
    `${API_BASE}/api/v1/missed-extraction?userId=${encodeURIComponent(userId)}`,
    {
      method: 'POST',
      headers,
      body: JSON.stringify({ text, sourceType }),
    }
  );
  if (!res.ok) throw new Error(`Missed extraction report failed: ${res.status}`);
  const body = (await res.json()) as ApiResponse<MissedExtractionResult>;
  return body.data ?? { found: false };
}

// ─── Admin ─────────────────────────────────────────────────────────────────────

export interface AdminMetrics {
  totalCommitments: number;
  totalFeedback: number;
  avgConfidence: number;
  falsePositiveRate: number;
}

/**
 * Fetch admin KPI metrics.
 * GET /api/v1/admin/metrics
 */
export async function getAdminMetrics(authToken?: string): Promise<AdminMetrics> {
  const headers: Record<string, string> = {};
  if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
  const res = await fetch(`${API_BASE}/api/v1/admin/metrics`, { headers });
  if (!res.ok) throw new Error(`Admin metrics failed: ${res.status}`);
  const body = (await res.json()) as ApiResponse<AdminMetrics>;
  return body.data ?? { totalCommitments: 0, totalFeedback: 0, avgConfidence: 0, falsePositiveRate: 0 };
}

/**
 * Fetch AI-generated admin insights.
 * GET /api/v1/admin/insights
 */
export async function getAdminInsights(authToken?: string): Promise<{ insights: string }> {
  const headers: Record<string, string> = {};
  if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
  const res = await fetch(`${API_BASE}/api/v1/admin/insights`, { headers });
  if (!res.ok) throw new Error(`Admin insights failed: ${res.status}`);
  const body = (await res.json()) as ApiResponse<{ insights: string }>;
  return body.data ?? { insights: '' };
}
