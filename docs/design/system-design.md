# System Design

## MVP Runtime

```text
MouseInputProvider
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
  -> Unity 2D Views
```

## Future RealSense Runtime

```text
RealSense D435
  -> Python Vision App
  -> UDP JSON
  -> UdpRealSenseInputProvider
  -> InteractionObject[]
  -> LittlePeopleWorldOrchestrator
  -> World
  -> Unity 2D Views
```

Unity world logic must not depend on whether an `InteractionObject` came from mouse or RealSense.

## Derived Runtime Data

`World.SetInteractionObjects` derives runtime objects from the current input list.

- `InteractionField[]`: influence areas for hand, round prop, and legacy bar data.
- `WalkableSurface[]`: walkable prop rules. MVP creates paths from the real long edges of each `BarProp` rectangle instead of from the bar center line or separate outside platforms.
- `PropObstacle[]`: non-walkable prop bodies. MVP creates an oriented rectangle from the visible bar body so edge walkers reverse direction instead of passing through a non-walkable side.

These derived objects are rebuilt from input every frame and are not owned by input providers.

## Coordinate System

All input and domain coordinates use normalized display coordinates.

- Origin: top-left.
- X range: `0.0` left to `1.0` right.
- Y range: `0.0` top to `1.0` bottom.

View code converts normalized coordinates into Unity world coordinates.

## Application Layer

- `CreateWorldUseCase`: creates a runtime `World` from `MasterDatabase` and world preset id.
- `ApplyInteractionObjectsUseCase`: applies input objects and lets `World` rebuild fields and walkable surfaces.
- `AdvanceWorldUseCase`: advances domain simulation for one frame.
- `LittlePeopleWorldOrchestrator`: calls the use cases in frame order.

Unity controllers own camera setup, GameObject creation, and view synchronization. Domain objects own behavior rules such as edge walking, prop blocking, surface walking, riding, falling, and ambient reactions.
