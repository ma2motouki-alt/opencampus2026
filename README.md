# Little People World MVP

Unity 2D prototype for an interactive exhibit inspired by a table inhabited by small people.

The first milestone intentionally does not use RealSense. It builds the little-people world in Unity and uses mouse-created virtual interaction objects as a stand-in for hands and props. The runtime is structured so a future UDP/RealSense input provider can emit the same `InteractionObject` model.

## Quick Start

1. Create or open a Unity 2D project.
2. Copy this repository's `Assets/` folder into the Unity project root.
3. Create an empty scene with a camera.
4. Add `LittlePeopleWorldBootstrap` to an empty GameObject.
5. Press Play.

## Controls

- `1`: select hand input
- `2`: select round prop input
- `3`: select bar prop input
- Left click: place/select an interaction object
- Drag: move the selected object
- Mouse wheel: resize the selected object
- `R`: rotate selected bar prop
- `Shift + R`: rotate selected bar prop counter-clockwise
- `Delete` / `Backspace` / `X`: remove selected object
- `D`: toggle debug overlays

## Documentation

Design docs live in `docs/design/`.
Team workflow docs live in `docs/development/`.

The RealSense Python prototype lives in `python/realsense/`.
