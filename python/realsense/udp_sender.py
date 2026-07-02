from __future__ import annotations

import json
import socket
from typing import Any, Iterable, Mapping

from config import DEFAULT_OBJECT_KIND, DEFAULT_OBJECT_STATE, UDP_HOST, UDP_PORT


def clamp01(value: Any) -> float:
    return max(0.0, min(1.0, float(value)))


def normalize_object(raw_object: Mapping[str, Any], fallback_id: int = 0) -> dict[str, Any]:
    kind = str(raw_object.get("kind", raw_object.get("type", DEFAULT_OBJECT_KIND)))
    return {
        "id": int(raw_object.get("id", fallback_id)),
        "kind": kind,
        "x": clamp01(raw_object.get("x", 0.0)),
        "y": clamp01(raw_object.get("y", 0.0)),
        "w": clamp01(raw_object.get("w", 0.12)),
        "h": clamp01(raw_object.get("h", 0.12)),
        "angle": float(raw_object.get("angle", 0.0)),
        "height": float(raw_object.get("height", 0.0)),
        "state": str(raw_object.get("state", DEFAULT_OBJECT_STATE)),
    }


def build_packet(
    frame: int,
    timestamp: float,
    objects: Iterable[Mapping[str, Any]],
) -> dict[str, Any]:
    normalized_objects = [
        normalize_object(raw_object, fallback_id=index + 1)
        for index, raw_object in enumerate(objects)
    ]
    return {
        "frame": int(frame),
        "timestamp": float(timestamp),
        "objects": normalized_objects,
    }


class UdpJsonSender:
    def __init__(self, host: str = UDP_HOST, port: int = UDP_PORT) -> None:
        self.host = host
        self.port = int(port)
        self._socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    def send_frame(
        self,
        frame: int,
        timestamp: float,
        objects: Iterable[Mapping[str, Any]],
    ) -> int:
        packet = build_packet(frame, timestamp, objects)
        message = json.dumps(packet, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        return self._socket.sendto(message, (self.host, self.port))

    def send_objects(self, objects: Iterable[Mapping[str, Any]]) -> int:
        return self.send_frame(0, 0.0, objects)

    def close(self) -> None:
        self._socket.close()

    def __enter__(self) -> "UdpJsonSender":
        return self

    def __exit__(self, exc_type, exc_value, traceback) -> None:
        self.close()


def send_frame(
    frame: int,
    timestamp: float,
    objects: Iterable[Mapping[str, Any]],
    host: str = UDP_HOST,
    port: int = UDP_PORT,
) -> int:
    with UdpJsonSender(host=host, port=port) as sender:
        return sender.send_frame(frame, timestamp, objects)
