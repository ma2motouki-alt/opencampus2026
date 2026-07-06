from __future__ import annotations

import json
import socket
from typing import Iterable, Mapping

from config import UDP_HOST, UDP_PORT
from protocol.interaction_object import build_packet


class UdpJsonSender:
    def __init__(self, host: str = UDP_HOST, port: int = UDP_PORT) -> None:
        self.host = host
        self.port = int(port)
        self._socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    def send_frame(
        self,
        frame: int,
        timestamp: float,
        objects: Iterable[Mapping],
    ) -> int:
        packet = build_packet(frame, timestamp, objects)
        message = json.dumps(packet, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        return self._socket.sendto(message, (self.host, self.port))

    def send_objects(self, objects: Iterable[Mapping]) -> int:
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
    objects: Iterable[Mapping],
    host: str = UDP_HOST,
    port: int = UDP_PORT,
) -> int:
    with UdpJsonSender(host=host, port=port) as sender:
        return sender.send_frame(frame, timestamp, objects)
