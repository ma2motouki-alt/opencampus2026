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

## Next Structural Steps

1. Split `DomainModels.cs` by interaction, little-person, ambient, rainbow, and world ownership without changing behavior.
2. Split `WorldSpaceMaskAnimationController` into recognition mask, fairy simulation, plant lifecycle, rain occlusion, and renderers.
3. Move plant-look and leaf-hang timers out of `LittlePersonView` into a runtime state object.
4. Split UDP transport, JSON parsing, and track state while keeping `IInteractionInputProvider` stable.
5. Move frequently tuned visual values into dedicated settings assets after the runtime classes are separated.

Each step must preserve the accepted baseline and pass C# compilation plus `python -m compileall python/realsense`.
