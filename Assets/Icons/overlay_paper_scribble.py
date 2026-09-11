#!/usr/bin/env python3
"""Bake ink scribbles onto paper.png → paper_written.png (ink clipped to parchment alpha)."""
from __future__ import annotations

import math
import random
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError as e:
    raise SystemExit("Pillow required: pip install Pillow") from e

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "paper.png"
OUT = ROOT / "paper_written.png"

# Only keep ink where parchment alpha is above this (0–255).
PAPER_ALPHA_MIN = 140


def main() -> None:
    if not SRC.is_file():
        raise SystemExit(f"Missing {SRC}")

    img = Image.open(SRC).convert("RGBA")
    w, h = img.size
    paper_a = img.split()[3]

    bbox = paper_a.point(lambda a: 255 if a >= PAPER_ALPHA_MIN else 0).getbbox()
    if not bbox:
        raise SystemExit("No opaque parchment found in paper.png")
    left, top, right, bottom = bbox
    pad_x = int((right - left) * 0.10)
    pad_y = int((bottom - top) * 0.10)
    margin_l = left + pad_x
    margin_r = right - pad_x
    margin_t = top + pad_y
    margin_b = bottom - pad_y

    overlay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    rng = random.Random(42)

    ink = (32, 28, 22, 200)
    line_count = 9
    stroke_w = 2
    for i in range(line_count):
        y0 = margin_t + int((margin_b - margin_t) * (i + 0.5) / line_count)
        x0 = margin_l + rng.randint(0, max(1, (margin_r - margin_l) // 30))
        x1 = margin_r - rng.randint(0, max(1, (margin_r - margin_l) // 30))
        pts = []
        steps = 16
        for s in range(steps + 1):
            t = s / steps
            x = int(x0 + (x1 - x0) * t)
            y = int(
                y0
                + math.sin(t * math.pi * 2 + i) * ((bottom - top) * 0.006)
                + rng.uniform(-(bottom - top) * 0.004, (bottom - top) * 0.004)
            )
            pts.append((x, y))
        stroke_w = max(2, int((bottom - top) * 0.005 + rng.uniform(0, 1.2)))
        draw.line(pts, fill=ink, width=stroke_w, joint="curve")

    for _ in range(4):
        cx = rng.randint(margin_l, margin_r)
        cy = rng.randint(int(margin_t + (margin_b - margin_t) * 0.55), margin_b)
        r = rng.randint(int((right - left) * 0.03), int((right - left) * 0.07))
        draw.arc(
            [cx - r, cy - r // 2, cx + r, cy + r // 2],
            start=rng.randint(200, 250),
            end=rng.randint(300, 340),
            fill=ink,
            width=max(2, stroke_w),
        )

    # Zero ink alpha outside solid parchment (fixes border chunks / wrap bleed).
    o_r, o_g, o_b, o_a = overlay.split()
    clip = paper_a.point(lambda a: 255 if a >= PAPER_ALPHA_MIN else 0)
    o_a = Image.composite(o_a, Image.new("L", (w, h), 0), clip)
    overlay = Image.merge("RGBA", (o_r, o_g, o_b, o_a))

    out = Image.alpha_composite(img, overlay)
    # Preserve original parchment alpha exactly (no ink extending the silhouette).
    out.putalpha(paper_a)
    out.save(OUT, optimize=True)
    print(f"Wrote {OUT} ({w}x{h}) ink clipped to parchment alpha>={PAPER_ALPHA_MIN}")


if __name__ == "__main__":
    main()
