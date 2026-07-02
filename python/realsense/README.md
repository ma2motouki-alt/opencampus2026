# RealSense Python Prototype

This folder sends object recognition results to Unity as UDP JSON.

## Quick Test Without RealSense

Start Unity, switch `LittlePeopleWorldController` to `Udp Real Sense`, press Play, then run:

```powershell
python test_sender.py --kind bar_prop
```

## RealSense Prototype

Install dependencies:

```powershell
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

Run:

```powershell
python realsense_detect.py
```

The first version treats detected props as `bar_prop`.

## Output Format

```json
{
  "frame": 1,
  "timestamp": 0.1,
  "objects": [
    {
      "id": 1,
      "kind": "bar_prop",
      "x": 0.5,
      "y": 0.5,
      "w": 0.18,
      "h": 0.04,
      "angle": 10,
      "height": 0.03,
      "state": "placed"
    }
  ]
}
```
