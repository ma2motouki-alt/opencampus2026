from __future__ import annotations

import cv2
import numpy as np

from config import HEIGHT_THRESHOLD_METERS, MORPH_KERNEL_SIZE


def build_height_mask(
    baseline_depth,
    current_depth,
    height_threshold_meters: float = HEIGHT_THRESHOLD_METERS,
    morph_kernel_size: int = MORPH_KERNEL_SIZE,
    return_debug: bool = False,
):
    valid_pixels = current_depth > 0.0
    height_map = np.where(valid_pixels, baseline_depth - current_depth, 0.0)
    height_map = np.where(height_map > 0.0, height_map, 0.0)
    mask_before_morph = (height_map > height_threshold_meters).astype(np.uint8) * 255

    kernel_size = max(1, int(morph_kernel_size))
    kernel = np.ones((kernel_size, kernel_size), dtype=np.uint8)
    mask_after_open = cv2.morphologyEx(mask_before_morph, cv2.MORPH_OPEN, kernel)
    mask_after_morph = cv2.morphologyEx(mask_after_open, cv2.MORPH_CLOSE, kernel)

    if not return_debug:
        return mask_after_morph, height_map

    debug = {
        "height_threshold_meters": height_threshold_meters,
        "morph_kernel_size": kernel_size,
        "mask_before_morph": mask_before_morph,
        "mask_after_open": mask_after_open,
        "mask_after_morph": mask_after_morph,
    }
    return mask_after_morph, height_map, debug
