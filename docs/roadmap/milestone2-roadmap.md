# Milestone 2: Retired Bar Surface Prototype

## Status

Retired during the architecture refactor.

## Decision

The prototype that classified slender RealSense contours as `bar_prop` and generated walkable bar edges did not reach a reliable exhibition-quality interaction. It has been removed from the runtime.

Current rules:

- RealSense foreground contours are sent as `hand`.
- Mouse mode `3` is a temporary development mask stroke and has no walking behavior.
- `WalkableSurface` remains in the domain only for rainbow walking.
- `PropObstacle`, `ClimbBar`, riding, and bar-to-bar transfer are no longer supported.

## Regression Check

- Hand contour display and reaction still work.
- Mouse hand and round-prop modes still work.
- Rainbow generation, walking, touch drop, and expiry still work.
- Rain, plants, flowers, fairies, leaf hanging, and audio are unchanged.
