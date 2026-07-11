# Experience Specification

## Goal

Create an interactive exhibit on a horizontal display. Visitors place or hover hands and objects over the screen. The work is not score-based; the experience is discovering how a living little world reacts.

## Current Experience

- Little people normally move along an inset path near the display edge.
- RealSense / Python detects hands and objects as depth contours.
- Unity receives normalized `InteractionObject` values over UDP.
- Contour objects are filled on screen so visitors can see their own hand or object shape reflected in the world.
- Particles react to the contour mask, rain, and plants.
- Three clouds drift as ambient world objects.
- One sun remains fixed near the upper-right area.
- A cloud touched by a little person or by small particles creates rain.
- Rain grows plants from the lower part of the screen.
- Particles climb plants and gather near flowers.
- Touching a flower with a hand contour bursts attached particles outward.

## Interaction Objects

### Hand

Hands are usually sent as:

```json
{
  "kind": "hand",
  "shape": "contour",
  "points": []
}
```

The contour is displayed as a filled mesh. Little people react to the contour shape, not only to a circular center point.

### Bar-Like Object

Slender contours can be classified as `bar_prop`. Unity still uses the same contour points for display when available, while the game logic also creates bar-derived walkable surfaces and obstacles.

Little people can:

- board a walkable real rectangle edge,
- walk toward the center-side tip,
- ride slow movement,
- transfer to a nearby bar surface, or
- fall back to the display edge.

### Round Prop

Round prop remains available for mouse testing and primitive UDP fallback.

## Ambient Objects

Clouds and stars are owned by the world, not by RealSense input.

- Clouds trigger `RainColumn`.
- Stars trigger `StarBurst`.
- Cloud movement is constrained to a central upper band so rain is less likely to start accidentally from normal edge walking.

## Visual / Animation Layer

The current visual layer contains:

- contour fill mesh on `InteractionObjectView`,
- optional low-resolution mask texture for debugging,
- particle motion in mask-pixel space rendered into Unity world space,
- rain-to-plant growth,
- plant climbing behavior,
- flower burst behavior.

## Non Goals

- No scoring, stages, or win/lose state.
- No full 3D hand reconstruction.
- No high-precision physics simulation.
- No external master data system yet.
- No final commercial-quality art pipeline yet.
