import React from 'react';
import { AbsoluteFill, Img, interpolate, spring, useCurrentFrame, useVideoConfig, staticFile } from 'remotion';

const options = [
  { id: 'A', label: 'Resolve Fast', color: '#22c55e', confidence: 82, desc: 'Focus Priya 2d on latency fix, drop 2 low-impact items' },
  { id: 'B', label: 'Parallel Work', color: '#6366f1', confidence: 71, desc: 'Omar proceeds with mock API contract, merge on latency fix' },
  { id: 'C', label: 'Clean Slip', color: '#f97316', confidence: 64, desc: 'Negotiate 3-day extension, auto-notify all 6 watchers' },
];

export const SceneReplan: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const fadeIn = interpolate(frame, [0, 20], [0, 1], { extrapolateRight: 'clamp' });
  const fadeOut = interpolate(frame, [180, 205], [1, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  const labelY = spring({ frame, fps, from: -20, to: 0, config: { stiffness: 100, damping: 16 } });

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
      }}>
        <p style={{ color: '#6366f1', fontSize: 12, fontWeight: 600, letterSpacing: 2, textTransform: 'uppercase', margin: '0 0 4px' }}>
          AI Replan
        </p>
        <h2 style={{ color: '#f1f5f9', fontSize: 24, fontWeight: 700, margin: 0 }}>
          3 options generated — one click to approve
        </h2>
      </div>

      {/* Two-column layout: screenshot left, options right */}
      <div style={{
        flex: 1,
        display: 'flex',
        gap: 24,
        padding: '0 48px 24px',
      }}>
        {/* Screenshot */}
        <div style={{
          flex: 1.4,
          borderRadius: 12,
          overflow: 'hidden',
          boxShadow: '0 20px 60px rgba(0,0,0,0.5)',
          border: '1px solid rgba(255,255,255,0.08)',
        }}>
          <Img
            src={staticFile('screenshots/04-replan-panel.png')}
            style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top' }}
          />
        </div>

        {/* Options */}
        <div style={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          gap: 14,
          justifyContent: 'center',
        }}>
          {options.map((opt, i) => {
            const delay = 25 + i * 22;
            const opacity = interpolate(frame, [delay, delay + 20], [0, 1], { extrapolateRight: 'clamp' });
            const x = spring({
              frame: Math.max(0, frame - delay),
              fps,
              from: 24,
              to: 0,
              config: { stiffness: 110, damping: 16 },
            });
            const isTop = i === 0;

            return (
              <div key={opt.id} style={{
                opacity,
                transform: `translateX(${x}px)`,
                background: isTop ? `${opt.color}12` : 'rgba(255,255,255,0.03)',
                border: `1px solid ${isTop ? opt.color + '40' : 'rgba(255,255,255,0.07)'}`,
                borderRadius: 12,
                padding: '16px 18px',
                position: 'relative',
                overflow: 'hidden',
              }}>
                {isTop && (
                  <div style={{
                    position: 'absolute', top: 8, right: 10,
                    background: opt.color, borderRadius: 100, padding: '2px 8px',
                    fontSize: 10, fontWeight: 700, color: 'white',
                  }}>
                    RECOMMENDED
                  </div>
                )}

                <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
                  <div style={{
                    width: 28, height: 28, borderRadius: 6,
                    background: opt.color,
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 13, fontWeight: 800, color: 'white',
                  }}>
                    {opt.id}
                  </div>
                  <span style={{ color: '#f1f5f9', fontWeight: 600, fontSize: 15 }}>{opt.label}</span>
                  <span style={{ marginLeft: 'auto', color: opt.color, fontSize: 13, fontWeight: 600 }}>
                    {opt.confidence}% confidence
                  </span>
                </div>
                <p style={{ color: '#94a3b8', fontSize: 13, margin: 0, lineHeight: 1.4 }}>{opt.desc}</p>
              </div>
            );
          })}

          {/* Approve nudge */}
          {interpolate(frame, [130, 150], [0, 1], { extrapolateRight: 'clamp' }) > 0.1 && (
            <div style={{
              opacity: interpolate(frame, [130, 150], [0, 1], { extrapolateRight: 'clamp' }),
              marginTop: 4,
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              color: '#64748b',
              fontSize: 13,
            }}>
              <span style={{ fontSize: 16 }}>👆</span>
              <span>Approve → agents send Teams messages to all watchers</span>
            </div>
          )}
        </div>
      </div>
    </AbsoluteFill>
  );
};
