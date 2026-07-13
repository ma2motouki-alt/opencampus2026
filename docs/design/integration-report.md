# Integration Report

## Summary

This repository has moved from the early mouse-only MVP into an integrated `main` branch that combines:

- core little-people domain logic,
- UDP / RealSense contour input,
- contour mesh fill display,
- mask-driven particles,
- rain-to-plant animation,
- flower burst animation,
- cloud and star ambient reactions.

## Kept From The Domain Architecture

- `Domain/`
- `Application/`
- `Master/`
- `IInteractionInputProvider`
- `LittlePeopleWorldOrchestrator`
- derived `InteractionField` and rainbow `WalkableSurface`

The domain remains independent of mouse vs UDP input.

## Integrated Visual / Animation Layer

`WorldSpaceMaskAnimationController` now owns the Unity-side animation layer:

- low-resolution internal mask,
- particles,
- rain landing,
- plant growth,
- particle climbing,
- flower burst,
- particle-cloud rain triggering.

`InteractionObjectView` owns contour mesh fill and outline display.

## Current Key Decisions

- Visible detected regions are drawn from contour points, not from the raw low-resolution mask texture.
- The low-resolution mask remains useful for particle and plant behavior.
- Hand contour reaction uses polygon distance.
- RealSense contours are treated uniformly as hand input; slender-object classification has been retired.
- Walkable surfaces are reserved for rainbow paths.
- RealSense front/top-view mapping is the default practical setup.
- Homography remains available but is not required for the current expected camera placement.
- Exhibition setup currently assumes a 52-inch TV facing upward and no protective acrylic.

## Notes For Future Integration

- Keep code changes small and branch-based.
- Update these docs whenever tuning values or exhibition assumptions change.
- Treat `parameter-manual.md` as the first place to document tuning knobs.
