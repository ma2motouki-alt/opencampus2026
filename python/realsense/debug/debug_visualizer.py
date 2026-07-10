from __future__ import annotations

import time

import cv2
import numpy as np


class DebugVisualizer:
    def __init__(
        self,
        *,
        show_height_map: bool = True,
        show_mask_stages: bool = True,
        show_rejected_contours: bool = True,
        print_object_summary: bool = False,
        print_summary_interval_seconds: float = 1.0,
    ):
        self.show_height_map_enabled = show_height_map
        self.show_mask_stages_enabled = show_mask_stages
        self.show_rejected_contours_enabled = show_rejected_contours
        self.print_object_summary_enabled = print_object_summary
        self.print_summary_interval_seconds = max(0.1, float(print_summary_interval_seconds))
        self.last_summary_time = 0.0

    def show(
        self,
        *,
        height_map,
        mask_debug: dict,
        contour_debug: dict,
        objects: list[dict],
    ) -> None:
        if self.show_height_map_enabled:
            cv2.imshow("debug height map", colorize_height_map(height_map))

        if self.show_mask_stages_enabled:
            mask_before = mask_debug.get("mask_before_morph")
            mask_after_open = mask_debug.get("mask_after_open")
            mask_after_morph = mask_debug.get("mask_after_morph")
            if mask_before is not None:
                cv2.imshow("debug mask before morph", mask_before)
            if mask_after_open is not None:
                cv2.imshow("debug mask after open", mask_after_open)
            if mask_after_morph is not None:
                cv2.imshow("debug mask after morph", mask_after_morph)

        preview_mask = mask_debug.get("mask_after_morph")
        if preview_mask is None:
            preview_mask = mask_debug.get("mask_before_morph")
        if preview_mask is not None:
            cv2.imshow(
                "debug contours accepted rejected",
                self.build_contour_preview(preview_mask, contour_debug, objects),
            )
            cv2.imshow("debug sent objects", self.build_sent_objects_preview(preview_mask, objects))

        if self.print_object_summary_enabled:
            self.print_summary_if_needed(contour_debug, objects)

    def build_contour_preview(self, mask, contour_debug: dict, objects: list[dict]):
        preview = cv2.cvtColor(mask, cv2.COLOR_GRAY2BGR)

        for item in contour_debug.get("accepted", []):
            draw_contour_debug_item(
                preview,
                item,
                color=(60, 220, 80),
                label_prefix="send",
            )

        if self.show_rejected_contours_enabled:
            for item in contour_debug.get("rejected", []):
                reason = item.get("reason", "")
                color = rejected_reason_color(reason)
                draw_contour_debug_item(
                    preview,
                    item,
                    color=color,
                    label_prefix=reason,
                )

        draw_sent_objects(preview, objects)
        draw_pipeline_summary(preview, contour_debug, objects)
        return preview

    def build_sent_objects_preview(self, mask, objects: list[dict]):
        preview = np.zeros((mask.shape[0], mask.shape[1], 3), dtype=np.uint8)
        draw_sent_objects(preview, objects)
        return preview

    def print_summary_if_needed(self, contour_debug: dict, objects: list[dict]) -> None:
        now = time.monotonic()
        if now - self.last_summary_time < self.print_summary_interval_seconds:
            return
        self.last_summary_time = now

        accepted = contour_debug.get("accepted", [])
        rejected = contour_debug.get("rejected", [])
        rejected_reasons = {}
        for item in rejected:
            reason = item.get("reason", "unknown")
            rejected_reasons[reason] = rejected_reasons.get(reason, 0) + 1

        object_summary = [
            f"id={obj.get('id')} {obj.get('kind')} pts={len(obj.get('points') or [])} "
            f"xy=({obj.get('x', 0.0):.3f},{obj.get('y', 0.0):.3f}) "
            f"wh=({obj.get('w', 0.0):.3f},{obj.get('h', 0.0):.3f})"
            for obj in objects
        ]
        print(
            "debug summary "
            f"raw={contour_debug.get('raw_count', 0)} "
            f"accepted={len(accepted)} rejected={len(rejected)} "
            f"reasons={rejected_reasons} "
            f"objects={object_summary}"
        )


def colorize_height_map(height_map):
    clipped = np.clip(height_map, 0.0, 0.12)
    normalized = (clipped / 0.12 * 255).astype(np.uint8)
    return cv2.applyColorMap(normalized, cv2.COLORMAP_TURBO)


def draw_contour_debug_item(image, item: dict, *, color, label_prefix: str) -> None:
    contour = item.get("contour")
    if contour is None:
        return

    cv2.drawContours(image, [contour], -1, color, thickness=2)
    x, y, width, height = item.get("bbox") or cv2.boundingRect(contour)
    cv2.rectangle(image, (x, y), (x + width, y + height), color, thickness=1)
    label = f"{label_prefix} area:{int(item.get('area_pixels', 0))}"
    draw_text(image, label, (x, max(14, y - 4)), color)


def draw_sent_objects(image, objects: list[dict]) -> None:
    image_height, image_width = image.shape[:2]
    for detected_object in objects:
        cx = normalized_to_pixel_x(detected_object.get("x", 0.0), image_width)
        cy = normalized_to_pixel_y(detected_object.get("y", 0.0), image_height)
        kind = detected_object.get("kind", "object")
        points = detected_object.get("points") or []
        color = (0, 255, 255) if kind == "hand" else (255, 180, 40)

        cv2.circle(image, (cx, cy), 8, color, thickness=2)
        if len(points) >= 3:
            polyline = np.array(
                [
                    [
                        normalized_to_pixel_x(point.get("x", 0.0), image_width),
                        normalized_to_pixel_y(point.get("y", 0.0), image_height),
                    ]
                    for point in points
                ],
                dtype=np.int32,
            ).reshape((-1, 1, 2))
            cv2.polylines(image, [polyline], True, color, thickness=1)

        label = f"id:{detected_object.get('id')} {kind} pts:{len(points)}"
        draw_text(image, label, (cx + 10, cy), color)


def draw_pipeline_summary(image, contour_debug: dict, objects: list[dict]) -> None:
    accepted = len(contour_debug.get("accepted", []))
    rejected = len(contour_debug.get("rejected", []))
    text = (
        f"raw:{contour_debug.get('raw_count', 0)} "
        f"accepted:{accepted} rejected:{rejected} sent:{len(objects)} "
        f"area:{contour_debug.get('min_area_pixels')}..{contour_debug.get('max_area_pixels')}"
    )
    draw_text(image, text, (8, 20), (255, 255, 255))


def draw_text(image, text: str, position, color) -> None:
    x, y = position
    cv2.putText(
        image,
        text,
        (int(x), int(y)),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.45,
        (0, 0, 0),
        3,
        cv2.LINE_AA,
    )
    cv2.putText(
        image,
        text,
        (int(x), int(y)),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.45,
        color,
        1,
        cv2.LINE_AA,
    )


def rejected_reason_color(reason: str):
    return {
        "area_too_small": (40, 40, 255),
        "area_too_large": (0, 140, 255),
        "max_objects_exceeded": (180, 180, 180),
    }.get(reason, (200, 80, 200))


def normalized_to_pixel_x(value: float, image_width: int) -> int:
    return int(np.clip(value, 0.0, 1.0) * (image_width - 1))


def normalized_to_pixel_y(value: float, image_height: int) -> int:
    return int(np.clip(value, 0.0, 1.0) * (image_height - 1))
