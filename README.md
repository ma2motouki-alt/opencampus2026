# Little People World

オープンキャンパス用の展示型ゲーム。ステレオカメラによって検出された物体の位置に応じて小人世界に影響を与えることができる。

## 環境構築:

- Unity 6000.4.10f1
- Python
- opencv 
- numpy
- pyrealsense2

## Quick Start

1. Open this repository with Unity Hub using Unity `6000.4.10f1`.
2. Open `Assets/Scenes/LittlePeopleWorldMvp.unity`.
3. Press Play.
4. Use mouse input first, then switch to UDP RealSense input from the `LittlePeopleWorldController` inspector when testing the camera.

## Manipulate for debug

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

## detection for RealSense

Python code lives in `python/realsense/`.

The current detection MVP assumes a mostly top-down / front-view RealSense placement. It captures an empty display baseline, builds a depth-height mask, extracts OpenCV contours, maps them to normalized display coordinates, and sends UDP JSON to Unity.


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
