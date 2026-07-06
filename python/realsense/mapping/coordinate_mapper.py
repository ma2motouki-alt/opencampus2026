from __future__ import annotations

from abc import ABC, abstractmethod


class CoordinateMapper(ABC):
    @abstractmethod
    def point_to_display(
        self,
        x: float,
        y: float,
        image_width: int,
        image_height: int,
    ) -> tuple[float, float]:
        raise NotImplementedError

    def points_to_display(self, points, image_width: int, image_height: int) -> list[tuple[float, float]]:
        return [self.point_to_display(float(x), float(y), image_width, image_height) for x, y in points]
