# System Design

## Runtime Overview

```text
Mouse / UDP RealSense input
  -> IInteractionInputProvider
  -> InteractionObject[]
  -> LittlePeopleWorldOrchestrator
    -> ApplyInteractionObjectsUseCase
    -> AdvanceWorldUseCase
  -> World
    -> InteractionField[]
    -> WalkableSurface[]
    -> PropObstacle[]
    -> LittlePerson[]
    -> AmbientObject[]
    -> VisualEffectInstance[]
  -> Unity Views
    -> InteractionObjectView
    -> LittlePersonView
    -> AmbientObjectView
    -> VisualEffectView
    -> WorldSpaceMaskAnimationController
```

## Input Modes

### Mouse

Mouse input is used for local development and fallback testing. It creates primitive hand, round, and bar objects.

### UDP RealSense

`UdpRealSenseInputProviderBehaviour` listens on UDP port `5005` by default. Python sends a frame-level JSON object containing current `InteractionObject` values.

The domain model does not know whether input came from mouse or RealSense.

## Python RealSense Runtime

```text
RealSense D435
  -> depth frame
  -> baseline depth subtraction
  -> height mask
  -> morphology open/close
  -> contour extraction
  -> contour simplification
  -> optional bar classification
  -> normalized display mapping
  -> UDP JSON
```

The current practical setup assumes a mostly top-down / front-view camera, so `MAPPER_MODE = "front"` is the default. Homography support remains available for future oblique placement.

## Derived Runtime Data

`World.SetInteractionObjects` derives runtime objects from the current input list every frame.

- `InteractionField[]`: reaction fields for hand, round prop, and primitive fallback.
- `WalkableSurface[]`: line or polyline rules generated from visible bar edges and active rainbow curves.
- `PropObstacle[]`: blocking prop bodies so edge walkers turn around instead of passing through a non-walkable side.

These are not sent over UDP. They are generated inside Unity.

## Coordinates

### Normalized Display Coordinates

Used by Python, UDP, domain objects, ambient objects, and fields.

```text
x: 0.0 left -> 1.0 right
y: 0.0 top  -> 1.0 bottom
```

### Unity World Coordinates

Used by views. `NormalizedScreenMapper` converts normalized coordinates to Unity world positions.

### Mask Pixel Coordinates

Used internally by `WorldSpaceMaskAnimationController`.

Default mask size:

```text
256 x 144
```

Particles and plants are simulated mainly in this mask-pixel coordinate space, then rendered through `MaskToWorld()`.

## Application Layer

- `CreateWorldUseCase`: creates a runtime `World`.
- `ApplyInteractionObjectsUseCase`: applies current input and rebuilds derived runtime data.
- `AdvanceWorldUseCase`: advances simulation for one frame.
- `LittlePeopleWorldOrchestrator`: coordinates the use cases.

Unity controllers own camera setup, GameObject creation, and view synchronization. Domain objects own behavior rules such as edge walking, prop blocking, surface walking, riding, falling, and ambient reactions.
