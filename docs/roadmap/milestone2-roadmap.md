# Milestone 2: Bar Surface Rule Refinement

## Goal

棒状物体を、見た目と一致した歩行可能な物体として扱う。小人は画面縁から棒の実物長辺へ乗り、先端まで歩き、近くに別の棒があれば乗り移り、なければ落下する。

## Target Scope

- `WalkableSurface`
- `PropObstacle`
- `LittlePerson` の surface movement
- `InteractionObjectView` の bar display
- debug surface display

## Current Implementation

- Bar surfaces are generated from the visible rectangle long edges.
- `BarVisualScale = 4.32` is used to align domain geometry and visible bar sprite.
- Tilted bars expose only the screen-up side.
- Near-vertical bars can expose both sides.
- Non-walkable sides act as obstacles.
- Surface-to-surface transfer exists when another valid attach point is near the current path end.
- If no nearby surface exists, the little person falls back to the screen edge.

## Acceptance Check

- 小人が棒の中心線ではなく実物辺に沿って歩く。
- debug line が実物棒から離れて表示されない。
- 傾いた棒では片側からしか乗れない。
- 乗れない側では小人が棒を貫通せず反転する。
- 近い棒があれば地面に落ちずに乗り移る。
- 近い棒がなければ先端から落下する。
- 削除、ドラッグ、回転、RealSense入力、雲/星リアクションが壊れない。

## Handoff Notes

今後の調整では `WalkableSurfaceMaster` と `InteractionObjectView` の表示スケールを同時に確認すること。
