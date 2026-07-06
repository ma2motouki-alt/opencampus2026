# RealSense UDP Input

RealSense input is split into two parts:

- Python detects props and sends normalized UDP JSON.
- Unity receives UDP JSON through `UdpRealSenseInputProviderBehaviour` and exposes `InteractionObject` values to the existing world logic.

The world domain does not know whether the input came from mouse or RealSense.

## Unity Setup

1. Open the Unity project with `6000.4.10f1`.
2. Select the GameObject that has `LittlePeopleWorldController`.
3. Set `Input Provider Mode` to `Udp Real Sense`.
4. Keep the UDP provider port at `5005` unless Python is configured differently.
5. Press Play.
6. Press `D` to show debug overlays.

If no UDP packet arrives for about one second, Unity clears the input objects.

## Python Setup

Create a Python environment and install dependencies:

```powershell
cd python/realsense
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

Run a dummy UDP sender without RealSense:

```powershell
python test_sender.py --kind bar_prop
```

Run RealSense detection:

```powershell
python realsense_detect.py
```

## JSON Contract

Python sends one frame message:

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

Coordinate rules:

- `x`, `y`, `w`, and `h` are normalized `0.0..1.0`.
- Origin is the top-left of the display.
- `x` increases right.
- `y` increases down.
- `angle` uses the same convention as the mouse bar input: `atan2(dy, dx)` in degrees.

## RealSense Detection Strategy

The prototype uses:

- RealSense D435 depth stream.
- Baseline depth capture with no object on the display.
- Height mask from `baseline_depth - current_depth`.
- OpenCV contour extraction.
- `cv2.minAreaRect` for center, size, and angle.
- Homography calibration for camera-to-display mapping.
- A small nearest-neighbor tracker for stable object ids.

The first target is a dedicated bar prop sent as `bar_prop`. Better classification can be added later.
