"""Particle sprites (assets/textures/fx.png): 9x9 cells, white bodies with a soft grey outline so the
game can tint them with a block's accent colour."""
from __future__ import annotations

from pathlib import Path

from PIL import Image

from px import grid, outline

ROOT = Path(__file__).resolve().parent.parent
CELL = 9

BODY = (255, 255, 255, 255)
SHADE = (214, 214, 226, 255)
EDGE = (92, 80, 104, 255)

SHAPES = {
    'note': ["..WW.",
             "..WSW",
             "..W.W",
             "..W..",
             "WWW..",
             "WWS..",
             ".S..."],
    'notes': ["...WWWW",
              "...WSSW",
              "...W..W",
              "...W..W",
              ".WWW.WW",
              "WWSWWWS",
              ".S...S."],
    'sparkle': ["..W..",
                "..W..",
                "WWSWW",
                "..W..",
                "..W.."],
    'heart': [".WW.WW.",
              "WWWWWSW",
              "WWWWWSW",
              ".WWWSW.",
              "..WSW..",
              "...W..."],
}
ORDER = ['note', 'notes', 'sparkle', 'heart']


def build() -> Image.Image:
    sheet = Image.new('RGBA', (CELL * len(ORDER), CELL), (0, 0, 0, 0))
    for i, name in enumerate(ORDER):
        body = grid(SHAPES[name], {'W': BODY, 'S': SHADE})
        framed = Image.new('RGBA', (body.width + 2, body.height + 2), (0, 0, 0, 0))
        framed.alpha_composite(body, (1, 1))
        framed = outline(framed, EDGE)
        ox = i * CELL + (CELL - framed.width) // 2
        oy = (CELL - framed.height) // 2
        sheet.alpha_composite(framed, (ox, oy))
    return sheet


if __name__ == '__main__':
    out = ROOT / 'assets' / 'textures' / 'fx.png'
    build().save(out)
    build().resize((CELL * len(ORDER) * 8, CELL * 8), Image.NEAREST).save(ROOT / 'preview' / 'fx_x8.png') if (ROOT / 'preview').exists() else None
    print('wrote', out)
