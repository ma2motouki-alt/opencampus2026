from __future__ import annotations

from dataclasses import dataclass

import cv2
import numpy as np

from mapping.calibration_store import CalibrationData, load_calibration
from mapping.coordinate_mapper import CoordinateMapper
from mapping.front_view_mapper import FrontViewMapper
from protocol.interaction_object import clamp01


@dataclass(frozen=True)
class HomographyMapper(CoordinateMapper):
    calibration: CalibrationData

    @classmethod
    def load(cls, path: str) -> "HomographyMapper":
        return cls(load_calibration(path))

    def point_to_display(
        self,
        x: float,
        y: float,
        image_width: int,
        image_height: int,
    ) -> tuple[float, float]:
        if self.calibration.homography is None:
            return FrontViewMapper(
                flip_x=self.calibration.flip_x,
                flip_y=self.calibration.flip_y,
            ).point_to_display(x, y, image_width, image_height)

        src = np.array([[[x, y]]], dtype=np.float32)
        transformed = cv2.perspectiveTransform(src, self.calibration.homography)
        normalized_x, normalized_y = transformed[0][0]

        if self.calibration.flip_x:
            normalized_x = 1.0 - normalized_x
        if self.calibration.flip_y:
            normalized_y = 1.0 - normalized_y

        return clamp01(float(normalized_x)), clamp01(float(normalized_y))
