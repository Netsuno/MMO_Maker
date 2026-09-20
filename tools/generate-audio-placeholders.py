#!/usr/bin/env python3
"""Generate original CC0 placeholder WAV stubs for the FRoG audio MVP.

No downloads. No ripped assets. Stdlib math + struct only.
Outputs (checked in so CI / packaged client need no generator):

  Frog.Client/Assets/Audio/ui-click.wav
  Frog.Client/Assets/Audio/music-loop.wav
"""
from __future__ import annotations

import math
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Frog.Client" / "Assets" / "Audio"
SAMPLE_RATE = 22050


def write_wav_mono16(path: Path, samples: list[int]) -> None:
    data = b"".join(struct.pack("<h", max(-32768, min(32767, s))) for s in samples)
    header = struct.pack(
        "<4sI4s4sIHHIIHH4sI",
        b"RIFF",
        36 + len(data),
        b"WAVE",
        b"fmt ",
        16,
        1,
        1,
        SAMPLE_RATE,
        SAMPLE_RATE * 2,
        2,
        16,
        b"data",
        len(data),
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(header + data)


def tone(freq: float, n: int, amp: float, phase: float = 0.0) -> list[float]:
    return [
        amp * math.sin(2.0 * math.pi * freq * i / SAMPLE_RATE + phase)
        for i in range(n)
    ]


def click() -> list[int]:
    """Two short decaying pops (~90 ms). Integer-ish cycles, original."""
    n = int(SAMPLE_RATE * 0.09)
    samples = [0.0] * n
    bursts = ((0.004, 1400.0, 0.55), (0.045, 880.0, 0.40))
    burst_n = int(SAMPLE_RATE * 0.018)
    for start_s, freq, amp in bursts:
        start = int(start_s * SAMPLE_RATE)
        for i in range(burst_n):
            idx = start + i
            if idx >= n:
                break
            env = math.exp(-i / (SAMPLE_RATE * 0.006))
            samples[idx] += amp * env * math.sin(2.0 * math.pi * freq * i / SAMPLE_RATE)
    return [int(round(x * 32767)) for x in samples]


def music_loop() -> list[int]:
    """2 s seamless pad: 220 Hz + 330 Hz (integer cycles at 22050 Hz)."""
    seconds = 2
    n = SAMPLE_RATE * seconds
    a = tone(220.0, n, 0.18)
    b = tone(330.0, n, 0.10, phase=math.pi / 5)
    mixed = [a[i] + b[i] for i in range(n)]
    return [int(round(x * 32767)) for x in mixed]


def main() -> None:
    click_path = OUT / "ui-click.wav"
    music_path = OUT / "music-loop.wav"
    write_wav_mono16(click_path, click())
    write_wav_mono16(music_path, music_loop())
    print(f"wrote {click_path} ({click_path.stat().st_size} bytes)")
    print(f"wrote {music_path} ({music_path.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
