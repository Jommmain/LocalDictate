#!/bin/sh
# Rebuild icon-*.png and LocalDictate.ico from localdictate.svg.
set -eu
cd "$(dirname "$0")"
for size in 16 24 32 48 64 128 256; do
  rsvg-convert -w "$size" -h "$size" localdictate.svg -o "icon-${size}.png"
done
python3 - <<'PY'
import struct
from pathlib import Path

root = Path(".")
sizes = [16, 24, 32, 48, 64, 128, 256]
images = [(size, (root / f"icon-{size}.png").read_bytes()) for size in sizes]
header = struct.pack("<HHH", 0, 1, len(images))
offset = 6 + 16 * len(images)
entries = b""
blobs = b""
for size, png in images:
    dim = 0 if size >= 256 else size
    entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(png), offset)
    offset += len(png)
    blobs += png
(root / "LocalDictate.ico").write_bytes(header + entries + blobs)
PY
