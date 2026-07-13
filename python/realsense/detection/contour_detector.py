from __future__ import annotations

import cv2

from config import (
    HAND_MAX_CONTOUR_AREA_PIXELS,
    HAND_MIN_CONTOUR_AREA_PIXELS,
    MAX_OBJECTS,
)


def find_interaction_contours(mask, return_debug: bool = False):
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    contours = sorted(contours, key=cv2.contourArea, reverse=True)

    candidates = []
    accepted = []
    rejected = []
    for contour in contours:
        area_pixels = cv2.contourArea(contour)
        if area_pixels < HAND_MIN_CONTOUR_AREA_PIXELS:
            rejected.append(build_contour_debug(contour, area_pixels, "area_too_small"))
            continue
        if area_pixels > HAND_MAX_CONTOUR_AREA_PIXELS:
            rejected.append(build_contour_debug(contour, area_pixels, "area_too_large"))
            continue
        if len(candidates) >= MAX_OBJECTS:
            rejected.append(build_contour_debug(contour, area_pixels, "max_objects_exceeded"))
            continue

        candidates.append(contour)
        accepted.append(build_contour_debug(contour, area_pixels, "accepted"))

    if return_debug:
        return candidates, {
            "raw_count": len(contours),
            "accepted": accepted,
            "rejected": rejected,
            "min_area_pixels": HAND_MIN_CONTOUR_AREA_PIXELS,
            "max_area_pixels": HAND_MAX_CONTOUR_AREA_PIXELS,
            "max_objects": MAX_OBJECTS,
        }

    return candidates


def find_hand_contours(mask):
    # Backward-compatible alias for callers that only need accepted hand contours.
    return find_interaction_contours(mask)


def build_contour_debug(contour, area_pixels: float, reason: str):
    x, y, width, height = cv2.boundingRect(contour)
    return {
        "contour": contour,
        "area_pixels": area_pixels,
        "bbox": (x, y, width, height),
        "reason": reason,
    }
