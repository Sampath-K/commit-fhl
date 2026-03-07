import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';

const stats = [
  { value: '100%', label: 'Commitments captured automatically', icon: '🤖', color: '#6366f1' },
  { value: '0', label: 'Dropped commitments this week', icon: '✓', color: '#22c55e' },
  { value: '6', label: 'People auto-notified on replan', icon: '📣', color: '#f97316' },
  { value: '3s', label: 'Time to approve an AI draft action', icon: '⚡', color: '#eab308' },
];

export const SceneStats: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const fadeIn = interpolate(frame, [0, 20], [0, 1], { extrapolateRight: 'clamp' });
  const fadeOut = interpolate(frame, [120, 145], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });
  const titleY = spring({ frame, fps, from: -20, to: 0, config: { stiffness: 100, damping: 16 } });

  return (
    <AbsoluteFill
      style={{
        background: 'linear-gradient(180deg, #0f172a 0%, #0d1a0d 100%)',
        opacity: fadeIn * fadeOut,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '0 60px',
      }}
    >
      {/* Ambient glow */}
      <div style={{
        position: 'absolute',
        width: 600,
        height: 600,
        borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(34,197,94,0.07) 0%, transparent 70%)',
        top: '50%', left: '50%',
        transform: 'translate(-50%, -50%)',
      }} />

      <div style={{ transform: `translateY(${titleY}px)`, textAlign: 'center', marginBottom: 48 }}>
        <p style={{ color: '#22c55e', fontSize: 13, fontWeight: 600, letterSpacing: 2, textTransform: 'uppercase', margin: '0 0 10px' }}>
          By the numbers
        </p>
        <h2 style={{ color: '#f1f5f9', fontSize: 32, fontWeight: 700, margin: 0 }}>
          Commit in action — one sprint
        </h2>
      </div>

      <div style={{
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 20,
        width: '100%',
        maxWidth: 780,
      }}>
        {stats.map((s, i) => {
          const delay = 20 + i * 18;
          const opacity = interpolate(frame, [delay, delay + 20], [0, 1], { extrapolateRight: 'clamp' });
          const scale = spring({
            frame: Math.max(0, frame - delay),
            fps,
            from: 0.85,
            to: 1,
            config: { stiffness: 120, damping: 18 },
          });

          return (
            <div key={i} style={{
              opacity,
              transform: `scale(${scale})`,
              background: `${s.color}0d`,
              border: `1px solid ${s.color}25`,
              borderRadius: 16,
              padding: '28px 24px',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              textAlign: 'center',
              gap: 10,
            }}>
              <div style={{
                width: 48, height: 48, borderRadius: 12,
                background: `${s.color}20`,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 22,
              }}>
                {s.icon}
              </div>
              <div style={{ color: s.color, fontSize: 44, fontWeight: 800, lineHeight: 1 }}>
                {s.value}
              </div>
              <div style={{ color: '#94a3b8', fontSize: 14, lineHeight: 1.4 }}>
                {s.label}
              </div>
            </div>
          );
        })}
      </div>
    </AbsoluteFill>
  );
};
