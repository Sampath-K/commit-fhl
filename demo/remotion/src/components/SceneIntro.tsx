import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';

export const SceneIntro: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const titleOpacity = interpolate(frame, [0, 20], [0, 1], { extrapolateRight: 'clamp' });
  const titleY = spring({ frame, fps, from: 40, to: 0, config: { stiffness: 80, damping: 15 } });

  const subtitleOpacity = interpolate(frame, [25, 50], [0, 1], { extrapolateRight: 'clamp' });
  const subtitleY = spring({ frame: Math.max(0, frame - 25), fps, from: 20, to: 0, config: { stiffness: 80, damping: 15 } });

  const badgeOpacity = interpolate(frame, [55, 80], [0, 1], { extrapolateRight: 'clamp' });

  const fadeOut = interpolate(frame, [90, 115], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  return (
    <AbsoluteFill
      style={{
        background: 'linear-gradient(135deg, #0f172a 0%, #1e3a5f 50%, #0f172a 100%)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        opacity: fadeOut,
      }}
    >
      {/* Ambient glow */}
      <div style={{
        position: 'absolute',
        width: 600,
        height: 600,
        borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(99,102,241,0.15) 0%, transparent 70%)',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
      }} />

      {/* Logo mark */}
      <div style={{
        width: 72,
        height: 72,
        borderRadius: 18,
        background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: 36,
        marginBottom: 28,
        boxShadow: '0 0 40px rgba(99,102,241,0.4)',
        opacity: badgeOpacity,
      }}>
        ✓
      </div>

      {/* Title */}
      <div style={{
        opacity: titleOpacity,
        transform: `translateY(${titleY}px)`,
        textAlign: 'center',
      }}>
        <h1 style={{
          fontSize: 64,
          fontWeight: 800,
          color: '#f8fafc',
          margin: 0,
          letterSpacing: -2,
          lineHeight: 1.1,
        }}>
          Commit
        </h1>
      </div>

      {/* Subtitle */}
      <div style={{
        opacity: subtitleOpacity,
        transform: `translateY(${subtitleY}px)`,
        textAlign: 'center',
        marginTop: 16,
      }}>
        <p style={{
          fontSize: 22,
          color: '#94a3b8',
          margin: 0,
          fontWeight: 400,
          letterSpacing: 0.5,
        }}>
          Your commitments. In one place. Never dropped.
        </p>
      </div>

      {/* Teams badge */}
      <div style={{
        opacity: badgeOpacity,
        marginTop: 40,
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        background: 'rgba(255,255,255,0.06)',
        border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: 100,
        padding: '8px 20px',
      }}>
        <div style={{
          width: 20,
          height: 20,
          borderRadius: 4,
          background: '#6264a7',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 12, fontWeight: 700, color: 'white',
        }}>T</div>
        <span style={{ color: '#94a3b8', fontSize: 14 }}>Microsoft Teams App</span>
      </div>
    </AbsoluteFill>
  );
};
