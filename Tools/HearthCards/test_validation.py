#!/usr/bin/env python3
"""Regression tests for the one thing that went badly wrong.

The fetcher believed a URL because it answered HTTP 200. The site answers
every unknown path with its own index page, at two hundred, so twelve guessed
filenames all looked like successes and twelve copies of a web page were
written into the project named .webp.

These pin that shut. They need no network: the failing response is three
hundred bytes of HTML, and that is exactly the point — the bug was never about
the network.

    python test_validation.py
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from fetch_card_assets import check_image, load_manifest  # noqa: E402


# The shape of what actually came back, at HTTP 200, for every wrong name.
FALLBACK_PAGE = (
    b'<!doctype html><html lang="en"><head><meta charset="utf-8"/>'
    b'<link rel="icon" href="/favicon.ico"/>'
    b'<meta name="viewport" content="width=device-width,initial-scale=1"/>'
    b"</head><body><div id=\"root\"></div></body></html>"
)

# A real, minimal WEBP: RIFF header, size, WEBP tag, then a VP8L chunk.
TINY_WEBP = (
    b"RIFF" + (0x1A).to_bytes(4, "little") + b"WEBP"
    + b"VP8L" + (0x0E).to_bytes(4, "little")
    + b"\x2f\x00\x00\x00\x00\x07\x10\x11\x11\x88\x88\xfe\x07\x00"
)

# A real one by one pixel PNG, built rather than pasted so it cannot rot.
def _tiny_png() -> bytes:
    import struct
    import zlib

    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (struct.pack('>I', len(payload)) + kind + payload
                + struct.pack('>I', zlib.crc32(kind + payload) & 0xFFFFFFFF))

    header = struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0)
    pixels = zlib.compress(bytes([0, 255, 0, 0, 255]))

    return (bytes([0x89]) + b'PNG' + bytes([13, 10, 26, 10])
            + chunk(b'IHDR', header)
            + chunk(b'IDAT', pixels)
            + chunk(b'IEND', b''))


TINY_PNG = _tiny_png()


def check(name, condition, detail=""):
    print(f"  {'ok  ' if condition else 'FAIL'}  {name}{('   ' + detail) if detail else ''}")
    return condition


def main() -> int:
    print("Validating responses that are not images.\n")

    passed = []

    # --- the bug itself -------------------------------------------------
    result = check_image(FALLBACK_PAGE, "text/html; charset=utf-8")
    passed.append(check(
        "an HTML page at HTTP 200 is not a valid image",
        not result, result.reason))

    result = check_image(FALLBACK_PAGE)
    passed.append(check(
        "and is still not one when the content type is missing",
        not result, result.reason))

    # --- the other ways a response can lie ------------------------------
    passed.append(check("an empty response is not an image", not check_image(b"")))
    passed.append(check("JSON is not an image", not check_image(b'{"error":"nope"}')))
    passed.append(check(
        "a correct signature with a broken body does not decode",
        not check_image(b"RIFF\x00\x00\x00\x00WEBP" + b"\x00" * 40)))
    passed.append(check(
        "an image content type does not rescue a non-image body",
        not check_image(FALLBACK_PAGE, "image/webp")))

    # --- and a real one still passes ------------------------------------
    result = check_image(TINY_WEBP, "image/webp")
    passed.append(check("a real webp is valid", bool(result), result.describe()))

    result = check_image(TINY_PNG, "image/png")
    passed.append(check("a real png is valid", bool(result), result.describe()))

    # --- the manifest reads -------------------------------------------
    print("\nReading the manifest.\n")

    manifest = load_manifest()
    passed.append(check(f"{len(manifest.entries)} entries load", len(manifest.entries) > 0))

    ids = [entry.id for entry in manifest.entries]
    passed.append(check("every id is unique", len(ids) == len(set(ids))))

    passed.append(check(
        "every url is https",
        all(entry.url.startswith("https://") for entry in manifest.entries)))

    passed.append(check(
        "every url ends with the filename it records",
        all(entry.url.endswith(entry.filename) for entry in manifest.entries)))

    failures = len(passed) - sum(passed)
    print(f"\n{sum(passed)} passed, {failures} failed.")
    return 0 if failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
