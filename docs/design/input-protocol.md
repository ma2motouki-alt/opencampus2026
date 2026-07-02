# Input Protocol

The MVP uses mouse input inside Unity. RealSense input will be added later by sending the same normalized runtime object model from a Python vision process to Unity.

## Coordinate System

- Use normalized coordinates from `0.0` to `1.0`.
- The origin is the top-left of the display.
- `x` increases to the right.
- `y` increases downward.
- Object size is also normalized against the display.

## Interaction Object

`InteractionObject` represents an object that came from mouse input or, later, from RealSense recognition.

```json
{
  "id": 1,
  "kind": "bar_prop",
  "x": 0.42,
  "y": 0.58,
  "w": 0.12,
  "h": 0.026,
  "angle": 18,
  "height": 0.04,
  "state": "placed"
}
```

Fields:

| Field | Meaning |
|---|---|
| `id` | Stable object id while the object exists |
| `kind` | `hand`, `round_prop`, `bar_prop`, or `block_prop` |
| `x`, `y` | Normalized center position |
| `w`, `h` | Normalized object size |
| `angle` | Degrees, clockwise-compatible with the Unity view |
| `height` | Reserved for RealSense-derived height |
| `state` | `placed`, `dragging`, or `removed` |

## Mouse MVP

- Left click: create or select an object.
- Drag: move the selected object.
- Mouse wheel: resize the selected object.
- `1`: next object is hand.
- `2`: next object is round prop.
- `3`: next object is bar prop.
- `R`: rotate the selected bar prop.
- `Delete`, `Backspace`, `X`: delete the selected object.
- `D`: toggle debug display.

## Derived Runtime Data

`InteractionField` is derived inside Unity from each `InteractionObject`. It describes broad influence such as hand repulsion or round-prop curiosity.

`WalkableSurface` is also derived inside Unity. It is not sent by the mouse provider or by the future RealSense provider. In the MVP, a `bar_prop` generates paths from the real long edges of the visible rectangle according to its tilt. Little people can transfer from the display edge to these edges, walk along the edge, ride it while the object moves slowly, transfer directly to a nearby different bar surface, and fall back to the display edge when no nearby surface exists or when the surface is removed or becomes unsuitable.

`PropObstacle` is derived inside Unity as well. It is not sent by input providers. In the MVP, a `bar_prop` generates a blocking oriented rectangle that matches the visible bar body, so little people walking on the display edge turn around when they hit a non-walkable side instead of passing through the prop.

`AmbientObject` is not input data. Clouds and stars are world-owned environmental objects that drift naturally and react when touched by little people.

## Future UDP JSON

The Python vision app should send a frame-level message that contains the current list of interaction objects:

```json
{
  "frame": 1280,
  "timestamp": 12.48,
  "objects": [
    {
      "id": 7,
      "kind": "bar_prop",
      "x": 0.36,
      "y": 0.74,
      "w": 0.18,
      "h": 0.04,
      "angle": -24,
      "height": 0.05,
      "state": "placed"
    }
  ]
}
```

Unity should treat the message as a replacement for the current input object set. The little-people world should not care whether the source is mouse input or RealSense input.
