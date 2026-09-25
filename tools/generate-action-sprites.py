#!/usr/bin/env python3
"""Attack + death sheets (96×128) from the walk sheets already in the repo.

Same 3 columns × 4 rows as WalkClock (down, left, right, up). Cells stay 32×32.
Attack is a short lunge along the facing. Death staggers, kneels, then lies
along that facing. Player layers (body, head, tunic, armor, hat, weapon) get
the same shift so the paperdoll composite stays registered. The strike frame
pushes the weapon two extra pixels.

Source pixels are the existing Eldiran / slime walk sheets. No downloads.
Never embeds Graal sheets. No interface-gold fill.
"""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

CELL = 32
COLS = 3
ROWS = 4
# Row order matches Frog.Core.Enums.Direction: Down, Left, Right, Up.
FACING = ((0, 1), (-1, 0), (1, 0), (0, -1))
# Wind-up, strike, recover — pixels along the facing.
ATTACK_MAG = (-2, 3, 1)
WEAPON_STRIKE_EXTRA = 2
CLEAR = (0, 0, 0, 0)


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
            width, height, bit, color, _comp, _filt, inter = struct.unpack(">IIBBBBB", chunk)
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


def empty_sheet() -> list[list[tuple[int, int, int, int]]]:
    return [[CLEAR] * (COLS * CELL) for _ in range(ROWS * CELL)]


def empty_cell() -> list[list[tuple[int, int, int, int]]]:
    return [[CLEAR] * CELL for _ in range(CELL)]


def crop(sheet: list[list[tuple[int, int, int, int]]], col: int, row: int) -> list[list[tuple[int, int, int, int]]]:
    x0, y0 = col * CELL, row * CELL
    return [line[x0 : x0 + CELL] for line in sheet[y0 : y0 + CELL]]


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


def shift(cell: list[list[tuple[int, int, int, int]]], dx: int, dy: int) -> list[list[tuple[int, int, int, int]]]:
    out = empty_cell()
    for y, row in enumerate(cell):
        for x, px in enumerate(row):
            if px[3] == 0:
                continue
            nx, ny = x + dx, y + dy
            if 0 <= nx < CELL and 0 <= ny < CELL:
                out[ny][nx] = px
    return out


def squash_down(cell: list[list[tuple[int, int, int, int]]]) -> list[list[tuple[int, int, int, int]]]:
    """Kneel: pull pixels toward the feet (bottom of the cell)."""
    out = empty_cell()
    for y, row in enumerate(cell):
        ny = 31 - ((31 - y) * 5 // 8)
        for x, px in enumerate(row):
            if px[3] == 0:
                continue
            out[ny][x] = px
    return out


def lay_flat(
    cell: list[list[tuple[int, int, int, int]]],
    fx: int,
    fy: int,
) -> list[list[tuple[int, int, int, int]]]:
    """Fallen body along the facing, still inside the 32×32 cell."""
    pts = [(x, y, px) for y, row in enumerate(cell) for x, px in enumerate(row) if px[3]]
    out = empty_cell()
    if not pts:
        return out
    minx = min(p[0] for p in pts)
    maxx = max(p[0] for p in pts)
    miny = min(p[1] for p in pts)
    maxy = max(p[1] for p in pts)
    bw = max(1, maxx - minx)
    bh = max(1, maxy - miny)
    cx = (minx + maxx) / 2
    for x, y, px in pts:
        along = (maxy - y) / bh
        across = (x - cx) / bw
        if fx != 0:
            nx = int(round(16 + along * 13 * fx))
            ny = int(round(27 + across * 9))
        elif fy > 0:
            nx = int(round(16 + across * 10))
            ny = int(round(18 + along * 12))
        else:
            nx = int(round(16 + across * 10))
            ny = int(round(28 - along * 12))
        if 0 <= nx < CELL and 0 <= ny < CELL:
            out[ny][nx] = px
    return out


def opaque_count(cell: list[list[tuple[int, int, int, int]]]) -> int:
    return sum(1 for row in cell for px in row if px[3])


def attack_sheet(
    walk: list[list[tuple[int, int, int, int]]],
    weapon: bool = False,
) -> list[list[tuple[int, int, int, int]]]:
    out = empty_sheet()
    for row, (fx, fy) in enumerate(FACING):
        for col, mag in enumerate(ATTACK_MAG):
            extra = WEAPON_STRIKE_EXTRA if weapon and col == 1 else 0
            cell = shift(crop(walk, col, row), fx * (mag + extra), fy * (mag + extra))
            if opaque_count(cell) < 8 and opaque_count(crop(walk, col, row)) >= 8:
                raise ValueError(f"attack cell row={row} col={col} clipped away")
            blit(out, cell, col * CELL, row * CELL)
    return out


def death_sheet(walk: list[list[tuple[int, int, int, int]]]) -> list[list[tuple[int, int, int, int]]]:
    out = empty_sheet()
    for row, (fx, fy) in enumerate(FACING):
        idle = crop(walk, 1, row)
        frames = (
            shift(idle, -fx, -fy + 1),
            squash_down(shift(idle, fx, max(fy, 0) + 1)),
            lay_flat(idle, fx, fy),
        )
        for col, cell in enumerate(frames):
            if opaque_count(idle) >= 8 and opaque_count(cell) < 8:
                raise ValueError(f"death cell row={row} col={col} lost the sprite")
            blit(out, cell, col * CELL, row * CELL)
    return out


def transform(world: Path, src_name: str, attack_name: str, death_name: str, weapon: bool = False) -> None:
    src = world / src_name
    width, height, walk = read_png_rgba(src)
    if (width, height) != (COLS * CELL, ROWS * CELL):
        raise ValueError(f"{src_name} is {width}×{height}, expected 96×128")
    attack = attack_sheet(walk, weapon=weapon)
    death = death_sheet(walk)
    write_png(world / attack_name, attack)
    write_png(world / death_name, death)
    print(f"wrote {attack_name} + {death_name} from {src_name}")


def main() -> None:
    repo = Path(__file__).resolve().parents[1]
    world = repo / "Frog.Client" / "Assets" / "World"
    pairs = (
        ("player-walk.png", "player-attack.png", "player-death.png", False),
        ("player-walk-body.png", "player-attack-body.png", "player-death-body.png", False),
        ("player-walk-head.png", "player-attack-head.png", "player-death-head.png", False),
        ("player-walk-tunic.png", "player-attack-tunic.png", "player-death-tunic.png", False),
        ("player-walk-armor.png", "player-attack-armor.png", "player-death-armor.png", False),
        ("player-walk-hat.png", "player-attack-hat.png", "player-death-hat.png", False),
        ("player-walk-weapon.png", "player-attack-weapon.png", "player-death-weapon.png", True),
        ("npc-walk.png", "npc-attack.png", "npc-death.png", False),
        ("monster-walk.png", "monster-attack.png", "monster-death.png", False),
    )
    for src, attack, death, weapon in pairs:
        transform(world, src, attack, death, weapon)


if __name__ == "__main__":
    main()
