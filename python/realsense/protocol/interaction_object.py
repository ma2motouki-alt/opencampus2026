from __future__ import annotations

from typing import Any, Iterable, Mapping

from config import DEFAULT_OBJECT_KIND, DEFAULT_OBJECT_STATE


def clamp01(value: Any) -> float:
    return max(0.0, min(1.0, float(value)))


def normalize_point(raw_point: Mapping[str, Any] | tuple[float, float]) -> dict[str, float]:
    if isinstance(raw_point, Mapping):
        x = raw_point.get("x", 0.0)
        y = raw_point.get("y", 0.0)
    else:
        x, y = raw_point
    return {"x": clamp01(x), "y": clamp01(y)}


def normalize_object(raw_object: Mapping[str, Any], fallback_id: int = 0) -> dict[str, Any]:
    kind = str(raw_object.get("kind", raw_object.get("type", DEFAULT_OBJECT_KIND)))
    normalized = {
        "id": int(raw_object.get("id", fallback_id)),
        "kind": kind,
        "shape": str(raw_object.get("shape", "primitive")),
        "x": clamp01(raw_object.get("x", 0.0)),
        "y": clamp01(raw_object.get("y", 0.0)),
        "w": clamp01(raw_object.get("w", 0.12)),
        "h": clamp01(raw_object.get("h", 0.12)),
        "angle": float(raw_object.get("angle", 0.0)),
        "height": float(raw_object.get("height", 0.0)),
        "state": str(raw_object.get("state", DEFAULT_OBJECT_STATE)),
    }

    points = raw_object.get("points")
    if points:
        normalized["points"] = [normalize_point(point) for point in points]

    return normalized


def build_packet(
    frame: int,
    timestamp: float,
    objects: Iterable[Mapping[str, Any]],
) -> dict[str, Any]:
    normalized_objects = [
        normalize_object(raw_object, fallback_id=index + 1)
        for index, raw_object in enumerate(objects)
    ]
    return {
        "frame": int(frame),
        "timestamp": float(timestamp),
        "objects": normalized_objects,
    }
