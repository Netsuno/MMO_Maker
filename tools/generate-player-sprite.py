#!/usr/bin/env python3
"""Author an original 16×16 top-down player PNG (CC0) for Frog.Client.

No third-party pack. No download. Pixel map below is the drawing.
Classic chunky top-down MMO silhouette (Graal-inspired look only).
"""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

# Palette — cloak / hair / body, readable on grass. Never gold-ball.
PALETTE: dict[str, tuple[int, int, int, int]] = {
    ".": (0, 0, 0, 0),
    "K": (26, 18, 14, 255),  # outline
    "H": (92, 46, 20, 255),  # hair
    "L": (138, 74, 34, 255),  # hair light
    "S": (232, 184, 136, 255),  # skin
    "D": (196, 144, 104, 255),  # skin shadow
    "E": (42, 24, 16, 255),  # eyes
    "C": (48, 78, 122, 255),  # cloak (slate)
    "N": (32, 52, 84, 255),  # cloak shadow
    "P": (78, 110, 150, 255),  # cloak highlight
    "T": (90, 118, 52, 255),  # tunic (olive)
    "U": (64, 86, 38, 255),  # tunic fold
    "A": (122, 86, 42, 255),  # belt
    "B": (58, 36, 24, 255),  # boots
    "F": (107, 72, 48, 255),  # boot face
}

# South-facing hooded cloak + hair + body. 16 columns × 16 rows.
PIXELS = [
    "................",  # 16
    "....KKKKKKKK....",
    "...KHHHHHHHHK...",
    "..KHHLLLLLLHHK..",
    "..KHLLSSSSLLHK..",
    "..KHLSESSSELHK..",
    "...KHSSDDSSHK...",
    "..KNNCCPPCCNNK..",
    ".KNCCCTTTTCCCNK.",
    "KNCCCTTTTTTCCCNK",
    "KNCCTTTAAATTCCNK",
    ".KNCCCTTTTTCCNK.",
    "..KNNCCCTTCCNNK.",
    "...KNBBKKBBNK...",
    "....KBFKKFBK....",
    ".....KK..KK.....",
]


def _chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def write_png(path: Path, rows: list[str]) -> None:
    height = len(rows)
    width = len(rows[0])
    if any(len(r) != width for r in rows):
        raise ValueError("ragged pixel map")
    raw = bytearray()
    for row in rows:
        raw.append(0)  # filter None
        for ch in row:
            raw.extend(PALETTE[ch])
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n"
    png += _chunk(b"IHDR", ihdr)
    png += _chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += _chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def main() -> None:
    repo = Path(__file__).resolve().parents[1]
    dest = repo / "Frog.Client" / "Assets" / "World" / "player.png"
    write_png(dest, PIXELS)
    print(f"wrote {dest} ({dest.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
