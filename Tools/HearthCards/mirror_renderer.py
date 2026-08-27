#!/usr/bin/env python3
"""Mirror the renderer's public source so its typography can be read, not guessed.

The card components were found once by reading the site's own layer templates
rather than by inventing filenames, and the same rule applies here: how a title
is warped, which font draws the rules text and how a stat number is stroked are
all decisions the renderer already encodes somewhere. This copies what a browser
would load - the entry page, the scripts and stylesheets it links, and the layer
templates those name - into a local folder so the values can be looked up.

It follows exactly three things: the entry page, what that page links, and what
those files reference by name. It invents no paths, downloads no fonts and no
images, and touches nothing a browser opening the page would not.

    python mirror_renderer.py --out <folder>

Reading only. Nothing here writes into the Unity project.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import time
import urllib.parse
from pathlib import Path

SITE = "https://www.hearthcards.net/"
USER_AGENT = "ConquestOfHearthstone-renderer-study/1.0 (non-commercial fan project)"
TIMEOUT = 30
PAUSE = 0.4

# The templates the component discovery already proved are public and are the
# renderer's own description of a card.
TEMPLATES = (
    "assets_template_new/minion.json",
    "assets_template_new/spell.json",
    "assets_template_new/weapon.json",
    "assets_template_new/hero.json",
    "assets_template_new/hero_power.json",
    "assets_template_new/location.json",
)

TEXT_TYPES = ("javascript", "json", "css", "html", "text", "xml", "ecmascript")

# What a linked or referenced source looks like.
LINKED = (
    r'<script[^>]+src=["\']([^"\']+)["\']',
    r'<link[^>]+href=["\']([^"\']+\.css[^"\']*)["\']',
)

# References a bundle makes to further code or data it loads itself. Narrow on
# purpose: a bare word matches half of any minified file.
REFERENCED = re.compile(r'["\']([A-Za-z0-9_\-./]+\.(?:js|mjs|json|css))["\']')

SKIP = re.compile(
    r"(google-?analytics|googletagmanager|doubleclick|adsbygoogle|/ads?[./]|"
    r"facebook|twitter|recaptcha|gstatic\.com/recaptcha)",
    re.IGNORECASE,
)

# curl reports the status and type after the body, behind a marker that will not
# occur in either field.
TRAILER = b"\n@@@"


def get(url: str) -> tuple[int, bytes, str]:
    """Fetch one URL, verifying the certificate.

    Through curl rather than urllib, because this machine terminates TLS at a
    local root that only the system certificate store knows about: urllib
    rejects the chain, and certifi's roots do not contain it either. curl uses
    the system store and so verifies normally. The alternative - an unverified
    context - would have silenced every future certificate problem too, to work
    around something that is a fact about this machine rather than about the
    site.
    """
    command = [
        "curl", "--silent", "--show-error", "--location",
        "--max-time", str(TIMEOUT),
        "--user-agent", USER_AGENT,
        "--write-out", TRAILER.decode() + "%{http_code}\t%{content_type}",
        url,
    ]

    result = subprocess.run(command, capture_output=True)

    if result.returncode != 0:
        message = result.stderr.decode("utf-8", errors="replace").strip()
        print(f"  ! {url}: {message}", file=sys.stderr)
        return 0, b"", ""

    body, _, trailer = result.stdout.rpartition(TRAILER)
    status, _, content_type = trailer.decode("utf-8", errors="replace").partition("\t")

    try:
        return int(status), body, content_type

    except ValueError:
        return 0, b"", ""


def is_text(content_type: str, body: bytes) -> bool:
    """Whether this is source we can read.

    The site answers an unknown path with its own index page at HTTP 200, so a
    status code proves nothing. Content type plus a look at the bytes does.
    """
    if any(kind in content_type.lower() for kind in TEXT_TYPES):
        return True

    return b"\x00" not in body[:2048]


def local_name(url: str) -> str:
    parsed = urllib.parse.urlparse(url)
    path = (parsed.netloc + parsed.path).replace("/", "_").strip("_")
    return path or "index.html"


def save(folder: Path, url: str, body: bytes) -> Path:
    target = folder / local_name(url)
    target.write_bytes(body)
    return target


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True, help="where to mirror the source")
    parser.add_argument("--depth", type=int, default=2, help="how far to follow references")
    arguments = parser.parse_args()

    folder: Path = arguments.out
    folder.mkdir(parents=True, exist_ok=True)

    print(f"Reading {SITE}")

    status, body, _ = get(SITE)

    if status != 200 or not body:
        print(f"The entry page answered {status}. Nothing to read.")
        return 1

    html = body.decode("utf-8", errors="replace")
    save(folder, SITE + "index.html", body)

    queue: list[tuple[str, int]] = []

    for pattern in LINKED:
        for match in re.finditer(pattern, html, re.IGNORECASE):
            queue.append((urllib.parse.urljoin(SITE, match.group(1)), 1))

    for template in TEMPLATES:
        queue.append((urllib.parse.urljoin(SITE, template), 1))

    seen: set[str] = set()
    written = 0

    while queue:
        url, depth = queue.pop(0)

        if url in seen or SKIP.search(url):
            continue

        seen.add(url)
        time.sleep(PAUSE)

        status, body, content_type = get(url)

        if status != 200 or not body or not is_text(content_type, body):
            print(f"  skip {url} ({status}, {content_type or 'no type'}, {len(body)} bytes)")
            continue

        target = save(folder, url, body)
        written += 1
        print(f"  {len(body):>9,} bytes  {target.name}")

        if depth >= arguments.depth:
            continue

        text = body.decode("utf-8", errors="replace")

        for match in REFERENCED.finditer(text):
            queue.append((urllib.parse.urljoin(url, match.group(1)), depth + 1))

    print(f"\n{written} file(s) mirrored into {folder}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
