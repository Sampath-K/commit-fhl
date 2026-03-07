import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';

const atRiskItems = [
  { title: 'BizChat Skill GA Package', owner: 'Priya Kumar', due: 'Mar 10', impact: 92, status: 'In Progress' },
  { title: 'Fix p99 Latency Regression', owner: 'Priya Kumar', due: 'Mar 7', impact: 88, status: 'Pending' },
  { title: 'API Contract → SDK Team', owner: 'Priya Kumar', due: 'Mar 6', impact: 79, status: 'Pending' },
];

export const SceneAtRisk: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const fadeIn = interpolate(frame, [0, 15], [0, 1], { extrapolateRight: 'clamp' });
  const fadeOut = interpolate(frame, [155, 175], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  const titleY = spring({ frame, fps, from: -20, to: 0, config: { stiffness: 100, damping: 16 } });

  return (
    <AbsoluteFill
      style={{
        background: 'linear-gradient(180deg, #0f172a 0%, #1f0a0a 100%)',
        opacity: fadeIn * fadeOut,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '0 80px',
      }}
    >
      {/* Pulsing warning ring */}
      <div style={{
        position: 'absolute',
        width: 500,
        height: 500,
        borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(239,68,68,0.08) 0%, transparent 70%)',
        top: '50%', left: '50%',
        transform: `translate(-50%, -50%) scale(${1 + Math.sin(frame / 15) * 0.03})`,
      }} />

      <div style={{ transform: `translateY(${titleY}px)`, textAlign: 'center', marginBottom: 40 }}>
        <p style={{ color: '#ef4444', fontSize: 13, fontWeight: 600, letterSpacing: 2, textTransform: 'uppercase', margin: '0 0 10px' }}>
          ⚠ At Risk
        </p>
        <h2 style={{ color: '#f1f5f9', fontSize: 32, fontWeight: 700, margin: 0, lineHeight: 1.2 }}>
          3 commitments need attention now
        </h2>
        <p style={{ color: '#94a3b8', fontSize: 16, margin: '10px 0 0' }}>
          One latency fix is blocking the entire GA chain
        </p>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 14, width: '100%', maxWidth: 680 }}>
        {atRiskItems.map((item, i) => {
          const delay = 20 + i * 20;
          const opacity = interpolate(frame, [delay, delay + 18], [0, 1], { extrapolateRight: 'clamp' });
          const x = spring({
            frame: Math.max(0, frame - delay),
            fps,
            from: 30,
            to: 0,
            config: { stiffness: 120, damping: 18 },
          });
          const pulse = i === 1 ? 0.5 + 0.5 * Math.sin(frame / 12) : 0;

          return (
            <div key={i} style={{
              opacity,
              transform: `translateX(${x}px)`,
              display: 'flex',
              alignItems: 'center',
              gap: 16,
              background: 'rgba(239,68,68,0.08)',
              border: `1px solid rgba(239,68,68,${0.2 + pulse * 0.3})`,
              borderRadius: 12,
              padding: '16px 20px',
              boxShadow: i === 1 ? `0 0 ${12 + pulse * 8}px rgba(239,68,68,0.2)` : 'none',
            }}>
              {/* Impact pill */}
              <div style={{
                background: item.impact >= 85 ? '#ef4444' : '#f97316',
                borderRadius: 6,
                padding: '4px 10px',
                minWidth: 44,
                textAlign: 'center',
              }}>
                <span style={{ color: 'white', fontWeight: 700, fontSize: 14 }}>{item.impact}</span>
              </div>

              <div style={{ flex: 1 }}>
                <div style={{ color: '#f1f5f9', fontWeight: 600, fontSize: 15 }}>{item.title}</div>
                <div style={{ color: '#94a3b8', fontSize: 13, marginTop: 2 }}>{item.owner}</div>
              </div>

              <div style={{ textAlign: 'right' }}>
                <div style={{
                  background: item.status === 'Pending' ? 'rgba(239,68,68,0.15)' : 'rgba(234,179,8,0.15)',
                  color: item.status === 'Pending' ? '#fca5a5' : '#fde047',
                  borderRadius: 100,
                  padding: '3px 10px',
                  fontSize: 12,
                  fontWeight: 500,
                }}>
                  {item.status}
                </div>
                <div style={{ color: '#64748b', fontSize: 12, marginTop: 4 }}>Due {item.due}</div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Consequence callout */}
      {interpolate(frame, [110, 130], [0, 1], { extrapolateRight: 'clamp' }) > 0.1 && (
        <div style={{
          opacity: interpolate(frame, [110, 130], [0, 1], { extrapolateRight: 'clamp' }),
          marginTop: 32,
          color: '#fca5a5',
          fontSize: 15,
          textAlign: 'center',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
        }}>
          <span>↓</span>
          <span>Latency fix delayed → GA slips → 6 people blocked → leadership impact</span>
        </div>
      )}
    </AbsoluteFill>
  );
};
