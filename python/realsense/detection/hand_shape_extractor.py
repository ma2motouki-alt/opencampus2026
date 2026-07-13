from __future__ import annotations

import cv2
import numpy as np

from config import (
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
):
    objects = []
    for contour in contours:
        hand = build_hand_object_from_contour(
            contour,
            height_map,
            mapper,
            image_width,
            image_height,
        )
        if hand is not None:
            objects.append(hand)
    return objects


def build_hand_object_from_contour(
    contour,
    height_map,
    mapper: CoordinateMapper,
    image_width: int,
    image_height: int,
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
        center_x, center_y = mapper.point_to_display(
            center_camera[0],
            center_camera[1],
            image_width,
            image_height,
        )

    average_height = average_contour_height(contour, height_map)
    return {
        "kind": "hand",
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


def average_contour_height(contour, height_map) -> float:
    contour_mask = np.zeros(height_map.shape, dtype=np.uint8)
    cv2.drawContours(contour_mask, [contour], -1, 255, thickness=-1)
    heights = height_map[contour_mask == 255]
    return float(np.mean(heights)) if len(heights) > 0 else 0.0


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
