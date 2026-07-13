from __future__ import annotations

import argparse
import math
import time

from config import DEFAULT_OBJECT_KIND, SEND_RATE_HZ, UDP_HOST, UDP_PORT
from protocol.udp_sender import UdpJsonSender


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Send dummy Unity InteractionObject UDP JSON.")
    parser.add_argument("--host", default=UDP_HOST)
    parser.add_argument("--port", type=int, default=UDP_PORT)
    parser.add_argument("--rate", type=float, default=SEND_RATE_HZ)
    parser.add_argument("--kind", default=DEFAULT_OBJECT_KIND)
    parser.add_argument("--count", type=int, default=0)
    parser.add_argument("--center-x", type=float, default=0.5)
    parser.add_argument("--center-y", type=float, default=0.65)
    parser.add_argument("--radius", type=float, default=0.18)
    return parser.parse_args()


def build_dummy_object(args: argparse.Namespace, elapsed: float) -> dict:
    x = args.center_x + math.cos(elapsed * 0.8) * args.radius
    y = args.center_y + math.sin(elapsed * 0.6) * args.radius * 0.35
    angle = math.sin(elapsed * 0.7) * 28.0
    return {
        **build_dummy_hand(args, x, y, elapsed)
    } if args.kind == "hand" else {
        "id": 1,
        "kind": args.kind,
        "x": clamp01(x),
        "y": clamp01(y),
        "w": 0.12,
        "h": 0.12,
        "angle": angle,
        "height": 0.04,
        "state": "placed",
    }


def build_dummy_hand(args: argparse.Namespace, center_x: float, center_y: float, elapsed: float) -> dict:
    wave = math.sin(elapsed * 1.7) * 0.01
    offsets = [
        (-0.055, -0.010),
        (-0.045, -0.070),
        (-0.020, -0.105),
        (-0.005, -0.050),
        (0.012, -0.118),
        (0.028, -0.050),
        (0.052, -0.102),
        (0.058, -0.030),
        (0.090, -0.060),
        (0.075, 0.008),
        (0.058, 0.070),
        (0.010, 0.092),
        (-0.050, 0.070),
    ]
    points = [{"x": clamp01(center_x + x + wave), "y": clamp01(center_y + y)} for x, y in offsets]
    xs = [point["x"] for point in points]
    ys = [point["y"] for point in points]
    return {
        "id": 1,
        "kind": "hand",
        "shape": "contour",
        "x": clamp01(sum(xs) / len(xs)),
        "y": clamp01(sum(ys) / len(ys)),
        "w": clamp01(max(xs) - min(xs)),
        "h": clamp01(max(ys) - min(ys)),
        "angle": 0.0,
        "height": 0.05,
        "state": "placed",
        "points": points,
    }


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, float(value)))


def main() -> None:
    args = parse_args()
    interval = 1.0 / max(1.0, args.rate)
    start_time = time.monotonic()
    sent_count = 0

    print(f"Sending dummy UDP JSON to {args.host}:{args.port}")
    with UdpJsonSender(host=args.host, port=args.port) as sender:
        try:
            while args.count <= 0 or sent_count < args.count:
                now = time.monotonic()
                elapsed = now - start_time
                obj = build_dummy_object(args, elapsed)
                sender.send_frame(sent_count, elapsed, [obj])
                print(
                    f"send id={obj['id']} kind={obj['kind']} "
                    f"x={obj['x']:.3f} y={obj['y']:.3f} angle={obj['angle']:.1f} "
                    f"points={len(obj.get('points', []))}",
                    end="\r",
                    flush=True,
                )
                sent_count += 1
                time.sleep(interval)
        except KeyboardInterrupt:
            print("\nStopped.")
        else:
            print(f"\nSent {sent_count} frames.")


if __name__ == "__main__":
    main()
