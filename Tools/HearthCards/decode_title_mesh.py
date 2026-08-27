#!/usr/bin/env python3
"""Decode the renderer's title meshes and measure what they actually do to a title.

The first attempt at reproducing the warp read the numbers around the mesh - the
stretch, the calibration, the curved path - and guessed at the mesh itself. The
result bent every letter separately and looked like a wave. The mesh is public
and its format is written out in the bundle that parses it, so this decodes it
and reports the shape it really produces, which is a very different thing from
the shape those numbers suggested.

    python decode_title_mesh.py --bundle <main.js>

Reads a mirrored bundle. Downloads nothing, writes nothing into the project.
"""

from __future__ import annotations

import argparse
import base64
import re
import struct
from pathlib import Path

# The renderer's own reader, transcribed:
#
#   const count = meshType === "ally" ? 63 : 234;
#   for (const size of [48, 40, 32, 44, 56, 64])
#       if (bytes.byteLength >= count * size) { stride = size; break; }
#   x = f32(o + 0); y = f32(o + 4); z = f32(o + 8);
#   u = f32(o + 24); v = f32(o + 28);
#   wx = -x; wy = -z; wz = y;
#
# then the vertices are centred on their own bounds and projected:
#
#   rx = wx * scale; ry = wy * scale; rz = cameraZ - wz * scale
#   t  = tan(45deg / 2)
#   sx = 800 * (rx / (rz * (1600/600) * t) + 1)
#   sy = 300 * (1 - ry / (rz * t))
VERTEX_COUNTS = {"ally": 63, "spell": 234}
STRIDES = (48, 40, 32, 44, 56, 64)

CALIBRATION = {
    # span, stretch, height, scale, cameraZ - straight out of the templates.
    "ally": dict(span=1010, stretch=1.6, height=256, scale=2.3, cameraZ=2.3),
    "spell": dict(span=1010, stretch=1.7, height=256, scale=2.3, cameraZ=2.3),
}

PLACEMENT = {
    "ally": dict(x=27, y=500, width=759, height=290),
    "spell": dict(x=7, y=481, width=800, height=300),
}

BASE64 = re.compile(r'"([A-Za-z0-9+/=]{400,})"')


def blobs(bundle: str) -> list[str]:
    """Every long base64 string near the mesh reader."""
    anchor = bundle.find("meshType")

    if anchor < 0:
        raise SystemExit("No mesh reader in this bundle.")

    window = bundle[max(0, anchor - 400_000):anchor + 20_000]
    return [match.group(1) for match in BASE64.finditer(window)]


def decode_vertices(blob: str, count: int) -> list[dict]:
    data = base64.b64decode(blob)

    stride = 32

    for candidate in STRIDES:
        if len(data) >= count * candidate:
            stride = candidate
            break

    vertices = []

    for index in range(count):
        at = index * stride

        if at + 12 > len(data):
            break

        x, y, z = struct.unpack_from("<fff", data, at)

        u = v = 0.0

        if at + 32 <= len(data):
            u, v = struct.unpack_from("<ff", data, at + 24)

        vertices.append(dict(wx=-x, wy=-z, wz=y, u=u, v=v))

    return vertices, stride, len(data)


def project(vertices: list[dict], scale: float, camera_z: float) -> list[tuple[float, float]]:
    import math

    # Centred on its own bounds, exactly as the renderer does.
    for axis in ("wx", "wy", "wz"):
        low = min(vertex[axis] for vertex in vertices)
        high = max(vertex[axis] for vertex in vertices)
        middle = (low + high) / 2

        for vertex in vertices:
            vertex[axis] -= middle

    tangent = math.tan(math.radians(45) / 2)
    points = []

    for vertex in vertices:
        rx = vertex["wx"] * scale
        ry = vertex["wy"] * scale
        rz = camera_z - vertex["wz"] * scale

        points.append((
            800 * (rx / (rz * (1600 / 600) * tangent) + 1),
            300 * (1 - ry / (rz * tangent)),
        ))

    return points


def report(kind: str, vertices: list[dict], points: list[tuple[float, float]]) -> None:
    place = PLACEMENT[kind]

    print(f"\n=== {kind} ===")
    print(f"  vertices {len(vertices)}")

    xs = [point[0] for point in points]
    ys = [point[1] for point in points]

    print(f"  projected x {min(xs):8.1f} .. {max(xs):8.1f}   (span {max(xs) - min(xs):7.1f})")
    print(f"  projected y {min(ys):8.1f} .. {max(ys):8.1f}   (span {max(ys) - min(ys):7.1f})")

    # The interesting question: where does a point of the flat title texture end
    # up? u runs across the title, v down it. Sampling the top and bottom edges
    # separately says how much the surface bends and how much it stretches.
    top = [(vertex["u"], point) for vertex, point in zip(vertices, points) if vertex["v"] > 0.85]
    bottom = [(vertex["u"], point) for vertex, point in zip(vertices, points) if vertex["v"] < 0.15]

    print(f"  samples on the top edge {len(top)}, on the bottom edge {len(bottom)}")

    for name, edge in (("top", top), ("bottom", bottom)):
        if not edge:
            continue

        edge.sort(key=lambda item: item[0])

        print(f"  --- {name} edge, u -> screen y (in the {place['height']}px placement) ---")

        for u, point in edge:
            # Into the placement's own pixels, which is where it lands on a card.
            y = (point[1] / 600) * place["height"]
            x = (point[0] / 1600) * place["width"]
            print(f"      u {u:5.3f}   x {x:7.1f}   y {y:7.1f}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True)
    arguments = parser.parse_args()

    bundle = arguments.bundle.read_text(encoding="utf-8", errors="replace")
    found = blobs(bundle)

    print(f"{len(found)} long base64 string(s) near the mesh reader:")

    for index, blob in enumerate(found):
        print(f"  [{index}] {len(blob)} chars -> {len(base64.b64decode(blob))} bytes")

    # Which blob is which is decided by size rather than by position: a vertex
    # buffer is far larger than an index buffer, and there are two of each.
    by_size = sorted(range(len(found)), key=lambda index: len(found[index]))

    if len(found) < 2:
        raise SystemExit("Not enough blobs to work with.")

    for kind in ("ally", "spell"):
        count = VERTEX_COUNTS[kind]

        # Try every blob and keep the one whose length matches a plausible
        # stride for this vertex count and whose UVs land inside [0, 1].
        best = None

        for index in by_size:
            vertices, stride, size = decode_vertices(found[index], count)

            if len(vertices) < count:
                continue

            us = [vertex["u"] for vertex in vertices]
            vs = [vertex["v"] for vertex in vertices]

            if min(us) < -0.01 or max(us) > 1.01 or min(vs) < -0.01 or max(vs) > 1.01:
                continue

            best = (index, vertices, stride, size)
            break

        if best is None:
            print(f"\n=== {kind} === no blob decoded to {count} vertices with sane UVs.")
            continue

        index, vertices, stride, size = best
        print(f"\n{kind}: blob [{index}], {size} bytes, stride {stride}, {count} vertices")

        calibration = CALIBRATION[kind]
        points = project(vertices, calibration["scale"], calibration["cameraZ"])
        report(kind, vertices, points)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
