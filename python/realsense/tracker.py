from __future__ import annotations

import math
from dataclasses import dataclass


@dataclass
class Track:
    object_id: int
    x: float
    y: float
    last_seen: float


class NearestObjectTracker:
    def __init__(self, max_distance: float, ttl_seconds: float) -> None:
        self.max_distance = float(max_distance)
        self.ttl_seconds = float(ttl_seconds)
        self.next_id = 1
        self.tracks: dict[int, Track] = {}

    def assign_ids(self, detections: list[dict], now: float) -> list[dict]:
        self._drop_stale(now)
        unmatched_tracks = set(self.tracks.keys())
        assigned = []

        for detection in detections:
            match_id = self._find_match(detection, unmatched_tracks)
            if match_id is None:
                match_id = self.next_id
                self.next_id += 1
            else:
                unmatched_tracks.discard(match_id)

            detection_with_id = dict(detection)
            detection_with_id["id"] = match_id
            self.tracks[match_id] = Track(
                object_id=match_id,
                x=float(detection_with_id["x"]),
                y=float(detection_with_id["y"]),
                last_seen=now,
            )
            assigned.append(detection_with_id)

        return assigned

    def _find_match(self, detection: dict, candidate_ids: set[int]) -> int | None:
        best_id = None
        best_distance = self.max_distance
        x = float(detection["x"])
        y = float(detection["y"])

        for object_id in candidate_ids:
            track = self.tracks[object_id]
            distance = math.hypot(x - track.x, y - track.y)
            if distance <= best_distance:
                best_distance = distance
                best_id = object_id

        return best_id

    def _drop_stale(self, now: float) -> None:
        stale_ids = [
            object_id
            for object_id, track in self.tracks.items()
            if now - track.last_seen > self.ttl_seconds
        ]

        for object_id in stale_ids:
            del self.tracks[object_id]
