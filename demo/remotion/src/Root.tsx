import React from 'react';
import { Composition } from 'remotion';
import { CommitDemo } from './Video';

export const Root: React.FC = () => {
  return (
    <>
      {/*
       * CommitDemo — 60s at 30fps = 1800 frames
       * Resolution: 1280×720 (720p) — matches viewport in Playwright
       */}
      <Composition
        id="CommitDemo"
        component={CommitDemo}
        durationInFrames={1800}
        fps={30}
        width={1280}
        height={720}
      />
    </>
  );
};
