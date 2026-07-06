from __future__ import annotations

import pyrealsense2 as rs
import numpy as np

from config import (
    REALSENSE_FPS,
    REALSENSE_FRAME_RETRY_COUNT,
    REALSENSE_FRAME_TIMEOUT_MS,
    REALSENSE_HEIGHT,
    REALSENSE_WIDTH,
)


def start_realsense_pipeline():
    pipeline = rs.pipeline()
    config = rs.config()
    config.enable_stream(
        rs.stream.depth,
        REALSENSE_WIDTH,
        REALSENSE_HEIGHT,
        rs.format.z16,
        REALSENSE_FPS,
    )

    profile = pipeline.start(config)
    depth_sensor = profile.get_device().first_depth_sensor()
    depth_scale = depth_sensor.get_depth_scale()
    print(f"RealSense depth: {REALSENSE_WIDTH}x{REALSENSE_HEIGHT} @{REALSENSE_FPS}fps")
    print(f"Depth scale: {depth_scale}")
    return pipeline, depth_scale


def read_depth_meters(
    pipeline,
    depth_scale: float,
    timeout_ms: int = REALSENSE_FRAME_TIMEOUT_MS,
    retry_count: int = REALSENSE_FRAME_RETRY_COUNT,
):
    for _ in range(max(1, retry_count)):
        try:
            frames = pipeline.wait_for_frames(timeout_ms)
        except RuntimeError:
            continue

        depth_frame = frames.get_depth_frame()
        if not depth_frame:
            continue

        depth_image = np.asanyarray(depth_frame.get_data()).astype(np.float32)
        return depth_image * depth_scale
    return None
