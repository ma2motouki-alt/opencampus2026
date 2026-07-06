# Domain Object Design

Domain objects are runtime concepts with state and behavior. Master data is immutable definition data.

## Core Domain Objects

| Domain Object | Responsibility |
|---|---|
| `World` | Owns little people, input objects, fields, walkable surfaces, ambient objects, visual effects, and frame advance logic. |
| `LittlePerson` | Runtime individual with position, velocity, emotion, behavior state, target, and active reaction. |
| `InteractionObject` | Runtime hand or prop input from mouse or future RealSense. |
| `InteractionField` | Influence area created by an input object, such as hand repulsion or round-prop curiosity. |
| `WalkableSurface` | A walkable rule generated from an input object. MVP generates real rectangle-edge paths from a `BarProp`. |
| `PropObstacle` | A runtime blocking shape derived from an input prop. MVP uses the visible bar rectangle so edge walkers do not pass through a non-walkable side. |
| `AmbientObject` | World-owned object such as cloud or star. It is separate from input objects. |
| `ReactionInstance` | Temporary little-person reaction state. |
| `VisualEffectInstance` | Runtime visual effect such as rain or star burst. |
| `SensorFrame` | Future RealSense/UDP input frame. |

## Little Person Behavior

- `EdgeWalk`: default state. The little person walks only on the inset rectangular screen edge path.
- `TransferToSurface`: short transition from the screen edge to a nearby walkable prop surface, or from one prop surface tip to another nearby prop surface.
- `SurfaceWalk`: walks along a real prop edge using one-dimensional surface progress.
- `RideSurface`: keeps relative progress on the prop surface while the source prop is being dragged slowly.
- `Falling`: returns to the nearest edge after surface deletion, exit-point reach with no nearby surface connection, fast prop movement, or connection loss.

`ClimbBar` remains in the enum for compatibility, but new bar behavior should use `WalkableSurface`.

## Walkable Surface Model

- A `WalkableSurface` has `sourceObjectId`, `sourceKind`, `sourceState`, `sourceVelocity`, `start`, `end`, `width`, `sideIndex`, `attachProgress`, `exitProgress`, `attachPoint`, `pathEndPoint`, `exitPoint`, `physicalTipPoint`, and a walkable-side normal.
- MVP only generates surfaces for `BarProp`.
- For bar props, `start` and `end` are the two corners of a real long edge of the visible bar rectangle.
- The surface is the visible rectangle edge itself, not the bar center line and not a separate visual platform outside the bar.
- Bar rectangle corners are generated with the current display aspect so the domain surface stays parallel to the Unity-rendered bar.
- Little people attach at `attachPoint`, not at the closest point on the bar.
- Little people can attach only when they approach from the walkable side of the surface. A little person on the opposite side must not transfer through the prop.
- The walkable surface should be almost the same length as the visible bar. `attachProgress` may be slightly inset for a natural transfer, but `exitProgress` should normally be `1.0` so the little person reaches the path tip before exiting.
- `pathEndPoint` is the center-side corner on the walked long edge.
- `exitPoint` is the same as `pathEndPoint` for Milestone 2.
- `SurfaceWalk` advances from `attachProgress` to `exitProgress`; reaching `pathEndPoint` starts a short dwell.
- After the dwell, the little person first searches for a nearby different `WalkableSurface` whose closest point is within `SurfaceConnectionDistance`.
- A valid surface-to-surface connection must target a different `sourceObjectId`, must be `Placed`, and must pass the target surface's walkable-side check.
- If a valid nearby surface exists, `TransferToSurface` is reused to move from the current `pathEndPoint` to the closest valid point on the target surface.
- If no valid nearby surface exists, falling starts from the actual `exitPoint`. Do not clamp the falling start position to the normalized screen bounds, because prop movement or future lane tuning may legitimately place the exit point slightly outside `0.0..1.0`.
- Bar props that are close to vertical may generate both long edges. Tilted bar props generate only the screen-up long edge as walkable.
- The debug surface line should overlap the real long edge of the visible bar rectangle. It should not appear as a center line or as a separate light-blue platform outside the bar.
- Fast source movement causes falling from the current position. Slow dragging keeps the little person riding the same surface progress.
- Object glow is not part of this model. Debug view may show walkable paths and endpoints.

## Prop Obstacle Model

- A `PropObstacle` is not a walkable path. It represents the physical area that edge walkers should not pass through.
- MVP derives an oriented rectangle obstacle from each `BarProp` using the bar center segment as its length and the visible bar width plus padding as its blocking width.
- The obstacle does not have rounded end caps. Its blocking area stops at the visible bar tips so debug display and collision do not protrude beyond the cyan rectangle.
- During `EdgeWalk`, if a little person's next edge movement would enter or cross a bar obstacle, the world first tries to start a valid surface transfer.
- If transfer is not allowed because the little person is on the non-walkable side, the little person reverses `edgeDirection` and backs off slightly instead of walking through the bar.
- A short per-source cooldown prevents repeated same-frame or next-frame reversals from causing jitter.

## Ambient Reaction Model

- Clouds and stars are `AmbientObject`, not `InteractionObject`.
- Cloud touch keeps a `RainColumn` visual effect alive.
- Star touch creates a one-shot `StarBurst` visual effect and enters cooldown.
- Falling little people do not trigger ambient reactions.

## Visual Effect Rendering Model

- Domain creates `VisualEffectInstance` with kind, position, size, and lifetime.
- Unity renderers own visual details such as procedural particles, sprites, prefabs, and animators.
- MVP uses procedural `RainColumnEffectRenderer` and `StarBurstEffectRenderer`.
- Future production assets can use `VisualEffectMaster.RenderMode = Prefab` and `AssetKey`.
