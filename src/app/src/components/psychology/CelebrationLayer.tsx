import { useEffect, useState } from 'react';
import { animated, useSpring } from '@react-spring/web';
import { useReducedMotion } from '../../hooks/useReducedMotion';
import { ANIMATION_DURATIONS, SPRING_CONFIGS } from '../../config/psychology.config';

// ─── Types ────────────────────────────────────────────────────────────────────

export type CelebrationVariant = 'taskComplete' | 'firstWin' | 'levelUp' | 'streak' | 'dayWrap';

interface Particle {
  id:    number;
  x:     number;   // % horizontal center
  y:     number;   // % vertical center
  color: string;
  vx:    number;   // horizontal spread
  vy:    number;   // vertical launch
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const PALETTE = ['#0078d4', '#00b294', '#ffb900', '#d13438', '#8764b8', '#00bcf2'];

const VARIANT_COUNT: Record<CelebrationVariant, number> = {
  taskComplete: 12,
  firstWin:     20,
  levelUp:      40,
  streak:       25,
  dayWrap:      30,
};

function makeParticles(count: number): Particle[] {
  return Array.from({ length: count }, (_, i) => ({
    id:    i,
    x:     50 + (Math.random() - 0.5) * 20,
    y:     40,
    color: PALETTE[Math.floor(Math.random() * PALETTE.length)] ?? PALETTE[0],
    vx:    (Math.random() - 0.5) * 8,
    vy:    -(Math.random() * 4 + 2),
  }));
}

// ─── Component ────────────────────────────────────────────────────────────────

interface CelebrationLayerProps {
  variant: CelebrationVariant;
  onDone?: () => void;
}

export function CelebrationLayer({ variant, onDone }: CelebrationLayerProps): JSX.Element | null {
  const reduced   = useReducedMotion();
  const [particles] = useState<Particle[]>(() => makeParticles(VARIANT_COUNT[variant]));

  const duration = variant === 'levelUp'
    ? ANIMATION_DURATIONS.levelUp
    : ANIMATION_DURATIONS.celebration;

  // Animate fade-out after duration
  const { opacity } = useSpring({
    from:      { opacity: 1 },
    to:        { opacity: 0 },
    delay:     duration - 300,
    config:    SPRING_CONFIGS.gentle,
    immediate: reduced,
    onRest:    () => onDone?.(),
  });

  // When reduced motion: skip visual, just call onDone quickly
  useEffect(() => {
    if (!reduced) return;
    const id = window.setTimeout(() => onDone?.(), 50);
    return () => window.clearTimeout(id);
  }, [reduced, onDone]);

  // Render nothing for reduced-motion users (effect above calls onDone)
  if (reduced) return null;

  return (
    <animated.div
      aria-hidden="true"
      style={{
        opacity,
        position:      'fixed',
        inset:         0,
        pointerEvents: 'none',
        zIndex:        9999,
        overflow:      'hidden',
      }}>
      <svg width="100%" height="100%">
        {particles.map(p => (
          <circle
            key={p.id}
            cx={`${p.x + p.vx * 8}%`}
            cy={`${p.y + p.vy * 8}%`}
            r={4}
            fill={p.color}
            opacity={0.85}
          />
        ))}
      </svg>
    </animated.div>
  );
}

export default CelebrationLayer;
