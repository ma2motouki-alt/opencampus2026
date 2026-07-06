# RealSense Python Prototype

This folder sends object recognition results to Unity as UDP JSON.

## Quick Test Without RealSense

Start Unity, switch `LittlePeopleWorldController` to `Udp Real Sense`, press Play, then run:

```powershell
python test_sender.py --kind bar_prop
```

## RealSense Hand Contour Prototype

Install dependencies:

```powershell
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

Run:

```powershell
python realsense_detect.py --kind hand
```

The MVP assumes a front-facing camera placement. It extracts hand-shaped contours from the depth difference between an empty display baseline and the current frame, then sends `kind=hand`, `shape=contour`, and simplified contour `points` to Unity.

For an oblique camera placement, switch only the mapper:

```powershell
python realsense_detect.py --kind hand --mapper homography --calibration calibration.json
```

The detection pipeline is intentionally split so detection and coordinate mapping can evolve separately:

```text
detection -> mapping -> tracking -> protocol/udp_sender
```

## Output Format

```json
{
  "frame": 1,
  "timestamp": 0.1,
  "objects": [
    {
      "id": 1,
      "kind": "hand",
      "shape": "contour",
      "x": 0.5,
      "y": 0.5,
      "w": 0.18,
      "h": 0.04,
      "angle": 0,
      "height": 0.03,
      "state": "placed",
      "points": [
        { "x": 0.45, "y": 0.48 },
        { "x": 0.51, "y": 0.50 },
        { "x": 0.53, "y": 0.59 },
        { "x": 0.47, "y": 0.63 }
      ]
    }
  ]
}
```
