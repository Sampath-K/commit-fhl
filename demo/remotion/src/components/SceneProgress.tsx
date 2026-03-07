import React from 'react';
import { AbsoluteFill, Img, interpolate, spring, useCurrentFrame, useVideoConfig, staticFile } from 'remotion';

export const SceneProgress: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const fadeIn = interpolate(frame, [0, 20], [0, 1], { extrapolateRight: 'clamp' });
  const fadeOut = interpolate(frame, [240, 265], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  const labelY = spring({ frame, fps, from: -20, to: 0, config: { stiffness: 100, damping: 16 } });
  const screenshotScale = spring({
    frame: Math.max(0, frame - 8),
    fps,
    from: 0.93,
    to: 1,
    config: { stiffness: 80, damping: 18 },
  });

  const callout1Opacity = interpolate(frame, [80, 105], [0, 1], { extrapolateRight: 'clamp' });
  const callout2Opacity = interpolate(frame, [115, 140], [0, 1], { extrapolateRight: 'clamp' });
  const callout3Opacity = interpolate(frame, [150, 175], [0, 1], { extrapolateRight: 'clamp' });

  // Animate the delivery score counter
  const displayScore = Math.round(interpolate(frame, [30, 90], [0, 87], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' }));

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
          <p style={{ color: '#22c55e', fontSize: 12, fontWeight: 600, letterSpacing: 2, textTransform: 'uppercase', margin: '0 0 4px' }}>
            Progress Tab
          </p>
          <h2 style={{ color: '#f1f5f9', fontSize: 24, fontWeight: 700, margin: 0 }}>
            Completions + motivation — you're shipping work
          </h2>
        </div>

        {/* Animated delivery score */}
        <div style={{
          background: 'rgba(34,197,94,0.1)',
          border: '1px solid rgba(34,197,94,0.3)',
          borderRadius: 12,
          padding: '10px 20px',
          textAlign: 'center',
        }}>
          <div style={{ color: '#86efac', fontSize: 11, fontWeight: 600, letterSpacing: 1, textTransform: 'uppercase' }}>
            Delivery Score
          </div>
          <div style={{ color: '#22c55e', fontSize: 36, fontWeight: 800, lineHeight: 1 }}>
            {displayScore}
          </div>
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
          src={staticFile('screenshots/03-progress-tab.png')}
          style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top' }}
        />

        {/* Callout 1 — streak */}
        <div style={{
          position: 'absolute',
          top: '8%',
          left: '3%',
          opacity: callout1Opacity,
          background: 'rgba(234,179,8,0.9)',
          borderRadius: 8,
          padding: '8px 14px',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          boxShadow: '0 4px 20px rgba(234,179,8,0.3)',
        }}>
          <span style={{ fontSize: 16 }}>🔥</span>
          <span style={{ color: '#1c1917', fontSize: 13, fontWeight: 700 }}>5-day streak — keep going!</span>
        </div>

        {/* Callout 2 — merged PRs */}
        <div style={{
          position: 'absolute',
          top: '45%',
          right: '3%',
          opacity: callout2Opacity,
          background: 'rgba(99,102,241,0.9)',
          borderRadius: 8,
          padding: '8px 14px',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          boxShadow: '0 4px 20px rgba(99,102,241,0.3)',
        }}>
          <span style={{ fontSize: 16 }}>✅</span>
          <span style={{ color: 'white', fontSize: 13, fontWeight: 600 }}>2 PRs merged this week</span>
        </div>

        {/* Callout 3 — Level */}
        <div style={{
          position: 'absolute',
          bottom: '10%',
          left: '50%',
          transform: 'translateX(-50%)',
          opacity: callout3Opacity,
          background: 'rgba(0,0,0,0.85)',
          border: '1px solid rgba(139,92,246,0.4)',
          borderRadius: 10,
          padding: '10px 20px',
          display: 'flex',
          alignItems: 'center',
          gap: 12,
        }}>
          <span style={{ fontSize: 20 }}>🏅</span>
          <span style={{ color: '#c4b5fd', fontSize: 14, fontWeight: 600 }}>
            Level 3 — Consistent Contributor · 187 XP to Level 4
          </span>
        </div>
      </div>
    </AbsoluteFill>
  );
};
