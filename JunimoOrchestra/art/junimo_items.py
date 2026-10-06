"""The three Junimo items' inventory icons: what you craft (free, once the Community Center is restored) and place to get
a classical Junimo, a Junimo musician or a band Junimo instead of a plain block.

    python3 junimo_items.py [Junimo.png]      # Junimo.png = the game's Characters/Junimo.png

Writes assets/textures/items.png, three 16x16 icons side by side (Data/Objects sprite index 0, 1 and 2):

    0 classical Junimo   the classical set's Junimo (junimo_classical.py) in concert dress, cheering, in the violin's pink
    1 Junimo musician    the orchestra's Junimo (tools/orchestra_sprites.py) in the game's own green and the same dress,
                         a quaver over its shoulder
    2 band Junimo        the modern set's Junimo (junimo_blocks.py) in the trumpet's blue, cheering, in the jazz
                         trumpeter's shades: no dress

All are the game's own frame 0, tinted as the game tints it; the first two with the orchestra's top hat and red bow tie.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path
from PIL import Image

from junimo_blocks import DEFAULT_JUNIMO, FAMILY, ROOT, WEAR, WEAR_PAL, junimo16, lighten, tint
from junimo_classical import DRESS
from px import zoom

GREEN = (120, 220, 120, 255)                       # OrchestraArt's default tint, the game's own Junimo green
NOTE, NOTE_D = (246, 196, 64, 255), (150, 98, 24, 255)

# a quaver over its shoulder: the musician is the one that's played for us (x, y in the 16x16 frame)
QUAVER = {(13, 0): NOTE, (13, 1): NOTE, (13, 2): NOTE, (13, 3): NOTE, (14, 1): NOTE, (15, 2): NOTE,
          (11, 3): NOTE, (12, 3): NOTE, (11, 4): NOTE_D, (12, 4): NOTE_D, (14, 0): NOTE}


def dressed(base: Image.Image, colour, arms: tuple[str, ...] = ()) -> Image.Image:
    j = tint(junimo16(base, 'open', arms), colour)
    for part in ('hat', 'tie'):
        for (x, y), c in DRESS[part].items():
            j.putpixel((x, y), c)
    return j


def main() -> None:
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(os.environ.get('JUNIMO_PNG', DEFAULT_JUNIMO))
    base = Image.open(path).convert('RGBA').crop((0, 0, 16, 16))
    sheet = Image.new('RGBA', (48, 16), (0, 0, 0, 0))

    sheet.alpha_composite(dressed(base, lighten(FAMILY['violin'][1]), ('upL', 'upR')), (0, 0))

    musician = dressed(base, GREEN, ('L', 'R'))
    for (x, y), c in QUAVER.items():
        musician.putpixel((x, y), c)
    sheet.alpha_composite(musician, (16, 0))

    band = tint(junimo16(base, 'open', ('upL', 'upR')), lighten(FAMILY['trumpet'][1]))
    top, rows = WEAR['trumpet']
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch not in '. ':
                band.putpixel((x, top + y), WEAR_PAL[ch])
    sheet.alpha_composite(band, (32, 0))

    out = ROOT / 'assets/textures/items.png'
    sheet.save(out)
    zoom(sheet, 8).save(ROOT / 'art/preview/items_x8.png')
    print(out)


if __name__ == '__main__':
    main()
