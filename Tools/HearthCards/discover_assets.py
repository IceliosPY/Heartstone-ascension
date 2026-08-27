#!/usr/bin/env python3
"""Find the real component filenames in the renderer's public source.

We guessed once and got twelve of fifteen wrong, because the site answers every
unknown path with its own index page at HTTP 200, so a guess and a real file
look identical from the outside. This reads what the page actually loads and
reports the asset names it actually references.

It fetches the entry page, then the scripts and stylesheets that page links,
then looks in those for strings that name an asset. That is all: it follows
nothing else, tries no path of its own, and touches no endpoint the browser
would not.

    python discover_assets.py                 # report what the bundles name
    python discover_assets.py --json out.json # and write it out

Nothing here downloads a component. It produces names for a human to put in the
manifest.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

SITE = "https://www.hearthcards.net/"
USER_AGENT = "ConquestOfHearthstone-asset-discovery/1.0 (non-commercial fan project)"
TIMEOUT = 30
PAUSE = 0.5

# The strings we already know are real. Finding one of these in a bundle tells
# us we are reading the right file, and the text around it is where the rest of
# the names live.
ANCHORS = (
    "Card_Inhand_Minion_Neutral",
    "Card_Inhand_Hero_Neutral",
    "Card_Inhand_Weapon_Neutral",
)

# What an asset reference looks like. Deliberately narrow: a bare word would
# match half the minified code in the file.
ASSET = re.compile(r"[A-Za-z0-9_\-./]+\.(?:webp|png|jpg|jpeg|svg|gif)", re.IGNORECASE)

# Things that are certainly not card components.
NOT_A_COMPONENT = re.compile(
    r"(favicon|logo|banner_ad|avatar|thumb|screenshot|placeholder\.|/gallery/|/user)",
    re.IGNORECASE,
)


def get(url: str) -> tuple[int, bytes, str]:
    try:
        request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})

        with urllib.request.urlopen(request, timeout=TIMEOUT) as response:
            return response.status, response.read(), response.headers.get("Content-Type", "")

    except urllib.error.HTTPError as error:
        return error.code, b"", ""

    except Exception as error:  # noqa: BLE001
        print(f"  ! {url}: {error}", file=sys.stderr)
        return 0, b"", ""


def linked_sources(html: str, base: str) -> list[str]:
    """Every script and stylesheet the entry page loads."""
    found = []

    for pattern in (
        r'<script[^>]+src=["\']([^"\']+)["\']',
        r'<link[^>]+href=["\']([^"\']+\.css[^"\']*)["\']',
    ):
        for match in re.finditer(pattern, html, re.IGNORECASE):
            found.append(urllib.parse.urljoin(base, match.group(1)))

    # Stable order, no duplicates.
    seen = set()
    ordered = []

    for url in found:
        if url not in seen:
            seen.add(url)
            ordered.append(url)

    return ordered


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--json", type=Path, help="write the findings to a file")
    parser.add_argument("--context", type=int, default=600, help="characters of context around an anchor")
    arguments = parser.parse_args()

    print(f"Reading {SITE}\n")

    status, body, _ = get(SITE)

    if status != 200 or not body:
        print(f"The entry page answered {status}. Nothing to read.")
        return 1

    html = body.decode("utf-8", errors="replace")
    sources = linked_sources(html, SITE)

    print(f"The page loads {len(sources)} script(s) and stylesheet(s).\n")

    documents = [("index.html", html)]

    for url in sources:
        time.sleep(PAUSE)
        status, body, content_type = get(url)

        if status == 200 and body:
            documents.append((url, body.decode("utf-8", errors="replace")))
            print(f"  read  {url.rsplit('/', 1)[-1]:<40} {len(body):>9,} bytes")
        else:
            print(f"  MISS  {url}   [{status}]")

    # --- where the known names live ------------------------------------
    print("\nLooking for the names we already know are real.\n")

    hits = []

    for name, text in documents:
        for anchor in ANCHORS:
            for match in re.finditer(re.escape(anchor), text):
                hits.append((name, anchor, match.start()))

    if not hits:
        print(
            "  None of them appears in anything the page loads.\n"
            "  The component names are therefore not in the shipped source: they are\n"
            "  either built at runtime from parts, or fetched from somewhere this\n"
            "  script does not look. Do not guess from here."
        )
    else:
        for name, anchor, at in hits[:12]:
            print(f"  {anchor} in {name.rsplit('/', 1)[-1]} at offset {at}")

    # --- everything that looks like an asset ---------------------------
    references: dict[str, set[str]] = {}

    for name, text in documents:
        for match in ASSET.finditer(text):
            reference = match.group(0)

            if NOT_A_COMPONENT.search(reference):
                continue

            references.setdefault(reference, set()).add(name.rsplit("/", 1)[-1])

    print(f"\n{len(references)} asset reference(s) in the public source.\n")

    for reference in sorted(references):
        where = ", ".join(sorted(references[reference]))
        print(f"  {reference:<52} [{where}]")

    # --- the text around each known name -------------------------------
    if hits:
        print("\nContext around the first known name, which is where a table would be:\n")
        name, anchor, at = hits[0]
        text = dict(documents)[name]
        window = arguments.context
        print(text[max(0, at - window):at + window])

    if arguments.json:
        arguments.json.write_text(
            json.dumps(
                {
                    "site": SITE,
                    "sources": sources,
                    "anchorsFound": [{"file": n, "anchor": a, "offset": o} for n, a, o in hits],
                    "assetReferences": {k: sorted(v) for k, v in sorted(references.items())},
                },
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )
        print(f"\nWritten to {arguments.json}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
