# Domain Object Design

## Core Runtime Objects

| Object | Responsibility |
|---|---|
| `World` | Owns runtime state and advances the simulation. |
| `LittlePerson` | Runtime little-person entity. |
| `LittlePersonBehaviorState` | Current movement and emotional state. |
| `InteractionObject` | Runtime input object from mouse or UDP RealSense. |
| `InteractionField` | Reaction field generated from an input object. |
| `WalkableSurface` | Walkable rule generated from a `BarProp`. |
| `PropObstacle` | Blocking prop body generated from a `BarProp`. |
| `AmbientObject` | World-owned cloud or star. |
| `VisualEffectInstance` | Runtime visual effect such as rain or star burst. |
| `SensorFrame` | UDP input frame model. |

## Interaction Object

`InteractionObject` stores:

- id
- kind
- normalized position
- normalized size
- angle
- height
- state
- shape kind
- optional contour points

Contour points are normalized display coordinates. They are clamped to `0.0..1.0`.

## Interaction Field

`InteractionField.DistanceTo(point)` supports:

- primitive circle/box fallback,
- bar distance,
- contour polygon distance.

For contour hands, a point inside the contour has distance `0`. A point outside uses the shortest distance to the contour edge. Little people use `HandContourReactionPadding` for contour reaction distance.

## Little Person Movement

Current movement states include:

- `EdgeWalk`
- `TransferToSurface`
- `SurfaceWalk`
- `RideSurface`
- `Falling`

Little people usually live on the inset edge path. When a valid bar surface attach point is near enough and approachable from the walkable side, they transfer onto the surface.

## Walkable Surface

Current `BarProp` surfaces are generated from the visible bar rectangle.

- The path lies on the real long edge of the rectangle.
- The attach side is near the edge-side / far end.
- The path end is near the center-side tip.
- A tilted bar exposes only the screen-up side as walkable.
- A near-vertical bar can expose both sides.
- If another surface is close to the current tip, the little person transfers directly.
- If no nearby surface exists, the little person falls back to the display edge.

The debug surface line should overlap the visible bar lane. It is not a separate platform.

## Prop Obstacle

`PropObstacle` prevents edge-walking little people from passing through a non-walkable bar side.

It is an oriented rectangle matching the visible bar body with a small padding. It should not create rounded end caps extending beyond the visible tip.

## Ambient Objects

Clouds and stars are not input objects.

- Cloud touch keeps a `RainColumn` effect alive.
- Star touch triggers `StarBurst` with cooldown.
- Clouds have movement constraints so they do not drift through the normal edge-walk route too often.

Clouds can be touched by:

- little people, through domain ambient reaction checks,
- particles, through `WorldSpaceMaskAnimationController` calling `MarkCloudTouchedByExternalSource`.

## Visual Effects

Visual effects are domain instances rendered by Unity views.

- Rain uses `VisualEffectKind.RainColumn`.
- Stars use `VisualEffectKind.StarBurst`.
- Rain can feed the plant system through `WorldSpaceMaskAnimationController`.

## Mask / Particle / Plant Runtime

The particle and plant system is a Unity-side animation layer, not a core domain object.

It reads `World.InteractionObjects` and `World.VisualEffects`, then maintains:

- low-resolution mask buffer,
- particles,
- plants,
- flower burst state.

This layer may use mask-pixel coordinates internally while rendering through Unity world coordinates.
