/**
 * Psychology layer configuration.
 * All animation timing, spring physics, and threshold constants live here.
 * Canvas owns this file. Do not hardcode these values elsewhere.
 * @see .specify/memory/ux-psychology.md
 * @see Constitution P-27
 */

/** Spring physics configs for @react-spring/web */
export const SPRING_CONFIGS = {
  bounce:  { tension: 300, friction: 15 },   // badges, level-up
  smooth:  { tension: 200, friction: 25 },   // card transitions
  gentle:  { tension: 150, friction: 30 },   // digest reveal
  stiff:   { tension: 400, friction: 20 },   // overcommit shake
  wobbly:  { tension: 180, friction: 12 },   // confetti
} as const;

/** Stagger delays for cascade/list reveals (ms between each item) */
export const STAGGER_DELAYS = {
  digestCards:   50,   // Morning Digest card reveal
  cascadeItems:  40,   // Cascade chain item reveal
  statsCounters: 100,  // Day Wrap counter stagger
} as const;

/** Animation durations (ms) */
export const ANIMATION_DURATIONS = {
  microFeedback:  150,  // hover lift, button press
  stateChange:    300,  // task status change
  celebration:    600,  // task completion burst
  levelUp:       4000,  // full level-up ceremony
  dayWrap:       3000,  // end-of-day summary
} as const;

/** Delivery score display thresholds */
export const SCORE_THRESHOLDS = {
  low:    50,  // below 50: needs attention (blue)
  medium: 80,  // 50-79: making progress (blue-green)
  high:   90,  // 80-89: doing well (green)
             // 90+: excellent (green with shimmer)
} as const;

/** Streak milestone days for special celebrations */
export const STREAK_MILESTONES = [3, 7, 14, 30] as const;

/** XP awarded per event (from ux-psychology.md) */
export const XP_EVENTS = {
  resolvedOnTime:      5,
  resolvedEarly:       8,
  cascadePrevented:   15,
  agentApproved:       2,
  resolvedAfterSlip:   1,
  overcommitAccepted: -3,
  streakMilestone:    25,
} as const;

/** Maximum psychology triggers per user per day (P-27) */
export const MAX_TRIGGERS_PER_DAY = 3;

/** Minimum pool for variable rewards + session history to prevent repeats */
export const VARIABLE_REWARD_MIN_POOL = 20;
export const VARIABLE_REWARD_HISTORY_LENGTH = 5;
