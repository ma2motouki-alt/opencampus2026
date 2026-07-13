# RealSense Detection Prototype

This folder contains the Python side of the RealSense / UDP input pipeline.

## Purpose

Detect foreground hand regions with RealSense depth, convert their contours to normalized display coordinates, and send them to Unity as `InteractionObject` JSON.

## Current Assumption

The current practical setup uses a mostly top-down / front-view RealSense placement above a 52-inch TV facing upward. This means the default mapper is:

```python
MAPPER_MODE = "front"
```

Homography support remains available for future oblique placement, but it is not required for the current exhibition plan.

## Setup

```powershell
cd python/realsense
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

## Run Without RealSense

```powershell
python test_sender.py --kind hand
```

## Run With RealSense

```powershell
python realsense_detect.py
```

The script captures an empty baseline after startup. Keep the display empty until the baseline capture finishes.

## Useful Options

```powershell
python realsense_detect.py --no-preview
python realsense_detect.py --mapper front
python realsense_detect.py --mapper homography --calibration calibration.json
```

## Pipeline

```text
RealSense depth
  -> baseline depth
  -> baseline_depth - current_depth
  -> height threshold
  -> morphology open / close
  -> contour detection
  -> contour filtering
  -> contour simplification
  -> hand contour conversion
  -> tracking
  -> UDP JSON
```

## Current Config Values

Location: `config.py`

| Parameter | Value |
|---|---:|
| `MIN_CONTOUR_AREA_PIXELS` | `500` |
| `MORPH_KERNEL_SIZE` | `5` |
| `MAPPER_MODE` | `front` |
| `HAND_MIN_CONTOUR_AREA_PIXELS` | `800` |
| `HAND_MAX_CONTOUR_AREA_PIXELS` | `80000` |
| `HAND_APPROX_EPSILON_RATIO` | `0.005` |
| `HAND_MAX_POINTS` | `80` |
| `HAND_MIN_POINTS` | `8` |
| `HAND_MAX_ASPECT_RATIO` | `4.5` |
All accepted contours are sent as `kind = "hand"` and can include:

```json
{
  "shape": "contour",
  "points": [
    { "x": 0.1, "y": 0.2 }
  ]
}
```

## Debug Preview

By default, debug preview is enabled in `config.py`.

Windows may include:

- `debug height map`
- `debug mask before morph`
- `debug mask after open`
- `debug mask after morph`
- `debug contours accepted rejected`
- `debug sent objects`

Use these to tune reflection/noise problems before changing Unity.

## UDP Contract

Unity expects frame-level JSON:

```json
{
  "frame": 1,
  "timestamp": 0.0,
  "objects": [
    {
      "id": 1,
      "kind": "hand",
      "shape": "contour",
      "x": 0.5,
      "y": 0.5,
      "w": 0.2,
      "h": 0.2,
      "angle": 0,
      "height": 0.05,
      "state": "placed",
      "points": []
    }
  ]
}
```
