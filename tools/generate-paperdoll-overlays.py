#!/usr/bin/env python3
"""Original 32×32 paperdoll overlays for the FRoG chibi (Netsun).

Not Eldiran cells and never a Graal sheet. No downloads.

Layers (same grid as player-walk-*.png, 3 cols × 4 rows):
  armor  — green chest on the torso (server Armor slot; tunic stays empty)
  hat    — crimson crown + brim drawn on top of the head
  weapon — short blade in the free pixels beside the body

South idle PNGs are the walk sheet's row 0, column 1 (planted frame).
Each walk cell is painted on that frame's silhouette, so the 3 columns already differ.
Marker fills (tests): hat (196,48,72), armor (42,138,78), blade (214,224,232).
"""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

CELL = 32
WALK_COLS = 3
WALK_ROWS = 4
CLEAR = (0, 0, 0, 0)
HAT = (196, 48, 72, 255)
HAT_EDGE = (120, 24, 40, 255)
ARMOR = (42, 138, 78, 255)
ARMOR_EDGE = (16, 84, 44, 255)
BLADE = (214, 224, 232, 255)
BLADE_EDGE = (48, 56, 68, 255)
HILT = (122, 74, 36, 255)
MARKERS = {HAT[:3], ARMOR[:3], BLADE[:3]}


def _chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def read_png_rgba(path: Path) -> tuple[int, int, list[list[tuple[int, int, int, int]]]]:
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"not a PNG: {path}")
    pos = 8
    width = height = None
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


def empty_cell() -> list[list[tuple[int, int, int, int]]]:
    return [[CLEAR for _ in range(CELL)] for _ in range(CELL)]


def crop(sheet: list[list[tuple[int, int, int, int]]], col: int, row: int) -> list[list[tuple[int, int, int, int]]]:
    y0, x0 = row * CELL, col * CELL
    return [line[x0 : x0 + CELL] for line in sheet[y0 : y0 + CELL]]


def opaque(px: tuple[int, int, int, int]) -> bool:
    return px[3] != 0


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


def nearest_scale(src: list[list[tuple[int, int, int, int]]], factor: int) -> list[list[tuple[int, int, int, int]]]:
    h, w = len(src), len(src[0])
    return [[src[y // factor][x // factor] for x in range(w * factor)] for y in range(h * factor)]


def paint_hat(canvas: list[list[tuple[int, int, int, int]]], head: list[list[tuple[int, int, int, int]]]) -> None:
    """Crimson crown on helmet rows 0–3, brim sticking out on row 4. Face stays."""
    for y in range(0, 4):
        xs = [x for x in range(CELL) if opaque(head[y][x])]
        if not xs:
            continue
        left, right = xs[0], xs[-1]
        for x in xs:
            canvas[y][x] = HAT_EDGE if x == left or x == right or y == 0 else HAT
    band = [x for x in range(CELL) if opaque(head[3][x]) or opaque(head[4][x])]
    if not band:
        return
    left, right = band[0], band[-1]
    for x in range(left - 2, right + 3):
        if not 0 <= x < CELL or opaque(head[4][x]):
            continue
        canvas[4][x] = HAT_EDGE if x <= left - 2 or x >= right + 2 else HAT


def paint_armor(canvas: list[list[tuple[int, int, int, int]]], body: list[list[tuple[int, int, int, int]]]) -> None:
    """Green chest on the inner torso. Arms and legs stay the base body."""
    for y in range(16, 21):
        xs = [x for x in range(CELL) if opaque(body[y][x])]
        if len(xs) < 8:
            continue
        left, right = xs[0], xs[-1]
        inset = max(2, (right - left) // 4)
        inner = range(left + inset, right - inset + 1)
        painted = [x for x in inner if opaque(body[y][x])]
        if not painted:
            continue
        for x in painted:
            edge = x == painted[0] or x == painted[-1] or y == 16 or y == 20
            canvas[y][x] = ARMOR_EDGE if edge else ARMOR


def paint_weapon(
    canvas: list[list[tuple[int, int, int, int]]],
    body: list[list[tuple[int, int, int, int]]],
    head: list[list[tuple[int, int, int, int]]],
    facing_left: bool,
) -> None:
    """Blade in empty pixels beside the sprite. Left facing holds it on the left."""

    def free(x: int, y: int) -> bool:
        return 0 <= x < CELL and 0 <= y < CELL and not opaque(body[y][x]) and not opaque(head[y][x])

    xs = [x for y in range(CELL) for x in range(CELL) if opaque(body[y][x])]
    if not xs:
        return
    if facing_left:
        candidates = [min(xs) - 2, min(xs) - 3, 0]
    else:
        candidates = [max(xs) + 2, max(xs) + 1, CELL - 2]
    cols: list[int] = []
    for base in candidates:
        trial = [base, base + 1] if not facing_left else [base, base + 1]
        trial = [c for c in trial if 0 <= c < CELL]
        if not trial:
            continue
        free_rows = sum(1 for y in range(8, 22) if any(free(c, y) for c in trial))
        if free_rows >= 8:
            cols = trial
            break
    if len(cols) < 1:
        return
    for y in range(8, 22):
        for i, x in enumerate(cols):
            if free(x, y):
                canvas[y][x] = BLADE_EDGE if i == 0 else BLADE
    for y in (22, 23):
        for x in cols:
            if free(x, y):
                canvas[y][x] = HILT


def count_color(cell: list[list[tuple[int, int, int, int]]], rgb: tuple[int, int, int]) -> int:
    return sum(1 for row in cell for px in row if px[:3] == rgb and px[3] != 0)


def assert_cell(
    hat: list[list[tuple[int, int, int, int]]],
    armor: list[list[tuple[int, int, int, int]]],
    weapon: list[list[tuple[int, int, int, int]]],
    body: list[list[tuple[int, int, int, int]]],
    head: list[list[tuple[int, int, int, int]]],
    label: str,
) -> None:
    if count_color(hat, HAT[:3]) < 12:
        raise SystemExit(f"{label}: hat fill too small")
    if count_color(armor, ARMOR[:3]) < 8:
        raise SystemExit(f"{label}: armor fill too small")
    if count_color(weapon, BLADE[:3]) < 4:
        raise SystemExit(f"{label}: blade fill too small")
    covered = 0
    for y in range(CELL):
        for x in range(CELL):
            if opaque(hat[y][x]) and opaque(head[y][x]):
                covered += 1
            if opaque(armor[y][x]) and not opaque(body[y][x]):
                raise SystemExit(f"{label}: armor outside the body at {x},{y}")
            if opaque(weapon[y][x]) and (opaque(body[y][x]) or opaque(head[y][x])):
                raise SystemExit(f"{label}: weapon covers the body at {x},{y}")
    if covered < 16:
        raise SystemExit(f"{label}: hat does not sit on the head ({covered} px)")


def blank_sheet() -> list[list[tuple[int, int, int, int]]]:
    return [[CLEAR for _ in range(WALK_COLS * CELL)] for _ in range(WALK_ROWS * CELL)]


def paste_cell(
    sheet: list[list[tuple[int, int, int, int]]],
    cell: list[list[tuple[int, int, int, int]]],
    col: int,
    row: int,
) -> None:
    blit(sheet, cell, col * CELL, row * CELL)


def base_has_marker(sheet: list[list[tuple[int, int, int, int]]], name: str) -> None:
    for row in sheet:
        for px in row:
            if px[3] and px[:3] in MARKERS:
                raise SystemExit(f"{name} already uses a paperdoll marker color {px[:3]}")


def composite_idle(
    body: list[list[tuple[int, int, int, int]]],
    head: list[list[tuple[int, int, int, int]]],
    armor: list[list[tuple[int, int, int, int]]] | None,
    hat: list[list[tuple[int, int, int, int]]] | None,
    weapon: list[list[tuple[int, int, int, int]]] | None,
) -> list[list[tuple[int, int, int, int]]]:
    """body → (tunic empty) → armor → head → hat → weapon."""
    out = empty_cell()
    blit(out, body, 0, 0)
    if armor is not None:
        blit(out, armor, 0, 0)
    blit(out, head, 0, 0)
    if hat is not None:
        blit(out, hat, 0, 0)
    if weapon is not None:
        blit(out, weapon, 0, 0)
    return out


def main() -> None:
    repo = Path(__file__).resolve().parents[1]
    world = repo / "Frog.Client" / "Assets" / "World"
    _bw, _bh, body_sheet = read_png_rgba(world / "player-walk-body.png")
    _hw, _hh, head_sheet = read_png_rgba(world / "player-walk-head.png")
    if (_bw, _bh) != (WALK_COLS * CELL, WALK_ROWS * CELL):
        raise SystemExit(f"unexpected body sheet {_bw}×{_bh}")
    base_has_marker(body_sheet, "player-walk-body.png")
    base_has_marker(head_sheet, "player-walk-head.png")

    hat_sheet = blank_sheet()
    armor_sheet = blank_sheet()
    weapon_sheet = blank_sheet()
    for row in range(WALK_ROWS):
        for col in range(WALK_COLS):
            body = crop(body_sheet, col, row)
            head = crop(head_sheet, col, row)
            hat, armor, weapon = empty_cell(), empty_cell(), empty_cell()
            paint_hat(hat, head)
            paint_armor(armor, body)
            paint_weapon(weapon, body, head, facing_left=(row == 1))
            assert_cell(hat, armor, weapon, body, head, f"r{row}c{col}")
            paste_cell(hat_sheet, hat, col, row)
            paste_cell(armor_sheet, armor, col, row)
            paste_cell(weapon_sheet, weapon, col, row)

    idle_hat = crop(hat_sheet, 1, 0)
    idle_armor = crop(armor_sheet, 1, 0)
    idle_weapon = crop(weapon_sheet, 1, 0)
    write_png(world / "player-walk-hat.png", hat_sheet)
    write_png(world / "player-walk-armor.png", armor_sheet)
    write_png(world / "player-walk-weapon.png", weapon_sheet)
    write_png(world / "player-hat.png", idle_hat)
    write_png(world / "player-armor.png", idle_armor)
    write_png(world / "player-weapon.png", idle_weapon)
    print("wrote original paperdoll overlays (hat, armor, weapon) idle 32 + walk 96×128")

    body_idle = crop(body_sheet, 1, 0)
    head_idle = crop(head_sheet, 1, 0)
    bare = composite_idle(body_idle, head_idle, None, None, None)
    geared = composite_idle(body_idle, head_idle, idle_armor, idle_hat, idle_weapon)
    # Hat must replace crown pixels (drawn after the head).
    replaced = sum(
        1
        for y in range(CELL)
        for x in range(CELL)
        if opaque(head_idle[y][x]) and geared[y][x][:3] == HAT[:3]
    )
    if replaced < 16:
        raise SystemExit(f"preview hat did not cover the head ({replaced})")

    grass = (120, 160, 100, 255)
    pad = 8
    preview_w = CELL * 2 + pad * 3
    preview_h = CELL + pad * 2
    preview = [[grass for _ in range(preview_w)] for _ in range(preview_h)]
    blit(preview, bare, pad, pad)
    blit(preview, geared, pad * 2 + CELL, pad)
    shot = Path("/opt/cursor/artifacts/screenshots")
    shot.mkdir(parents=True, exist_ok=True)
    write_png(shot / "paperdoll-mvp-bare-vs-equipped.png", nearest_scale(preview, 6))

    strip_w = WALK_COLS * CELL + (WALK_COLS + 1) * 4
    strip_h = WALK_ROWS * CELL + (WALK_ROWS + 1) * 4
    checker_a, checker_b = (40, 44, 52, 255), (28, 32, 40, 255)
    strip = [
        [checker_a if ((x // 4) + (y // 4)) % 2 == 0 else checker_b for x in range(strip_w)]
        for y in range(strip_h)
    ]
    for row in range(WALK_ROWS):
        for col in range(WALK_COLS):
            cell = composite_idle(
                crop(body_sheet, col, row),
                crop(head_sheet, col, row),
                crop(armor_sheet, col, row),
                crop(hat_sheet, col, row),
                crop(weapon_sheet, col, row),
            )
            blit(strip, cell, 4 + col * (CELL + 4), 4 + row * (CELL + 4))
    write_png(shot / "paperdoll-mvp-4dir-equipped.png", nearest_scale(strip, 4))
    print(f"wrote previews in {shot}")


if __name__ == "__main__":
    main()
