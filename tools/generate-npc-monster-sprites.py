#!/usr/bin/env python3
"""Generate NPC + monster walk sheets (96×128) in-repo.

NPC: extract Eldiran CC0 row 9 (brown villager) already vendored next to
the player generator. Same 3-frame / 4-dir layout as player-walk.

Monster: original procedural green slime (stdlib zlib only). Not Eldiran,
no external packs, never Graal.

No downloads. Never embeds Graal sheets.
"""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

SHEET_REL = Path("tools") / "third_party" / "eldiran" / "RPGCharacterSprites32x32.png"
CELL = 32
# Brown villager, distinct from player row-4 blue knight.
NPC_SRC_ROW = 9
WALK_SRC = {
    0: (0, 1, 2),    # down
    1: (8, 9, 10),   # left (flipped right)
    2: (8, 9, 10),   # right
    3: (4, 5, 6),    # up
}
WALK_FLIP_ROWS = {1}
WALK_COLS = 3
WALK_ROWS = 4
MAGENTA = (255, 0, 255)
SHEET_YELLOW = (255, 216, 0)
DARK_OUTLINE = (26, 18, 14)

SLIME_BODY = (46, 168, 72, 255)
SLIME_DARK = (28, 110, 48, 255)
SLIME_HI = (96, 210, 110, 255)
SLIME_BELLY = (170, 220, 130, 255)
SLIME_EYE_W = (240, 245, 230, 255)
SLIME_EYE = (26, 18, 14, 255)
SLIME_OUT = (18, 40, 22, 255)


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


def flip_h(cell: list[list[tuple[int, int, int, int]]]) -> list[list[tuple[int, int, int, int]]]:
    return [list(reversed(row)) for row in cell]


def empty_sheet(cols: int, rows: int) -> list[list[tuple[int, int, int, int]]]:
    clear = (0, 0, 0, 0)
    return [[clear] * (cols * CELL) for _ in range(rows * CELL)]


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


def extract_npc_walk(
    sheet: list[list[tuple[int, int, int, int]]],
) -> tuple[list[list[tuple[int, int, int, int]]], list[list[tuple[int, int, int, int]]]]:
    composed = empty_sheet(WALK_COLS, WALK_ROWS)
    idle_south = None
    for row, src_cols in WALK_SRC.items():
        for col, src_col in enumerate(src_cols):
            cell = extract_cell(sheet, src_col, NPC_SRC_ROW)
            if row in WALK_FLIP_ROWS:
                cell = flip_h(cell)
            blit(composed, cell, col * CELL, row * CELL)
            if row == 0 and col == 1:
                idle_south = cell
    assert idle_south is not None
    return composed, idle_south


def _set(px, x, y, color) -> None:
    if 0 <= y < CELL and 0 <= x < CELL:
        px[y][x] = color


def _fill_ellipse(px, cx, cy, rx, ry, color) -> None:
    rx2 = max(1, rx * rx)
    ry2 = max(1, ry * ry)
    for y in range(cy - ry, cy + ry + 1):
        for x in range(cx - rx, cx + rx + 1):
            dx, dy = x - cx, y - cy
            if (dx * dx) / rx2 + (dy * dy) / ry2 <= 1.05:
                _set(px, x, y, color)


def slime_cell(facing: int, col: int) -> list[list[tuple[int, int, int, int]]]:
    """4-dir / 3-frame blob. col 0/2 = squash + shift; idle = planted middle."""
    px = [[(0, 0, 0, 0)] * CELL for _ in range(CELL)]
    shift = -2 if col == 0 else (2 if col == 2 else 0)
    squash = 1 if col != 1 else 0
    cx = 16 + shift
    cy = 21 + squash
    rx = 10 - squash
    ry = 8 - squash
    _fill_ellipse(px, cx, cy, rx + 1, ry + 1, SLIME_OUT)
    _fill_ellipse(px, cx, cy, rx, ry, SLIME_DARK)
    _fill_ellipse(px, cx, cy - 1, rx - 1, ry - 1, SLIME_BODY)
    _fill_ellipse(px, cx, cy + 2, rx - 3, ry - 4, SLIME_BELLY)
    _fill_ellipse(px, cx - 3, cy - 3, 3, 2, SLIME_HI)

    if facing == 3:  # up — small back spots, no face
        _set(px, cx - 2, cy - 4, SLIME_OUT)
        _set(px, cx + 2, cy - 4, SLIME_OUT)
    else:
        if facing == 1:  # left
            ex0, ex1 = cx - 4, cx - 1
        elif facing == 2:  # right
            ex0, ex1 = cx + 1, cx + 4
        else:  # down
            ex0, ex1 = cx - 3, cx + 3
        ey = cy - 2
        for ex in (ex0, ex1):
            _set(px, ex, ey, SLIME_EYE_W)
            _set(px, ex + 1, ey, SLIME_EYE_W)
            _set(px, ex, ey + 1, SLIME_EYE_W)
            _set(px, ex + 1, ey + 1, SLIME_EYE)
        if col != 1:
            # moving "foot" highlight
            _fill_ellipse(px, cx + shift, cy + ry - 1, 3, 2, SLIME_DARK)
    return px


def extract_monster_walk() -> tuple[list[list[tuple[int, int, int, int]]], list[list[tuple[int, int, int, int]]]]:
    composed = empty_sheet(WALK_COLS, WALK_ROWS)
    idle_south = None
    for row in range(WALK_ROWS):
        for col in range(WALK_COLS):
            cell = slime_cell(row, col)
            blit(composed, cell, col * CELL, row * CELL)
            if row == 0 and col == 1:
                idle_south = cell
    assert idle_south is not None
    return composed, idle_south


def nearest_scale(src: list[list[tuple[int, int, int, int]]], factor: int) -> list[list[tuple[int, int, int, int]]]:
    h, w = len(src), len(src[0])
    return [[src[y // factor][x // factor] for x in range(w * factor)] for y in range(h * factor)]


def write_cycle_preview(path: Path, walk: list[list[tuple[int, int, int, int]]]) -> None:
    pad = 4
    cycle_w = WALK_COLS * CELL + (WALK_COLS + 1) * pad
    cycle_h = WALK_ROWS * CELL + (WALK_ROWS + 1) * pad
    checker_a, checker_b = (40, 44, 52, 255), (28, 32, 40, 255)
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
    write_png(path, nearest_scale(cycle, 4))


def main() -> None:
    repo = Path(__file__).resolve().parents[1]
    sheet_path = repo / SHEET_REL
    world = repo / "Frog.Client" / "Assets" / "World"
    width, height, sheet = read_png_rgba(sheet_path)
    if width != 384 or height != 672:
        raise ValueError(f"unexpected sheet size {width}×{height}")

    npc_walk, npc_idle = extract_npc_walk(sheet)
    monster_walk, monster_idle = extract_monster_walk()
    write_png(world / "npc.png", npc_idle)
    write_png(world / "npc-walk.png", npc_walk)
    write_png(world / "monster.png", monster_idle)
    write_png(world / "monster-walk.png", monster_walk)
    print(
        f"wrote npc-walk + monster-walk 96×128 "
        f"(npc row={NPC_SRC_ROW}, slime procedural) "
        f"npc-walk={ (world / 'npc-walk.png').stat().st_size }B "
        f"monster-walk={ (world / 'monster-walk.png').stat().st_size }B"
    )

    shot_dir = Path("/opt/cursor/artifacts/screenshots")
    shot_dir.mkdir(parents=True, exist_ok=True)
    write_cycle_preview(shot_dir / "npc-walk-mvp-4dir-3frame.png", npc_walk)
    write_cycle_preview(shot_dir / "monster-walk-mvp-4dir-3frame.png", monster_walk)
    grass = (120, 160, 100, 255)
    preview = [[grass for _ in range(96)] for _ in range(64)]
    blit(preview, npc_idle, 16 - 16, 48 - CELL + 1)
    blit(preview, monster_idle, 48 - 16, 48 - CELL + 1)
    write_png(shot_dir / "npc-monster-idle-on-grass.png", nearest_scale(preview, 4))
    print(f"wrote previews in {shot_dir}")


if __name__ == "__main__":
    main()
