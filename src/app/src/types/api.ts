/**
 * Commit FHL — Frontend API Contract Types
 * @module types/api
 *
 * TypeScript shapes that mirror the C# backend DTOs.
 * Canvas imports from here. Never inline API shapes in components.
 * Forge must keep C# Models/ in sync with this file.
 */

// ─── Enumerations ─────────────────────────────────────────────────────────────

/** Source system where the commitment was made */
export type CommitmentSourceType = 'meeting' | 'chat' | 'email' | 'ado' | 'drive' | 'planner';

/** Eisenhower priority matrix quadrant */
export type EisenhowerQuadrant =
  | 'urgent-important'
  | 'not-urgent-important'
  | 'urgent-not-important'
  | 'not-urgent-not-important';

/** Lifecycle state of a commitment */
export type CommitmentStatus = 'pending' | 'in-progress' | 'done' | 'deferred' | 'delegated';

/** Dependency edge strength */
export type EdgeType = 'hard' | 'soft' | 'date' | 'capacity';

/** Replan strategy */
export type ReplanStrategy = 'resolve-fast' | 'parallel-work' | 'clean-slip';

// ─── Core Data Models ─────────────────────────────────────────────────────────

/** Source provenance of an extracted commitment. */
export interface CommitmentSource {
  type: CommitmentSourceType;
  /** Deep link back to the original message/meeting/email */
  url: string;
  /** When the commitment was made in the source system */
  timestamp: string; // ISO 8601 — serialized from DateTimeOffset
  /** Meeting ID, message ID, email ID, or PR ID */
  sourceId?: string;
}

/**
 * Primary data model — one commitment tracked by the system.
 * Matches C# CommitmentResponse DTO.
 */
export interface CommitmentRecord {
  id: string;
  title: string;
  owner: string;
  watchers: string[];
  source: CommitmentSource;
  committedAt: string;  // ISO 8601
  dueAt?: string;       // ISO 8601
  status: CommitmentStatus;
  priority: EisenhowerQuadrant;
  blockedBy: string[];
  blocks: string[];
  impactScore: number;
  burnoutContribution: number;
  lastActivity?: string; // ISO 8601
  agentDraft?: AgentDraft;
  ownerDeliveryScoreAtCreation?: number;
  /** Human-readable explanation of how the system detected completion. Shown as the "aha moment". */
  resolutionReason?: string;
  /** Project or team context inferred from source (e.g. "Q2 Planning", "Engineering", team name). */
  projectContext?: string;
  /** Artifact name within the project (e.g. "Q2 Roadmap.docx", "#general", email subject). */
  artifactName?: string;
}

/** Dependency edge between two commitments. */
export interface GraphEdge {
  fromId: string;
  toId: string;
  edgeType: EdgeType;
  confidence: number;
  detectedBy: 'conversation-thread' | 'overlapping-people' | 'nlp-similarity' | 'manual';
}

/** AI-generated draft awaiting one-click human approval. */
export interface AgentDraft {
  draftId: string;
  actionType: 'send-message' | 'create-calendar-event' | 'post-pr-comment' | 'send-email';
  content: string;
  contextSummary: string;
  recipients: string[];
  createdAt: string; // ISO 8601
  status: 'pending' | 'approved' | 'edited' | 'skipped';
  editedContent?: string;
}

// ─── Cascade Simulation ───────────────────────────────────────────────────────

/** Result of running the cascade simulation from a root task. */
export interface CascadeResult {
  rootTaskId: string;
  slipDays: number;
  affectedTasks: Record<string, number>;
  newEtas: Record<string, string>; // ISO 8601 dates
  totalImpactScore: number;
  peopleAffected: number;
  ownerCalendarPressure: number;
  simulatedAt: string; // ISO 8601
}

/** One of three replan options generated for a cascade. */
export interface ReplanOption {
  optionId: string;
  name: string;
  strategy: ReplanStrategy;
  confidence: number;
  newEtas: Record<string, string>; // ISO 8601 dates
  requiredComms: AgentDraft[];
  additionalEffortHours: number;
  risk: 'low' | 'medium' | 'high';
}

// ─── Capacity & Wellbeing ─────────────────────────────────────────────────────

/** Available time block. */
export interface TimeSlot {
  start: string;   // ISO 8601
  end: string;     // ISO 8601
  durationMinutes: number;
}

/** User capacity snapshot. */
export interface CapacitySnapshot {
  userId: string;
  loadIndex: number;
  burnoutTrend: number;
  freeSlots: TimeSlot[];
  snapshotAt: string; // ISO 8601
}

// ─── Psychology / Motivation Layer ───────────────────────────────────────────

/** User motivation state — drives the psychology UI layer. */
export interface UserMotivationState {
  userId: string;
  deliveryScore: number;
  deliveryScorePrevious: number;
  streakDays: number;
  totalXp: number;
  competencyLevel: 1 | 2 | 3 | 4 | 5;
  onTimeRate: number;
  cascadeHealthRate: number;
  triggersShownToday: number;
  lastStreakDate: string;
}

// ─── Backend API Response Shapes (Day 3 routes) ───────────────────────────────

/** Matches C# AffectedTask record returned by /graph/cascade */
export interface AffectedTask {
  taskId: string;
  title: string;
  cumulativeSlipDays: number;
  originalEta?: string;   // ISO 8601
  newEta?: string;        // ISO 8601
  calendarPressure: number;
}

/** Response shape of POST /api/v1/graph/cascade */
export interface CascadeApiResponse {
  rootTaskId: string;
  slipDays: number;
  impactScore: number;
  affectedCount: number;
  affectedTasks: AffectedTask[];
}

/** Matches C# ReplanOption record returned by /graph/replan */
export interface ReplanApiOption {
  optionId: string;          // "A" | "B" | "C"
  label: string;
  description: string;
  confidence: number;
  requiredActions: string[];
}

/** Approval decision payload for POST /api/v1/approvals */
export interface ApprovalDecision {
  draftId: string;
  commitmentId: string;
  decision: 'approve' | 'edit' | 'skip';
  editedContent?: string;
  /** Original draft text — sent to backend for Teams message dispatch */
  draftContent?: string;
  /** Draft action type — backend uses this to route side effects */
  draftActionType?: string;
  /** Recipient display names — backend resolves OIDs for Teams send */
  draftRecipients?: string[];
}

// ─── API Response Envelope ────────────────────────────────────────────────────

/** Standard API response envelope — all routes return this shape. */
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: {
    code: string;
    message: string;
  };
  requestId: string;
}

/** Health check response. */
export interface HealthResponse {
  status: 'ok' | 'degraded';
  user: string;
  graphConnected: boolean;
  storageConnected: boolean;
  timestamp: string; // ISO 8601
}
