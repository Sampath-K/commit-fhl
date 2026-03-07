# Commit FHL — Demo Video Pipeline

Two-step pipeline: **Playwright** captures live app screenshots → **Remotion** stitches them into a 60-second video.

## Architecture

```
demo/
├── playwright/          # Step 1 — Screenshot capture
│   ├── record-demo.ts   # Seeds data + captures 4 screenshots
│   ├── package.json
│   └── tsconfig.json
│
└── remotion/            # Step 2 — Video composition
    ├── src/
    │   ├── index.ts         # Remotion entry point
    │   ├── Root.tsx         # Composition registration
    │   ├── Video.tsx        # Timeline (9 scenes × 30fps)
    │   └── components/      # 9 scene components
    │       ├── SceneIntro.tsx
    │       ├── SceneProblem.tsx
    │       ├── ScenePriorityBoard.tsx
    │       ├── SceneAtRisk.tsx
    │       ├── SceneCascade.tsx
    │       ├── SceneReplan.tsx
    │       ├── SceneProgress.tsx
    │       ├── SceneStats.tsx
    │       └── SceneOutro.tsx
    ├── public/
    │   └── screenshots/     # Output from Playwright (auto-created)
    ├── out/                 # Rendered video output
    ├── package.json
    └── tsconfig.json
```

## Scene Timeline (60s · 30fps · 1280×720)

| # | Scene | Frames | Duration |
|---|-------|--------|----------|
| 1 | Intro title | 0–120 | 4s |
| 2 | Problem statement | 120–300 | 6s |
| 3 | Priority Board (screenshot) | 300–570 | 9s |
| 4 | At-risk highlight | 570–750 | 6s |
| 5 | Cascade view (screenshot) | 750–990 | 8s |
| 6 | Replan panel (screenshot) | 990–1200 | 7s |
| 7 | Progress tab (screenshot) | 1200–1470 | 9s |
| 8 | Stats callout | 1470–1620 | 5s |
| 9 | Outro + CTA | 1620–1800 | 6s |

## Step 1 — Capture screenshots

```bash
cd demo/playwright
npm install
npx playwright install chromium
npx ts-node --project tsconfig.json record-demo.ts
```

This will:
1. Warm up the Azure Container App API (cold start ~15s)
2. Seed 9 demo commitments (6 active + 3 completions) for the fallback user
3. Open the live app in headless Chromium (1280×720 @2x)
4. Navigate through Priority Board → Cascade → Progress → Replan
5. Save 4 screenshots to `remotion/public/screenshots/`

Screenshots produced:
- `01-priority-board.png`
- `02-cascade-view.png`
- `03-progress-tab.png`
- `04-replan-panel.png`

## Step 2 — Compose and render video

```bash
cd demo/remotion
npm install

# Open Remotion Studio (live preview in browser)
npm run dev

# Render final MP4
npm run render
# Output: out/commit-demo.mp4

# Or render as GIF (smaller, shareable inline)
npm run render:gif
# Output: out/commit-demo.gif
```

## Tips

- **Missing screenshots?** Run Step 1 first. Scenes 3/5/6/7 show a fallback dark background if screenshots are absent — they still look decent for demo purposes.
- **Cold start warning:** The first `record-demo.ts` run after the Container App scales to zero will wait ~15s for API warmup. Subsequent runs are instant.
- **Custom scenarios:** Edit `DEMO_COMMITMENTS` in `record-demo.ts` to seed different data.
- **Scene timing:** Adjust `durationInFrames` in `Video.tsx` to change pacing.
