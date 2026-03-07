import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';

export const SceneOutro: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const fadeIn = interpolate(frame, [0, 25], [0, 1], { extrapolateRight: 'clamp' });
  const logoScale = spring({ frame, fps, from: 0.8, to: 1, config: { stiffness: 80, damping: 14 } });
  const titleY = spring({ frame: Math.max(0, frame - 15), fps, from: 30, to: 0, config: { stiffness: 80, damping: 16 } });
  const subtitleOpacity = interpolate(frame, [35, 60], [0, 1], { extrapolateRight: 'clamp' });
  const ctaOpacity = interpolate(frame, [70, 95], [0, 1], { extrapolateRight: 'clamp' });
  const ctaScale = spring({ frame: Math.max(0, frame - 70), fps, from: 0.9, to: 1, config: { stiffness: 100, damping: 16 } });

  const linksOpacity = interpolate(frame, [110, 135], [0, 1], { extrapolateRight: 'clamp' });

  return (
    <AbsoluteFill
      style={{
        background: 'linear-gradient(135deg, #0f172a 0%, #1e3a5f 50%, #0f172a 100%)',
        opacity: fadeIn,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 0,
      }}
    >
      {/* Ambient ring */}
      <div style={{
        position: 'absolute',
        width: 700,
        height: 700,
        borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(99,102,241,0.12) 0%, transparent 70%)',
        top: '50%', left: '50%',
        transform: 'translate(-50%, -50%)',
      }} />

      {/* Logo */}
      <div style={{
        transform: `scale(${logoScale})`,
        width: 80,
        height: 80,
        borderRadius: 20,
        background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: 38,
        marginBottom: 24,
        boxShadow: '0 0 50px rgba(99,102,241,0.4)',
      }}>
        ✓
      </div>

      {/* Title */}
      <div style={{ transform: `translateY(${titleY}px)`, textAlign: 'center', marginBottom: 16 }}>
        <h1 style={{
          fontSize: 56,
          fontWeight: 800,
          color: '#f8fafc',
          margin: 0,
          letterSpacing: -2,
        }}>
          Commit
        </h1>
      </div>

      {/* Tagline */}
      <p style={{
        opacity: subtitleOpacity,
        fontSize: 20,
        color: '#94a3b8',
        margin: '0 0 48px',
        fontWeight: 400,
        textAlign: 'center',
        maxWidth: 480,
        lineHeight: 1.4,
      }}>
        Surface every commitment. Simulate every risk.<br />
        Ship with confidence.
      </p>

      {/* CTA */}
      <div style={{
        opacity: ctaOpacity,
        transform: `scale(${ctaScale})`,
        background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
        borderRadius: 100,
        padding: '14px 40px',
        cursor: 'default',
        boxShadow: '0 8px 32px rgba(99,102,241,0.4)',
        marginBottom: 32,
      }}>
        <span style={{ color: 'white', fontWeight: 700, fontSize: 18 }}>
          Try Commit in your Teams tenant →
        </span>
      </div>

      {/* Links */}
      <div style={{
        opacity: linksOpacity,
        display: 'flex',
        gap: 32,
        color: '#64748b',
        fontSize: 14,
      }}>
        <span>github.com/Sampath-K/commit-fhl</span>
        <span>·</span>
        <span>Microsoft Teams App</span>
        <span>·</span>
        <span>Azure Container Apps + Static Web Apps</span>
      </div>
    </AbsoluteFill>
  );
};
