from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np


@dataclass(frozen=True)
class CalibrationData:
    homography: np.ndarray | None = None
    flip_x: bool = False
    flip_y: bool = False


def load_calibration(path: str | Path | None) -> CalibrationData:
    if path is None:
        return CalibrationData()

    calibration_path = Path(path)
    if not calibration_path.exists():
        return CalibrationData()

    payload = json.loads(calibration_path.read_text(encoding="utf-8"))
    matrix = payload.get("homography")
    homography = np.array(matrix, dtype=np.float32) if matrix is not None else None
    return CalibrationData(
        homography=homography,
        flip_x=bool(payload.get("flip_x", False)),
        flip_y=bool(payload.get("flip_y", False)),
    )


def save_calibration(path: str | Path, calibration: CalibrationData) -> None:
    payload = {
        "homography": calibration.homography.tolist() if calibration.homography is not None else None,
        "flip_x": calibration.flip_x,
        "flip_y": calibration.flip_y,
    }
    Path(path).write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def compute_homography(
    camera_points: list[tuple[float, float]],
    display_points: list[tuple[float, float]],
) -> np.ndarray:
    if len(camera_points) < 4 or len(display_points) < 4:
        raise ValueError("At least four camera/display points are required.")
    homography, _ = cv2.findHomography(np.array(camera_points, dtype=np.float32), np.array(display_points, dtype=np.float32))
    if homography is None:
        raise RuntimeError("Failed to compute homography.")
    return homography
