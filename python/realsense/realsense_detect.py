from __future__ import annotations

import argparse
import math
import time

import cv2
import numpy as np
import pyrealsense2 as rs

from calibration import DisplayCalibration
from config import (
    BASELINE_FRAME_COUNT,
    CALIBRATION_PATH,
    DEBUG_PREVIEW,
    DEFAULT_OBJECT_KIND,
    DEFAULT_OBJECT_STATE,
    HEIGHT_THRESHOLD_METERS,
    MAX_OBJECTS,
    MAX_SENT_HEIGHT_METERS,
    MIN_CONTOUR_AREA_PIXELS,
    MORPH_KERNEL_SIZE,
    REALSENSE_FPS,
    REALSENSE_HEIGHT,
    REALSENSE_WIDTH,
    TRACK_MAX_DISTANCE,
    TRACK_TTL_SECONDS,
    UDP_HOST,
    UDP_PORT,
)
from tracker import NearestObjectTracker
from udp_sender import UdpJsonSender


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Detect RealSense depth objects and send Unity UDP JSON.")
    parser.add_argument("--host", default=UDP_HOST)
    parser.add_argument("--port", type=int, default=UDP_PORT)
    parser.add_argument("--kind", default=DEFAULT_OBJECT_KIND)
    parser.add_argument("--calibration", default=CALIBRATION_PATH)
    parser.add_argument("--no-preview", action="store_true")
    return parser.parse_args()


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


def read_depth_meters(pipeline, depth_scale: float):
    frames = pipeline.wait_for_frames()
    depth_frame = frames.get_depth_frame()
    if not depth_frame:
        return None

    depth_image = np.asanyarray(depth_frame.get_data()).astype(np.float32)
    return depth_image * depth_scale


def capture_baseline_depth(pipeline, depth_scale: float):
    print("Keep the display empty. Capturing baseline depth in 2 seconds...")
    time.sleep(2.0)

    frames = []
    for index in range(BASELINE_FRAME_COUNT):
        depth = read_depth_meters(pipeline, depth_scale)
        if depth is not None:
            frames.append(depth)
        print(f"baseline {index + 1}/{BASELINE_FRAME_COUNT}", end="\r", flush=True)

    print()
    if not frames:
        raise RuntimeError("Could not capture baseline depth.")

    return np.median(np.stack(frames, axis=0), axis=0)


def build_height_mask(baseline_depth, current_depth):
    valid_pixels = current_depth > 0.0
    height_map = np.where(valid_pixels, baseline_depth - current_depth, 0.0)
    height_map = np.where(height_map > 0.0, height_map, 0.0)
    mask = (height_map > HEIGHT_THRESHOLD_METERS).astype(np.uint8) * 255

    kernel = np.ones((MORPH_KERNEL_SIZE, MORPH_KERNEL_SIZE), dtype=np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
    return mask, height_map


def detect_objects(
    mask,
    height_map,
    calibration: DisplayCalibration,
    tracker: NearestObjectTracker,
    kind: str,
) -> list[dict]:
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    contours = sorted(contours, key=cv2.contourArea, reverse=True)[:MAX_OBJECTS]
    image_height, image_width = mask.shape[:2]
    now = time.monotonic()

    raw_detections = []
    for contour in contours:
        area_pixels = cv2.contourArea(contour)
        if area_pixels < MIN_CONTOUR_AREA_PIXELS:
            continue

        rect = cv2.minAreaRect(contour)
        box_points = cv2.boxPoints(rect)
        display_points = calibration.points_to_display(box_points, image_width, image_height)
        object_data = build_object_from_box(display_points, contour, height_map, kind)
        if object_data is not None:
            raw_detections.append(object_data)

    return tracker.assign_ids(raw_detections, now)


def build_object_from_box(
    display_points: list[tuple[float, float]],
    contour,
    height_map,
    kind: str,
) -> dict | None:
    if len(display_points) != 4:
        return None

    center_x = sum(point[0] for point in display_points) / 4.0
    center_y = sum(point[1] for point in display_points) / 4.0

    edges = []
    for index in range(4):
        start = display_points[index]
        end = display_points[(index + 1) % 4]
        dx = end[0] - start[0]
        dy = end[1] - start[1]
        length = math.hypot(dx, dy)
        edges.append((length, dx, dy))

    long_edge = max(edges, key=lambda item: item[0])
    short_edge = min(edges, key=lambda item: item[0])
    width = max(long_edge[0], 0.015)
    height = max(short_edge[0], 0.015)
    angle = math.degrees(math.atan2(long_edge[2], long_edge[1]))

    contour_mask = np.zeros(height_map.shape, dtype=np.uint8)
    cv2.drawContours(contour_mask, [contour], -1, 255, thickness=-1)
    heights = height_map[contour_mask == 255]
    average_height = float(np.mean(heights)) if len(heights) > 0 else 0.0

    return {
        "kind": kind,
        "x": clamp01(center_x),
        "y": clamp01(center_y),
        "w": clamp01(width),
        "h": clamp01(height),
        "angle": angle,
        "height": max(0.0, min(MAX_SENT_HEIGHT_METERS, average_height)),
        "state": DEFAULT_OBJECT_STATE,
    }


def draw_debug_preview(mask, objects: list[dict]) -> None:
    preview = cv2.cvtColor(mask, cv2.COLOR_GRAY2BGR)
    image_height, image_width = mask.shape[:2]

    for detected_object in objects:
        cx = int(detected_object["x"] * (image_width - 1))
        cy = int(detected_object["y"] * (image_height - 1))
        cv2.circle(preview, (cx, cy), 8, (0, 255, 255), thickness=2)
        cv2.putText(
            preview,
            f"id:{detected_object['id']} {detected_object['kind']}",
            (cx + 10, cy),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (0, 255, 255),
            1,
            cv2.LINE_AA,
        )

    cv2.imshow("height mask", preview)


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, float(value)))


def main() -> None:
    args = parse_args()
    pipeline = None
    preview_enabled = DEBUG_PREVIEW and not args.no_preview
    calibration = DisplayCalibration.load(args.calibration)
    tracker = NearestObjectTracker(max_distance=TRACK_MAX_DISTANCE, ttl_seconds=TRACK_TTL_SECONDS)

    try:
        pipeline, depth_scale = start_realsense_pipeline()
        baseline_depth = capture_baseline_depth(pipeline, depth_scale)
        frame_index = 0

        print(f"Sending UDP JSON to {args.host}:{args.port}")
        with UdpJsonSender(host=args.host, port=args.port) as sender:
            while True:
                current_depth = read_depth_meters(pipeline, depth_scale)
                if current_depth is None:
                    continue

                mask, height_map = build_height_mask(baseline_depth, current_depth)
                objects = detect_objects(mask, height_map, calibration, tracker, args.kind)
                sender.send_frame(frame_index, time.monotonic(), objects)
                frame_index += 1

                if preview_enabled:
                    draw_debug_preview(mask, objects)
                    if cv2.waitKey(1) & 0xFF == ord("q"):
                        break
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        if pipeline is not None:
            pipeline.stop()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
