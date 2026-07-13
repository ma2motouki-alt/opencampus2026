# Architecture Refactor

## Goal

Reduce legacy code and make each feature easier to understand without changing the accepted exhibition experience.

## Accepted Baseline

- RealSense hand contour display
- little-person hand stop / look reaction
- rain, plants, flowers, and fairies
- leaf hanging and drop
- rainbow creation, walking, touch drop, and expiry
- audio playback

## Retired Feature

Slender-object classification and bar-derived walking have been removed. They were disconnected from `World.SetInteractionObjects` before this refactor and did not provide a reliable experience.

Compatibility rules:

- Python sends all accepted contours as `hand`.
- Unity treats legacy `bar`, `stick`, and `bar_prop` UDP values as `hand`.
- Mouse mode `3` is retained as a temporary visual mask stroke and does not create a walking surface.
- `WalkableSurface` remains dedicated to rainbow paths.

## Current Runtime Boundary

```text
Mouse / UDP RealSense
  -> IInteractionInputProvider
  -> InteractionObject[]
  -> LittlePeopleWorldOrchestrator
  -> World
     -> hand reaction fields
     -> rainbow walkable surfaces
     -> ambient objects / visual effects
  -> Unity views and mask animation
```

## Completed Structural Steps

`DomainModels.cs` has been split by ownership without changing namespaces or public APIs:

- `InteractionModels.cs`: input objects, interaction fields, and sensor frames.
- `EffectModels.cs`: reaction and visual-effect instances.
- `AmbientModels.cs`: clouds, the sun, and ambient runtime state.
- `RainbowModels.cs`: rainbow lifecycle and rainbow walkable surfaces.
- `LittlePerson.cs`: little-person state and movement rules.
- `World.cs`: aggregate root and frame progression.

`Masters.cs` has also been split without changing master IDs, defaults, or public APIs:

- `MasterTable.cs`: immutable lookup table.
- `WorldAndLittlePersonMasters.cs`: world and little-person definitions.
- `InteractionMasters.cs`: input, field, and reaction definitions.
- `EnvironmentMasters.cs`: rainbow and ambient-object definitions.
- `PresentationMasters.cs`: visual-effect and sound definitions.
- `TuningParameterMaster.cs`: shared runtime tuning values.
- `MasterDatabase.cs`: table ownership and default record assembly.

## Next Structural Steps

1. Split `WorldSpaceMaskAnimationController` into recognition mask, fairy simulation, plant lifecycle, rain occlusion, and renderers.
2. Move plant-look and leaf-hang timers out of `LittlePersonView` into a runtime state object.
3. Split UDP transport and track state while keeping `InteractionProtocolParser` and `IInteractionInputProvider` stable.
4. Move frequently tuned visual values into dedicated settings assets after the runtime classes are separated.

Each step must preserve the accepted baseline and pass C# compilation plus `python -m compileall python/realsense`.
