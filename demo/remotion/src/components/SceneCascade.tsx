import React from 'react';
import { AbsoluteFill, Img, interpolate, spring, useCurrentFrame, useVideoConfig, staticFile } from 'remotion';

export const SceneCascade: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const fadeIn = interpolate(frame, [0, 20], [0, 1], { extrapolateRight: 'clamp' });
  const fadeOut = interpolate(frame, [210, 235], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  const labelY = spring({ frame, fps, from: -20, to: 0, config: { stiffness: 100, damping: 16 } });
  const screenshotScale = spring({
    frame: Math.max(0, frame - 8),
    fps,
    from: 0.93,
    to: 1,
    config: { stiffness: 80, damping: 18 },
  });

  const stat1Opacity = interpolate(frame, [70, 95], [0, 1], { extrapolateRight: 'clamp' });
  const stat2Opacity = interpolate(frame, [100, 125], [0, 1], { extrapolateRight: 'clamp' });
  const stat3Opacity = interpolate(frame, [130, 155], [0, 1], { extrapolateRight: 'clamp' });

  return (
    <AbsoluteFill
      style={{
        background: '#0f172a',
        opacity: fadeIn * fadeOut,
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {/* Top label */}
      <div style={{
        transform: `translateY(${labelY}px)`,
        padding: '24px 48px 16px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
      }}>
        <div>
          <p style={{ color: '#f97316', fontSize: 12, fontWeight: 600, letterSpacing: 2, textTransform: 'uppercase', margin: '0 0 4px' }}>
            Cascade Simulation
          </p>
          <h2 style={{ color: '#f1f5f9', fontSize: 24, fontWeight: 700, margin: 0 }}>
            What slips if the latency fix is late?
          </h2>
        </div>

        {/* Stat pills */}
        <div style={{ display: 'flex', gap: 10 }}>
          {[
            { label: '+3 slip days', opacity: stat1Opacity, color: '#ef4444' },
            { label: '6 people affected', opacity: stat2Opacity, color: '#f97316' },
            { label: 'Impact: 88 → 92', opacity: stat3Opacity, color: '#eab308' },
          ].map((s, i) => (
            <div key={i} style={{
              opacity: s.opacity,
              background: `${s.color}1a`,
              border: `1px solid ${s.color}40`,
              borderRadius: 100,
              padding: '6px 14px',
            }}>
              <span style={{ color: s.color, fontSize: 13, fontWeight: 600 }}>{s.label}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Screenshot */}
      <div style={{
        flex: 1,
        margin: '0 48px 24px',
        borderRadius: 12,
        overflow: 'hidden',
        transform: `scale(${screenshotScale})`,
        transformOrigin: 'top center',
        boxShadow: '0 20px 60px rgba(0,0,0,0.5)',
        border: '1px solid rgba(255,255,255,0.08)',
        position: 'relative',
      }}>
        <Img
          src={staticFile('screenshots/02-cascade-view.png')}
          style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top' }}
        />

        {/* Animated cascade annotation */}
        <div style={{
          position: 'absolute',
          bottom: '10%',
          left: '50%',
          transform: 'translateX(-50%)',
          opacity: stat3Opacity,
          background: 'rgba(0,0,0,0.8)',
          border: '1px solid rgba(239,68,68,0.4)',
          borderRadius: 10,
          padding: '10px 20px',
          textAlign: 'center',
        }}>
          <span style={{ color: '#fca5a5', fontSize: 14, fontWeight: 600 }}>
            BFS graph simulation · calendar pressure · 15-min polling
          </span>
        </div>
      </div>
    </AbsoluteFill>
  );
};
