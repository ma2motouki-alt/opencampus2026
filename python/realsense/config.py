UDP_HOST = "127.0.0.1"
UDP_PORT = 5005
SEND_RATE_HZ = 30

REALSENSE_WIDTH = 640
REALSENSE_HEIGHT = 480
REALSENSE_FPS = 30

BASELINE_FRAME_COUNT = 30
HEIGHT_THRESHOLD_METERS = 0.015
MIN_CONTOUR_AREA_PIXELS = 500
MORPH_KERNEL_SIZE = 5
MAX_OBJECTS = 8

DEBUG_PREVIEW = True

DEFAULT_OBJECT_KIND = "bar_prop"
DEFAULT_OBJECT_STATE = "placed"

# If no calibration file exists, image coordinates are normalized directly.
CALIBRATION_PATH = "calibration.json"

# Tracker values are in normalized display coordinates.
TRACK_MAX_DISTANCE = 0.12
TRACK_TTL_SECONDS = 0.5

# Small contours can produce very thin boxes. These defaults keep Unity visible.
MIN_NORMALIZED_WIDTH = 0.015
MIN_NORMALIZED_HEIGHT = 0.015

# Height is sent in meters for now. Unity clamps it to 0.0..1.0.
MAX_SENT_HEIGHT_METERS = 0.3
