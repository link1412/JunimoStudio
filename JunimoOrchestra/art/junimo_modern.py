"""The modern Junimo band at full size: the game's own Junimo (tinted with the family colour at runtime) playing a
modern instrument, in 32x32 cells like the Voyage orchestra. Too wide for blocks on neighbouring tiles (those use the
little Junimo in junimo_blocks.py); kept for a stage with room between the seats.

    python3 junimo_modern.py [Junimo.png]      # Junimo.png = the game's Characters/Junimo.png

Writes assets/textures/junimo_modern.png (32x32 cells like assets/textures/orchestra.png: the Junimo's feet at (16, 31) plus the
instrument's offset) and previews in art/preview/. One row per instrument:

    [back, Junimo, Junimo on a long note, front, hands] x [idle, A, B]   (15 cells)

'Back' is drawn behind the Junimo and 'front' over it; the Junimo and its 'hands' (drawn over everything) are in the
Junimo's own grey palette so the game tints them like any Junimo. A and B alternate on successive notes (left stick /
right stick, strum down / up...); on a long note the Junimo savours it (eyes shut) - that frame also serves as a blink.
The Junimo itself keeps the game's look (it is the game's standing Junimo, re-posed: new eyes and mouths, a lean, squash
and stretch), so building the sheet needs the game's Characters/Junimo.png, like tools/orchestra_sprites.py does.

The face is sacred: nothing in front may touch the eyes, their shine, the blush or a 1px margin round them (see FACE;
the build fails if anything does), so instruments go beside, below, behind or on top of the Junimo.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path
from PIL import Image

from px import grid, hex2rgba, outline, zoom

C = hex2rgba
CELL = 32
FEET = 31
ROOT = Path(__file__).resolve().parent.parent

# the Junimo's own palette (the game tints these)
J_BODY, J_LINE = (196, 206, 206, 255), (55, 55, 81, 255)

LINE = C('#2e2440')
PAL = {
    'k': C('#241c33'), 'K': C('#4a3f5e'), 'x': C('#6f6488'),                     # black, its sheen
    'w': C('#fffaf0'), 'W': C('#d9d0c2'),                                       # pickguard / drum head
    's': C('#eef3fa'), 'S': C('#b8c2d4'), 't': C('#7d88a3'),                    # chrome
    'y': C('#ffe58a'), 'Y': C('#f2b63c'), 'z': C('#b8741c'),                    # brass / cymbals / gold
    'n': C('#5a3424'), 'N': C('#8a5636'), 'm': C('#f0cf96'), 'M': C('#c9995c'), # rosewood, maple
    'r': C('#ff5a6e'), 'R': C('#c22d4a'), 'p': C('#ffb3c6'),                    # red, pink
    'v': C('#ffffff'),
    'G': C('#7fd8be'), 'H': C('#c2f2df'), 'g': C('#45a68e'),                    # seafoam (guitar)
    'B': C('#3d4f9e'), 'b': C('#26306b'), 'L': C('#6f86d6'),                    # midnight blue (drum shells)
}


def canvas() -> Image.Image:
    return Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))


def part(rows: list[str], x: int, y: int, line: bool = True) -> Image.Image:
    """An ASCII drawing placed at (x, y) on an empty cell, optionally with a 1px outline around it."""
    im = canvas()
    im.alpha_composite(grid(rows, PAL), (x, y))
    return outline(im, LINE) if line else im


def shear_up(rows: list[str], k: float) -> list[str]:
    """Tilt a drawing by lifting each column k pixels per column to the right (adds rows on top)."""
    w = max(len(r) for r in rows)
    lift = [round(k * x) for x in range(w)]
    top = max(lift)
    out = [['.'] * w for _ in range(len(rows) + top)]
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch != '.':
                out[y + top - lift[x]][x] = ch
    return [''.join(r) for r in out]


def stack(*layers: Image.Image) -> Image.Image:
    im = canvas()
    for l in layers:
        im.alpha_composite(l)
    return im


def line_px(x0: int, y0: int, x1: int, y1: int):
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        yield x0, y0
        if x0 == x1 and y0 == y1:
            return
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def stick(x0: int, y0: int, x1: int, y1: int, colour: str = 'm', tip: str = 'v') -> Image.Image:
    """A thin stick (drumstick, mic stand...) from (x0, y0) to (x1, y1), outlined."""
    im = canvas()
    px = im.load()
    for x, y in line_px(x0, y0, x1, y1):
        px[x, y] = PAL[colour]
    if tip:
        px[x1, y1] = PAL[tip]
    return outline(im, LINE)


# ---------------------------------------------------------------- the Junimo's hands
# Junimo-sprite coordinates of its shoulders (where the game's own little arms start).
SHOULDER = {'L': (1, 10), 'R': (14, 10)}


def hands(dx: int, dy: int, *grips: tuple[str, int, int]) -> Image.Image:
    """Arms from the Junimo's shoulders (it stands at offset dx, dy) to little round mittens at cell (x, y)."""
    im = canvas()
    px = im.load()
    ox, oy = 8 + dx, FEET - 15 + dy
    for side, hx, hy in grips:
        sx, sy = SHOULDER[side]
        for x, y in line_px(ox + sx, oy + sy, hx, hy):
            px[x, y] = J_LINE
    for _, hx, hy in grips:
        for mx, my in ((-1, 0), (-1, 1), (2, 0), (2, 1), (0, -1), (1, -1), (0, 2), (1, 2)):
            px[hx + mx, hy + my] = J_LINE
        for mx in (0, 1):
            for my in (0, 1):
                px[hx + mx, hy + my] = J_BODY
    return im


# ---------------------------------------------------------------- electric guitar (guitar family)
# Rock-star pose: the guitar stands at the Junimo's side with its neck raised high (beside the face, never across it).
# The near hand strums over the pickups; the neck rocks down on A and up on B, and on B the far arm goes up, rock on.
def guitar_parts(cx: float, cy: float, angle: float, neck_len: float = 9.0) -> tuple[Image.Image, Image.Image]:
    """A solid-body electric guitar along an axis (degrees: 0 = neck to the right, -90 = straight up), lower bout centred
    on (cx, cy). Returns (body, neck): bouts, pickups, bridge and knob; neck and headstock."""
    import math
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)
    body = [[None] * CELL for _ in range(CELL)]
    nk = [[None] * CELL for _ in range(CELL)]
    for y in range(CELL):
        for x in range(CELL):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            u, v = dx * ca + dy * sa, -dx * sa + dy * ca          # along the guitar (to the headstock), across it
            c = None
            if (u / 3.9) ** 2 + (v / 3.7) ** 2 <= 1:
                c = 'G'                                               # lower bout
            elif ((u - 4.4) / 2.7) ** 2 + ((v - 0.3) / 2.9) ** 2 <= 1 and not (u > 4.2 and v > 1.0):
                c = 'G'                                               # upper bout with its cutaway
            if c:
                if abs(u - 0.2) < 0.55 and abs(v) < 1.9 or abs(u - 2.4) < 0.55 and abs(v) < 1.9:
                    c = 'k'                                           # two pickups
                elif abs(u + 1.8) < 0.5 and abs(v) < 1.4:
                    c = 'S'                                           # bridge
                elif (u + 1.6) ** 2 + (v - 2.4) ** 2 < 0.5:
                    c = 'm'                                           # knob
                body[y][x] = c
            elif 5.5 < u < 5.5 + neck_len and abs(v) < 0.75:
                nk[y][x] = 'n'
            elif 5.5 + neck_len <= u < 8.7 + neck_len and -0.9 < v < 1.4:
                nk[y][x] = 'm'
    src = [r[:] for r in body]                                        # light from the upper left
    at = lambda x, y: src[y][x] if 0 <= x < CELL and 0 <= y < CELL else None
    for y in range(CELL):
        for x in range(CELL):
            if src[y][x] == 'G':
                if at(x - 1, y) is None or at(x, y - 1) is None:
                    body[y][x] = 'H'
                elif at(x + 1, y) is None or at(x, y + 1) is None:
                    body[y][x] = 'g'

    def image(px):
        im = canvas()
        for y in range(CELL):
            for x in range(CELL):
                if px[y][x]:
                    im.putpixel((x, y), PAL[px[y][x]])
        return outline(im, LINE)
    return image(body), image(nk)


def guitar(pose: int):
    body, nk = guitar_parts(9.0, 26.5, (-105, -98, -112)[pose])
    strum = {0: ('L', 9, 27), 1: ('L', 9, 29), 2: ('L', 10, 26)}[pose]
    return canvas(), stack(nk, body), hands(4, 0, strum)


# ---------------------------------------------------------------- drum kit (drumkit family)
# The Junimo sits behind a little kit: a kick drum with a gold star on its head, a snare on the left and a crash cymbal
# on a stand on the right. A = left stick on the snare (right one up), B = right stick on the crash (left one up).
KICK = [
    "..BBBBB..",
    ".BLwwwwB.",
    "BLwwywwwb",
    "BwyyYyywb",
    "BwwyYywWb",
    ".bwywywb.",
    "..bbbbb..",
]
SNARE = [
    ".wwwww.",
    "sSSSSSt",
    "LBsBsBb",
    "BBtBtBb",
    "tSSSSSt",
]
CRASH = [
    "...yy...",
    ".yYYYYz.",
    "zzzzzzzz",
]


def drums(pose: int):
    kick = part(KICK, 12, 25)
    snare = part(SNARE, 3, 22)
    crash = part(CRASH, 20, 13 if pose != 2 else 14)
    stand = part(["t"] * 13, 24, 18, line=False)
    if pose == 0:      # resting: one stick on the snare, one by the cymbal
        grips = [('L', 6, 19), ('R', 24, 20)]
        sticks = [stick(7, 20, 4, 22), stick(25, 20, 27, 17)]
    elif pose == 1:    # left stick down on the snare, right one raised
        grips = [('L', 7, 20), ('R', 24, 16)]
        sticks = [stick(7, 21, 4, 22), stick(25, 16, 27, 11)]
    else:              # right stick on the crash, left one raised
        grips = [('L', 6, 15), ('R', 25, 19)]
        sticks = [stick(6, 15, 4, 10), stick(26, 19, 27, 16)]
    return stack(stand, crash), stack(snare, kick), stack(*sticks, hands(0, -5, *grips))


# ---------------------------------------------------------------- vocal mic (choir family)
# A ball mic on a stand, angled at the Junimo's mouth. It sings with its arms up (eyes shut, mouth open on A).
MIC = [
    ".sss.",
    "sKsKS",
    "sKsKS",
    ".SSt.",
    "..t..",
]


def mic(pose: int):
    stand = stack(stick(24, 24, 24, 30, 'K', ''), part(["kkKkk"], 22, 31, line=False),
                  stick(24, 24, 21, 27, 'K', ''))
    head = part(MIC, 19, 21)
    return canvas(), stack(stand, head), hands(-2, 0, ('R', 22, 27))       # a hand on the stand


# ---------------------------------------------------------------- the Junimo, in the game's own style
# Drawn by editing the game's standing Junimo (frame 0) in its own grey palette, so the game tints it like any Junimo:
# new eyes and mouths, a lean, squash & stretch, arms taken off where its hands hold the instrument.
# (tools/orchestra_sprites.py ships its closed-eye Junimos the same way.)
JA, JB, JW, JD, JE = J_BODY, J_LINE, (255, 255, 255, 255), (134, 134, 153, 255), (216, 164, 165, 255)
EYES = {
    'open': [((5, 11), JB), ((5, 12), JW), ((10, 11), JB), ((10, 12), JW)],                # the game's own
    'happy': [((4, 12), JB), ((5, 11), JB), ((6, 12), JB), ((9, 12), JB), ((10, 11), JB), ((11, 12), JB)],  # ^ ^
    'line': [((4, 12), JB), ((5, 12), JB), ((10, 12), JB), ((11, 12), JB)],               # blissfully shut
    'wink': [((4, 12), JB), ((5, 11), JB), ((6, 12), JB), ((10, 11), JB), ((10, 12), JW)],
    'big': [((5, 10), JB), ((5, 11), JB), ((5, 12), JW), ((10, 10), JB), ((10, 11), JB), ((10, 12), JW)],
    'squeeze': [((4, 11), JB), ((5, 12), JB), ((6, 11), JB), ((9, 11), JB), ((10, 12), JB), ((11, 11), JB)],  # > <
}
MOUTHS = {
    'none': [],
    'small': [((8, 12), JD)],
    'smile': [((7, 13), JD), ((8, 13), JD)],
    'open': [((7, 12), JB), ((8, 12), JB), ((7, 13), JE), ((8, 13), JE)],
    'O': [((7, 12), JB), ((8, 12), JB), ((7, 13), JB), ((8, 13), JB)],
}
ARMS = {'L': [(0, 9), (1, 10)], 'R': [(15, 9), (14, 10)]}
ARMS_UP = {'L': [(2, 2), (1, 3), (1, 4), (1, 5), (1, 6)], 'R': [(13, 2), (14, 3), (14, 4), (14, 5), (14, 6)]}  # as frame 44


def junimo(base: Image.Image, eyes: str = 'open', mouth: str = 'none', keep: str = 'LR', up: str = '', lean: int = 0,
           body: int = 0) -> Image.Image:
    """The standing Junimo with a new face; 'keep' = which of its own little arms stay down (the others are drawn holding
    the instrument), 'up' = arms thrown up in the air (as the game's cheering Junimo); lean = its top leans one pixel
    left (-1) or right (+1); body = squash (-1) or stretch (+1)."""
    im = base.copy()
    for side, pts in ARMS.items():
        if side not in keep:
            for p in pts:
                im.putpixel(p, (0, 0, 0, 0))
    for side in up:
        for p in ARMS_UP[side]:
            im.putpixel(p, JB)
    for p in ((5, 11), (5, 12), (10, 11), (10, 12)):
        im.putpixel(p, JA)
    for p, c in EYES[eyes] + MOUTHS[mouth]:
        im.putpixel(p, c)
    rows = [[im.getpixel((x, y)) for x in range(16)] for y in range(16)]
    if body < 0:                                   # squash: lose a plain row, the top comes down
        del rows[8]
        rows.insert(0, [(0, 0, 0, 0)] * 16)
    elif body > 0:                                 # stretch
        rows.insert(8, rows[8][:])
        del rows[0]
    out = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    for y in range(16):
        shift = lean if y <= 7 else 0
        for x in range(16):
            if rows[y][x][3] and 0 <= x + shift < 16:
                out.putpixel((x + shift, y), rows[y][x])
    return out


# ---------------------------------------------------------------- the band
# name -> family, where the Junimo stands (feet offset from (16, 31)), how it looks for idle / A / B, what changes on a
# long note (savouring it), the draw function, and whether it carries the instrument (it moves when the Junimo bounces).
BAND = {
    'guitar': dict(family='guitar', at=(4, 0), draw=guitar, carried=True,
                   junimo=[dict(eyes='open', mouth='smile', keep='R'),
                           dict(eyes='happy', mouth='open', keep='R', lean=-1, body=-1),
                           dict(eyes='wink', mouth='open', keep='', up='R', lean=1, body=1)],
                   long=dict(eyes='happy', mouth='smile')),
    'drums': dict(family='drumkit', at=(0, -5), draw=drums, carried=False,
                  junimo=[dict(eyes='open', mouth='small', keep=''),
                          dict(eyes='squeeze', mouth='open', keep='', body=-1),
                          dict(eyes='big', mouth='O', keep='', body=1)],
                  long=dict(eyes='happy', mouth='open')),
    'mic': dict(family='choir', at=(-2, 0), draw=mic, carried=False,
                junimo=[dict(eyes='open', mouth='smile', keep='L'),
                        dict(eyes='line', mouth='O', keep='L', lean=1),
                        dict(eyes='happy', mouth='open', keep='', up='L')],
                long=dict(eyes='line', mouth='O')),
}
ACCENT = {'guitar': '#ffa83d', 'drumkit': '#ff5a6a', 'choir': '#b88cf0'}   # the families' accents (Families.cs)


def lighten(hexcol: str, k: float = 0.3) -> str:
    """The band's Junimos are tinted with their family's accent lifted 30% towards white: the game multiplies its grey
    Junimo by the tint, and the plain accent comes out darker than the pastel pixel blocks and the panel."""
    r, g, b, _ = hex2rgba(hexcol)
    return '#%02x%02x%02x' % tuple(round(c + (255 - c) * k) for c in (r, g, b))


def junimo_cell(base: Image.Image, name: str, pose: int, long: bool = False) -> Image.Image:
    """The Junimo for a pose, placed in its 32x32 cell."""
    spec = dict(BAND[name]['junimo'][pose], **(BAND[name]['long'] if long else {}))
    dx, dy = BAND[name]['at']
    cell = canvas()
    cell.alpha_composite(junimo(base, **spec), (8 + dx, FEET - 15 + dy))
    return cell


def build(base: Image.Image) -> Image.Image:
    """One row per instrument: [back, Junimo, Junimo on a long note, front, hands] x [idle, A, B]."""
    sheet = Image.new('RGBA', (CELL * 15, CELL * len(BAND)), (0, 0, 0, 0))
    for r, name in enumerate(BAND):
        for pose in range(3):
            back, front, hand = BAND[name]['draw'](pose)
            layers = [back, junimo_cell(base, name, pose), junimo_cell(base, name, pose, True), front, hand]
            for k, im in enumerate(layers):
                sheet.alpha_composite(im, ((k * 3 + pose) * CELL, r * CELL))
    return sheet


# The face is sacred: the eyes (row 11) with their shine and blush (row 12), and a 1px margin round them.
# Rows/columns are in Junimo-sprite space.
FACE = {(x, y) for y in range(9, 13) for x in range(4, 12)}


def face_hits(name: str) -> list[tuple[int, int, int]]:
    """Pixels of an instrument's front or hands layers that would sit on the Junimo's face, as (pose, x, y)."""
    dx, dy = BAND[name]['at']
    hits = []
    for pose in range(3):
        _, front, hand = BAND[name]['draw'](pose)
        for im in (front, hand):
            for (fx, fy) in FACE:
                x, y = 8 + dx + fx, FEET - 15 + dy + fy
                if im.getpixel((x, y))[3]:
                    hits.append((pose, x, y))
    return sorted(set(hits))


# ---------------------------------------------------------------- previews
DEFAULT_JUNIMO = Path.home() / 'Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS' \
    / 'Content (unpacked)/Characters/Junimo.png'


def standing_junimo(path: Path) -> Image.Image:
    """Frame 0 of the game's Characters/Junimo.png: the Junimo every pose is drawn from."""
    return Image.open(path).convert('RGBA').crop((0, 0, 16, 16))


def tint(im: Image.Image, hexcol: str) -> Image.Image:
    t = hex2rgba(hexcol)
    r, g, b, a = im.split()
    return Image.merge('RGBA', [ch.point(lambda v, k=k: v * t[k] // 255) for k, ch in enumerate((r, g, b))] + [a])


def scene(base: Image.Image, name: str, pose: int, long: bool = False, hop: int = 0) -> Image.Image:
    """What the game draws: back, the tinted Junimo, front, then its tinted hands (with the sticks they hold)."""
    accent = lighten(ACCENT[BAND[name]['family']])
    back, front, hand = BAND[name]['draw'](pose)
    lift = hop if BAND[name]['carried'] else 0
    cell = canvas()
    cell.alpha_composite(back, (0, lift))
    cell.alpha_composite(tint(junimo_cell(base, name, pose, long), accent), (0, hop))
    cell.alpha_composite(front, (0, lift))
    h = hand.copy()
    hp, tp = h.load(), tint(hand, accent).load()
    for y in range(CELL):
        for x in range(CELL):
            if hp[x, y][3] and hp[x, y][:3] in (J_BODY[:3], J_LINE[:3]):
                hp[x, y] = tp[x, y]
    cell.alpha_composite(h, (0, hop))
    return cell


def timeline() -> list[tuple[int, bool, int, int]]:
    """A little performance for the preview: (pose, long, hop, milliseconds) per GIF frame.
    Idle with a blink, a run of eighth notes (A, B, A, B...), a long note savoured, idle again."""
    t = [(0, False, 0, 900), (0, True, 0, 120), (0, False, 0, 700)]
    for i in range(8):
        pose = 1 + i % 2
        t += [(pose, False, -2, 70), (pose, False, -1, 60), (pose, False, 0, 120)]
    t += [(1, False, -2, 70), (1, False, -1, 60), (1, True, 0, 900), (0, True, 0, 300), (0, False, 0, 900)]
    return t


def gif(base: Image.Image, path: Path, z: int = 6) -> None:
    grass, shadow = (118, 160, 92, 255), (88, 128, 70, 255)
    names = list(BAND)
    out = []
    for pose, long, hop, ms in timeline():
        board = Image.new('RGBA', (CELL * len(names), CELL + 4), grass)
        for i, n in enumerate(names):
            dx, _ = BAND[n]['at']
            for sx in range(-5, 6):                          # a soft shadow under its feet
                board.putpixel((i * CELL + 16 + dx + sx, CELL + 1), shadow)
            board.alpha_composite(scene(base, n, pose, long, hop), (i * CELL, 2))
        out.append((zoom(board, z).convert('RGB'), ms))
    out[0][0].save(path, save_all=True, append_images=[im for im, _ in out[1:]], duration=[ms for _, ms in out],
                   loop=0, optimize=False)


if __name__ == '__main__':
    bad = {n: face_hits(n) for n in BAND if face_hits(n)}
    if bad:
        sys.exit(f'covers the face: {bad}')
    jpath = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(os.environ.get('JUNIMO_PNG', DEFAULT_JUNIMO))
    base = standing_junimo(jpath)
    out = ROOT / 'assets' / 'textures' / 'junimo_modern.png'
    sheet = build(base)
    sheet.save(out)
    prev = ROOT / 'art' / 'preview'
    names = list(BAND)
    board = Image.new('RGBA', (CELL * 4, CELL * len(names)), (0, 0, 0, 0))
    for r, n in enumerate(names):
        for i, (pose, long) in enumerate(((0, False), (1, False), (2, False), (1, True))):
            board.alpha_composite(scene(base, n, pose, long), (i * CELL, r * CELL))
    zoom(board, 8, bg=(118, 160, 92, 255)).save(prev / 'modern_band_x8.png')
    gif(base, prev / 'modern_band.gif')
    print('wrote', out, sheet.size)
