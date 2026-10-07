"""Derives the written sheet (ink baked once) and both square icons from textures/paper_sheet.png."""
import math
import random
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "textures" / "paper_sheet.png"
OUT = ROOT / "textures"
OUT.mkdir(parents=True, exist_ok=True)

sheet = Image.open(SRC).convert("RGBA")
# paper_sheet.png is the source art; everything below is derived from it.

# Written: ink lines like overlay_paper_scribble.py, clipped to the parchment.
w, h = sheet.size
alpha = sheet.split()[3]
left, top, right, bottom = alpha.point(lambda a: 255 if a >= 140 else 0).getbbox()
ml, mr = left + int((right - left) * 0.11), right - int((right - left) * 0.11)
mt, mb = top + int((bottom - top) * 0.10), bottom - int((bottom - top) * 0.12)
ink_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
draw = ImageDraw.Draw(ink_layer)
rng = random.Random(7)
ink = (38, 30, 22, 215)
lines = 12
for i in range(lines):
    y0 = mt + int((mb - mt) * (i + 0.5) / lines)
    x0 = ml + rng.randint(0, (mr - ml) // 25)
    # Some lines end early, like paragraphs.
    x1 = mr - rng.randint(0, (mr - ml) // 25) - (rng.randint((mr - ml) // 4, (mr - ml) // 2) if i % 4 == 3 else 0)
    pts = []
    for s in range(25):
        t = s / 24
        pts.append((int(x0 + (x1 - x0) * t),
                    int(y0 + math.sin(t * math.pi * 3 + i) * (bottom - top) * 0.004 + rng.uniform(-1.2, 1.2))))
    draw.line(pts, fill=ink, width=max(2, int((bottom - top) * 0.004 + rng.uniform(0, 1))), joint="curve")
# A signature flourish at the bottom right.
cx, cy = mr - (mr - ml) // 5, mb + (bottom - mb) // 3
draw.arc([cx - 60, cy - 14, cx + 40, cy + 14], 190, 350, fill=ink, width=3)
draw.line([(cx - 50, cy + 8), (cx + 55, cy + 2)], fill=ink, width=2)

clip = alpha.point(lambda a: 255 if a >= 140 else 0)
r, g, b, a = ink_layer.split()
ink_layer = Image.merge("RGBA", (r, g, b, Image.composite(a, Image.new("L", (w, h), 0), clip)))
written = Image.alpha_composite(sheet, ink_layer)
written.putalpha(alpha)
written.save(OUT / "paper_sheet_written.png", optimize=True)


def icon(img, name, tilt):
    size = 256
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    scale = (size * 0.86) / max(img.size)
    art = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS).rotate(tilt, Image.BICUBIC, expand=True)
    x, y = (size - art.width) // 2, (size - art.height) // 2
    # Soft drop shadow so the sheet reads on the dark inventory slots.
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 150), (x + 5, y + 6), art.split()[3])
    canvas = Image.alpha_composite(canvas, shadow.filter(ImageFilter.GaussianBlur(5)))
    canvas.alpha_composite(art, (x, y))
    canvas.save(OUT / name, optimize=True)


icon(sheet, "paper_icon.png", 4)
icon(written, "paper_written_icon.png", -4)
for f in sorted(OUT.iterdir()):
    print(f.name, Image.open(f).size, f.stat().st_size)
