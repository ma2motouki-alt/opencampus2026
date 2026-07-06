from __future__ import annotations

import cv2
import math
import numpy as np

from config import (
    BAR_MAX_THICKNESS_NORMALIZED,
    BAR_MIN_ASPECT_RATIO,
    BAR_MIN_LENGTH_NORMALIZED,
    CLASSIFIER_MODE,
    DEFAULT_OBJECT_STATE,
    HAND_APPROX_EPSILON_RATIO,
    HAND_MAX_POINTS,
    HAND_MIN_NORMALIZED_SIZE,
    HAND_MIN_POINTS,
    HAND_REACTION_SIZE_SCALE,
    MAX_SENT_HEIGHT_METERS,
)
from mapping.coordinate_mapper import CoordinateMapper
from protocol.interaction_object import clamp01


def extract_hand_shapes(
    contours,
    height_map,
    mapper: CoordinateMapper,
    image_width: int,
    image_height: int,
    kind: str = "hand",
    classifier_mode: str = CLASSIFIER_MODE,
):
    objects = []
    for contour in contours:
        object_data = build_object_from_contour(
            contour,
            height_map,
            mapper,
            image_width,
            image_height,
            kind,
            classifier_mode,
        )
        if object_data is not None:
            objects.append(object_data)
    return objects


def build_object_from_contour(
    contour,
    height_map,
    mapper: CoordinateMapper,
    image_width: int,
    image_height: int,
    kind: str,
    classifier_mode: str,
):
    normalized_kind = normalize_kind(kind)
    normalized_mode = (classifier_mode or CLASSIFIER_MODE).strip().lower()
    metrics = build_rotated_rect_metrics(contour, mapper, image_width, image_height)

    if normalized_mode == "fixed":
        if normalized_kind == "bar_prop":
            return build_bar_object_from_contour(contour, height_map, metrics, mapper, image_width, image_height)
        return build_hand_object_from_contour(contour, height_map, mapper, image_width, image_height, normalized_kind)

    if is_bar_like(metrics):
        return build_bar_object_from_contour(contour, height_map, metrics, mapper, image_width, image_height)

    return build_hand_object_from_contour(contour, height_map, mapper, image_width, image_height, "hand")


def build_hand_object_from_contour(
    contour,
    height_map,
    mapper: CoordinateMapper,
    image_width: int,
    image_height: int,
    kind: str,
):
    points_camera = simplify_contour_points(contour)
    if len(points_camera) < 3:
        return None

    points_display = mapper.points_to_display(points_camera, image_width, image_height)
    if len(points_display) < 3:
        return None

    xs = [point[0] for point in points_display]
    ys = [point[1] for point in points_display]
    min_x = clamp01(min(xs))
    max_x = clamp01(max(xs))
    min_y = clamp01(min(ys))
    max_y = clamp01(max(ys))
    width = max(HAND_MIN_NORMALIZED_SIZE, (max_x - min_x) * HAND_REACTION_SIZE_SCALE)
    height = max(HAND_MIN_NORMALIZED_SIZE, (max_y - min_y) * HAND_REACTION_SIZE_SCALE)

    center_camera = contour_centroid(contour)
    if center_camera is None:
        center_x = sum(xs) / len(xs)
        center_y = sum(ys) / len(ys)
    else:
        center_x, center_y = mapper.point_to_display(center_camera[0], center_camera[1], image_width, image_height)

    average_height = average_contour_height(contour, height_map)

    return {
        "kind": kind,
        "shape": "contour",
        "x": clamp01(center_x),
        "y": clamp01(center_y),
        "w": clamp01(width),
        "h": clamp01(height),
        "angle": 0.0,
        "height": max(0.0, min(MAX_SENT_HEIGHT_METERS, average_height)),
        "state": DEFAULT_OBJECT_STATE,
        "points": [{"x": clamp01(x), "y": clamp01(y)} for x, y in points_display],
    }


def build_bar_object_from_contour(
    contour,
    height_map,
    metrics,
    mapper: CoordinateMapper,
    image_width: int,
    image_height: int,
):
    if metrics is None:
        return None

    points_camera = simplify_contour_points(contour)
    points_display = mapper.points_to_display(points_camera, image_width, image_height)
    if len(points_display) < 3:
        return None

    return {
        "kind": "bar_prop",
        "shape": "contour",
        "x": clamp01(metrics["center"][0]),
        "y": clamp01(metrics["center"][1]),
        "w": clamp01(metrics["long_length"]),
        "h": clamp01(metrics["short_length"]),
        "angle": metrics["angle"],
        "height": max(0.0, min(MAX_SENT_HEIGHT_METERS, average_contour_height(contour, height_map))),
        "state": DEFAULT_OBJECT_STATE,
        "points": [{"x": clamp01(x), "y": clamp01(y)} for x, y in points_display],
    }


def build_rotated_rect_metrics(contour, mapper: CoordinateMapper, image_width: int, image_height: int):
    rect = cv2.minAreaRect(contour)
    box_camera = cv2.boxPoints(rect)
    box_camera_points = [(float(point[0]), float(point[1])) for point in box_camera]
    box_display = mapper.points_to_display(box_camera_points, image_width, image_height)
    if len(box_display) < 4:
        return None

    center_x = sum(point[0] for point in box_display) / len(box_display)
    center_y = sum(point[1] for point in box_display) / len(box_display)
    edges = []
    for index, start in enumerate(box_display):
        end = box_display[(index + 1) % len(box_display)]
        dx = end[0] - start[0]
        dy = end[1] - start[1]
        length = math.hypot(dx, dy)
        edges.append((length, dx, dy))

    positive_edges = [edge for edge in edges if edge[0] > 0.000001]
    if not positive_edges:
        return None

    long_edge = max(positive_edges, key=lambda edge: edge[0])
    short_edge = min(positive_edges, key=lambda edge: edge[0])
    angle = math.degrees(math.atan2(long_edge[2], long_edge[1]))
    return {
        "center": (center_x, center_y),
        "long_length": long_edge[0],
        "short_length": max(0.001, short_edge[0]),
        "angle": angle,
    }


def is_bar_like(metrics) -> bool:
    if metrics is None:
        return False

    short_length = max(0.001, metrics["short_length"])
    long_length = metrics["long_length"]
    aspect_ratio = long_length / short_length
    return (
        aspect_ratio >= BAR_MIN_ASPECT_RATIO
        and long_length >= BAR_MIN_LENGTH_NORMALIZED
        and short_length <= BAR_MAX_THICKNESS_NORMALIZED
    )


def average_contour_height(contour, height_map) -> float:
    contour_mask = np.zeros(height_map.shape, dtype=np.uint8)
    cv2.drawContours(contour_mask, [contour], -1, 255, thickness=-1)
    heights = height_map[contour_mask == 255]
    return float(np.mean(heights)) if len(heights) > 0 else 0.0


def normalize_kind(kind: str) -> str:
    value = (kind or "hand").strip().lower().replace("-", "_")
    return {
        "bar": "bar_prop",
        "stick": "bar_prop",
        "round": "round_prop",
        "circle": "round_prop",
    }.get(value, value)


def simplify_contour_points(contour):
    arc_length = cv2.arcLength(contour, True)
    epsilon = max(0.5, arc_length * HAND_APPROX_EPSILON_RATIO)
    approx = cv2.approxPolyDP(contour, epsilon, True).reshape(-1, 2)
    raw = contour.reshape(-1, 2)

    if len(approx) < HAND_MIN_POINTS and len(raw) > len(approx):
        points = sample_points(raw, min(HAND_MAX_POINTS, max(HAND_MIN_POINTS, len(approx))))
    else:
        points = approx

    if len(points) > HAND_MAX_POINTS:
        points = sample_points(points, HAND_MAX_POINTS)

    return [(float(point[0]), float(point[1])) for point in points]


def sample_points(points, count: int):
    if len(points) <= count:
        return points
    indices = np.linspace(0, len(points) - 1, count, dtype=np.int32)
    return points[indices]


def contour_centroid(contour):
    moments = cv2.moments(contour)
    if abs(moments["m00"]) <= 0.000001:
        return None
    return moments["m10"] / moments["m00"], moments["m01"] / moments["m00"]
