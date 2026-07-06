from __future__ import annotations

from dataclasses import dataclass

from mapping.coordinate_mapper import CoordinateMapper
from protocol.interaction_object import clamp01


@dataclass(frozen=True)
class FrontViewMapper(CoordinateMapper):
    flip_x: bool = False
    flip_y: bool = False

    def point_to_display(
        self,
        x: float,
        y: float,
        image_width: int,
        image_height: int,
    ) -> tuple[float, float]:
        normalized_x = x / max(image_width - 1, 1)
        normalized_y = y / max(image_height - 1, 1)

        if self.flip_x:
            normalized_x = 1.0 - normalized_x
        if self.flip_y:
            normalized_y = 1.0 - normalized_y

        return clamp01(normalized_x), clamp01(normalized_y)
