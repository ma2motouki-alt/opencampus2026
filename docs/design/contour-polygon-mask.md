# Contour Polygon Mask Spec

## Summary

RealSense / Python sends `InteractionObject.points`. Unity uses those points in two ways:

1. `InteractionObjectView` fills the contour as a world-space mesh.
2. `WorldSpaceMaskAnimationController` rasterizes contour points into a low-resolution internal mask for particles and plants.

The current production display should rely on the contour mesh fill. The low-resolution mask texture is optional debug/internal animation data.

## Why Not Display The Raw Mask Texture

Displaying a `256x144` mask texture directly can create visible mismatch:

- Python preview image coordinates,
- normalized UDP coordinates,
- Unity texture pixels,
- Unity world coordinates,
- display aspect ratio,
- vertical flip rules.

Therefore, visible hand/object fill is generated from `points` as a mesh in Unity world space.

## Current Implementation

### Visible Contour Fill

Implemented in:

```text
Assets/Scripts/LittlePeopleWorld/Unity/InteractionObjectView.cs
```

Responsibilities:

- Convert `ContourPoints` with `NormalizedScreenMapper.ToWorld()`.
- Build a mesh with `ContourTriangulator`.
- Render a semi-transparent fill.
- Render an outline through `LineRenderer`.
- Fall back to primitive display if triangulation fails.

The triangulator uses ear clipping. It expects reasonably ordered contour points and does not fully repair self-intersections.

### Internal Mask Animation

Implemented in:

```text
Assets/Scripts/LittlePeopleWorld/Unity/WorldSpaceMaskAnimationController.cs
```

Responsibilities:

- Rasterize hand/bar contour points into `maskWidth x maskHeight`.
- Remove tiny blobs with `minBlobPixels`.
- Compute `effectiveMask` and field values.
- Move particles in mask-pixel space.
- Render particles, plants, and optional mask texture into Unity world space.

Default mask size:

```text
256 x 144
```

## Coordinate Policy

### Normalized

Python, UDP, and domain use normalized display coordinates.

### World

Visible contour fill uses Unity world coordinates.

### Mask Pixels

Particles and plants use mask-pixel coordinates internally. Conversion is done by:

- `NormalizedToMaskPx`
- `MaskPxToNormalized`
- `MaskToWorld`

## Behavior

- Hand contours are filled and influence little people through contour distance.
- Bar contours are filled when available and can still produce bar walkable surfaces.
- Particles are attracted to current contour masks.
- Rain creates or grows plants.
- Particles can climb plants and attach near flowers.
- A hand contour touching a flower bursts attached particles.

## Non Goals

- No full self-intersection repair for complex polygons.
- No 3D hand reconstruction.
- No direct display of the raw mask texture as the main visual.
- No perfect equality between mask-pixel animation coordinates and world-space contour fill.
