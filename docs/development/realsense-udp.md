# RealSense UDP Input

## Overview

RealSense input is split into two parts:

- Python detects depth contours and sends normalized UDP JSON.
- Unity receives UDP JSON through `UdpRealSenseInputProviderBehaviour`.

The world domain does not know whether the input came from mouse or RealSense.

## Unity Setup

1. Open the project with Unity `6000.4.10f1`.
2. Open `Assets/Scenes/LittlePeopleWorldMvp.unity`.
3. Select the GameObject with `LittlePeopleWorldController`.
4. Set `Input Provider Mode` to `Udp Real Sense`.
5. Keep UDP port `5005` unless Python is configured differently.
6. Press Play.
7. Press `D` to toggle Unity debug overlays.

If no UDP packet arrives for about one second, Unity clears input objects.

## Python Setup

```powershell
cd python/realsense
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

Run a dummy UDP sender:

```powershell
python test_sender.py --kind hand
```

Run RealSense detection:

```powershell
python realsense_detect.py
```

Useful options:

```powershell
python realsense_detect.py --no-preview
python realsense_detect.py --kind hand
python realsense_detect.py --classifier-mode auto
python realsense_detect.py --mapper front
python realsense_detect.py --mapper homography --calibration calibration.json
```

## JSON Contract

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

Coordinate rules:

- `x`, `y`, `w`, `h`, and `points` are normalized `0.0..1.0`.
- Origin is top-left.
- `x` increases right.
- `y` increases down.
- `angle` is degrees and mainly used for primitive bar fallback.

## Detection Strategy

Current MVP uses:

- RealSense D435 depth stream.
- Baseline depth capture with empty display.
- `baseline_depth - current_depth`.
- Height threshold.
- Morphological open/close.
- `cv2.findContours`.
- Area filtering.
- `cv2.approxPolyDP`.
- Optional slender-object classification.
- Nearest-neighbor tracking.
- UDP JSON send.

## Debugging Noise

Python debug windows show:

- height map,
- mask before morphology,
- mask after open,
- mask after close,
- accepted / rejected contours,
- sent objects.

Use these windows to determine whether noise comes from:

- depth reflection,
- too low height threshold,
- too small min area,
- morphology kernel too small,
- unstable baseline,
- camera placement or display reflection.

Main tuning values are in `python/realsense/config.py`.
