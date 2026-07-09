# Little People World

Unity 2D exhibit prototype for Open Campus 2026. The work is inspired by a small world living on a horizontal display: little people, particles, plants, clouds, stars, hands, and objects react to each other without scores or missions.

The current `main` branch is no longer only a mouse MVP. It includes:

- Unity 6000.4.10f1 project files.
- Mouse input for local testing.
- UDP / RealSense input boundary.
- Python RealSense depth detection prototype.
- Hand and object contour input through `shape=contour` and `points`.
- Contour fill rendering in Unity.
- Little people walking on the display edge and on bar-prop surfaces.
- Ambient cloud / star reactions.
- Particle, rain, plant, and flower-burst animation.

## Quick Start

1. Open this repository with Unity Hub using Unity `6000.4.10f1`.
2. Open `Assets/Scenes/LittlePeopleWorldMvp.unity`.
3. Press Play.
4. Use mouse input first, then switch to UDP RealSense input from the `LittlePeopleWorldController` inspector when testing the camera.

## Mouse Controls

- `1`: hand input
- `2`: round prop input
- `3`: bar prop input
- `4`: observation mode; mouse editing is ignored
- Left click: place or select an interaction object
- Drag: move the selected object
- Mouse wheel: resize the selected object
- `R`: rotate selected bar prop clockwise
- `Shift + R`: rotate selected bar prop counter-clockwise
- `Delete` / `Backspace` / `X`: remove selected object
- `D`: toggle debug overlays

## RealSense

Python code lives in `python/realsense/`.

The current detection MVP assumes a mostly top-down / front-view RealSense placement. It captures an empty display baseline, builds a depth-height mask, extracts OpenCV contours, maps them to normalized display coordinates, and sends UDP JSON to Unity.

Run a dummy sender without RealSense:

```powershell
cd python/realsense
python test_sender.py --kind hand
```

Run RealSense detection:

```powershell
cd python/realsense
python realsense_detect.py
```

Unity listens on UDP port `5005` by default.

## Documentation

- `docs/design/`: current system, domain, input, and data design.
- `docs/development/`: Git and RealSense development workflow.
- `docs/roadmap/`: milestone-style development plan and current completion status.
- `parameter-manual.md`: runtime tuning guide for rain, particles, plants, masks, and input.
