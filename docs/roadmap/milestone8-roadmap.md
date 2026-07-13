# Milestone 8: RealSense Prototype

## Goal

RealSense D435 と Python 認識アプリから、手や物体の輪郭をUnityへ送る。

## Target Scope

- RealSense D435 depth stream
- Baseline depth capture
- Height mask
- OpenCV contour extraction
- Contour simplification
- Auto classification for slender bar-like objects
- Front-view coordinate mapping
- Optional homography mapping
- UDP JSON send
- Debug preview

## Current Implementation

- `python/realsense/realsense_detect.py` is the entry point.
- `MAPPER_MODE = "front"` is the default.
- Accepted regions are sent uniformly as `kind=hand`, `shape=contour`.
- Contour points are simplified with `cv2.approxPolyDP`.
- Tiny and huge contours are filtered by area.
- Debug windows show height map, mask stages, accepted/rejected contours, and sent objects.

## Acceptance Check

- RealSense depth frames are acquired.
- Empty-screen baseline is captured.
- A hand appears in debug preview as a valid contour.
- Unity receives `hand` contour objects.
- Slender regions are handled through the same hand-contour contract.
- Noise blobs below the area threshold are rejected.
- Unity display follows the detected contour position.

## Handoff Notes

The current exhibition plan uses mostly top-down / front-view placement. Homography remains available but is not the default path.
