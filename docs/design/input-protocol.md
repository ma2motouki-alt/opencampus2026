# Input Protocol

## Coordinate Rules

All input coordinates are normalized display coordinates:

```text
x: 0.0 left -> 1.0 right
y: 0.0 top  -> 1.0 bottom
```

The origin is the top-left of the display.

## Input Boundary

Unity reads input through `IInteractionInputProvider`.

Implementations:

- `MouseInputProviderBehaviour`
- `UdpRealSenseInputProviderBehaviour`

The domain world does not know which provider produced the input.

## Mouse Controls

- `1`: hand
- `2`: round prop
- `3`: temporary development mask stroke
- `4`: observation mode, ignore mouse editing
- Left click: place or select
- Drag: move selected object
- Wheel: resize selected object
- `R`: rotate selected primitive clockwise
- `Shift + R`: rotate selected primitive counter-clockwise
- `Delete` / `Backspace` / `X`: delete selected object
- `D`: toggle debug overlays

## UDP JSON

Python sends a frame object:

```json
{
  "frame": 1280,
  "timestamp": 12.48,
  "objects": [
    {
      "id": 7,
      "kind": "hand",
      "shape": "contour",
      "x": 0.42,
      "y": 0.58,
      "w": 0.22,
      "h": 0.18,
      "angle": 0,
      "height": 0.06,
      "state": "placed",
      "points": [
        { "x": 0.38, "y": 0.51 },
        { "x": 0.44, "y": 0.49 },
        { "x": 0.50, "y": 0.54 }
      ]
    }
  ]
}
```

`UdpRealSenseInputProviderBehaviour` listens on port `5005` by default. If no packet arrives for about one second, Unity clears the input objects.

## Object Kinds

Current supported `kind` values:

- `hand`
- `round_prop`
- `block_prop`

Unknown or omitted kind falls back to `hand` in the Unity parser. Legacy `bar`, `stick`, and `bar_prop` values are also treated as hand input.

## Shape

`shape = "contour"` with at least three points creates a contour interaction object.

Rules:

- `points` must be ordered around the contour.
- `x/y/w/h` remain required as center and bounding-size fallback.
- `angle` is used by primitive fallback rendering.
- `points` are used for contour fill display and contour distance.

If no valid contour is present, Unity uses primitive rendering and primitive reaction logic.

## Ambient Objects

Clouds and stars are not input data. They are spawned and advanced by the world.
