from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np


@dataclass(frozen=True)
class DisplayCalibration:
    homography: np.ndarray | None = None
    flip_x: bool = False
    flip_y: bool = False

    @classmethod
    def load(cls, path: str | Path | None) -> "DisplayCalibration":
        if path is None:
            return cls()

        calibration_path = Path(path)
        if not calibration_path.exists():
            return cls()

        payload = json.loads(calibration_path.read_text(encoding="utf-8"))
        matrix = payload.get("homography")
        homography = np.array(matrix, dtype=np.float32) if matrix is not None else None
        return cls(
            homography=homography,
            flip_x=bool(payload.get("flip_x", False)),
            flip_y=bool(payload.get("flip_y", False)),
        )

    def image_to_display(
        self,
        point: tuple[float, float],
        image_width: int,
        image_height: int,
    ) -> tuple[float, float]:
        if self.homography is None:
            x = point[0] / max(image_width - 1, 1)
            y = point[1] / max(image_height - 1, 1)
        else:
            src = np.array([[point]], dtype=np.float32)
            transformed = cv2.perspectiveTransform(src, self.homography)
            x, y = transformed[0][0]

        if self.flip_x:
            x = 1.0 - x
        if self.flip_y:
            y = 1.0 - y

        return clamp01(float(x)), clamp01(float(y))

    def points_to_display(
        self,
        points: np.ndarray,
        image_width: int,
        image_height: int,
    ) -> list[tuple[float, float]]:
        return [
            self.image_to_display((float(point[0]), float(point[1])), image_width, image_height)
            for point in points
        ]


def compute_homography(
    camera_points: list[tuple[float, float]],
    display_points: list[tuple[float, float]],
) -> np.ndarray:
    if len(camera_points) < 4 or len(display_points) < 4:
        raise ValueError("At least four camera/display points are required.")

    src = np.array(camera_points, dtype=np.float32)
    dst = np.array(display_points, dtype=np.float32)
    homography, _ = cv2.findHomography(src, dst)
    if homography is None:
        raise RuntimeError("Failed to compute homography.")
    return homography


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))
