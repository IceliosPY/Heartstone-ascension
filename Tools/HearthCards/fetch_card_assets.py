#!/usr/bin/env python3
"""Fetch, verify and convert the card components named in the manifest.

The manifest is the allowlist. Nothing here discovers, crawls or follows a
link: it reads a list of URLs somebody wrote down and fetches exactly those.
A component that is not in the manifest cannot be downloaded by this script,
which is the point of having one.

    verify    ask what exists and look at the answer, write nothing
    fetch     download what is missing or changed
    convert   Raw/*.webp -> Imported/*.png, losslessly
    status    what is on disk, what is not
    all       verify, fetch, convert

Every run writes hearthcards-assets.lock.json: the URL, the status it came
back with, its SHA-256, its size and when it was fetched. That file is what
makes a second run reproducible, and what turns the manifest's guesses into
facts.

Usage:
    python fetch_card_assets.py status
    python fetch_card_assets.py verify
    python fetch_card_assets.py all
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

HERE = Path(__file__).resolve().parent
MANIFEST = HERE / "hearthcards-assets.json"
LOCK = HERE / "hearthcards-assets.lock.json"

# The repository root, two folders up from Tools/HearthCards/.
ROOT = HERE.parent.parent

USER_AGENT = "ConquestOfHearthstone-asset-fetch/1.0 (non-commercial fan project)"
TIMEOUT = 30
RETRIES = 3
PAUSE = 0.4  # Between requests. Their server, our impatience.


# ----------------------------------------------------------------------
#  The manifest
# ----------------------------------------------------------------------

@dataclass
class Entry:
    id: str
    filename: str
    url: str
    status: str
    category: str
    purpose: str
    slot: str
    card_type: str | None
    card_class: str | None
    rarity: str | None
    priority: int
    raw_path: Path
    imported_path: Path

    def describe(self) -> str:
        bits = [b for b in (self.card_type, self.card_class, self.rarity) if b]
        return f"{self.slot:<16} {'+'.join(bits) if bits else 'any card':<24} {self.filename}"


@dataclass
class Manifest:
    raw_folder: Path
    imported_folder: Path
    entries: list[Entry] = field(default_factory=list)


def load_manifest(path: Path = MANIFEST) -> Manifest:
    if not path.exists():
        sys.exit(f"No manifest at {path}")

    data = json.loads(path.read_text(encoding="utf-8"))

    raw = ROOT / data["rawFolder"]
    imported = ROOT / data["importedFolder"]

    entries = []
    seen = set()

    for raw_entry in data["entries"]:
        identifier = raw_entry["id"]

        if identifier in seen:
            sys.exit(f"The manifest lists '{identifier}' twice.")

        seen.add(identifier)

        filename = raw_entry["filename"]
        stem = Path(filename).stem

        entries.append(Entry(
            id=identifier,
            filename=filename,
            url=raw_entry["url"],
            status=raw_entry.get("status", "unverified"),
            category=raw_entry.get("category", ""),
            purpose=raw_entry.get("purpose", ""),
            slot=raw_entry["slot"],
            card_type=raw_entry.get("cardType"),
            card_class=raw_entry.get("cardClass"),
            rarity=raw_entry.get("rarity"),
            priority=int(raw_entry.get("priority", 99)),
            raw_path=raw / filename,
            imported_path=imported / f"{stem}.png",
        ))

    entries.sort(key=lambda e: (e.priority, e.id))
    return Manifest(raw_folder=raw, imported_folder=imported, entries=entries)


# ----------------------------------------------------------------------
#  The lock file
# ----------------------------------------------------------------------

def load_lock() -> dict:
    if LOCK.exists():
        try:
            return json.loads(LOCK.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            print("  ! the lock file is unreadable and is being rebuilt")

    return {"schemaVersion": 1, "assets": {}}


def save_lock(lock: dict) -> None:
    lock["updatedAt"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    LOCK.write_text(json.dumps(lock, indent=2, sort_keys=False) + "\n", encoding="utf-8")


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()

    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(65536), b""):
            digest.update(block)

    return digest.hexdigest()


# ----------------------------------------------------------------------
#  Fetching
# ----------------------------------------------------------------------

# ----------------------------------------------------------------------
#  Is this actually a picture?
# ----------------------------------------------------------------------
#
# HTTP 200 proves nothing. A single page application answers every unknown
# path with its own index.html, cheerfully, at two hundred, with a content
# type of text/html. The first version of this script trusted the status line
# and wrote twelve copies of a web page into the project named .webp.
#
# So a response is only an image if it says it is one, looks like one, and
# decodes as one. Three checks, because each catches something the others do
# not: a header can be wrong, a signature can be forged by coincidence, and a
# file can be truncated in transit.

IMAGE_SIGNATURES = (
    (b"RIFF", 8, b"WEBP"),
    (b"\x89PNG\r\n\x1a\n", None, None),
    (b"\xff\xd8\xff", None, None),
    (b"GIF87a", None, None),
    (b"GIF89a", None, None),
)


class ImageCheck:
    """What a response turned out to be."""

    def __init__(self, valid, reason="", fmt="", width=0, height=0):
        self.valid = valid
        self.reason = reason
        self.format = fmt
        self.width = width
        self.height = height

    def describe(self) -> str:
        if self.valid:
            return f"{self.format} {self.width}x{self.height}"
        return self.reason

    def __bool__(self) -> bool:
        return self.valid


def _matches_a_signature(body: bytes) -> bool:
    for head, at, expected in IMAGE_SIGNATURES:
        if not body.startswith(head):
            continue
        if at is None or body[at:at + len(expected)] == expected:
            return True
    return False


def _what_came_back_instead(body: bytes) -> str:
    start = body[:64].lstrip().lower()

    if start.startswith(b"<!doctype html") or start.startswith(b"<html"):
        return "an HTML page, not an image"

    if start.startswith(b"{") or start.startswith(b"["):
        return "JSON, not an image"

    return f"{len(body)} bytes with no image signature"


def check_image(body: bytes, content_type: str = "") -> ImageCheck:
    """Whether this response is a picture, and what kind."""
    if not body:
        return ImageCheck(False, "an empty response")

    if content_type and not content_type.lower().startswith("image/"):
        return ImageCheck(False, f"content type {content_type.split(';')[0]}, not an image")

    if not _matches_a_signature(body):
        return ImageCheck(False, _what_came_back_instead(body))

    try:
        from PIL import Image
    except ImportError:
        # Signature only. Weaker, and said so rather than claimed as a decode.
        return ImageCheck(True, "", "image (undecoded)", 0, 0)

    from io import BytesIO

    try:
        # verify() reads the structure and then invalidates the object, so the
        # size has to be taken from a second open. Cheap, and it is the only
        # way to be sure the bytes are a whole image rather than a header.
        Image.open(BytesIO(body)).verify()

        with Image.open(BytesIO(body)) as image:
            return ImageCheck(True, "", image.format or "?", image.width, image.height)

    except Exception as error:  # noqa: BLE001 - any decode failure is a failure
        return ImageCheck(False, f"looks like an image but will not decode: {error}")


def request(url: str, method: str = "GET") -> tuple[int, bytes | None, str, str]:
    """Returns (status, body, contentType, note). Never raises for an HTTP error."""
    last = ""

    for attempt in range(1, RETRIES + 1):
        try:
            req = urllib.request.Request(
                url, method=method, headers={"User-Agent": USER_AGENT})

            with urllib.request.urlopen(req, timeout=TIMEOUT) as response:
                body = response.read() if method == "GET" else b""
                return response.status, body, response.headers.get("Content-Type", ""), ""

        except urllib.error.HTTPError as error:
            # A 404 is an answer, not a failure to get one. Do not retry it.
            return error.code, None, "", error.reason or ""

        except urllib.error.URLError as error:
            last = str(error.reason)

        except Exception as error:  # noqa: BLE001 - reported, never swallowed
            last = str(error)

        if attempt < RETRIES:
            time.sleep(PAUSE * attempt * 2)

    return 0, None, "", last or "no response"


def command_verify(manifest: Manifest) -> int:
    """Asks what exists, and looks at what came back. Writes no image files.

    A GET rather than a HEAD, because the status line is not the interesting
    part. Three words, and they mean three different things:

        valid        it is a picture, and here is what kind
        invalid      the server answered, with something that is not a picture
        unreachable  the server did not answer at all

    The middle one is the whole reason this command was rewritten.
    """
    lock = load_lock()
    valid = invalid = unreachable = 0

    print(f"Verifying {len(manifest.entries)} URL(s).\n")

    for entry in manifest.entries:
        status, body, content_type, note = request(entry.url)

        record = lock["assets"].setdefault(entry.id, {})
        record["url"] = entry.url
        record["httpStatus"] = status
        record["contentType"] = content_type
        record["checkedAt"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())

        if status != 200 or body is None:
            unreachable += 1
            record["validImage"] = False
            record["invalidReason"] = note or "no response"
            print(f"  unreachable  {entry.filename}")
            print(f"               [{status or 'no response'}] {note or 'not found'}")

        else:
            check = check_image(body, content_type)
            record["size"] = len(body)
            record["sha256"] = hashlib.sha256(body).hexdigest()
            record["validImage"] = bool(check)

            if check:
                valid += 1
                record["imageFormat"] = check.format
                record["width"] = check.width
                record["height"] = check.height
                record.pop("invalidReason", None)
                print(f"  valid        {entry.filename}")
                print(f"               {check.describe()}  {len(body) / 1024:,.1f} KiB")
            else:
                invalid += 1
                record["invalidReason"] = check.reason
                for gone in ("imageFormat", "width", "height"):
                    record.pop(gone, None)
                print(f"  invalid      {entry.filename}")
                print(f"               HTTP {status} but {check.reason}")

        time.sleep(PAUSE)

    save_lock(lock)

    print(f"\n{valid} valid images, {invalid} invalid responses, {unreachable} unreachable.")
    print(f"Written to {LOCK.name}.")

    if invalid:
        print(
            "\nAn invalid response is a filename this project got wrong, not a file\n"
            "the site does not have. Every name in the manifest is supposed to come\n"
            "from the renderer's own templates, so an invalid one means a template\n"
            "was misread or has changed. Do not guess a replacement: look it up."
        )

    return 0 if invalid == 0 and unreachable == 0 else 1


def command_fetch(manifest: Manifest) -> int:
    """Downloads what the manifest names, and only what turned out to be a picture."""
    lock = load_lock()
    manifest.raw_folder.mkdir(parents=True, exist_ok=True)

    downloaded = skipped = invalid = failed = 0
    total_bytes = 0

    print(f"Fetching into {manifest.raw_folder.relative_to(ROOT)}\n")

    for entry in manifest.entries:
        record = lock["assets"].setdefault(entry.id, {})

        # Already here, and unchanged since it was recorded.
        if entry.raw_path.exists():
            local = sha256_of(entry.raw_path)

            if record.get("sha256") == local and record.get("validImage"):
                skipped += 1
                total_bytes += entry.raw_path.stat().st_size
                print(f"  have         {entry.filename}")
                continue

        status, body, content_type, note = request(entry.url)

        if status != 200 or body is None:
            failed += 1
            record.update({
                "url": entry.url,
                "httpStatus": status,
                "validImage": False,
                "invalidReason": note or "no response",
            })
            print(f"  unreachable  {entry.filename}   [{status or 'no response'}] {note}")
            time.sleep(PAUSE)
            continue

        check = check_image(body, content_type)

        record.update({
            "url": entry.url,
            "httpStatus": status,
            "contentType": content_type,
            "size": len(body),
            "sha256": hashlib.sha256(body).hexdigest(),
            "validImage": bool(check),
            "checkedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        })

        # Nothing that is not a picture is ever written to disk. Otherwise a
        # web page becomes a file, the file becomes a catalog row, and the
        # first anybody hears of it is a card with an error page on the front.
        if not check:
            invalid += 1
            record["invalidReason"] = check.reason
            print(f"  invalid      {entry.filename}   HTTP {status} but {check.reason}")
            time.sleep(PAUSE)
            continue

        entry.raw_path.write_bytes(body)

        record.update({
            "imageFormat": check.format,
            "width": check.width,
            "height": check.height,
            "fetchedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        })
        record.pop("invalidReason", None)

        downloaded += 1
        total_bytes += len(body)
        print(f"  got          {entry.filename}   {check.describe()}  {len(body):,} bytes")

        time.sleep(PAUSE)

    save_lock(lock)

    print(
        f"\n{downloaded} downloaded, {skipped} already present, "
        f"{invalid} invalid, {failed} unreachable."
        f"  {total_bytes / 1024:,.1f} KiB on disk."
    )

    return 0 if invalid == 0 and failed == 0 else 1


# ----------------------------------------------------------------------
#  Converting
# ----------------------------------------------------------------------

def command_convert(manifest: Manifest) -> int:
    """Raw/*.webp to Imported/*.png, losslessly, and never all-or-nothing.

    One unreadable file used to end the run and leave everything after it
    unconverted, which is the worst way to fail: the batch looks finished and
    is not. Every file is now converted on its own and a bad one is reported
    and stepped over.

    The originals are never touched. Imported/ is derived, and deleting it
    costs one command.
    """
    try:
        from PIL import Image
    except ImportError:
        print("Pillow is not installed. `pip install pillow`, then run convert again.")
        return 1

    manifest.imported_folder.mkdir(parents=True, exist_ok=True)

    converted = current = invalid = missing = 0
    problems = []

    print(f"Converting into {manifest.imported_folder.relative_to(ROOT)}\n")

    for entry in manifest.entries:
        if not entry.raw_path.exists():
            missing += 1
            continue

        try:
            body = entry.raw_path.read_bytes()
            check = check_image(body)

            if not check:
                invalid += 1
                problems.append(f"{entry.filename}: {check.reason}")
                print(f"  invalid  {entry.filename}   {check.reason}")
                continue

            if entry.raw_path.suffix.lower() == ".png":
                # Already a PNG. Copied rather than re-encoded, so nothing is
                # resampled on the way through.
                if entry.imported_path.exists():
                    current += 1
                else:
                    entry.imported_path.write_bytes(body)
                    converted += 1
                    print(f"  copy     {entry.imported_path.name}")
                continue

            # Only when the source is newer, so a second run does nothing.
            if (entry.imported_path.exists()
                    and entry.imported_path.stat().st_mtime >= entry.raw_path.stat().st_mtime):
                current += 1
                continue

            with Image.open(entry.raw_path) as image:
                # RGBA always: these components are mostly transparent, and a
                # mode that dropped the alpha would fill the window the artwork
                # shows through.
                image.convert("RGBA").save(entry.imported_path, format="PNG", optimize=True)
                converted += 1
                print(f"  png      {entry.imported_path.name}   {image.width}x{image.height}")

        except Exception as error:  # noqa: BLE001 - one bad file, not a dead batch
            invalid += 1
            problems.append(f"{entry.filename}: {error}")
            print(f"  FAILED   {entry.filename}   {error}")

    print(f"\n{converted} converted, {current} already current, "
          f"{invalid} invalid, {missing} not downloaded yet.")

    if problems:
        print("\nProblems:")
        for problem in problems:
            print(f"  {problem}")
        print(
            "\nAn invalid source in Raw/ is a file that should never have been\n"
            "written. Delete it and run verify to find out what its URL really\n"
            "answers with."
        )

    return 0 if invalid == 0 else 1


# ----------------------------------------------------------------------
#  Reporting
# ----------------------------------------------------------------------

def command_status(manifest: Manifest) -> int:
    lock = load_lock()

    present = absent = 0
    total_bytes = 0

    by_category: dict[str, list[Entry]] = {}

    for entry in manifest.entries:
        by_category.setdefault(entry.category, []).append(entry)

    print(f"{len(manifest.entries)} entries in the manifest.\n")

    for category in sorted(by_category):
        print(f"{category}")

        for entry in by_category[category]:
            record = lock["assets"].get(entry.id, {})

            if entry.raw_path.exists():
                size = entry.raw_path.stat().st_size
                total_bytes += size
                present += 1
                mark = f"raw {size:>8,}b"
            elif record.get("softError"):
                absent += 1
                mark = f"--  {'bad name':>9}"
            else:
                absent += 1
                http = record.get("httpStatus")
                mark = f"--  {'unchecked' if http is None else f'http {http}':>9}"

            imported = "png" if entry.imported_path.exists() else "   "
            print(f"   {mark}  {imported}  {entry.describe()}")

        print()

    print(f"{present} present, {absent} missing, {total_bytes / 1024:,.1f} KiB on disk.")

    if absent:
        print("\nRun `verify` to check the URLs, then `fetch`.")

    return 0


COMMANDS = {
    "verify": command_verify,
    "fetch": command_fetch,
    "convert": command_convert,
    "status": command_status,
}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=[*COMMANDS, "all"], nargs="?", default="status")
    parser.add_argument("--manifest", type=Path, default=MANIFEST)
    arguments = parser.parse_args()

    manifest = load_manifest(arguments.manifest)

    if arguments.command == "all":
        command_verify(manifest)
        print()
        code = command_fetch(manifest)
        print()
        command_convert(manifest)
        print()
        command_status(manifest)
        return code

    return COMMANDS[arguments.command](manifest)


if __name__ == "__main__":
    sys.exit(main())
