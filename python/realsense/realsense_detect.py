from __future__ import annotations

import argparse
import time

import cv2
from config import (
    CALIBRATION_PATH,
    CLASSIFIER_MODE,
    DEBUG_PRINT_OBJECT_SUMMARY,
    DEBUG_PRINT_SUMMARY_INTERVAL_SECONDS,
    DEBUG_PREVIEW,
    DEBUG_SHOW_HEIGHT_MAP,
    DEBUG_SHOW_MASK_STAGES,
    DEBUG_SHOW_REJECTED_CONTOURS,
    DEFAULT_OBJECT_KIND,
    FLIP_X,
    FLIP_Y,
    MAPPER_MODE,
    TRACK_MAX_DISTANCE,
    TRACK_TTL_SECONDS,
    UDP_HOST,
    UDP_PORT,
)
from debug.debug_visualizer import DebugVisualizer
from detection.baseline import capture_baseline_depth
from detection.camera import read_depth_meters, start_realsense_pipeline
from detection.contour_detector import find_interaction_contours
from detection.hand_shape_extractor import extract_hand_shapes
from detection.height_mask import build_height_mask
from mapping.front_view_mapper import FrontViewMapper
from mapping.homography_mapper import HomographyMapper
from protocol.udp_sender import UdpJsonSender
from tracking.nearest_tracker import NearestObjectTracker


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Detect RealSense hand contours and send Unity UDP JSON.")
    parser.add_argument("--host", default=UDP_HOST)
    parser.add_argument("--port", type=int, default=UDP_PORT)
    parser.add_argument("--kind", default=DEFAULT_OBJECT_KIND)
    parser.add_argument("--classifier-mode", choices=("auto", "fixed"), default=CLASSIFIER_MODE)
    parser.add_argument("--mapper", choices=("front", "homography"), default=MAPPER_MODE)
    parser.add_argument("--calibration", default=CALIBRATION_PATH)
    parser.add_argument("--no-preview", action="store_true")
    parser.add_argument("--print-json", action="store_true")
    return parser.parse_args()


def create_mapper(mode: str, calibration_path: str):
    if mode == "homography":
        return HomographyMapper.load(calibration_path)
    return FrontViewMapper(flip_x=FLIP_X, flip_y=FLIP_Y)


def main() -> None:
    args = parse_args()
    pipeline = None
    preview_enabled = DEBUG_PREVIEW and not args.no_preview
    mapper = create_mapper(args.mapper, args.calibration)
    tracker = NearestObjectTracker(max_distance=TRACK_MAX_DISTANCE, ttl_seconds=TRACK_TTL_SECONDS)

    visualizer = DebugVisualizer(
        show_height_map=DEBUG_SHOW_HEIGHT_MAP,
        show_mask_stages=DEBUG_SHOW_MASK_STAGES,
        show_rejected_contours=DEBUG_SHOW_REJECTED_CONTOURS,
        print_object_summary=DEBUG_PRINT_OBJECT_SUMMARY,
        print_summary_interval_seconds=DEBUG_PRINT_SUMMARY_INTERVAL_SECONDS,
    ) if preview_enabled else None

    try:
        pipeline, depth_scale = start_realsense_pipeline()
        baseline_depth = capture_baseline_depth(pipeline, depth_scale)
        frame_index = 0

        print(f"Sending UDP JSON to {args.host}:{args.port}")
        with UdpJsonSender(host=args.host, port=args.port) as sender:
            while True:
                current_depth = read_depth_meters(pipeline, depth_scale)
                if current_depth is None:
                    print("waiting for RealSense depth frame...", end="\r", flush=True)
                    continue

                if preview_enabled:
                    mask, height_map, mask_debug = build_height_mask(baseline_depth, current_depth, return_debug=True)
                else:
                    mask, height_map = build_height_mask(baseline_depth, current_depth)
                    mask_debug = {}

                image_height, image_width = mask.shape[:2]
                contour_result = find_interaction_contours(mask, return_debug=preview_enabled)
                contours, contour_debug = contour_result if preview_enabled else (contour_result, {})
                raw_objects = extract_hand_shapes(
                    contours,
                    height_map,
                    mapper,
                    image_width,
                    image_height,
                    args.kind,
                    args.classifier_mode,
                )
                objects = tracker.assign_ids(raw_objects, time.monotonic())
                sender.send_frame(frame_index, time.monotonic(), objects)
                if args.print_json:
                    print(objects)
                frame_index += 1

                if preview_enabled:
                    visualizer.show(
                        height_map=height_map,
                        mask_debug=mask_debug,
                        contour_debug=contour_debug,
                        objects=objects,
                    )
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
