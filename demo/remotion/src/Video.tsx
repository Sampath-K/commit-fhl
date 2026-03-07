/**
 * CommitDemo — main composition
 *
 * Scene timeline (30fps):
 *   Scene 1 — Intro title          0   – 120  (4s)
 *   Scene 2 — Problem statement    120 – 300  (6s)
 *   Scene 3 — Priority board       300 – 570  (9s)
 *   Scene 4 — At-risk highlight    570 – 750  (6s)
 *   Scene 5 — Cascade view         750 – 990  (8s)
 *   Scene 6 — Replan panel         990 – 1200 (7s)
 *   Scene 7 — Progress tab         1200 – 1470 (9s)
 *   Scene 8 — Stats callout        1470 – 1620 (5s)
 *   Scene 9 — Outro / CTA          1620 – 1800 (6s)
 */

import React from 'react';
import { AbsoluteFill, Series } from 'remotion';
import { SceneIntro } from './components/SceneIntro';
import { SceneProblem } from './components/SceneProblem';
import { ScenePriorityBoard } from './components/ScenePriorityBoard';
import { SceneAtRisk } from './components/SceneAtRisk';
import { SceneCascade } from './components/SceneCascade';
import { SceneReplan } from './components/SceneReplan';
import { SceneProgress } from './components/SceneProgress';
import { SceneStats } from './components/SceneStats';
import { SceneOutro } from './components/SceneOutro';

export const CommitDemo: React.FC = () => {
  return (
    <AbsoluteFill style={{ backgroundColor: '#0f172a', fontFamily: "'Segoe UI', system-ui, sans-serif" }}>
      <Series>
        <Series.Sequence durationInFrames={120}>
          <SceneIntro />
        </Series.Sequence>

        <Series.Sequence durationInFrames={180}>
          <SceneProblem />
        </Series.Sequence>

        <Series.Sequence durationInFrames={270}>
          <ScenePriorityBoard />
        </Series.Sequence>

        <Series.Sequence durationInFrames={180}>
          <SceneAtRisk />
        </Series.Sequence>

        <Series.Sequence durationInFrames={240}>
          <SceneCascade />
        </Series.Sequence>

        <Series.Sequence durationInFrames={210}>
          <SceneReplan />
        </Series.Sequence>

        <Series.Sequence durationInFrames={270}>
          <SceneProgress />
        </Series.Sequence>

        <Series.Sequence durationInFrames={150}>
          <SceneStats />
        </Series.Sequence>

        <Series.Sequence durationInFrames={180}>
          <SceneOutro />
        </Series.Sequence>
      </Series>
    </AbsoluteFill>
  );
};
