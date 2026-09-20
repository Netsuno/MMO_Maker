#!/usr/bin/env python3
"""Generate original CC0-style procedural prefab PNGs (sofa + fence kit).

No downloads. Never embeds Graal sheets. Output is deterministic RGBA PNG
under assets/prefabs/. Pillow is not required — stdlib zlib only.
"""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets" / "prefabs"

TRANSPARENT = (0, 0, 0, 0)
WOOD_DARK = (86, 54, 32, 255)
WOOD_MID = (132, 86, 48, 255)
WOOD_LIGHT = (176, 122, 72, 255)
CUSHION = (168, 72, 78, 255)
CUSHION_HI = (196, 104, 108, 255)
SEAT = (148, 92, 58, 255)
OUTLINE = (32, 20, 14, 255)
METAL = (90, 96, 104, 255)


def _chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def write_png(path: Path, width: int, height: int, pixels: list[list[tuple[int, int, int, int]]]) -> None:
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw.extend(pixels[y][x])
    compressed = zlib.compress(bytes(raw), 9)
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + _chunk(b"IHDR", ihdr) + _chunk(b"IDAT", compressed) + _chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def blank(w: int, h: int) -> list[list[tuple[int, int, int, int]]]:
    return [[TRANSPARENT for _ in range(w)] for _ in range(h)]


def set_px(px: list[list[tuple[int, int, int, int]]], x: int, y: int, color: tuple[int, int, int, int]) -> None:
    if 0 <= y < len(px) and 0 <= x < len(px[0]):
        px[y][x] = color


def fill_rect(px, x0, y0, x1, y1, color) -> None:
    for y in range(y0, y1):
        for x in range(x0, x1):
            set_px(px, x, y, color)


def rect_outline(px, x0, y0, x1, y1, color) -> None:
    for x in range(x0, x1):
        set_px(px, x, y0, color)
        set_px(px, x, y1 - 1, color)
    for y in range(y0, y1):
        set_px(px, x0, y, color)
        set_px(px, x1 - 1, y, color)


def sofa_south() -> list[list[tuple[int, int, int, int]]]:
    w, h = 64, 32
    px = blank(w, h)
    fill_rect(px, 4, 6, 60, 28, WOOD_MID)
    fill_rect(px, 6, 4, 58, 16, WOOD_DARK)  # backrest
    fill_rect(px, 8, 14, 30, 26, CUSHION)
    fill_rect(px, 34, 14, 56, 26, CUSHION)
    fill_rect(px, 10, 16, 28, 20, CUSHION_HI)
    fill_rect(px, 36, 16, 54, 20, CUSHION_HI)
    fill_rect(px, 8, 24, 56, 28, SEAT)
    fill_rect(px, 8, 27, 12, 31, WOOD_DARK)
    fill_rect(px, 52, 27, 56, 31, WOOD_DARK)
    rect_outline(px, 4, 4, 60, 28, OUTLINE)
    return px


def sofa_north() -> list[list[tuple[int, int, int, int]]]:
    w, h = 64, 32
    px = blank(w, h)
    fill_rect(px, 4, 8, 60, 30, WOOD_MID)
    fill_rect(px, 6, 16, 58, 30, WOOD_DARK)
    fill_rect(px, 8, 10, 56, 18, SEAT)
    fill_rect(px, 8, 6, 12, 10, WOOD_DARK)
    fill_rect(px, 52, 6, 56, 10, WOOD_DARK)
    rect_outline(px, 4, 8, 60, 30, OUTLINE)
    return px


def sofa_east() -> list[list[tuple[int, int, int, int]]]:
    w, h = 32, 64
    px = blank(w, h)
    fill_rect(px, 6, 4, 28, 60, WOOD_MID)
    fill_rect(px, 16, 6, 28, 58, WOOD_DARK)
    fill_rect(px, 8, 8, 18, 30, CUSHION)
    fill_rect(px, 8, 34, 18, 56, CUSHION)
    fill_rect(px, 10, 10, 16, 16, CUSHION_HI)
    fill_rect(px, 10, 36, 16, 42, CUSHION_HI)
    fill_rect(px, 6, 8, 10, 12, WOOD_DARK)
    fill_rect(px, 6, 52, 10, 56, WOOD_DARK)
    rect_outline(px, 6, 4, 28, 60, OUTLINE)
    return px


def sofa_west() -> list[list[tuple[int, int, int, int]]]:
    w, h = 32, 64
    px = blank(w, h)
    fill_rect(px, 4, 4, 26, 60, WOOD_MID)
    fill_rect(px, 4, 6, 16, 58, WOOD_DARK)
    fill_rect(px, 14, 8, 24, 30, CUSHION)
    fill_rect(px, 14, 34, 24, 56, CUSHION)
    fill_rect(px, 16, 10, 22, 16, CUSHION_HI)
    fill_rect(px, 16, 36, 22, 42, CUSHION_HI)
    fill_rect(px, 22, 8, 26, 12, WOOD_DARK)
    fill_rect(px, 22, 52, 26, 56, WOOD_DARK)
    rect_outline(px, 4, 4, 26, 60, OUTLINE)
    return px


def fence_post() -> list[list[tuple[int, int, int, int]]]:
    w = h = 32
    px = blank(w, h)
    fill_rect(px, 12, 4, 20, 28, WOOD_MID)
    fill_rect(px, 11, 3, 21, 7, WOOD_LIGHT)
    fill_rect(px, 13, 7, 15, 26, WOOD_LIGHT)
    fill_rect(px, 12, 26, 20, 30, WOOD_DARK)
    rect_outline(px, 12, 4, 20, 28, OUTLINE)
    set_px(px, 15, 5, METAL)
    return px


def fence_h() -> list[list[tuple[int, int, int, int]]]:
    w = h = 32
    px = blank(w, h)
    fill_rect(px, 1, 10, 31, 15, WOOD_MID)
    fill_rect(px, 1, 18, 31, 23, WOOD_MID)
    fill_rect(px, 1, 11, 31, 13, WOOD_LIGHT)
    fill_rect(px, 1, 19, 31, 21, WOOD_LIGHT)
    fill_rect(px, 0, 6, 5, 28, WOOD_DARK)
    fill_rect(px, 27, 6, 32, 28, WOOD_DARK)
    rect_outline(px, 1, 10, 31, 15, OUTLINE)
    rect_outline(px, 1, 18, 31, 23, OUTLINE)
    return px


def fence_v() -> list[list[tuple[int, int, int, int]]]:
    w = h = 32
    px = blank(w, h)
    fill_rect(px, 10, 1, 15, 31, WOOD_MID)
    fill_rect(px, 18, 1, 23, 31, WOOD_MID)
    fill_rect(px, 11, 1, 13, 31, WOOD_LIGHT)
    fill_rect(px, 19, 1, 21, 31, WOOD_LIGHT)
    fill_rect(px, 6, 0, 26, 5, WOOD_DARK)
    fill_rect(px, 6, 27, 26, 32, WOOD_DARK)
    rect_outline(px, 10, 1, 15, 31, OUTLINE)
    rect_outline(px, 18, 1, 23, 31, OUTLINE)
    return px


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    write_png(OUT / "sofa-south.png", 64, 32, sofa_south())
    write_png(OUT / "sofa-north.png", 64, 32, sofa_north())
    write_png(OUT / "sofa-east.png", 32, 64, sofa_east())
    write_png(OUT / "sofa-west.png", 32, 64, sofa_west())
    write_png(OUT / "fence-post.png", 32, 32, fence_post())
    write_png(OUT / "fence-h.png", 32, 32, fence_h())
    write_png(OUT / "fence-v.png", 32, 32, fence_v())
    print(f"wrote prefab placeholders under {OUT}")


if __name__ == "__main__":
    main()
