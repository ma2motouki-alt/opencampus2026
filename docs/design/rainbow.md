# Rainbow Event Design

## Goal

Create a rare environmental event that rewards visitors for sustaining the plant and rain interactions without making the normal screen permanently busy.

## Trigger

A rainbow is created when all of the following are true:

- four or more plants are currently showing flowers,
- a cloud at least `0.50` normalized units from the fixed sun is raining,
- no rainbow is active,
- the rainbow cooldown is not active.

`Blooming` and `Wilting` plants count because both stages visibly show a flower.

The trigger is edge-based. Once it creates a rainbow, the condition is latched. The latch resets only after the visible bloom count drops below four or the distant cloud stops raining.

Development right-click rain does not satisfy the cloud condition.

## Lifetime

- Appear: `1.0 seconds`
- Total duration: `20.0 seconds`
- Fade: final `2.0 seconds`
- Cooldown after expiry: `30 seconds`
- Maximum active rainbows: `1`

No new little person can board during the appearing or fading states. If the rainbow expires while a little person is still walking on it, surface removal starts the existing falling behavior.

## Geometry

The rainbow is a quadratic Bezier curve represented by a 32-segment polyline.

- Horizontal span: `0.48`
- Rise from the bottom edge: `0.33`
- Center X: midpoint between the triggering cloud and the sun
- Feet: aligned to the normal inset edge path

The same normalized path points drive both `RainbowView` and the walkable surfaces, preventing visual/path drift.

## Walking

One rainbow creates two directed `WalkableSurface` values:

- left foot to right foot,
- right foot to left foot.

Both use `WalkableSurfaceShape.Polyline`. Little people attach at a foot, walk along the colored top band, and return to `EdgeWalk` at the opposite foot.

Rainbow surfaces use positive IDs in a reserved range. `-1` remains the domain sentinel for "no active surface", so a rainbow path is not lost immediately after boarding. Runtime interaction objects no longer generate bar-derived walkable surfaces or prop obstacles; the active surface system is dedicated to rainbow walking.

Defaults:

- Attach distance: `0.055`
- Walk speed: `0.13`
- Surface width: `0.018`
- Transfer duration: `0.22 seconds`
- Exit dwell: `0.20 seconds`
- Detach distance: `0.22`
- Reconnect cooldown: `1.10 seconds`

The sprite is positioned so its feet, rather than its center, sit on the path. Its body follows the local slope while remaining upright at the apex.

## Development Debug

Press `Y` during Play mode to create a rainbow immediately. This ignores flower, rain, and cooldown conditions, but still requires the world to contain a cloud and the fixed sun. Press `U` to force the nearest rainbow walker to jump to a cloud, ignoring only the distance condition. The debug overlay shows `Rainbows`, `Rainbow walkers`, and `Cloud jumpers` counts. Press `D` first if the overlay is hidden.

## Audio

Each successful rainbow creation increments `World.RainbowSpawnSequence`. `WorldAudioController` detects that increment and plays `Assets/Audio/SFX/虹.mp3` once through the shared one-shot AudioSource. Normal condition-based rainbows and the `Y` development rainbow use the same event.

## Touch And Falling

While a little person is transferring to or walking on a rainbow, hand fields are tested against its current normalized position.

- Contour hand: polygon distance with `TouchPadding = 0.02`
- Primitive hand fallback: existing field radius

A hit starts the existing curved fall from the current rainbow position. Falling ignores further touches until the person returns to the display edge.

## Cloud Jump

An active rainbow walker can reserve one nearby, non-raining cloud and jump to its lower contact point. Contact uses the existing ambient reaction path, so it starts the normal rain effect. After a short dwell, the little person follows a curved return trajectory to the closest valid point on the same directed rainbow path, never moving backward along that path.

Hand input does not interrupt `JumpToCloud`, `TouchingCloud`, or `ReturnToRainbow`. If the source rainbow is no longer available, the little person falls to the bottom ground edge instead of returning to an unrelated display edge.

## Ownership

- `RainbowMaster`: immutable trigger, geometry, lifetime, and movement values.
- `RainbowCloudJumpMaster`: immutable cloud jump and return tuning.
- `RainbowInstance`: runtime age, state, opacity, source cloud, and path points.
- `World`: trigger latch, cooldown, active rainbow list, and derived surfaces.
- `WorldSpaceMaskAnimationController`: exposes the current visible bloom count.
- `RainbowView`: draws seven procedural color bands.

The internal ambient `Star` kind is currently rendered as the fixed sun for compatibility with the existing ambient reaction model.

## Acceptance

- A rainbow does not appear with fewer than four visible flowers.
