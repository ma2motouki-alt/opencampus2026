from __future__ import annotations

import time

import numpy as np

from config import BASELINE_FRAME_COUNT
from detection.camera import read_depth_meters


def capture_baseline_depth(pipeline, depth_scale: float, frame_count: int = BASELINE_FRAME_COUNT):
    print("Keep the display empty. Capturing baseline depth in 2 seconds...")
    time.sleep(2.0)

    frames = []
    attempts = 0
    max_attempts = max(frame_count * 3, frame_count)
    while len(frames) < frame_count and attempts < max_attempts:
        attempts += 1
        depth = read_depth_meters(pipeline, depth_scale)
        if depth is not None:
            frames.append(depth)
        print(f"baseline {len(frames)}/{frame_count}", end="\r", flush=True)

    print()
    if not frames:
        raise RuntimeError("Could not capture baseline depth.")

    return np.median(np.stack(frames, axis=0), axis=0)
