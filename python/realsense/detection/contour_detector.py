from __future__ import annotations

import cv2

from config import (
    HAND_MAX_CONTOUR_AREA_PIXELS,
    HAND_MIN_CONTOUR_AREA_PIXELS,
    MAX_OBJECTS,
)


def find_interaction_contours(mask):
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    contours = sorted(contours, key=cv2.contourArea, reverse=True)

    candidates = []
    for contour in contours:
        area_pixels = cv2.contourArea(contour)
        if area_pixels < HAND_MIN_CONTOUR_AREA_PIXELS:
            continue
        if area_pixels > HAND_MAX_CONTOUR_AREA_PIXELS:
            continue

        candidates.append(contour)
        if len(candidates) >= MAX_OBJECTS:
            break

    return candidates


def find_hand_contours(mask):
    # Backward-compatible alias. The returned contours may become hand or bar_prop later.
    return find_interaction_contours(mask)
