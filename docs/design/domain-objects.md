# Domain Object Design

## Core Runtime Objects

| Object | Responsibility |
|---|---|
| `World` | Owns runtime state and advances the simulation. |
| `LittlePerson` | Runtime little-person entity. |
| `LittlePersonBehaviorState` | Current movement and emotional state. |
| `InteractionObject` | Runtime input object from mouse or UDP RealSense. |
| `InteractionField` | Reaction field generated from an input object. |
| `WalkableSurface` | Directed walking path generated from a rainbow. |
| `RainbowInstance` | Temporary curved world object created by bloom and distant-rain conditions. |
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
- contour polygon distance.

For contour hands, a point inside the contour has distance `0`. A point outside uses the shortest distance to the contour edge. Little people use `HandContourReactionPadding` for contour reaction distance.

## Little Person Movement

Current movement states include:

- `EdgeWalk`
- `TransferToSurface`
- `SurfaceWalk`
- `JumpToCloud`
- `TouchingCloud`
- `ReturnToRainbow`
- `Falling`

Little people usually live on the inset edge path. When an active rainbow foot is near enough, they transfer onto the rainbow path.

## Walkable Surface

Current surfaces are generated only from rainbow path points. Each rainbow creates one directed path in each direction. Little people attach at a foot and return to the display edge at the opposite foot.

While walking on an active rainbow, a little person can jump to a nearby cloud. It touches the cloud to start rain, then returns to the same directed rainbow surface. If that source rainbow expires before the return completes, the little person falls to the bottom ground edge.

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
