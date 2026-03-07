import React from 'react';
import { AbsoluteFill, Img, interpolate, spring, useCurrentFrame, useVideoConfig, staticFile } from 'remotion';

export const ScenePriorityBoard: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const fadeIn = interpolate(frame, [0, 20], [0, 1], { extrapolateRight: 'clamp' });
  const fadeOut = interpolate(frame, [240, 265], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  const labelY = spring({ frame, fps, from: -20, to: 0, config: { stiffness: 100, damping: 16 } });

  const screenshotScale = spring({
    frame: Math.max(0, frame - 10),
    fps,
    from: 0.92,
    to: 1,
    config: { stiffness: 80, damping: 18 },
  });

  const calloutOpacity = interpolate(frame, [80, 110], [0, 1], { extrapolateRight: 'clamp' });
  const callout2Opacity = interpolate(frame, [130, 160], [0, 1], { extrapolateRight: 'clamp' });

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
          <p style={{ color: '#6366f1', fontSize: 12, fontWeight: 600, letterSpacing: 2, textTransform: 'uppercase', margin: '0 0 4px' }}>
            Live Demo
          </p>
          <h2 style={{ color: '#f1f5f9', fontSize: 24, fontWeight: 700, margin: 0 }}>
            Priority Board — surfaces commitments automatically
          </h2>
        </div>
        <div style={{ background: 'rgba(99,102,241,0.15)', border: '1px solid rgba(99,102,241,0.3)', borderRadius: 100, padding: '6px 16px' }}>
          <span style={{ color: '#a5b4fc', fontSize: 13 }}>Meeting · Chat · Email · ADO</span>
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
          src={staticFile('screenshots/01-priority-board.png')}
          style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top' }}
        />

        {/* Callout 1 — Urgent-Important quadrant */}
        <div style={{
          position: 'absolute',
          top: '18%',
          left: '3%',
          opacity: calloutOpacity,
          background: 'rgba(239,68,68,0.9)',
          borderRadius: 8,
          padding: '8px 14px',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          boxShadow: '0 4px 20px rgba(239,68,68,0.3)',
        }}>
          <span style={{ fontSize: 16 }}>🔥</span>
          <span style={{ color: 'white', fontSize: 13, fontWeight: 600 }}>4 urgent — need action today</span>
        </div>

        {/* Callout 2 — Impact score */}
        <div style={{
          position: 'absolute',
          top: '55%',
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
          <span style={{ fontSize: 16 }}>⚡</span>
          <span style={{ color: 'white', fontSize: 13, fontWeight: 600 }}>Impact score 92 — cross-team</span>
        </div>
      </div>
    </AbsoluteFill>
  );
};
