"""UI sprites for the tuning panel — theme: "Strawberry-milk music box".

Everything is authored in art pixels and drawn in-game at 4x (Game1.pixelZoom),
exactly like vanilla menus. Fixed-size pieces (panel body, stage) are pre-baked;
stateful widgets (buttons, keys, pills, knobs) are separate sprites.
"""
from __future__ import annotations

import math
from PIL import Image

from px import hex2rgba as C, grid, outline as px_outline

# ------------------------------------------------------------------ palette
P = {
    'berry': C('#6e2b4b'), 'berry2': C('#a13f68'),
    'rose': C('#e8789f'), 'rose2': C('#d05a84'), 'pink': C('#f7a9c5'), 'blush': C('#ffd6e4'),
    'milk': C('#fff7ee'), 'milk2': C('#f8e8d9'), 'milk3': C('#eed5c0'), 'milk4': C('#d9b8a0'),
    'ging1': C('#fff1f5'), 'ging2': C('#ffe3ed'), 'ging3': C('#fdd3e2'),
    'gold': C('#f5c75d'), 'gold2': C('#d4952f'), 'gold3': C('#ffeaa8'), 'goldD': C('#8a5a1c'),
    'ink': C('#5b2742'), 'ink2': C('#a0647f'), 'ink3': C('#c996ad'),
    'plum': C('#3b2140'), 'plum2': C('#4d2b52'), 'plum3': C('#633a67'),
    'cur': C('#d8436b'), 'cur2': C('#a82652'), 'cur3': C('#f47196'), 'curD': C('#6e1236'),
    'wood': C('#d39a62'), 'wood2': C('#aa6d3f'), 'wood3': C('#ecc08a'), 'woodD': C('#6b3a1e'),
    'mint': C('#9be3c0'), 'mint2': C('#62bf96'), 'mint3': C('#d5f7e6'), 'mintD': C('#2f6e58'),
    'keyW': C('#fffdf8'), 'keyW2': C('#f4e4d8'), 'keyO': C('#8c5d72'),
    'keyB': C('#3d2440'), 'keyB2': C('#5f3c62'), 'keyB3': C('#26132a'),
    'white': C('#ffffff'),
}
T = (0, 0, 0, 0)

# corner profiles: inset (in px) for each row from the rounded edge
CORNERS = {0: [], 1: [1], 2: [2, 1], 3: [3, 1, 1], 4: [4, 2, 1, 1], 5: [5, 3, 2, 1, 1], 6: [6, 4, 3, 2, 1, 1],
           7: [7, 5, 3, 2, 2, 1, 1], 8: [8, 5, 4, 3, 2, 1, 1, 1]}


def new(w, h, fill=T):
    return Image.new('RGBA', (w, h), fill)


def inside_rr(x, y, w, h, r) -> bool:
    prof = CORNERS[r]
    for yy, inset in ((y, None), (h - 1 - y, None)):
        pass
    ry = y if y < r else (h - 1 - y if y >= h - r else None)
    if ry is None:
        return 0 <= x < w
    inset = prof[ry] if ry < len(prof) else 0
    return inset <= x < w - inset


def fill_rr(im, x0, y0, w, h, r, col, only_top=False, only_bottom=False):
    px = im.load()
    for y in range(h):
        for x in range(w):
            ry = y if y < r else (h - 1 - y if y >= h - r else None)
            inset = 0
            if ry is not None and ry < len(CORNERS[r]):
                top = y < r
                if (only_top and not top) or (only_bottom and top):
                    inset = 0
                else:
                    inset = CORNERS[r][ry]
            if inset <= x < w - inset:
                X, Y = x0 + x, y0 + y
                if 0 <= X < im.width and 0 <= Y < im.height:
                    px[X, Y] = col


def rr_layers(im, x, y, w, h, r, layers, **kw):
    """layers: list of (inset, colour) drawn outside-in."""
    for inset, col in layers:
        rr = max(0, r - inset)
        fill_rr(im, x + inset, y + inset, w - 2 * inset, h - 2 * inset, rr, col, **kw)


def hline(im, x, y, w, col):
    px = im.load()
    for i in range(w):
        if 0 <= x + i < im.width and 0 <= y < im.height:
            px[x + i, y] = col


def vline(im, x, y, h, col):
    px = im.load()
    for i in range(h):
        if 0 <= x < im.width and 0 <= y + i < im.height:
            px[x, y + i] = col


def put(im, x, y, col):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.load()[x, y] = col


def stamp(im, rows, pal, x, y):
    im.alpha_composite(grid(rows, pal), (x, y))


# ------------------------------------------------------------------ backgrounds
def gingham(w, h, cell=4):
    im = new(w, h)
    px = im.load()
    for y in range(h):
        for x in range(w):
            sx = (x // cell) % 2 == 0
            sy = (y // cell) % 2 == 0
            px[x, y] = P['ging3'] if (sx and sy) else (P['ging2'] if (sx or sy) else P['ging1'])
    return im


def stitch_rr(im, x, y, w, h, r, col, dash=2, gap=2):
    """Dashed 'sewn' line tracing a rounded rect perimeter."""
    pts = []
    # walk perimeter clockwise with corner insets
    prof = CORNERS[r]

    def inset(ry):
        return prof[ry] if ry < len(prof) else 0
    top = [(xx, 0) for xx in range(inset(0), w - inset(0))]
    right = [(w - 1 - inset(min(yy, h - 1 - yy)) if min(yy, h - 1 - yy) < r else w - 1, yy) for yy in range(1, h - 1)]
    bottom = [(xx, h - 1) for xx in range(w - 1 - inset(0), inset(0) - 1, -1)]
    left = [(inset(min(yy, h - 1 - yy)) if min(yy, h - 1 - yy) < r else 0, yy) for yy in range(h - 2, 0, -1)]
    pts = top + right + bottom + left
    seen = set()
    k = 0
    for p in pts:
        if p in seen:
            continue
        seen.add(p)
        if k % (dash + gap) < dash:
            put(im, x + p[0], y + p[1], col)
        k += 1


def panel(w, h):
    im = new(w, h)
    r = 7
    rr_layers(im, 0, 0, w, h, r, [(0, P['berry']), (1, P['rose']), (4, P['berry2'])])
    g = gingham(w - 10, h - 10)
    mask = new(w - 10, h - 10)
    fill_rr(mask, 0, 0, w - 10, h - 10, 3, P['white'])
    im.paste(g, (5, 5), mask)
    # soft top light on border + bottom shade
    for xx in range(8, w - 8):
        put(im, xx, 1, P['cur3'])
        put(im, xx, h - 2, P['rose2'])
        put(im, xx, h - 3, P['rose2'])
    stitch_rr(im, 2, 2, w - 4, h - 4, 5, P['blush'])
    # shadow under the inner top edge (depth)
    for xx in range(8, w - 8):
        put(im, xx, 5, P['ging3'])
    return im


def card(w, h, r=4, fill='milk', edge='rose2', lace=True):
    im = new(w, h + 1)
    fill_rr(im, 0, 1, w, h, r, P['ging3'])  # drop shadow
    rr_layers(im, 0, 0, w, h, r, [(0, P[edge]), (1, P[fill])])
    hline(im, r, h - 2, w - 2 * r, P['milk2'])
    if lace:
        stitch_rr(im, 2, 2, w - 4, h - 4, max(0, r - 2), P['blush'], dash=1, gap=2)
    return im


def tab(w, h, selected: bool):
    im = new(w, h)
    if selected:
        rr_layers(im, 0, 0, w, h + 3, 3, [(0, P['rose2']), (1, P['milk'])], only_top=True)
        hline(im, 3, 1, w - 6, P['white'])
    else:
        rr_layers(im, 0, 2, w, h, 3, [(0, P['rose2']), (1, P['pink'])], only_top=True)
        hline(im, 3, 3, w - 6, P['blush'])
    return im


# ------------------------------------------------------------------ widgets
BTN = {
    'pink': ('berry', 'rose', 'rose2', 'pink', 'white'),
    'mint': ('mintD', 'mint', 'mint2', 'mint3', 'mintD'),
    'milk': ('berry2', 'milk', 'milk3', 'white', 'ink'),
    'gold': ('goldD', 'gold', 'gold2', 'gold3', 'goldD'),
}


def button(w, h, style='pink', state='normal', r=3):
    o, f, lip, hi, _ = BTN[style]
    im = new(w, h)
    dy = 1 if state == 'down' else 0
    body_h = h - (0 if state == 'down' else 0)
    rr_layers(im, 0, dy, w, body_h - dy, r, [(0, P[o]), (1, P[lip])])
    top_h = body_h - dy - (1 if state == 'down' else 3)
    fill_rr(im, 1, 1 + dy, w - 2, top_h - 1, max(0, r - 1), P[f])
    hline(im, r, 2 + dy, w - 2 * r - 2, P[hi])
    put(im, 2, 3 + dy, P[hi])
    if state == 'hover':
        fill_rr(im, 1, 1, w - 2, top_h - 1, max(0, r - 1), P[hi])
        fill_rr(im, 2, 3, w - 4, top_h - 4, max(0, r - 2), P[f])
        hline(im, r, 2, w - 2 * r - 2, P['white'])
    return im


def round_button(d, style='pink', state='normal'):
    return button(d, d, style, state, r=min(6, d // 2 - 1))


def pill(w, h, state='normal', accent=None):
    im = new(w, h)
    if state == 'selected':
        a = accent
        rr_layers(im, 0, 0, w, h, 3, [(0, a['O']), (1, a['L'])])
        hline(im, 3, 1, w - 6, a['h'])
        hline(im, 3, h - 2, w - 6, a['d'])
    elif state == 'hover':
        rr_layers(im, 0, 0, w, h, 3, [(0, P['rose2']), (1, P['blush'])])
        hline(im, 3, 1, w - 6, P['white'])
    else:
        rr_layers(im, 0, 0, w, h, 3, [(0, P['milk4']), (1, P['white'])])
        hline(im, 3, h - 2, w - 6, P['milk2'])
    return im


def slider_track(w):
    im = new(w, 7)
    rr_layers(im, 0, 0, w, 7, 3, [(0, P['berry2']), (1, P['milk2'])])
    hline(im, 3, 5, w - 6, P['milk'])
    return im


def slider_fill(w, accent):
    im = new(w, 7)
    rr_layers(im, 0, 0, w, 7, 3, [(0, P['berry2']), (1, accent['L'])])
    hline(im, 3, 2, w - 5, accent['h'])
    hline(im, 3, 5, w - 5, accent['m'])
    return im


HEART_KNOB = [
    "..OOO...OOO..",
    ".ORRRO.ORRRO.",
    "ORWWRRORRRRRO",
    "ORWRRRRRRRRRO",
    "ORRRRRRRRRRdO",
    ".ORRRRRRRRdO.",
    "..ORRRRRRdO..",
    "...ORRRRdO...",
    "....ORRdO....",
    ".....OdO.....",
    "......O......",
]


def heart_badge():
    return grid([".OO.OO.",
                 "ORWORRO",
                 "ORRRRRO",
                 ".ORRRO.",
                 "..ORO..",
                 "...O..."], {'O': P['berry'], 'R': C('#ff6f96'), 'W': P['white']})


def heart_knob():
    return grid(HEART_KNOB, {'O': P['berry'], 'R': C('#ff6f96'), 'W': P['white'], 'd': C('#d9406e')})


# piano keys ------------------------------------------------------
def white_key(w, h, state='normal', accent=None):
    im = new(w, h)
    fill = P['keyW']
    shade = P['keyW2']
    if state == 'hover':
        fill, shade = P['blush'], P['pink']
    elif state == 'selected':
        fill, shade = accent['h'], accent['L']
    elif state == 'down':
        fill, shade = P['pink'], P['rose']
    rr_layers(im, 0, -3, w, h + 3, 3, [(0, P['keyO']), (1, fill)], only_bottom=True)
    for yy in range(h - 5, h - 1):
        hline(im, 1, yy, w - 2, shade)
    fill_rr(im, 1, h - 5, w - 2, 4, 2, shade, only_bottom=True)
    hline(im, 1, h - 6, w - 2, P['white'] if state == 'normal' else fill)
    hline(im, 1, 0, w - 2, P['keyW2'])
    return im


def black_key(w, h, state='normal', accent=None):
    im = new(w, h)
    body, top, lip = P['keyB'], P['keyB2'], P['keyB3']
    if state == 'hover':
        body, top = P['plum3'], C('#8a5a8c')
    elif state == 'selected':
        body, top, lip = accent['m'], accent['h'], accent['O']
    rr_layers(im, 0, -2, w, h + 2, 2, [(0, P['keyB3']), (1, body)], only_bottom=True)
    fill_rr(im, 1, h - 4, w - 2, 3, 1, lip, only_bottom=True)
    vline(im, 1, 1, h - 6, top)
    hline(im, 1, h - 5, w - 2, top)
    return im


# ------------------------------------------------------------------ icons
ICONS_SRC = {
    'note': ["..OO.",
             "..OWO",
             "..O.O",
             "..O..",
             "OOO..",
             "OOO..",
             ".O..."],
    'notes': ["...OOOO",
              "...OWWO",
              "...O..O",
              "...O..O",
              ".OOO.OO",
              "OOOOOOO",
              ".O...O."],
    'speaker': ["....O...",
                "...OO.O.",
                "OOOWO..O",
                "OWWWO.OO",
                "OWWWO..O",
                "OOOWO.O.",
                "...OO...",
                "....O..."],
    'play': ["O....",
             "OO...",
             "OWO..",
             "OWWO.",
             "OWO..",
             "OO...",
             "O...."],
    'copy': [".OOOOO..",
             ".OWWWO..",
             "OOOOOWO.",
             "OWWWOOO.",
             "OWWWWO..",
             "OWWWWO..",
             "OWWWWO..",
             "OOOOOO.."],
    'paste': ["..OOO...",
              "OOWWWOO.",
              "OWOOOWO.",
              "OWWWWWO.",
              "OWOOOWO.",
              "OWWWWWO.",
              "OWOOWWO.",
              "OOOOOOO."],
    'check': ["......O",
              ".....OO",
              "O...OO.",
              "OO.OO..",
              ".OOO...",
              "..O...."],
    'reset': ["O.OOO..",               # back to the save's value: an arrow going round anticlockwise
              "OO...O.",
              "OOO...O",
              "......O",
              "O.....O",
              ".O...O.",
              "..OOO.."],
    'x': ["OO...OO",
          ".OO.OO.",
          "..OOO..",
          ".OO.OO.",
          "OO...OO"],
    'left': ["...O",
             "..OO",
             ".OOO",
             "OOOO",
             ".OOO",
             "..OO",
             "...O"],
    'right': ["O...",
              "OO..",
              "OOO.",
              "OOOO",
              "OOO.",
              "OO..",
              "O..."],
    'up': ["...O...",
           "..OOO..",
           ".OOOOO.",
           "OOOOOOO"],
    'down': ["OOOOOOO",
             ".OOOOO.",
             "..OOO..",
             "...O..."],
    'heart': [".OO.OO.",
              "OWOOOOO",
              "OOOOOOO",
              ".OOOOO.",
              "..OOO..",
              "...O..."],
    'sparkle': ["..O..",
                "..O..",
                "OOWOO",
                "..O..",
                "..O.."],
    'star': ["...O...",
             "..OWO..",
             "OOOWOOO",
             ".OWWWO.",
             "..OOO..",
             ".OO.OO.",
             ".O...O."],
    'keys': ["OOOOOOOOO",
             "OW.W.WW.O",
             "OW.W.WW.O",
             "OWWWWWWWO",
             "OWOWOWOWO",
             "OOOOOOOOO"],
    'dice': [".OOOOOO.",
             "OWWWWWWO",
             "OWOWWWWO",
             "OWWWOWWO",
             "OWWWWWWO",
             "OWWWWWOO",
             "OWWWWWWO",
             ".OOOOOO."],
    'stop': ["OOOOO",
             "OOOOO",
             "OOOOO",
             "OOOOO",
             "OOOOO"],
    'arrow_up': ["..O..",
                 ".OOO.",
                 "O.O.O",
                 "..O..",
                 "..O..",
                 "..O.."],
    'arrow_down': ["..O..",
                   "..O..",
                   "..O..",
                   "O.O.O",
                   ".OOO.",
                   "..O.."],
    'arrow_left': ["..O...",
                   ".O....",
                   "OOOOOO",
                   ".O....",
                   "..O..."],
    'arrow_right': ["...O..",
                    "....O.",
                    "OOOOOO",
                    "....O.",
                    "...O.."],
    'plus': ["..O..",
             "..O..",
             "OOOOO",
             "..O..",
             "..O.."],
    'minus': ["OOOOO"],
    'dot': ["OO", "OO"],
    'space': ["O.....O",
              "OOOOOOO"],
    'wave': ["..OO.....",
             ".O..O....",
             "O....O..O",
             "......OO."],
    'link': ["OOO......",
             "OWO......",
             "OOOOOO...",
             "....O....",
             "...OOOOO.",
             "....O.OWO",
             "......OOO"],
    'globe': ["..OOOOO..",
              ".OWOWOWO.",
              "OWWOWOWWO",
              "OOOOOOOOO",
              "OWWOWOWWO",
              ".OWOWOWO.",
              "..OOOOO.."],
    'mouse_l': [".OOOOO.",
                "OAAOWWO",
                "OAAOWWO",
                "OOOOOOO",
                "OWWWWWO",
                "OWWWWWO",
                ".OWWWO.",
                "..OOO.."],
    'mouse_r': [".OOOOO.",
                "OWWOAAO",
                "OWWOAAO",
                "OOOOOOO",
                "OWWWWWO",
                "OWWWWWO",
                ".OWWWO.",
                "..OOO.."],
    'person': ["..OO..",
               "..OO..",
               ".OOOO.",
               "O.OO.O",
               "..OO..",
               ".O..O.",
               ".O..O."],
    'chicken': ["..OO....",
                ".OWOO...",
                "OOWWO...",
                "..OWWOOO",
                "..OWWWWO",
                "...OWWO.",
                "....OO..",
                "....O.O."],
    'trash': [".OOOOO.",
              "OOOOOOO",
              ".OWOWO.",
              ".OWOWO.",
              ".OWOWO.",
              ".OOOOO."],
    'rest4': ["..O..",
              "...O.",
              "..OO.",
              ".OO..",
              "..O..",
              "...O.",
              "..OO.",
              ".O...",
              "O...."],
    'sn1': [".OOO.",
            "O...O",
            "O...O",
            ".OOO."],
    'sn2': ["...O",
            "...O",
            "...O",
            "...O",
            ".OOO",
            "O..O",
            ".OO."],
    'sn4': ["...O",
            "...O",
            "...O",
            "...O",
            ".OOO",
            "OOOO",
            ".OO."],
    'sn8': ["...O.",
            "...OO",
            "...O.",
            "...O.",
            ".OOO.",
            "OOOO.",
            ".OO.."],
    'sn16': ["...O.",
             "...OO",
             "...OO",
             "...O.",
             ".OOO.",
             "OOOO.",
             ".OO.."],
    'three': ["OOO",
              "..O",
              ".OO",
              "..O",
              "OOO"],
    'dotdot': ["OO",
               "OO"],
    'walk': ["..OO..",
             "..OO..",
             ".OOOO.",
             "O.OO.O",
             "..OO..",
             ".O..O.",
             "O....O"],
}

# note-value glyphs (length / delay) drawn in ink
NOTE_VALUES = {
    'n16': ["....O..",
            "....OO.",
            "....O.O",
            "....OO.",
            "....O.O",
            "....O..",
            ".OOOO..",
            "OOOOO..",
            ".OOO..."],
    'n8': ["....O..",
           "....OO.",
           "....O.O",
           "....O..",
           "....O..",
           "....O..",
           ".OOOO..",
           "OOOOO..",
           ".OOO..."],
    'n4': ["....O",
           "....O",
           "....O",
           "....O",
           "....O",
           "....O",
           ".OOOO",
           "OOOOO",
           ".OOO."],
    'n2': ["....O",
           "....O",
           "....O",
           "....O",
           "....O",
           "....O",
           ".OOOO",
           "O...O",
           ".OOO."],
    'n1': [".....",
           ".....",
           ".....",
           ".....",
           ".....",
           ".OOO.",
           "O...O",
           "O...O",
           ".OOO."],
    'hold': [".......",
             ".......",
             "..OOO..",
             ".O...O.",
             "O.....O",
             ".......",
             "...O...",
             "..OOO..",
             "...O..."],
}


def icon(name, col='ink', hi='white', acc=None):
    src = ICONS_SRC.get(name) or NOTE_VALUES[name]
    pal = {'O': P[col] if isinstance(col, str) else col, 'W': P[hi] if isinstance(hi, str) else hi}
    pal['A'] = acc if acc is not None else P['rose']
    return grid(src, pal)


def knob(accent):
    return grid(["..OOO..",
                 ".OHLLO.",
                 "OHLLLLO",
                 "OLLLLdO",
                 "OLLLLdO",
                 ".OddO..",
                 "..OOO.."], {'O': accent['O'], 'H': P['white'], 'L': accent['L'], 'd': accent['m']})


def slot(w, h, state='normal', accent=None, beat=False):
    im = new(w, h)
    if state == 'off':
        rr_layers(im, 0, 0, w, h, 2, [(0, P['milk3'])])
        rr_layers(im, 1, 1, w - 2, h - 2, 1, [(0, P['milk'])])
        for yy in range(2, h - 2, 2):
            put(im, w // 2, yy, P['milk3'])
        return im
    edge = accent['O'] if state == 'selected' else P['milk4']
    fill = P['white'] if not beat else P['ging1']
    rr_layers(im, 0, 0, w, h, 2, [(0, edge), (1, fill)])
    if state == 'selected':
        rr_layers(im, 1, 1, w - 2, h - 2, 1, [(0, accent['h'])])
        rr_layers(im, 2, 2, w - 4, h - 4, 0, [(0, P['white'])])
    return im


def keycap(inner: str, w=11):
    im = new(w, 10)
    rr_layers(im, 0, 0, w, 10, 2, [(0, P['ink3']), (1, P['white'])])
    hline(im, 1, 8, w - 2, P['milk3'])
    g = icon(inner, 'ink2')
    im.alpha_composite(g, ((w - g.width) // 2, (8 - g.height) // 2 + (1 if g.height < 6 else 0)))
    return im


def toggle(on: bool):
    im = new(19, 10)
    o, f = ('mintD', 'mint') if on else ('milk4', 'milk2')
    rr_layers(im, 0, 0, 19, 10, 4, [(0, P[o]), (1, P[f])])
    kx = 10 if on else 1
    k = new(8, 8)
    rr_layers(k, 0, 0, 8, 8, 3, [(0, P[o]), (1, P['white'])])
    im.alpha_composite(k, (kx, 1))
    return im


# ------------------------------------------------------------------ staff & clefs
TREBLE = [
    "....OO.",
    "...O..O",
    "...O..O",
    "...O.O.",
    "...OO..",
    "..OO...",
    ".OO....",
    "OO..O..",
    "O..OOO.",
    "O.O.O.O",
    "O.O.O.O",
    ".O..O.O",
    "..OOOO.",
    "....O..",
    "....O..",
    "..OOO..",
    "..OO...",
]
BASS = [
    ".OOO...",
    "O...O.O",
    "OO..O..",
    "OO..O.O",
    "...O...",
    "..O....",
    ".O.....",
    "O......",
]
NOTEHEAD = [
    ".OOO.",
    "OOOOO",
    "OOOOO",
    ".OOO.",
]
SHARP = [
    ".O.O.",
    "OOOOO",
    ".O.O.",
    "OOOOO",
    ".O.O.",
]


# ------------------------------------------------------------------ stage
def stage(w, h, seed=7):
    im = new(w, h)
    # proscenium: gold arch frame
    rr_layers(im, 0, 0, w, h, 8, [(0, P['goldD']), (1, P['gold']), (3, P['gold2']), (4, P['plum'])], only_top=True)
    for xx in range(9, w - 9):
        put(im, xx, 1, P['gold3'])
    px = im.load()
    # backdrop: soft light cone from the top
    cx = w / 2
    fy0 = h - 13
    for y in range(5, fy0):
        t = (y - 5) / (fy0 - 5)
        hw = 4 + t * 12
        for x in range(5, w - 5):
            if px[x, y] != P['plum']:
                continue
            d = abs(x + 0.5 - cx)
            if d < hw - 2:
                px[x, y] = P['plum3']
            elif d < hw and (x + y) % 2 == 0:
                px[x, y] = P['plum2']
            elif d < hw + 2 and (x + y) % 4 == 0:
                px[x, y] = P['plum2']
    # twinkles
    import random
    rnd = random.Random(seed)
    for _ in range(14):
        x, y = rnd.randrange(12, w - 12), rnd.randrange(8, h - 22)
        if px[x, y] in (P['plum'], P['plum2']):
            px[x, y] = P['gold3'] if rnd.random() < 0.5 else P['blush']
    # floor planks
    fy = h - 13
    for y in range(fy, h - 4):
        for x in range(4, w - 4):
            px[x, y] = P['wood']
        if (y - fy) % 3 == 0:
            hline(im, 4, y, w - 8, P['wood2'])
    for y in range(fy, h - 4):
        for x in range(4, w - 4):
            if (x + (y - fy) // 3 * 7) % 13 == 0 and (y - fy) % 3 != 0:
                px[x, y] = P['wood2']
    hline(im, 4, fy, w - 8, P['wood3'])
    hline(im, 4, fy + 1, w - 8, P['wood3'])
    # spotlight pool on floor
    for y in range(fy + 1, h - 5):
        for x in range(4, w - 4):
            d = ((x - cx) / (w * 0.30)) ** 2 + ((y - (fy + 5)) / 4.0) ** 2
            if d < 1 and px[x, y] == P['wood']:
                px[x, y] = P['wood3']
    hline(im, 4, h - 5, w - 8, P['woodD'])
    # curtains (left & right) with folds, gathered by a gold tie
    def curtain(side):
        for y in range(4, h - 4):
            t = (y - 4) / (h - 8)
            # width: wide at top, pinched at tie (~0.55), flares at bottom
            if t < 0.55:
                cw = 11 - int(5 * (t / 0.55) ** 1.4)
            else:
                cw = 6 + int(5 * ((t - 0.55) / 0.45) ** 0.9)
            for i in range(cw):
                x = 4 + i if side < 0 else w - 5 - i
                fold = (i + (y // 7)) % 4
                col = [P['cur2'], P['cur'], P['cur3'], P['cur']][fold] if i < cw - 1 else P['curD']
                px[x, y] = col
        ty = 4 + int(0.55 * (h - 8))
        for dx in range(0, 7):
            x = 4 + dx if side < 0 else w - 5 - dx
            put(im, x, ty, P['gold'])
            put(im, x, ty + 1, P['gold2'])
        x = 10 if side < 0 else w - 11
        put(im, x, ty + 2, P['gold'])
        put(im, x, ty + 3, P['gold2'])
    curtain(-1)
    curtain(1)
    # valance with scallops
    for x in range(4, w - 4):
        for y in range(4, 8):
            px[x, y] = P['cur'] if y < 7 else P['cur2']
        k = (x - 4) % 8
        depth = [2, 3, 4, 4, 4, 4, 3, 2][k]
        for y in range(8, 8 + depth):
            px[x, y] = P['cur'] if y < 8 + depth - 1 else P['curD']
        if k == 0:
            put(im, x, 8 + depth, P['gold'])
            put(im, x, 9 + depth, P['gold2'])
    hline(im, 4, 5, w - 8, P['cur3'])
    return im


# ------------------------------------------------------------------ ribbon title
def ribbon(w, h):
    """Title banner: stitched band with folded swallowtails tucked behind."""
    tw, drop = 13, 4
    im = new(w, h + drop)
    th = h - 2
    for side in (-1, 1):
        for y in range(drop, drop + th):
            mid = drop + th / 2 - 0.5
            notch = max(0, 4 - int(abs(y - mid) + 0.5))
            for i in range(tw):
                x = i if side < 0 else w - 1 - i
                if i < notch:
                    continue
                edge = (i == notch) or y in (drop, drop + th - 1)
                col = P['berry'] if edge else (P['rose2'] if y > drop + 1 else P['rose'])
                put(im, x, y, col)
        # fold: dark wedge where the band tucks behind
        fx = tw - 4 if side < 0 else w - tw
        for y in range(h - 1, h + drop - 1):
            for i in range(4):
                x = fx + i
                put(im, x, y, P['berry2'] if (y - (h - 1)) <= (i if side < 0 else 3 - i) else T)
    bx = tw - 4
    rr_layers(im, bx, 0, w - 2 * bx, h, 2, [(0, P['berry']), (1, P['rose'])])
    hline(im, bx + 2, 1, w - 2 * bx - 4, P['pink'])
    hline(im, bx + 2, h - 2, w - 2 * bx - 4, P['rose2'])
    stitch_rr(im, bx + 2, 3, w - 2 * bx - 4, h - 6, 0, P['blush'])
    return im


if __name__ == '__main__':
    from px import zoom
    parts = [panel(120, 80), stage(60, 54), ribbon(100, 15), button(40, 16), button(40, 16, 'mint'),
             pill(60, 11), white_key(9, 50), black_key(6, 30), heart_knob()]
    x = 0
    sheet = new(sum(p.width + 2 for p in parts), max(p.height for p in parts))
    for p in parts:
        sheet.alpha_composite(p, (x, 0))
        x += p.width + 2
    zoom(sheet, 4, bg=(80, 80, 80, 255)).save('preview/ui_parts.png')
