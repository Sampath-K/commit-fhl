import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';

const problems = [
  { icon: '💬', text: 'You promised a deliverable in a meeting' },
  { icon: '📧', text: 'You said "I\'ll get back to you" on email' },
  { icon: '📋', text: 'ADO tasks pile up across 3 projects' },
  { icon: '😰', text: 'One slip cascades to 6 downstream owners' },
];

export const SceneProblem: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const headerOpacity = interpolate(frame, [0, 20], [0, 1], { extrapolateRight: 'clamp' });
  const fadeOut = interpolate(frame, [150, 175], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  return (
    <AbsoluteFill
      style={{
        background: 'linear-gradient(180deg, #0f172a 0%, #1a1a2e 100%)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '0 80px',
        opacity: fadeOut,
      }}
    >
      {/* Header */}
      <div style={{ opacity: headerOpacity, marginBottom: 48, textAlign: 'center' }}>
        <p style={{ color: '#6366f1', fontSize: 14, fontWeight: 600, letterSpacing: 2, textTransform: 'uppercase', margin: '0 0 12px' }}>
          The Problem
        </p>
        <h2 style={{ color: '#f1f5f9', fontSize: 36, fontWeight: 700, margin: 0, lineHeight: 1.2 }}>
          Commitments live everywhere.<br />
          <span style={{ color: '#94a3b8', fontWeight: 400 }}>Nothing connects them.</span>
        </h2>
      </div>

      {/* Problem cards */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16, width: '100%', maxWidth: 640 }}>
        {problems.map((p, i) => {
          const delay = 20 + i * 18;
          const cardOpacity = interpolate(frame, [delay, delay + 20], [0, 1], { extrapolateRight: 'clamp' });
          const cardX = spring({
            frame: Math.max(0, frame - delay),
            fps,
            from: -30,
            to: 0,
            config: { stiffness: 120, damping: 18 },
          });

          return (
            <div key={i} style={{
              opacity: cardOpacity,
              transform: `translateX(${cardX}px)`,
              display: 'flex',
              alignItems: 'center',
              gap: 20,
              background: 'rgba(255,255,255,0.04)',
              border: '1px solid rgba(255,255,255,0.08)',
              borderRadius: 12,
              padding: '18px 24px',
            }}>
              <span style={{ fontSize: 28 }}>{p.icon}</span>
              <span style={{ color: '#cbd5e1', fontSize: 18, fontWeight: 400 }}>{p.text}</span>
            </div>
          );
        })}
      </div>

      {/* Bottom hook */}
      {interpolate(frame, [100, 120], [0, 1], { extrapolateRight: 'clamp' }) > 0.1 && (
        <div style={{
          opacity: interpolate(frame, [100, 120], [0, 1], { extrapolateRight: 'clamp' }),
          marginTop: 36,
          background: 'linear-gradient(90deg, rgba(99,102,241,0.15), rgba(139,92,246,0.15))',
          border: '1px solid rgba(99,102,241,0.3)',
          borderRadius: 100,
          padding: '10px 28px',
        }}>
          <span style={{ color: '#a5b4fc', fontSize: 16, fontWeight: 500 }}>
            Commit surfaces them all — automatically.
          </span>
        </div>
      )}
    </AbsoluteFill>
  );
};
