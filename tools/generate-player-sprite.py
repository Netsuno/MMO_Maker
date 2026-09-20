#!/usr/bin/env python3
"""Extract FRoG player PNGs from Eldiran's CC0 32×32 sheet (already vendored).

Source: tools/third_party/eldiran/RPGCharacterSprites32x32.png
OpenGameArt: https://opengameart.org/content/32x32-rpg-character-sprites (CC0)

South idle (compat): column 1, row 4 — blue knight stand.
Walk MVP: cols 0–2 × rows 4–7 (down / left / right / up, 3-frame cycle).
Each cell is split body (rows 14–31) + head (rows 0–13) so tunic/armor/weapon
can overlay later. Magenta chroma → transparent. Sheet yellow → dark 1px.
No downloads. Never embeds Graal sheets.
"""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

SHEET_REL = Path("tools") / "third_party" / "eldiran" / "RPGCharacterSprites32x32.png"
CELL = 32
COL = 1
ROW = 4
# Blue knight block on the vendored sheet (RPG Maker-style 3×4).
WALK_COL0 = 0
WALK_ROW0 = 4
WALK_COLS = 3
WALK_ROWS = 4  # down, left, right, up — matches Frog.Core.Enums.Direction
MAGENTA = (255, 0, 255)
SHEET_YELLOW = (255, 216, 0)
DARK_OUTLINE = (26, 18, 14)
# Rows 0–13 = helmet/face; 14–31 = torso/legs. Equipment slots overlay between later.
HEAD_ROWS = 14


def _chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def read_png_rgba(path: Path) -> tuple[int, int, list[list[tuple[int, int, int, int]]]]:
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"not a PNG: {path}")
    pos = 8
    width = height = color = None
    idat = b""
    while pos < len(data):
        length = int.from_bytes(data[pos : pos + 4], "big")
        tag = data[pos + 4 : pos + 8]
        chunk = data[pos + 8 : pos + 8 + length]
        pos += 12 + length
        if tag == b"IHDR":
            width, height, bit, color, comp, filt, inter = struct.unpack(">IIBBBBB", chunk)
            if bit != 8 or color != 6 or inter != 0:
                raise ValueError(f"need 8-bit RGBA non-interlaced, got {bit}/{color}/{inter}")
        elif tag == b"IDAT":
            idat += chunk
        elif tag == b"IEND":
            break
    assert width is not None and height is not None
    raw = zlib.decompress(idat)
    bpp = 4
    stride = width * bpp
    rows: list[bytearray] = []
    i = 0
    prev = bytearray(stride)
    for _y in range(height):
        f = raw[i]
        i += 1
        row = bytearray(raw[i : i + stride])
        i += stride
        if f == 1:
            for x in range(stride):
                left = row[x - bpp] if x >= bpp else 0
                row[x] = (row[x] + left) & 255
        elif f == 2:
            for x in range(stride):
                row[x] = (row[x] + prev[x]) & 255
        elif f == 3:
            for x in range(stride):
                left = row[x - bpp] if x >= bpp else 0
                row[x] = (row[x] + ((left + prev[x]) // 2)) & 255
        elif f == 4:
            for x in range(stride):
                a = row[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                row[x] = (row[x] + pr) & 255
        elif f != 0:
            raise ValueError(f"unsupported PNG filter {f}")
        rows.append(row)
        prev = row
    pixels: list[list[tuple[int, int, int, int]]] = []
    for y in range(height):
        line = []
        row = rows[y]
        for x in range(width):
            o = x * bpp
            line.append((row[o], row[o + 1], row[o + 2], row[o + 3]))
        pixels.append(line)
    return width, height, pixels


def write_png(path: Path, pixels: list[list[tuple[int, int, int, int]]]) -> None:
    height = len(pixels)
    width = len(pixels[0])
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for r, g, b, a in row:
            raw.extend((r, g, b, a))
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n"
    png += _chunk(b"IHDR", ihdr)
    png += _chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += _chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def split_body_head(
    cell: list[list[tuple[int, int, int, int]]],
) -> tuple[list[list[tuple[int, int, int, int]]], list[list[tuple[int, int, int, int]]]]:
    clear = (0, 0, 0, 0)
    body = [[clear] * CELL for _ in range(CELL)]
    head = [[clear] * CELL for _ in range(CELL)]
    for y in range(CELL):
        for x in range(CELL):
            if y < HEAD_ROWS:
                head[y][x] = cell[y][x]
            else:
                body[y][x] = cell[y][x]
    return body, head


def extract_cell(sheet: list[list[tuple[int, int, int, int]]], col: int, row: int) -> list[list[tuple[int, int, int, int]]]:
    x0, y0 = col * CELL, row * CELL
    out: list[list[tuple[int, int, int, int]]] = []
    for y in range(CELL):
        line = []
        for x in range(CELL):
            r, g, b, a = sheet[y0 + y][x0 + x]
            if (r, g, b) == MAGENTA:
                line.append((0, 0, 0, 0))
                continue
            if (r, g, b) == SHEET_YELLOW:
                r, g, b = DARK_OUTLINE
            line.append((r, g, b, 255))
        out.append(line)
    return out


def nearest_scale(src: list[list[tuple[int, int, int, int]]], factor: int) -> list[list[tuple[int, int, int, int]]]:
    h, w = len(src), len(src[0])
    return [[src[y // factor][x // factor] for x in range(w * factor)] for y in range(h * factor)]


def tint_other(px: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    r, g, b, a = px
    if a == 0:
        return px
    nr = min(255, int(0.70 * r))
    ng = min(255, int(0.80 * g + 0.04 * 255))
    nb = min(255, int(1.20 * b + 0.16 * 255))
    return (nr, ng, nb, a)


def blit(
    dst: list[list[tuple[int, int, int, int]]],
    src: list[list[tuple[int, int, int, int]]],
    left: int,
    top: int,
) -> None:
    dh, dw = len(dst), len(dst[0])
    for y, row in enumerate(src):
        sy = top + y
        if sy < 0 or sy >= dh:
            continue
        for x, px in enumerate(row):
            sx = left + x
            if sx < 0 or sx >= dw or px[3] == 0:
                continue
            dst[sy][sx] = px


def empty_sheet(cols: int, rows: int) -> list[list[tuple[int, int, int, int]]]:
    clear = (0, 0, 0, 0)
    return [[clear] * (cols * CELL) for _ in range(rows * CELL)]


def extract_walk_sheets(
    sheet: list[list[tuple[int, int, int, int]]],
) -> tuple[
    list[list[tuple[int, int, int, int]]],
    list[list[tuple[int, int, int, int]]],
    list[list[tuple[int, int, int, int]]],
    list[list[tuple[int, int, int, int]]],
]:
    composed = empty_sheet(WALK_COLS, WALK_ROWS)
    body_sheet = empty_sheet(WALK_COLS, WALK_ROWS)
    head_sheet = empty_sheet(WALK_COLS, WALK_ROWS)
    idle_south = None
    for row in range(WALK_ROWS):
        for col in range(WALK_COLS):
            cell = extract_cell(sheet, WALK_COL0 + col, WALK_ROW0 + row)
            body, head = split_body_head(cell)
            blit(composed, cell, col * CELL, row * CELL)
            blit(body_sheet, body, col * CELL, row * CELL)
            blit(head_sheet, head, col * CELL, row * CELL)
            if row == 0 and col == 1:
                idle_south = cell
    assert idle_south is not None
    return composed, body_sheet, head_sheet, idle_south


def main() -> None:
    repo = Path(__file__).resolve().parents[1]
    sheet_path = repo / SHEET_REL
    world = repo / "Frog.Client" / "Assets" / "World"
    dest = world / "player.png"
    width, height, sheet = read_png_rgba(sheet_path)
    if width != 384 or height != 672:
        raise ValueError(f"unexpected sheet size {width}×{height}")
    walk, walk_body, walk_head, idle = extract_walk_sheets(sheet)
    body, head = split_body_head(idle)
    write_png(dest, idle)
    write_png(world / "player-body.png", body)
    write_png(world / "player-head.png", head)
    write_png(world / "player-walk.png", walk)
    write_png(world / "player-walk-body.png", walk_body)
    write_png(world / "player-walk-head.png", walk_head)
    print(
        f"wrote {dest} + body/head + walk 96×128 sheets "
        f"({dest.stat().st_size} bytes) idle col={COL} row={ROW}"
    )

    grass = (120, 160, 100, 255)
    preview_h, preview_w = 64, 64
    preview = [[grass for _ in range(preview_w)] for _ in range(preview_h)]
    # Feet / bottom-center on tile centers (16,48) and (48,48); destY = cy - 32 + 1.
    blit(preview, idle, 16 - 16, 48 - CELL + 1)
    other = [[tint_other(px) for px in row] for row in idle]
    blit(preview, other, 48 - 16, 48 - CELL + 1)
    shot_dir = Path("/opt/cursor/artifacts/screenshots")
    shot_dir.mkdir(parents=True, exist_ok=True)
    write_png(shot_dir / "player-skin-v2-local-and-other-on-grass.png", preview)
    write_png(shot_dir / "player-skin-v2-native-32-nearest-x8.png", nearest_scale(idle, 8))
    strip_w, strip_h = CELL * 3 + 8, CELL
    checker_a, checker_b = (40, 44, 52, 255), (28, 32, 40, 255)
    strip = [
        [checker_a if ((x // 4) + (y // 4)) % 2 == 0 else checker_b for x in range(strip_w)]
        for y in range(strip_h)
    ]
    blit(strip, body, 0, 0)
    blit(strip, head, CELL + 4, 0)
    blit(strip, body, (CELL + 4) * 2, 0)
    blit(strip, head, (CELL + 4) * 2, 0)
    write_png(shot_dir / "player-skin-v2-body-head-layers.png", nearest_scale(strip, 4))

    pad = 4
    cycle_w = WALK_COLS * CELL + (WALK_COLS + 1) * pad
    cycle_h = WALK_ROWS * CELL + (WALK_ROWS + 1) * pad
    cycle = [
        [checker_a if ((x // 4) + (y // 4)) % 2 == 0 else checker_b for x in range(cycle_w)]
        for y in range(cycle_h)
    ]
    for row in range(WALK_ROWS):
        for col in range(WALK_COLS):
            cell = [
                walk[row * CELL + y][col * CELL : col * CELL + CELL]
                for y in range(CELL)
            ]
            blit(cycle, cell, pad + col * (CELL + pad), pad + row * (CELL + pad))
    write_png(shot_dir / "player-walk-mvp-4dir-3frame.png", nearest_scale(cycle, 4))
    print(f"wrote previews in {shot_dir}")


if __name__ == "__main__":
    main()
