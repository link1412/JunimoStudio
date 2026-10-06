"""Junimo blocks, classical set: the game's own Junimo in concert dress playing the most representative classical
instrument of each family - the third look for placed blocks, drawn big like the Voyage orchestra.

    python3 junimo_classical.py [Junimo.png]      # Junimo.png = the game's Characters/Junimo.png

Writes assets/textures/junimo_classical.png: one 32x32 cell per family (column = Family.Index) and frame (row):

    0 idle (also the icon)   1 blink   2 play A   3 play B   4 long note

and in the conductor's column (songbird) one more: 5 its bow (its top hat raised); in the clarinettist's (sax) 5 is its
stool alone, which the game draws under it (it sits, as an orchestra's winds do), and so in the flautist's (ocarina).
The bowed strings have their bow's
way across the strings, a row per position, so the bow travels the whole time a note sounds:

    violin  5-13 a down-bow (its fingers up by the scroll), positions 0 (frog at the strings) to 8 (tip at the strings)
            14-22 the same for an up-bow (its fingers down by the body), 23-31 a long note (scroll raised, eyes happy)
    bass    5-11 positions 0 (bow pushed in) to 6 (pulled out), 12-18 the same through a long note (eyes happy)

The block's tile is the cell's bottom-centre 16x16 (x 8-23, rows 16-31) and the cell is drawn with its bottom centre on
the tile's, so a drawing may spill 8 pixels into each neighbour and 16 rows above it, as the orchestra's instruments
do. Families, instruments and their order are in KIT; the pairing is in CLASSICAL below.

Rules (the modern set's, junimo_blocks.py, plus the orchestra's, tools/orchestra_sprites.py):
- the Junimo is the game's own frame 0, the same size, tinted as the game tints it; in the drawing its body never
  squashes, stretches, leans or hops (in the game, one holding its instrument keeps still while it plays and breathes
  while it doesn't, and a wind player swings its instrument: ClassicalArt.Motion). Only its arms (the game's own poses, short one-pixel forearms 'X', or the orchestra's round
  mitten hands, here in white concert gloves) and its eyes (open, shut as in its cheering frames, the orchestra's
  happy ^ ^, or a wind player's squeezed > < with red cheeks and a drop of sweat on its brow, blowing with all it
  has) change;
- it wears the orchestra's concert dress, a top hat on its sprout and a red bow tie under its face, unless the
  instrument needs the room;
- the face is sacred: nothing in front of the Junimo covers its eyes, their glints, its blush or a pixel round its eyes
  (the build fails if anything does); a wind instrument's mouthpiece may touch the bottom centre of its face;
- nor may a neighbour's: a row is drawn left to right, so what a block spills to its left lands over its left
  neighbour, and the build fails if any family's spill covers any family's face. In practice: nothing in the cell's
  four left columns at the faces' rows (12-28), and the Junimo no further right than the middle (dx <= 8);
- the instruments are drawn like the orchestra's: plain game pixels, each material outlined in a dark of its own
  colour. They are grand, elegant and gentlemanly - gilt, polished wood, gleaming brass - and big is welcome: the
  comedy is a tiny Junimo playing one with complete poise;
- no flashes, sparkles or notes: the instrument being played is the effect;
- it stands on the floor (its feet on row 31, where the game draws its shadow), or sits on something that does: a
  piano's bench, a podium, the winds' stool. Drawn higher it hovers over its shadow.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path
from PIL import Image

from junimo_blocks import (C, DEFAULT_JUNIMO, FAMILY, FRAMES, GRASS, GUARD, JB, ROOT, SHADOW, lighten, junimo16,
                           pixel_block, rows_of, tint)
from px import zoom

CW = CH = 32                                   # the cell
TILE = 8                                       # the tile's left edge in the cell (its 16 columns are x 8-23)
FEET = CH - 1                                  # the floor row
EMPTY = ['.' * CW] * CH
J_BODY, J_LINE = (196, 206, 206, 255), (55, 55, 81, 255)   # the Junimo sprite's own body and outline greys
GLOVE = C('#f7f5ee')                           # white concert gloves: the gentleman's hands

# the most representative classical instrument of each family (Families.cs order)
CLASSICAL = {
    'piano': 'grand piano', 'bells': 'tubular bells', 'organ': 'pipe organ', 'guitar': 'lute',
    'bass': 'double bass', 'violin': 'violin', 'choir': 'a soloist at a music stand', 'trumpet': 'French horn',
    'sax': 'clarinet', 'ocarina': 'flute', 'synth': 'theremin', 'cloud': 'harp', 'stardust': 'glass harp',
    'kalimba': 'sitar', 'steeldrum': 'timpani', 'songbird': 'a conductor on a podium, conducting a canary',
    'drumkit': 'crash cymbals',
}


# ---------------------------------------------------------------- drawing helpers (the junimo_blocks idiom, 32 wide)
def cell(rows: list[str]) -> list[str]:
    """A full-cell drawing, checked: CH rows of CW."""
    assert len(rows) == CH and all(len(r) == CW for r in rows), [len(r) for r in rows]
    return rows


def at(top: int, lines: list[str]) -> list[str]:
    """A drawing that starts at row `top` of the cell (rows above and below it are empty)."""
    return cell(['.' * CW] * top + lines + ['.' * CW] * (CH - top - len(lines)))


def put(rows: list[str], pts, ch: str) -> list[str]:
    out = [list(r.ljust(CW, '.')) for r in rows]
    for x, y in pts:
        if 0 <= x < CW and 0 <= y < len(out):
            out[y][x] = ch
    return [''.join(r) for r in out]


def stamp(rows: list[str], sprite: list[str], x0: int, y0: int) -> list[str]:
    """Paint a small sprite (rows of chars, '.' = clear) onto the cell at (x0, y0)."""
    for ch in sorted({c for r in sprite for c in r} - {'.'}):
        rows = put(rows, [(x0 + i, y0 + j) for j, r in enumerate(sprite) for i, c in enumerate(r) if c == ch], ch)
    return rows


def mirror(pts):
    """Points mirrored across the cell."""
    return [(CW - 1 - x, y) for x, y in pts]


def mitten(x: int, y: int) -> dict[str, list[tuple[int, int]]]:
    """The orchestra's round hand with its 2x2 palm at (x, y), in a white concert glove: {'X': its outline (the
    Junimo's own, tinted), 'Z': the glove}. Paint the outline first: put(put(rows, m['X'], 'X'), m['Z'], 'Z')."""
    ring = [(-1, 0), (-1, 1), (2, 0), (2, 1), (0, -1), (1, -1), (0, 2), (1, 2)]
    return {'X': [(x + dx, y + dy) for dx, dy in ring], 'Z': [(x + dx, y + dy) for dx in (0, 1) for dy in (0, 1)]}


def hand(rows: list[str], x: int, y: int) -> list[str]:
    m = mitten(x, y)
    return put(put(rows, m['X'], 'X'), m['Z'], 'Z')


def shifted(rows: list[str], dx: int = 0, dy: int = 0) -> list[str]:
    """A drawing moved dx right and dy down (what falls off the cell is lost)."""
    out = ['.' * CW] * CH
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch != '.' and 0 <= x + dx < CW and 0 <= y + dy < CH:
                out[y + dy] = out[y + dy][:x + dx] + ch + out[y + dy][x + dx + 1:]
    return out


def nudge(rows: list[str], box: tuple[int, int, int, int], dx: int = 0, dy: int = 0) -> list[str]:
    """The part of a drawing inside box (x0, y0, x1, y1, inclusive) moved by (dx, dy), drawn over what is there."""
    x0, y0, x1, y1 = box
    out = [list(r) for r in rows]
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            out[y][x] = '.'
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            c = rows[y][x]
            if c != '.' and 0 <= x + dx < CW and 0 <= y + dy < CH:
                out[y + dy][x + dx] = c
    return [''.join(r) for r in out]


def tilt(rows: list[str], box: tuple[int, int, int, int], tip: int, x2: int) -> list[str]:
    """The far part of a long instrument (inside box) tipped down (tip > 0) or up a pixel; at two pixels, the part
    beyond x2 goes a second pixel, so a straight tube stays straight."""
    x0, y0, x1, y1 = box
    rows = nudge(rows, box, 0, max(-1, min(1, tip)))
    if abs(tip) > 1:
        rows = nudge(rows, (x2, max(y0 - 1, 0), x1, min(y1 + 1, CH - 1)), 0, -1 if tip < 0 else 1)
    return rows


def line(x0: int, y0: int, x1: int, y1: int) -> list[tuple[int, int]]:
    """The pixels of a straight line (Bresenham), for arms and batons."""
    pts, dx, dy = [], abs(x1 - x0), -abs(y1 - y0)
    sx, sy, err = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1), abs(x1 - x0) - abs(y1 - y0)
    while True:
        pts.append((x0, y0))
        if (x0, y0) == (x1, y1):
            return pts
        e2 = 2 * err
        if e2 >= dy:
            err, x0 = err + dy, x0 + sx
        if e2 <= dx:
            err, y0 = err + dx, y0 + sy


def grid(rows: list[str], pal: dict) -> Image.Image:
    im = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch not in '. ':
                im.putpixel((x, y), pal[ch])
    return im


def eyes_for(frame: str) -> str:
    """Its eyes shut on a blink, happy ^ ^ through a long note (as in the orchestra), its bow's way too ('long3')."""
    return 'happy' if frame.rstrip('0123456789') == 'long' else {'blink': 'shut'}.get(frame, 'open')


def eyes_blowing(frame: str) -> str:
    """A wind player's: squeezed shut > <, red in the cheeks and a drop of sweat on its brow whenever it plays, as it
    blows with all it has (it has no mouth to puff out); open at rest, shut on a blink. (The drop stays on it: flying
    off, drops read as sparkles.)"""
    return {'idle': 'open', 'blink': 'shut'}.get(frame, 'effort')


def travel(frame: str) -> tuple[str, int] | None:
    """A bowed string's position on the bow's way (EXTRA: 'down3' = a down-bow at position 3), or None."""
    kind = frame.rstrip('0123456789')
    return (kind, int(frame[len(kind):])) if kind != frame else None


# ---------------------------------------------------------------- the Junimo and its concert dress
def junimo(base: Image.Image, eyes: str = 'open', arms: tuple[str, ...] = ()) -> Image.Image:
    """Frame 0 with the given arms; eyes open, shut (its cheering frames' lines) or happy (the orchestra's ^ ^)."""
    im = junimo16(base, 'shut' if eyes == 'shut' else 'open', arms)
    if eyes == 'happy':
        for ex in (5, 10):
            im.putpixel((ex, 12), J_BODY)                  # the eye's shine goes ...
            for p in ((ex - 1, 12), (ex, 11), (ex + 1, 12)):
                im.putpixel(p, J_LINE)                     # ... and the eye becomes a ^
    if eyes == 'effort':
        for ex, out in ((5, -1), (10, 1)):                 # > <: the shine goes, the eye squeezed to a point
            im.putpixel((ex, 12), J_BODY)
            for p in ((ex + out, 10), (ex, 11), (ex + out, 12)):
                im.putpixel(p, J_LINE)
    return im


CHEEKS = {(3, 12): (232, 84, 96, 255), (12, 12): (232, 84, 96, 255)}   # red with the effort, out where the blush was
SWEAT = {(x, y): {'b': (47, 111, 208, 255), 'L': (159, 216, 255, 255), 'w': (255, 255, 255, 255)}[c]
         for y, r in enumerate(['.b.', '.b.', 'bwb', 'bLb', '.b.'], start=5) for x, c in enumerate(r, start=3) if c != '.'}
                                                     # and a drop of sweat on its brow, its point up, clear of its eyes


HAT_K, HAT_HI, TIE, KNOT = (24, 20, 32, 255), (70, 66, 90, 255), (200, 40, 56, 255), (120, 20, 36, 255)
HAT = {**{(x, y): HAT_K for y in range(0, 4) for x in range(6, 10)},           # crown over the sprout and the
       **{(x, 4): HAT_K for x in range(4, 12)},                                 # head's top: worn, not perched, the
       **{(x, 3): TIE for x in range(6, 10)},                                   # brim a row down on the head; band
       (9, 0): HAT_HI, (9, 1): HAT_HI}                                          # sheen
BOW_TIE = {(5 + i, 13 + j): (TIE if ch == '#' else KNOT)
           for j, r in enumerate(['#....#', '##oo##', '#....#']) for i, ch in enumerate(r) if ch != '.'}
DRESS = {'hat': HAT, 'tie': BOW_TIE}                                            # in the Junimo's own 16x16 frame


class Frame:
    """One frame: a back layer, the Junimo (in its dress), a front layer (each 32x32). The Junimo's 16x16 frame goes
    at (dx, dy); by default it stands centred on the tile (8, 16). In either layer 'X' is the Junimo's own outline
    colour, tinted with it (for arms and the gloves' outline), and 'Z' a white concert glove."""

    def __init__(self, family: str, front: list[str], pal: dict, eyes: str = 'open', arms: tuple[str, ...] = (),
                 dx: int = TILE, dy: int = FEET - 15, back: list[str] | None = None,
                 dress: tuple[str, ...] = ('hat', 'tie'), junimo: bool = True):
        self.family, self.front, self.pal, self.eyes, self.arms = family, cell(front), pal, eyes, arms
        self.dx, self.dy, self.back, self.dress, self.junimo = dx, dy, cell(back or EMPTY), dress, junimo

    def image(self, base: Image.Image) -> Image.Image:
        colour = lighten(FAMILY[self.family][1])
        dot = lambda c: tint(Image.new('RGBA', (1, 1), c), colour).getpixel((0, 0))
        pal = dict(self.pal, X=dot(JB), Z=GLOVE)
        im = grid(self.back, pal)
        if not self.junimo:
            im.alpha_composite(grid(self.front, pal))
            return im
        j = tint(junimo(base, self.eyes, self.arms), colour)
        for part in self.dress:
            for (x, y), c in DRESS[part].items():
                if 0 <= x < 16 and 0 <= y < 16:
                    j.putpixel((x, y), c)
        if self.eyes == 'effort':                                   # untinted, as the dress is
            for (x, y), c in {**CHEEKS, **SWEAT}.items():
                j.putpixel((x, y), c)
        top = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
        top.alpha_composite(j.crop((max(-self.dx, 0), max(-self.dy, 0), 16, 16)), (max(self.dx, 0), max(self.dy, 0)))
        if 'hat' in self.dress:                                     # the crown rises a row above the sprite
            for (x, y), c in HAT.items():
                if y < 0 and 0 <= self.dx + x < CW and 0 <= self.dy + y < CH:
                    top.putpixel((self.dx + x, self.dy + y), c)
        im.alpha_composite(top)
        im.alpha_composite(grid(self.front, pal))
        return im

    def face_hits(self) -> list[tuple[int, int]]:
        return sorted((x, y) for x, y in GUARD
                      if 0 <= x + self.dx < CW and 0 <= y + self.dy < CH
                      and self.front[y + self.dy][x + self.dx] not in '. ')


# ---------------------------------------------------------------- whole frames, moved or turned
def moved(frame: Frame, dx: int) -> Frame:
    """The same frame with its whole drawing, instrument and Junimo, moved dx right."""
    return Frame(frame.family, shifted(frame.front, dx), frame.pal, frame.eyes, frame.arms, frame.dx + dx, frame.dy,
                 shifted(frame.back, dx), frame.dress)


def mirrored(frame: Frame) -> Frame:
    """The same frame turned left for right: the instrument mirrored, the Junimo (never itself mirrored) standing
    where its mirror image would, its arms swapped."""
    flip = lambda rows: [r[::-1] for r in rows]
    swap = {'L': 'R', 'R': 'L', 'upL': 'upR', 'upR': 'upL'}
    return Frame(frame.family, flip(frame.front), frame.pal, frame.eyes, tuple(swap[a] for a in frame.arms),
                 CW - 16 - frame.dx, frame.dy, flip(frame.back), frame.dress)


# ================================================================ strings, harp, piano and percussion, grown from
# the orchestra's own drawings (assets/textures/orchestra.png) in its own colour letters
# the orchestra's palette (tools/orchestra_sprites.py PAL)
ORCH = {
    'o': C('#3a1d12'), 'O': C('#1e100c'), 'W': C('#6b3214'), 'w': C('#9c4e22'), 'l': C('#c9763a'), 'h': C('#e8a060'),
    'e': C('#2b1d16'), 's': C('#efe3c4'), 'S': C('#beb096'), 't': C('#8c643c'), 'b': C('#221c2b'), 'B': C('#4a4060'),
    'x': C('#6e6482'), 'k': C('#f3ead6'), 'K': C('#b4aa96'), 'g': C('#b07a22'), 'G': C('#e8b640'), 'y': C('#ffe08a'),
    'c': C('#8a4a22'), 'q': C('#e2965a'), 'd': C('#efe2c2'), 'D': C('#cdbb94'), 'r': C('#c0392b'), 'R': C('#8c1e1e'),
}
ORCH['C'] = C('#c46f35')


# ---------------------------------------------------------------- violin: on its shoulder, the bow drawn across it
VIOLIN_PAL = dict(ORCH, l=C('#e8944c'), w=C('#b85c26'), W=C('#7a3a16'),   # a brighter amber varnish, clear of its
                  t=C('#3a1d0e'), S=C('#fffaf0'))                         # tint; the bow a dark stick and white hair
VIOLIN = at(16, [                     # the orchestra's violin (lower bout at its right shoulder), a row higher
    "...........................oo...",
    "..........................oWWo..",
    ".....................oooooeWWo..",
    "..................ooollleeooo...",
    ".................olllweeWo......",
    "................olwseeewWo......",
    "...............olwwsewWWo.......",
    "...............olwewsWoo........",
    "................olwwWo..........",
    ".................olWo...........",
    "..................oo............",
])
VIOLIN_RAISED = at(14, [              # the same, turned 10 degrees: its scroll lifted for a long note
    "..........................o.....",
    ".........................oWo....",
    ".........................oWWo...",
    ".....................ooooeoo....",
    "....................ollleo......",
    "..................oolweeWo......",
    ".................ollweewWo......",
    "................olwseeWWo.......",
    "...............olwwssWoo........",
    "...............olwewwWo.........",
    "................olwwWo..........",
    ".................olWo...........",
    "..................oo............",
])
BOW = {   # the orchestra's bow positions 0 (frog at the strings), 4 and 8 (tip at the strings): stick 't', hair 'S'
    0: at(21, ["..................t.............",
               "..................Sttt..........",
               "...................SSStt........",
               "......................SSttt.....",
               "........................SSSttt..",
               "...........................SSStt",
               "..............................SS"]),
    4: at(20, ["..............tt................",
               "..............SSttt.............",
               "................SSSttt..........",
               "...................SSSttt.......",
               "......................SSSttt....",
               ".........................SSSt...",
               "............................S..."]),
    8: at(19, ["...........ttt..................",
               "...........SSSttt...............",
               "..............SSStt.............",
               ".................SSttt..........",
               "...................SSStt........",
               "......................SSt.......",
               "........................S......."]),
}
GRIP = {0: (18, 21), 4: (14, 20), 8: (11, 19)}            # the bow hand's palm, at the frog
BOW_WAY = 8                                                 # its positions on the way: 0 (frog) to 8 (tip at the strings)


def frog(p: int) -> tuple[int, int]:
    """The bow's frog (and the bow hand's palm) at position p on its way, from GRIP[0] to GRIP[8]."""
    return int(18 - 7 * p / BOW_WAY + 0.5), int(21 - 2 * p / BOW_WAY + 0.5)


def bow_at(p: int) -> list[str]:
    """The bow at any position on its way, drawn as BOW's are: the stick a straight line out from the frog, its hair
    the row below."""
    fx, fy = frog(p)
    stick = line(fx, fy, fx + 13, fy + 5)
    return put(put(EMPTY, [(x, y + 1) for x, y in stick], 'S'), stick, 't')
FINGER = {   # the fingering hand on the neck (where its fingers stop the strings) and its arm from the shoulder: low
    # notes by the scroll, high by the body. No glove: a second white glove level with the bow hand read as a pair of
    # eyes on the violin
    0: ((26, 20), [(23, 22), (24, 22), (21, 23), (22, 23), (19, 24), (20, 24), (18, 25)]),
    2: ((24, 20), [(22, 22), (23, 22), (24, 22), (25, 22), (21, 23), (19, 24), (20, 24), (18, 25)]),
    4: ((23, 21), [(21, 23), (22, 23), (23, 23), (24, 23), (19, 24), (20, 24), (18, 25)]),
    'raised': ((23, 18), [(22, 22), (23, 22), (24, 22), (21, 23), (19, 24), (20, 24), (18, 25), (24, 21), (24, 20)]),
}


def violin(frame: str) -> Frame:
    """The orchestra's violin on its right shoulder, pointing up and out, in a bright amber varnish; its white-gloved
    bow hand draws the bow across the strings (the orchestra's three bow positions) while the other hand stops the
    strings on the neck. A is a down-bow to the tip, its fingers up by the scroll; B an up-bow to the frog, its
    fingers down by the body; on a long note it lifts the scroll and draws a full bow to the tip, eyes happy. As the
    mod plays it the bow travels through EXTRA's positions the whole time a note sounds (frame 'down3' etc.). (A blind
    test found the orchestra's tan-and-beige bow nearly invisible on the farm, and its flourish - the bow swept up
    over its hat - a fishing rod.)"""
    if way := travel(frame):                                  # on the bow's way: A's fingers, B's, or a long note's
        finger, raised = {'down': 0, 'up': 4, 'long': 'raised'}[way[0]], way[0] == 'long'
        bow, grip = bow_at(way[1]), frog(way[1])
    else:
        p, finger = {'A': (8, 0), 'B': (0, 4), 'long': (8, 'raised')}.get(frame, (4, 2))
        bow, grip, raised = BOW[p], GRIP[p], frame == 'long'
    (fx, fy), arm = FINGER[finger]
    rows = put(VIOLIN_RAISED if raised else VIOLIN, arm + [(fx, fy), (fx + 1, fy), (fx, fy + 1)], 'X')
    rows = hand(stamp(rows, bow, 0, 0), *grip)
    return Frame('violin', rows, VIOLIN_PAL, eyes_for(frame), (), dx=5, dy=16)


# ---------------------------------------------------------------- double bass: taller than the cell's tile by far
BASS = shifted(at(4, [                # the orchestra's double bass (cx 20, tall), one to the right
    "...................ooo..........",
    "..................oWWWo.........",
    "..................oWWWo.........",
    "...................oeo..........",
    "...................oeo..........",
    "...................oeo..........",
    "...................oeo..........",
    "...................oeo..........",
    "..................ooeoo.........",
    ".................olleloo........",
    "................olwweSwlo.......",
    "................olwweSwWo.......",
    "...............olwwwewwwlo......",
    "...............olwwwewwwWo......",
    "................olwweSwWo.......",
    "................olwweSwWo.......",
    ".................olwSwWo........",
    "................olwweSwlo.......",
    "...............olwwwewwwlo......",
    "..............olwwwwewwwwlo.....",
    "..............olwwwwewwwwWo.....",
    "..............olwwwwewwwwWo.....",
    "..............olwwwwswwwwWo.....",
    "...............olwwwewwwWo......",
    "................olwwwwwWo.......",
    ".................olWWWWo........",
    "..................ooOoo.........",
    "....................O...........",
]), 1, 0)


def bass(frame: str) -> Frame:
    """The orchestra's double bass, its scroll up at the top of the cell: the Junimo, half its height, stands at its
    left with a hand up on its shoulder (the game's cheering arm) and draws a long bow across its strings below its
    face. A pulls the bow out, B pushes it in; a long note draws it right out, eyes happy. As the mod plays it the bow
    travels through EXTRA's positions the whole time a note sounds (frame 'down3' etc.)."""
    way = travel(frame)                                       # on the bow's way: pushed in (0) to pulled out (6)
    frog = 13 - way[1] if way else {'A': 8, 'B': 13, 'long': 7}.get(frame, 10)
    rows = put(BASS, [(x, 23) for x in range(frog, frog + 13)], 't')          # the stick ...
    rows = put(rows, [(x, 24) for x in range(frog + 1, frog + 13)], 'S')      # ... its hair
    rows = hand(rows, frog - 1, 23)
    return Frame('bass', rows, ORCH, eyes_for(frame), ('upR',), dx=2, dy=16)


# ---------------------------------------------------------------- harp (the cloud family): standing on a little cloud
HARP_PAL = dict(ORCH, w=C('#ffffff'), c=C('#c3cff7'), u=C('#5d6fc0'))
HARP = at(6, [                        # the orchestra's harp, behind the Junimo
    "....oooo........................",
    "...oyyyyooo.ooooo...............",
    "..oyGGGGyyyoyyylgo..............",
    "..oyGoooGGGyGGlgo...............",
    "..oyGo.sooo.Gooolgo".ljust(32, '.')[:32],
    "..oyGo.s.s.oolgo................",
    "..oyGo.s.s.solgo................",
    "..oyGo.s.s.olgo.................",
    "..oyGo.s.s.olgo.................",
    "..oyGo.s.solgo..................",
    "..oyGo.s.solgo..................",
    "..oyGo.s.olgo...................",
    "..oyGo.s.olgo...................",
    "..oyGo.solgo....................",
    "..oyGo.solgo....................",
    "..oyGo.olgo.....................",
    "..oyGo.olgo.....................",
    "..oyGooolgo.....................",
    "..oyGoolgo......................",
    "..oyGolgo.......................",
    "..oyGolgo.......................",
    "..oyglgo........................",
    "..oyglgo........................",
    "..oyglgoo.......................",
    ".oGGGGGGGo......................",
    ".oGGGGGGGo......................",
])
CLOUD = at(26, [                      # the little cloud its foot stands in
    "...uu..uu.......................",
    ".uuwwuuwwuu.....................",
    "uwwwwwwwwwwu....................",
    "uwwwwwwwcwwcu...................",
    "uccwwcccwccu....................",
    ".uuuuuuuuuu.....................",
])
PLUCK = {'hi': (9, 15), 'lo': (10, 21)}                   # a palm on a high string, on a low string


def cloud(frame: str) -> Frame:
    """The cloud family's soft pads, as a harp: the orchestra's harp, its foot in a little cloud, drawn turned so it
    stands at the Junimo's right (nothing spills over the left neighbour) - its column still lit from the left; the
    Junimo reaches across to the strings. A plucks high, B low (the string beside the hand trembles); a
    long note is a glissando, both hands spread along the strings, eyes happy."""
    back = HARP
    hands = {'A': ['hi'], 'B': ['lo'], 'long': ['hi', 'lo']}.get(frame, ['lo'])
    rows = EMPTY
    for h in hands:
        x, y = PLUCK[h]
        arm = [(14, 25), (13, 24)] + ([(12, 23), (12, 22), (12, 21), (11, 20), (11, 19), (11, 18), (11, 17)]
                                      if h == 'hi' else [(12, 23)])
        rows = hand(put(rows, arm, 'X'), x, y)
        if frame in ('A', 'B'):
            back = put(put(back, [(x - 2, yy) for yy in range(y - 2, y + 3)], '.'), [(x - 3, y), (x - 3, y + 1)], 's')
    rows = stamp(rows, [r for r in CLOUD[26:]], 0, 26)
    turned = mirrored(Frame('cloud', rows, HARP_PAL, eyes_for(frame), (), dx=14, dy=16, back=back))
    relight = str.maketrans('yG', 'Gy')                     # its column (x 26-29) lit from the left again
    turned.back = [r[:26] + r[26:30].translate(relight) + r[30:] for r in turned.back]
    return turned


# ---------------------------------------------------------------- grand piano: a concert grand, its lid up
PIANO = at(4, [                       # the orchestra's case and keys, the lid propped high, gold inside
    "...........................O....",   # its lid propped high
    "..........................OBO...",
    ".........................OBbbO..",
    "........................OBbbbbO.",
    ".......................OBbbWbbO.",
    ".......................ObbWGbbO.",
    "......................OBbWggObO.",
    ".....................OBbegggObO.",
    "....................OBbbeGGGOO..",   # the gilt prop stick
    "...................OBbbWegggO...",
    "..................OBbbWgegggO...",
    ".................OBbbWGGeGGGO...",
    "................OBbbWgggegggO...",
    "................ObbWggggegggO...",
    "...............OBbWGGGGGeGGGO...",   # the gold frame and strings under the lid
    "..............OBbWggggggegggO...",
    ".............OBbbgggggggegggO...",
    "............OBbbWGGGGGGGeGGGO...",
    "....OOOOOOBBbbbbbbbbbbbbbbbbO...",   # the case
    "...OBkbbkkbbkkbbkkbbbbbbbbbbBO..",   # keys
    "...OBkkkkkkkkkkkkkbbbbbbbbbbBO..",
    "...OBbbbbbbbbbbbbbbbbbbbbbbbO...",
    "...OBbbbbbbbbbbbbbbbbbbbbbbO....",
    "...OBbbbbbbbbbbbbbbbbbbbbbO.....",
    "....OBbOOOOOOOOOOOOOOOBbOO......",   # legs
    "....OBbO.............OBbO.......",
    "....OBbO.............OBbO.......",
    "....OBbO.............OBbO.......",
])
PIANO_HAND = {'L': {'rest': (5, 22), 'press': (6, 23)}, 'R': {'rest': (11, 22), 'press': (12, 23)}}


def piano(frame: str) -> Frame:
    """A concert grand: the orchestra's piano with its black lid propped high on a gilt stick and the gold frame and
    strings showing under it; the Junimo sits behind the keyboard (the lid a pixel clear of its cheek), both white
    gloves on the keys. A presses with the left hand (its keys go down),
    B with the right; a long note holds a chord with both, eyes happy."""
    press = {'A': 'L', 'B': 'R', 'long': 'LR'}.get(frame, '')
    rows = PIANO
    for side in 'LR':
        x, y = PIANO_HAND[side]['press' if side in press else 'rest']
        if side in press:
            rows = put(rows, [(x - 1, 23), (x + 2, 23), (x - 1, 24), (x + 2, 24)], 'K')   # its keys, pressed
        rows = hand(rows, x, y)
    return moved(Frame('piano', rows, ORCH, eyes_for(frame), (), dx=1, dy=7), 1)


# ---------------------------------------------------------------- timpani (the steel drum family): a pair of kettles
KETTLE = [                            # the big kettle, 14 wide: a thin calfskin head, a gilt counter-hoop, a deep
    "..oooooooooo..",                 # polished copper bowl
    ".oddddddddddo.",
    "oDddddddddddDo",
    "oGGgGGgGGgGGgo",
    "ohyCCCCCCCcCco",
    "ohqCCCCCCccco.",
    ".oqCCCCCccco..",
    "..ocCCCcccco..",
    "...occcccco...",
    "...oOo..oOo...",
]
KETTLE_SMALL = [                      # its smaller, higher-pitched partner, 12 wide
    "..oooooooo..",
    ".oddddddddo.",
    "oDddddddddDo",
    "oGGgGGgGGgGo",
    "ohyCCCCCcCco",
    "ohqCCCCccco.",
    ".oqCCCccco..",
    "..ocCcccco..",
    "...occcco...",
    "..oOo..oOo..",
]
TIMPANI = stamp(stamp(EMPTY, KETTLE_SMALL, 4, 22), KETTLE, 16, 22)   # side by side, off the left neighbour
STICK = {   # the left mallet: its ebony shaft, its felt head lit 'r' and shaded 'R'; the right one mirrors it
    'up': ([(6, 18), (5, 17), (5, 16)], [(4, 14)], [(5, 14), (4, 15), (5, 15)]),
    'hit': ([], [(5, 23)], [(6, 23), (5, 24), (6, 24)]),
}
GLOVE_AT = {'up': (8, 20), 'hit': (7, 21)}    # the left glove's palm: on the rim, or pressed out over the head


def steeldrum(frame: str) -> Frame:
    """The steel drum family's orchestral voice, a pair of timpani: two copper kettles with a gilt hoop, a smaller
    and a bigger, side by side in front of the Junimo, who stands just behind them, a white glove on each rim. At rest
    both mallets are up in a V (the orchestra's timpanist); A brings the left one down on its head, B the right; a
    long note is a roll on both, eyes happy."""
    left, right = {'A': ('hit', 'up'), 'B': ('up', 'hit'), 'long': ('hit', 'hit')}.get(frame, ('up', 'up'))
    rows = TIMPANI
    for pose, flip in ((left, False), (right, True)):
        shaft, lit, shade = (mirror(p) if flip else p for p in STICK[pose])
        rows = put(put(put(rows, shaft, 'e'), lit, 'r'), shade, 'R')
        x, y = GLOVE_AT[pose]
        rows = hand(rows, CW - 2 - x if flip else x, y)
    return Frame('steeldrum', rows, ORCH, eyes_for(frame), (), dx=8, dy=8)


# ---------------------------------------------------------------- crash cymbals (the drum kit's orchestral voice)
CYMBAL = [                            # one big polished brass cymbal, three-quarters on, lit from the upper left
    "..OOO..",
    ".OyyGO.",
    "OyyGGgO",
    "OyGGGgO",
    "OyGyGgO",
    "OyykGgO",
    "OyGyggO",
    "OyGGggO",
    "OyGGGgO",
    "OGGGGgO",
    "OGGGggO",
    ".OGggO.",
    "..OOO..",
]
CYMBAL_R = [r[::-1] for r in CYMBAL]
DISC = [                              # the same cymbal turned to face us: a round disc, its domed bell in the middle
    "..OOOOO..",                      # (side-on at rest, the tall ovals read as a pair of loaves)
    ".OyyyyGO.",
    "OyyGGGGgO",
    "OyGGkGGgO",                      # the bell, lit on its upper left ...
    "OyGkkgGgO",                      # ... and shaded below
    "OyGGggGgO",
    "OGGGGGGgO",
    "OGGGGGggO",
    ".OGgggOO.",
    "..OOOOO..",
]
DISC_R = [r[::-1] for r in DISC]
CRASH_ARM = [(8, 9), (8, 10), (8, 11), (8, 12), (8, 13), (9, 14), (9, 15), (9, 16), (9, 17)]   # glove down to its arm


def drums(frame: str) -> Frame:
    """The drum kit's orchestral voice: a pair of big brass crash cymbals in white gloves. At rest it holds them at
    its sides, their faces and domed bells turned to us; A crashes them together high over its hat, side-on (the
    game's cheering arms, reaching on up to them); B swings them apart over its head, faces out; a long note is the
    big crash held, eyes happy."""
    arms = ('upL', 'upR')
    if frame in ('A', 'long'):
        rows = stamp(stamp(EMPTY, CYMBAL, 9, 1), CYMBAL_R, 16, 1)
        rows = put(put(rows, CRASH_ARM, 'X'), mirror(CRASH_ARM), 'X')
        rows = hand(hand(rows, 7, 6), 23, 6)
    elif frame == 'B':
        rows = stamp(stamp(EMPTY, DISC, 4, 5), DISC_R, 19, 5)
        rows = put(put(rows, [(10, 16), (10, 17)], 'X'), mirror([(10, 16), (10, 17)]), 'X')
        rows = hand(hand(rows, 10, 13), 20, 13)
    else:
        rows = stamp(stamp(EMPTY, DISC, 4, 17), DISC_R, 19, 17)
        rows, arms = hand(hand(rows, 9, 24), 21, 24), ('L', 'R')
    return Frame('drumkit', rows, ORCH, eyes_for(frame), arms, dx=8, dy=16)


# ================================================================ winds and keys: tubular bells, pipe organ, theremin,
# French horn, clarinet, flute

# ---------------------------------------------------------------- bells: tubular bells on a gallows frame
BELLS_PAL = dict(ORCH, n=C('#6a3a10'), v=C('#fff6d8'))        # n: the brass's own dark; v: its gleam
BELLS = cell([
    "...........................oo...",   # 0  gilt finial
    "..........................oyGo..",   # 1
    "..........................oGgo..",   # 2
    "..........................oyGo..",   # 3
    ".........................oooooo.",   # 4  the post's capital
    ".........oooooooooooooooooohwoo.",   # 5  the beam
    "........oGyhhhhhhhhhhhhhhhhhhwo.",   # 6
    "........oGgwwwwwwwwwwwwwwwwwwWo.",   # 7
    ".........ooGoooooooooooooohwoo..",   # 8
    "..........oGo.n..n..n..n.ohwo...",   # 9  a carved bracket; the cords
    "...........o.nnnnnnnnnnnnohwo...",   # 10 caps
    ".............nvynvynvynvynohwo..",   # 11
    ".............nggnggnggnggnohwo..",   # 12
    ".............nvGnvGnvGnvGnohwo..",   # 13
    ".............nyGnyGnyGnyGnohwo..",   # 14
    ".............nyGnyGnyGnyGnohwo..",   # 15
    ".............nGgnGgnGgnGgnohwo..",   # 16
    ".............nGgnGgnGgnGgnohwo..",   # 17
    ".............nGgnGgnGgnGgnohwo..",   # 18
    ".............nGgnGgnGgnGgnohwo..",   # 19
    ".............nGgnGgnGgnGgnohwo..",   # 20
    ".............nGgnGgnGgnGgnohwo..",   # 21
    ".............nGgnGgnGgnnnnohwo..",   # 22 the shortest ends
    ".............nGgnGgnGgn..ohwo...",   # 23
    ".............nGgnGgnnnn..ohwo...",   # 24
    ".............nGgnGgn.....ohwo...",   # 25
    ".............nGgnnnn.....ohwo...",   # 26
    ".............nGgn........ohwo...",   # 27
    ".............nnnn.......oGGGgo..",   # 28 gilt plinth
    "........................ohllwo..",   # 29
    "........................olwwWo..",   # 30
    "........................oooooo..",   # 31
])
TUBE = {1: (13, 27), 2: (16, 25), 3: (19, 23), 4: (22, 21)}       # tube: (its left outline column, its last row)
MALLET = {   # the rawhide hammer: its handle 't' from the raised glove, and where its head sits (on a tube's cap)
    'rest': ([(17, 14), (17, 13)], (16, 10)),
    2: ([(17, 14), (18, 13)], (17, 10)),
    3: ([(17, 14), (18, 13), (19, 12)], (20, 10)),
    4: ([(17, 14), (18, 13), (19, 13), (20, 12), (21, 12)], (23, 10)),
}
HEAD = [".oo.",                  # the rawhide head, a short barrel across the handle: pale hide, lit on top
        "oddo",
        "oDDo",
        ".oo."]


def bells(frame: str) -> Frame:
    """Tubular bells: four brass tubes hung by cords from a tall gallows of polished wood (a gilt finial on the post,
    a gilt bracket under the beam's end, a gilt plinth), the longest beside it. The Junimo stands at its foot, a
    rawhide hammer up in its gloved hand, the head on the tubes' caps. A strikes the second tube, B the third (the
    struck tube swings out a pixel at its foot); a long note sweeps to the last, two tubes ringing, eyes happy and its
    free hand lifted."""
    pose = {'A': 2, 'B': 3, 'long': 4}.get(frame, 'rest')
    back = BELLS
    for k in {'A': [2], 'B': [3], 'long': [3, 4]}.get(frame, []):
        x0, end = TUBE[k]
        back = nudge(back, (x0, 17, x0 + 3, end + 1), 1, 0)
    handle, (hx, hy) = MALLET[pose]
    rows = put(hand(EMPTY, 15, 15), handle, 't')
    rows = stamp(rows, HEAD, hx, hy)
    if frame == 'long':
        rows = hand(put(rows, [(4, 17), (4, 18), (4, 19), (4, 20)], 'X'), 4, 14)
    return moved(Frame('bells', rows, BELLS_PAL, eyes_for(frame), ('upR',), dx=1, dy=16, back=back), 1)


# ---------------------------------------------------------------- organ: a façade of pipes behind, a console in front
ORGAN_PAL = dict(ORCH, L=C('#f4f6fa'), D=C('#a7afc6'), P=C('#4a5270'), M=C('#262a3e'),
                 b=C('#2a2018'), k=C('#efe2c2'), K=C('#5e4a3a'))      # ebony naturals, bone sharps, a key pressed
ORGAN_PIPES = cell([
    "........oGo..........oGo........",   # 0  gilt pinnacles on the towers
    "........PPP..........PPP........",   # 1
    ".......PPLDPP......PPLDPP.......",   # 2  the towers
    ".......PLDPLDP....PLDPLDP.......",   # 3
    ".......PLDPLDP....PLDPLDP.......",   # 4
    "....PPPPLDPLDPPPPPPLDPLDPPPP....",   # 5  the flats
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 6
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 7
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 8
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 9
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 10
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 11
    "....PMDPLDPMDPLDPLDPMDPLDPMDP...",   # 12 mouths, in a wave
    "....PMMPMDPMMPLDPLDPMMPMDPMMP...",   # 13
    "....PLDPMMPLDPMDPMDPLDPMMPLDP...",   # 14
    "....PLDPLDPLDPMMPMMPLDPLDPLDP...",   # 15
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 16
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 17
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 18
    "....PLDPLDPLDPLDPLDPLDPLDPLDP...",   # 19
    ".....PDP.PDP.PDP.PDP.PDP.PDP....",   # 20 the feet, tapering into the case
    ".....oPo.oPo.oPo.oPo.oPo.oPo....",   # 21
    "....oooooooooooooooooooooooooo..",   # 22 the case behind the console
] + ['.' * CW] * 9)
ORGAN_CONSOLE = at(23, [
    "....ooooooooooooooooooooooooo...",   # 23 the console: its lid
    "....oGhlllllllllllllllllllGWo...",   # 24
    "....oWkooooooooooooooooooWkWo...",   # 25 drawknobs at the cheeks
    "....owrwbkbkbbkbkbkbbkbkbbwrwo..",   # 26 upper manual, in the baroque way: ebony naturals, bone sharps (on
    "....owwwbbbbbbbbbbbbbbbbbbwwwo..",   # 27 ivory keys the white gloves vanished)
    "....oWkwWWWWWWWWWWWWWWWWWWwkWo..",   # 28
    "....owrwbkbkbbkbkbkbbkbkbbwrwo..",   # 29 lower manual
    "....oGGGGGGGGGGGGGGGGGGGGGGGGo..",   # 30 gilt plinth
    "....oooooooooooooooooooooooooo..",   # 31
])
ORGAN_HAND = {'L': {'rest': (9, 25), 'press': (9, 26), 'lift': (8, 22)},
              'R': {'rest': (20, 28), 'press': (20, 29), 'lift': (23, 22)}}
ORGAN_KEYS = {'L': [(8, 27), (11, 27)], 'R': [(19, 29), (22, 29)]}


def organ(frame: str) -> Frame:
    """A pipe organ: a façade of silver pipes rising behind and above it - two towers crowned with gilt pinnacles,
    flats between, the mouths in a wave - open at the sides, and in front a console of two dark manuals with
    drawknobs in its cheeks. A presses the upper manual with the left glove (its keys sink) while the right lifts; B
    the lower with the right; a long note holds a chord with both, eyes happy."""
    pose = {'A': ('press', 'lift'), 'B': ('lift', 'press'), 'long': ('press', 'press')}.get(frame, ('rest', 'rest'))
    rows = ORGAN_CONSOLE
    for side, p in zip('LR', pose):
        if p == 'press':
            rows = put(rows, ORGAN_KEYS[side], 'K')
        rows = hand(rows, *ORGAN_HAND[side][p])
    return Frame('organ', rows, ORGAN_PAL, eyes_for(frame), (), dx=8, dy=8, back=ORGAN_PIPES)


# ---------------------------------------------------------------- synth: the theremin
THEREMIN_PAL = dict(ORCH, L=C('#f4f6fa'), D=C('#a7afc6'), P=C('#4a5270'))
THEREMIN = at(9, [
    "...........................LP...",   # 9  the pitch rod, about its own height
] + ["...........................LP..."] * 12 + [
    "..........................okko..",   # 22 its ivory mount
    ".....PLLLP......................",   # 23 the volume loop, out at its left, level with the cabinet's top
    "....PL...DPoooooooooooooooooooo.",   # 24 the cabinet
    ".....PDDDP.oGhllllllllllllllGWo.",   # 25
    "...........oWokokooooGoGoGoGWWo.",   # 26 two ivory knobs; a gilt grille over the speaker
    "...........oWooooooooGoGoGoGWWo.",   # 27
    "...........oWGGGGGGGGGGGGGGGGWo.",   # 28 gilt plinth
    "...........oooooooooooooooooooo.",   # 29
    "............owo............owo..",   # 30 legs
    ".............oo............oo...",   # 31
])
THEREMIN_HAND = {   # left palm over the loop and its arm, right palm beside the rod and its arm
    'idle': ((6, 18), [(9, 18)], (23, 15), [(22, 18), (23, 17)]),
    'A': ((6, 19), [(9, 19)], (25, 15), [(22, 18), (23, 18), (24, 17), (25, 17)]),
    'B': ((6, 15), [(9, 16)], (22, 16), [(22, 18)]),
    'long': ((7, 13), [(9, 16), (9, 15)], (24, 10), [(22, 17), (23, 16), (23, 15), (24, 14), (24, 13)]),
}


def synth(frame: str) -> Frame:
    """The synth family's ancestor, a theremin: a polished cabinet on legs (gilt trim, two ivory knobs, a gilt grille
    over the speaker), a silver pitch rod rising at its right and a volume loop out at its left, level with the top.
    The Junimo stands behind it and plays the air, never touching: its right glove beside the rod, its left over the
    loop. A brings the right hand in closer and the left down, B the right out and the left up; a long note lifts the
    right high up the rod, eyes happy."""
    lp, larm, rp, rarm = THEREMIN_HAND['idle' if frame == 'blink' else frame]
    rows = put(THEREMIN, larm + rarm, 'X')
    rows = hand(hand(rows, *lp), *rp)
    return Frame('synth', rows, THEREMIN_PAL, eyes_for(frame), (), dx=8, dy=9)


# ---------------------------------------------------------------- trumpet: the French horn
HORN_PAL = dict(ORCH, n=C('#6a3a10'), m=C('#3a1a08'), v=C('#fff6d8'))
HORN = cell(['.' * CW] * 7 + [
    ".................nnnnn..........",   # 7  the coil: a closed ring of brass tubing
    "................nyyyGGn.........",   # 8
    "...............nyGGGGGGn........",   # 9
    "..............nyGgnnnGGGn....nn.",   # 10
    ".............nyGgn...nGGGn..nyyn",   # 11 the bell flares out of its side ...
    ".............nyGn.....nGGnnnnymy",   # 12
    ".............nyGn.....nGgnyyymmy",   # 13
    ".............nGGn.....nGGGGGGmmy",   # 14 ... its dark mouth turned out to the right
    ".............nGGGn...nyGGGGGymmy",   # 15
    "..............nGGGnnnyGgnggGymmn",   # 16
    "...............nGGGGGGgn.nngymmn",   # 17
    "...............nGGGgggn....nymyn",   # 18
    "...............nGgnnnn.....nyyn.",   # 19 the leadpipe down out of the coil ...
    "...............nGgn.........nn..",   # 20
    "...............nGgn.............",   # 21
    "...............nGgn.............",   # 22
    "...............nGgn.............",   # 23
    "...............nGgn.............",   # 24
    "..............nGgn..............",   # 25
    ".........nn...nGgn..............",   # 26
    "........nGgnnnnGgn..............",   # 27 ... and under its cheek to the mouthpiece at its lips
    ".........nGGGGGGgn..............",   # 28
    "..........nnnnnnn...............",   # 29
] + ['.' * CW] * 2)
BELL_BOX = (26, 9, 31, 21)                     # the bell's flare and rim, which tips on a note (its rim, x >= 28,
HORN_GLOVE = {'idle': (15, 16), 'A': (15, 17), 'B': (15, 15), 'long': (15, 17)}   # further: bells up)
HORN_DOWN = 2                                  # the whole drawing this far down, so it stands on the floor (it was drawn
                                               # two rows up: a blind look saw it hovering)


def trumpet(frame: str) -> Frame:
    """The trumpet family's orchestral voice, a French horn held up at its right in the post horn's shape: a closed
    coil of brass tubing with a big bell flaring out of its side, its dark mouth turned out, and the leadpipe from the
    mouthpiece at the bottom centre of its face, under its cheek and up into the coil. A glove works the valves on
    the coil: A presses them and the bell dips a pixel, B lifts them and the bell rises; a long note is played bells
    up. Playing, it blows with all it has (eyes > <, cheeks red, sweat on its brow). (Bow tie off. A blind test read the first - an open coil, the bell a diagonal stroke - as a
    pretzel or a letter R.)"""
    f = 'idle' if frame == 'blink' else frame
    rows = tilt(HORN, BELL_BOX, {'A': 1, 'B': -1, 'long': -2}.get(f, 0), 28)
    rows = hand(rows, *HORN_GLOVE[f])
    return Frame('trumpet', shifted(rows, 0, HORN_DOWN), HORN_PAL, eyes_blowing(frame), (), dx=2, dy=14 + HORN_DOWN,
                 dress=('hat',))


# ---------------------------------------------------------------- sax: the clarinet
CLAR_PAL = dict(ORCH, E=C('#140d0b'), e=C('#2b1d16'), F=C('#6a564c'), L=C('#f4f6fa'), D=C('#a7afc6'))
CLARINET = at(20, [
    "...........EE...................",   # 20 the mouthpiece at its lips
    "..........EFFEE.................",   # 21
    "..........EeeLLEE...............",   # 22 a silver ligature
    "...........EEDDFFEE.............",   # 23
    ".............EEeeLFEE...........",   # 24 a line of silver keys
    "...............EEeeLFEE.........",   # 25
    ".................EEeeLFEE...EEE.",   # 26 the bell flares ...
    "...................EELDLFEEEFFFE",   # 27 ... from a silver ring, dark to its rim (a silver rim read as the
    ".....................EEeeFFFeeFE",   # 28 white tip of a pool cue)
    ".......................EEeeeeeeE",   # 29
    ".........................EEeeeeE",   # 30
    "...........................EEE..",   # 31
])


CLAR_GRIP = {   # upper glove (palm), lower glove (palm) and the right arm down to it
    'idle': ((15, 22), (22, 24), [(19, 17), (20, 18), (20, 19), (21, 20), (21, 21), (22, 22)]),
    'A': ((16, 22), (23, 25), [(19, 17), (20, 18), (21, 19), (21, 20), (22, 21), (22, 22), (23, 23)]),
    'B': ((14, 22), (21, 23), [(19, 17), (20, 18), (20, 19), (21, 20), (21, 21)]),
    'long': ((15, 22), (22, 23), [(19, 17), (20, 18), (20, 19), (21, 20), (21, 21)]),
}


def sax(frame: str) -> Frame:
    """The sax family's orchestral voice, a clarinet, drawn long: black wood, a silver ligature, a line of silver keys,
    its dark bell flaring well out at the lower right. The mouthpiece is at the bottom centre of its face and both
    gloves are on it, the right arm reaching down to the lower one. A and B shift the fingers along it and tilt it a
    pixel down, then up; a long note lifts the bell proudly. Playing, it blows with all it has (eyes > <, cheeks red, sweat on its brow).
    (Bow tie off: the mouthpiece is there.)"""
    if frame == 'seat':
        return stool(frame)
    f = 'idle' if frame == 'blink' else frame
    tip = {'A': 1, 'B': -1, 'long': -2}.get(f, 0)
    rows = tilt(CLARINET, (21, 19, 31, 31), tip, 25)
    up, low, arm = CLAR_GRIP[f]
    rows = hand(hand(put(rows, arm, 'X'), *up), *low)
    return Frame('sax', rows, CLAR_PAL, eyes_blowing(frame), ('R',), dx=4, dy=7, dress=('hat',))


# the clarinettist sits, as an orchestra's winds do: a concert stool under its feet (row 22), red velvet on a polished
# rail and two legs to the floor, behind the clarinet. It's a row of its own (SEATS), drawn under the Junimo and not
# moving with it, so the Junimo can bounce on its seat as it plays.
CLAR_STOOL = at(22, [
    "......oooooooooooo..............",   # 22 the cushion's top, under its feet
    ".....orrrrrrrrrrrro.............",   # 23 red velvet
    ".....owwwwwwwwwwwwo.............",   # 24 the rail
    "......oooooooooooo..............",   # 25
    "......oWo......oWo..............",   # 26 the legs
    "......oWo......oWo..............",   # 27
    "......oWooooooooWo..............",   # 28 a stretcher between them
    "......oWo......oWo..............",   # 29
    "......oWo......oWo..............",   # 30
    "......oWo......oWo..............",   # 31 on the floor
])


def stool(frame: str) -> Frame:
    """The clarinettist's stool, alone (SEATS)."""
    return Frame('sax', EMPTY, CLAR_PAL, dx=4, dy=7, back=CLAR_STOOL, dress=(), junimo=False)


# ---------------------------------------------------------------- ocarina: the flute
FLUTE_PAL = dict(ORCH, L=C('#e9edf5'), D=C('#9aa3bb'), P=C('#3c4360'), W=C('#ffffff'), G=C('#e8b640'))
FLUTE_Y = 21                                   # its tube's row: across the bottom centre of its face, where a mouth
                                               # would be (the Junimo's face is low on it), seated on its stool
FLUTE_KEYS = (21, 28, 30)                      # columns of its gold keys (clear of its gloves)


def flute_rows() -> list[str]:
    """The flute: a slim silver tube (a bright row with gold keys, outlined above and below) from its lip plate, a
    little oval at the bottom centre of its face, straight out to the right, level. The game tips it about its seat as
    a flautist swings it, up to the sky after a phrase (ClassicalArt.Swing): drawn tipped, a rising flute read as a
    gun, and level at its feet as a stick it was dragging."""
    y = FLUTE_Y
    rows = put(EMPTY, [(8, y), (CW - 1, y)], 'P')                     # the crown's cap at its lips, the foot's end
    for x in range(9, CW - 1):
        rows = put(rows, [(x, y - 1), (x, y + 1)], 'P')
        rows = put(rows, [(x, y)], 'G' if x in FLUTE_KEYS else 'L')
    rows = put(put(rows, [(10, y - 2), (11, y - 2)], 'P'), [(10, y - 1), (11, y - 1)], 'W')   # the lip plate
    return rows


FLUTE_GRIP = {   # the near glove (palm) at its side, the far one (palm) out along it, which the right arm reaches
    'idle': ((17, 20), (24, 20)),
    'A': ((17, 21), (24, 20)),
    'B': ((17, 20), (24, 21)),
    'long': ((17, 20), (24, 20)),
}


def ocarina(frame: str) -> Frame:
    """The ocarina family's orchestral voice, a silver concert flute with gold keys, long and slim: the lip plate at
    the bottom centre of its face, the flute held level out to its right, a white glove at its side and the other out
    along it (the right arm reaching out to it), each glove outlined so it stands out on the tube. It sits on the
    clarinettist's stool, so the flute is held out at mid-height. A and B press the fingers down in turn; playing, it
    blows with all it has (eyes > <, cheeks red, sweat on its brow), and the game swings the flute with its notes and raises it to the sky
    after a phrase. (Bow tie off: the flute is there. Blind tests saw the first, a 1-pixel line at its feet, as a stick
    it was dragging; a rising one as a gun; a level one at its lap, with gloves the colour of the tube, as nothing.)"""
    if frame == 'seat':                                                # its stool, alone (SEATED)
        return Frame('ocarina', EMPTY, CLAR_PAL, dx=3, dy=7, back=shifted(CLAR_STOOL, -1), dress=(), junimo=False)
    f = 'idle' if frame == 'blink' else frame
    near, far = FLUTE_GRIP[f]
    arm = line(19, 17, far[0] - 2, far[1] - 1)                       # from its shoulder (the game's arm) to the glove
    rows = hand(hand(put(flute_rows(), arm, 'X'), *near), *far)
    return Frame('ocarina', rows, FLUTE_PAL, eyes_blowing(frame), ('R',), dx=3, dy=7, dress=('hat',))


# ================================================================ strings and whimsy: lute, singer, glass harp, sitar,
# conductor

# ---------------------------------------------------------------- guitar: a lute
LUTE_PAL = {
    'o': C('#3a1d12'),                                             # the orchestra's wood outline
    'a': C('#b8662c'), 'h': C('#e8a060'), 'A': C('#8a4418'),       # the soundboard: amber varnish, lit edge, shade
    'W': C('#6b3214'), 'w': C('#9c4e22'),                          # the pegbox's dark; the neck's lit edge
    'k': C('#2b1d16'), 'L': C('#e8b640'),                          # the carved rosette and its gilt lace
    'e': C('#2b1d16'), 's': C('#dccaa0'), 'p': C('#f3ead6'),       # ebony (neck, bridge, pegbox); gut strings; pegs
    'G': C('#e8b640'),                                             # gilt (the nut)
}
LUTE = cell(['.' * CW] * 9 + [   # held across it as a lutenist holds it: a tilted pear low in front of its left side,
    "..........................ooo...",   # 9   the long neck up past its right cheek, the pegbox bent back at a
    ".........................owGWp..",   # 10  right angle at the top (the lute's own sign); the gilt nut
    ".........................oseWWop",   # 11
    "........................owseeeWo",   # 12
    ".......................owseepoeW",   # 13
    "......................owseo.poo.",   # 14
    ".....................owseo......",   # 15  neck: a lit edge, the strings, ebony
    "....................owseo.......",   # 16
    "...................owseo........",   # 17
    "..................owseo.........",   # 18
    ".................owseo..........",   # 19
    "................owseo...........",   # 20
    "...............owseo............",   # 21
    "............oohhsAo.............",   # 22  the body: amber soundboard lit on its upper edge ...
    "..........oohaasaAo.............",   # 23
    "........oohaakksaAo.............",   # 24  ... the carved rosette with its gilt lace ...
    ".......ohaaakLkkaAo.............",   # 25
    "......ohaaaakkLkAo..............",   # 26
    "......ohaaaaskkaAo..............",   # 27
    "......ohaaasaaaAo...............",   # 28
    "......ohaesaaaAo................",   # 29  ... and the bridge
    ".......oheeaAAo.................",   # 30
    "........ooooo...................",   # 31
])
LUTE_DX, LUTE_DY = 4, 8                # behind the body, its face just above it; the neck a pixel clear of its cheek
LUTE_STRING = [(12, 27), (11, 28), (10, 29)]             # the strings from the rosette to the bridge
LUTE_TREMBLE = [(13, 27), (10, 28), (11, 29)]            # ... plucked: they shiver either side
LUTE_PLUCK = {   # the plucking glove (its palm) and its forearm, from the bottom of its left side over the soundboard
    'rest': ((10, 26), line(6, 21, 8, 24)),                  # on the strings below the rosette
    'pluck': ((11, 28), line(6, 21, 9, 26)),                 # pulled down through them
    'lift': ((6, 23), []),                                   # lifted off, beside the soundboard
    'flourish': ((5, 13), []),                               # raised out at its side with a little lift
}
LUTE_FRET = {'mid': ((20, 17), []), 'high': ((22, 15), [(20, 16)]), 'low': ((18, 19), [])}


def lute(frame: str) -> Frame:
    """A grand lute held across it as a lutenist holds it: the pear body tilted low in front of its left side (the
    Junimo just behind it), the carved rosette laced in gilt, and the long neck up past its right cheek to the pegbox
    bent back at the top. Its left glove plucks below the rosette and its right glove stops the strings on the neck.
    A pulls the strings (they shiver) while the fretting hand climbs the neck; B lifts the plucking hand off and
    slides the other down towards the body; a long note lets the strings ring, the plucking hand raised in a flourish
    and its eyes happy. (Smaller and tilted after a blind test read the first - a body bigger than the Junimo, upright
    under it, its round ribbed back showing - as the Junimo sitting in a pot.)"""
    pluck, fret = {'A': ('pluck', 'high'), 'B': ('lift', 'low'),
                   'long': ('flourish', 'mid')}.get(frame, ('rest', 'mid'))
    rows = LUTE
    if frame in ('A', 'long'):
        rows = put(put(rows, LUTE_STRING, 'a'), LUTE_TREMBLE, 's')
    for (x, y), arm in (LUTE_PLUCK[pluck], LUTE_FRET[fret]):
        rows = hand(put(rows, arm, 'X'), x, y)
    return Frame('guitar', rows, LUTE_PAL, eyes_for(frame), (), dx=LUTE_DX, dy=LUTE_DY)


# ---------------------------------------------------------------- choir: a soloist at a gilt music stand
STAND_PAL = {
    'K': C('#18141f'), 'P': C('#efe3c4'), 'l': C('#9a8fa8'), 'n': C('#18141f'),   # the score: black covers, cream
    'O': C('#3a2810'), 'G': C('#e8b640'), 'g': C('#b07a22'),                     # pages, staves, notes; the gilt stand
}
STAND = at(13, [                      # at its right: the open score on the desk, a gilt ledge, a tripod
    "...................KKKKK.KKKKK..",
    "..................KPPPPPKPPPPPK.",
    "..................KPllllKllllPK.",
    "..................KPnPPPKPPnPPK.",
    "..................KPllllKllllPK.",
    "..................KPPPPPKPPPPPK.",
    "..................OGGGGGGGGGGGO.",
    "....................OgggggggO...",
    ".......................OGO......",
    ".......................OGO......",
    ".......................OGO......",
    ".......................OGO......",
    ".......................OGO......",
    ".......................OGO......",
    ".......................OGO......",
    "......................OGGGO.....",
    ".....................OGO.OGO....",
    "....................OGO...OGO...",
    "....................OO.....OO...",
])
SOLO_HAND = {   # the free (left) glove's palm and its arm, kept off its body (on it, a white glove reads as an eye)
    'side': ((5, 23), []),                                    # at its side, ready
    'out': ((5, 16), [(7, 19), (8, 20)]),                     # flung out wide: the aria
    'hat': ((7, 18), []),                                     # to the brim of its top hat, a gentleman's flourish
    'high': ((5, 13), [(6, 16), (6, 17), (7, 18), (8, 19)]),  # flung up high for the last note
}


def singer(frame: str) -> Frame:
    """The choir's soloist: a gilt music stand at its right with the open score on its desk (cream pages, staves and
    notes between black covers), its right glove on the ledge keeping its place, its left glove free for the aria. On
    A the arm is flung out wide; on B the glove goes to the brim of its top hat; on a long note the arm is flung up
    high, eyes happy."""
    (x, y), arm = SOLO_HAND[{'A': 'out', 'B': 'hat', 'long': 'high'}.get(frame, 'side')]
    rows = hand(put(STAND, [(17, 20)], 'X'), 19, 19)
    rows = hand(put(rows, arm, 'X'), x, y)
    return Frame('choir', rows, STAND_PAL, eyes_for(frame), (), dx=5, dy=16)


# ---------------------------------------------------------------- stardust: a glass harp
GLASS_PAL = {
    'o': C('#2a100c'), 'W': C('#4a1e14'), 'w': C('#6e2c1c'), 'l': C('#8e3c24'), 'h': C('#b8603a'),   # rosewood
    'G': C('#e8b640'), 'g': C('#b07a22'), 'y': C('#ffe08a'),                                       # gilt
    'c': C('#c8f0fa'), 'C': C('#5fa8c8'), 'v': C('#ffffff'),                       # crystal: lit, shade, glint
    'u': C('#e6faff'), 'q': C('#8ccfe8'),                                          # water: its surface, depth
}
GLASS_TABLE = at(21, [    # an elegant little table: polished rosewood, a gilt moulding, a carved cartouche, curved legs
    ".....oooooooooooooooooooooo.....",   # 21 the back edge, behind the glasses' feet
    "....ohhhhhhhhhhhhhhhhhhhhhho....",   # 22 the polished top, lit
    "....owwwwwwwwwwwwwwwwwwwwwwo....",   # 23 its front edge
    "....oGyGGGGGGGGGGGGGGGGGGGgo....",   # 24 the gilt moulding
    ".....owWWWWWWWgyGgWWWWWWWwo.....",   # 25 the apron and its gilt cartouche
    ".....oowWWWWWWWgWWWWWWWWwoo.....",   # 26
    "......owoooooooooooooooowo......",   # 27 knees
    "......owo..............owo......",   # 28 cabriole legs, curving in ...
    "......owo..............owo......",   # 29
    ".....owo................owo.....",   # 30 ... and their feet kicking out
    ".....oo..................oo.....",   # 31
])


def wine_glass(bowl: int, water: int, ripple: bool = False) -> list[str]:
    """A 5-wide crystal wine glass: a bowl `bowl` rows deep (its lip lit, a glint on it), a stem and a foot, holding
    `water` rows of water. Rippling, its surface jumps a pixel in the middle."""
    g = [list("v...C")] + [list("c...C") for _ in range(bowl - 2)] + [list(".c.C."), list("..C.."), list("..C.."),
                                                                         list(".cCC.")]
    for r in range(bowl - water, bowl):
        for x in ((2,) if r == bowl - 1 else (1, 2, 3)):
            g[r][x] = 'q'
    s = bowl - water
    for x in ((2,) if s == bowl - 1 else (1, 2, 3)):
        g[s][x] = 'u'
    if ripple and s < bowl - 1:
        g[s][2] = 'q'
        g[s - 1][2] = 'u'
    return [''.join(r) for r in g]


GLASSES = [(5, 7, 4), (10, 4, 2), (17, 4, 1), (22, 6, 3)]   # (x, bowl rows, water rows): a set tuned by size, its feet
GLASS_FEET = 22                                              # on the table top; beside its face they rise tallest
GLASS_DX, GLASS_DY = 8, 2                                    # it stands behind the table, its face above the rims
GLASS_HAND = {   # glove palm on a rim, arm out to it
    'L1': ((6, 12), [(9, 11)]), 'L2': ((9, 15), []),
    'R3': ((21, 15), []), 'R4': ((23, 13), [(22, 11), (22, 12)]),
    'flourish': ((6, 7), [(9, 10), (8, 10)]),
}


def glass_harp(frame: str) -> Frame:
    """A glass harp: four crystal wine glasses, tuned by size and water, on an elegant little rosewood table (gilt
    moulding, a carved cartouche, cabriole legs); the Junimo stands behind it, its face above the rims, a white glove
    on the rim of each inner glass. On A its left hand moves out to the tallest glass, whose water ripples; on B the
    left comes back and the right moves out to the far glass, rippling it; on a long note that glass rings on while
    the left hand lifts in a flourish, eyes happy."""
    left, right = {'A': ('L1', 'R3'), 'B': ('L2', 'R4'), 'long': ('flourish', 'R4')}.get(frame, ('L2', 'R3'))
    ringing = {'L1': 0, 'R4': 3, 'L2': 1, 'R3': 2}
    rows = GLASS_TABLE
    for i, (x, bowl, water) in enumerate(GLASSES):
        played = frame in ('A', 'B', 'long') and i in (ringing.get(left), ringing.get(right)) and \
            (frame != 'A' or i == 0) and (frame != 'B' or i == 3)
        rows = stamp(rows, wine_glass(bowl, water, played), x, GLASS_FEET - bowl - 2)
    for g in (left, right):
        (x, y), arm = GLASS_HAND[g]
        rows = hand(put(rows, arm, 'X'), x, y)
    return Frame('stardust', rows, GLASS_PAL, eyes_for(frame), (), dx=GLASS_DX, dy=GLASS_DY)


# ---------------------------------------------------------------- kalimba: a sitar
SITAR_PAL = {
    'o': C('#28100c'), 'W': C('#4a1a12'), 'w': C('#7a2c1a'), 'l': C('#a8482a'), 'h': C('#d27a48'),   # dark toon wood
    'G': C('#e8b640'), 's': C('#d8d8e0'), 'i': C('#f0e2c4'),            # brass frets, steel strings, bone inlay
}
SITAR = cell([            # standing on its big gourd at the Junimo's right, the neck leaning away up to the pegs
    "................................",
    "........................oWWo....",   # the pegbox and its bone pegs
    "......................iooWWooi..",
    "........................oWWo....",
    "......................iooWWooi..",
    ".......................ooWWoo...",
    "......................ohwwwwWo..",   # the small gourd tucked behind the neck
    "......................owwwwwWo..",
    ".......................oWWWWo...",   # the wide neck: brass frets, steel strings
    "......................olGGWo....",
    "......................olssWo....",
    ".....................iolssWo....",   # bone pegs for the sympathetic strings
    ".....................olGGWo.....",
    ".....................olssWo.....",
    "....................iolssWo.....",
    "....................olGGWo......",
    "....................olssWo......",
    "...................iolssWo......",
    "...................olGGWo.......",
    "...................olssWo.......",
    "...................olssWo.......",
    "..................olGGWo........",
    "..................oiiiiiio......",   # a collar of bone
    "................oohhlwwwwWoo....",   # the big polished gourd
    "...............ohhlwwwwwwwwWo...",
    "...............ohlwwwwwwwwwWo...",
    "...............olwwwwwwwwwwWo...",
    "...............olwwwwiiiwwwWo...",   # the bridge
    "...............olwwwwwwwwwWWo...",
    "................owwwwwwwwWWo....",
    ".................oWWWWWWWWo.....",
    "..................oooooooo......",
])
SITAR_PLUCK = {'rest': (19, 22), 'pluck': (20, 24), 'lift': (18, 18)}   # the right glove at the neck's foot


def sitar(frame: str) -> Frame:
    """A towering sitar standing on its big polished gourd at the Junimo's right, its wide neck - a ladder of brass
    frets, steel strings, bone pegs down its side - leaning away up past its hat to the small gourd and the pegbox at
    the top of the cell. Its white-gloved right hand plucks at the neck's foot: A pulls down onto the gourd, B lifts
    it high up the neck; a long note lifts it there, eyes happy. (Drawn upright beside it after a blind test read the
    first version, slung diagonally behind it, as a second lute it wasn't playing.)"""
    p = {'A': 'pluck', 'B': 'lift', 'long': 'lift'}.get(frame, 'rest')
    return Frame('kalimba', hand(SITAR, *SITAR_PLUCK[p]), SITAR_PAL, eyes_for(frame), (), dx=2, dy=16)


# ---------------------------------------------------------------- songbird: a conductor and a songbird
MAESTRO_PAL = {
    'o': C('#8a4512'), 'Y': C('#ffd630'), 'y': C('#eaa21f'),       # the canary: a dark of its yellow, yellow, shade
    'K': C('#3b2a20'), 'w': C('#fff4c8'), 'b': C('#ff8a2e'),       # its dark wing and tail, a wing bar, orange beak
    'k': C('#2a1a10'),                                             # and feet; its eye
    'v': C('#ffffff'), 'V': C('#8e9cb8'),                          # the white baton and its grey-blue underside
    'O': C('#2a140a'), 'W': C('#4a2614'), 'm': C('#7a4a2a'),       # the podium's polished wood: outline, shade, face,
    'h': C('#b07a4a'), 'G': C('#e8b640'), 'g': C('#b07a22'),       # lit top; its gilt
}
PODIUM = at(14, [         # behind it: the podium it stands on, and the rail's post at its right, the canary's perch
    ".........................GG.....",   # 14 the post's gilt cap
    ".........................hW.....",   # 15 the post, in the podium's wood (in gilt, the yellow bird melted into it)
    ".........................hW.....",   # 16
    ".........................hW.....",   # 17
    "..........hhhhhhhhhhhhhhhhW.....",   # 18 the rail, behind it
    "..........WWWWWWWWWWWWWWWWW.....",   # 19
] + [".........................hW....."] * 6 + [
    "....OOOOOOOOOOOOOOOOOOOOOOOO....",   # 26 the podium: its top, seen from above
    "....OhhhhhhhhhhhhhhhhhhhhhmO....",   # 27
    "....OGGGGGGGGGGGGGGGGGGGGGGO....",   # 28 a gilt edge
    "....OmWWWWWWWWWWWWWWWWWWWWWO....",   # 29 its front
    "....OmWgggggggggggggggggWWWO....",   # 30 a gilt panel
    "....OOOOOOOOOOOOOOOOOOOOOOOO....",   # 31
])
CANARY = {                # side-on on the post's cap, facing the conductor; 10x9, feet on the cap
    'idle': ["...ooo....",
             "..oYYYo...",
             ".oYkYYYo..",
             "bbYYYYYYo.",
             ".oYYYYKKo.",
             ".oyYYKwKKo",
             "..oyyKKKKK",
             "...oooo.KK",
             "...b..b..."],
    'A':    [".b.ooo....",            # sings: head up, beak open wide
             "..bYYYo...",
             ".oYkYYYo..",
             "bbYYYYYYo.",
             ".oYYYYKKo.",
             ".oyYYKwKKo",
             "..oyyKKKKK",
             "...oooo.KK",
             "...b..b..."],
    'B':    ["...ooo....",            # sings on, wings lifted
             "..oYYYoKK.",
             "bboYkYYoKK",
             "...oYYYYKo",
             "bboYYYYwKo",
             ".oyYYYKKo.",
             "..oyyKKKKK",
             "...oooo.KK",
             "...b..b..."],
    'long': ["b.b.......",            # holds the note: head thrown back, beak wide open to the sky
             ".b.ooo....",
             ".boYYYo...",
             ".oYkYYYo..",
             ".oYYYYYYo.",
             ".oyYYYKKKo",
             "..oyyKwKKK",
             "...oooo.KK",
             "...b..b..."],
}
MAESTRO_DX, MAESTRO_DY = 8, 11        # on its tile, feet on the podium's top
CANARY_AT = (22, 5)                   # on the post's cap, clear of the free hand
BATON = {   # the baton glove (palm), its arm, and the baton from the glove to its tip
    'ready': ((7, 19), [], (6, 17), (4, 12)),                  # poised up beside it
    'up': ((6, 14), [(9, 17)], (5, 12), (2, 8)),               # the up-beat: high, pointing up and out
    'down': ((7, 24), [], (9, 26), (13, 30)),                  # the down-beat: low, its tip down on the podium
    'cut': ((6, 13), [(9, 17), (8, 16)], (6, 11), (6, 6)),     # the final cut-off: straight up
}
FREE_HAND = {   # the free glove (palm) and its arm: on the rail, lower down the post, lifted up it (a row of post
    # left clear under the bird) and opened beside the bird, to the soloist
    'ready': ((23, 19), []), 'up': ((23, 22), []), 'down': ((23, 17), []), 'cut': ((22, 14), [(22, 17)]),
}


def baton(rows: list[str], start: tuple[int, int], tip: tuple[int, int]) -> list[str]:
    """A white baton from the glove to its tip, shaded grey-blue along its underside (so it shows on snow)."""
    shaft = line(*start, *tip)
    return put(put(rows, [(x, y + 1) for x, y in shaft], 'V'), shaft, 'v')


def maestro(frame: str) -> Frame:
    """The maestro on its podium (polished wood, a gilt edge), a white baton in its left glove, conducting a canary
    perched on the rail's post at its side. At rest the baton is poised up beside it, the free glove on the rail, and
    the bird listens; on A it beats up and out (the tip high in the sky) and the bird sings, head up, beak open; on B
    it beats down (the tip on the podium's front) while the free hand lifts up the post and the bird sings on, wings
    lifted; on a long note the baton goes straight up for the cut-off and the free hand opens to the soloist, eyes
    happy, as the bird throws its head back and holds the note. (A blind test read the first version, with the bird
    on its top hat, as a stage magician pulling a bird from his hat.)"""
    if frame == 'tip':
        return maestro_bow()
    beat = {'A': 'up', 'B': 'down', 'long': 'cut'}.get(frame, 'ready')
    rows = stamp(EMPTY, CANARY.get(frame, CANARY['idle']), *CANARY_AT)
    (x, y), arm, start, tip = BATON[beat]
    rows = hand(baton(put(rows, arm, 'X'), start, tip), x, y)
    (x, y), arm = FREE_HAND[beat]
    rows = hand(put(rows, arm, 'X'), x, y)
    return Frame('songbird', rows, MAESTRO_PAL, eyes_for(frame), (), dx=MAESTRO_DX, dy=MAESTRO_DY, back=PODIUM)


TOP_HAT = [               # the top hat lifted off its head by the brim: crown, sheen, red band, brim
    "..nnnq..",
    "..nnnq..",
    "..nnnn..",
    "..tttt..",
    "nnnnnnnn",
]
HAT_PAL = {'n': HAT_K, 'q': HAT_HI, 't': TIE}


def maestro_bow() -> Frame:
    """The bow, turned to the audience when the music is over: it lifts its top hat a little off its head by the brim
    (the baton put away), the sprout under it bare, eyes happy, the free hand on the rail; the canary sings on."""
    rows = stamp(EMPTY, CANARY['long'], *CANARY_AT)
    rows = hand(put(rows, line(10, 16, 10, 13), 'X'), 10, 10)
    (x, y), arm = FREE_HAND['ready']
    rows = hand(put(rows, arm, 'X'), x, y)
    return Frame('songbird', rows, dict(MAESTRO_PAL, **HAT_PAL), 'happy', (), dx=MAESTRO_DX, dy=MAESTRO_DY,
                 back=stamp(PODIUM, TOP_HAT, 12, 6), dress=('tie',))


# the frames beyond the five every family has, in their own columns only: the conductor's bow (row 5), and the bowed
# strings' bow on its way (rows 5 on: see the top)
EXTRA = {'songbird': ['tip'],
         'sax': ['seat'], 'ocarina': ['seat'],                # SEATED: the seat it sits on, drawn under it
         'violin': [f'{kind}{p}' for kind in ('down', 'up', 'long') for p in range(BOW_WAY + 1)],
         'bass': [f'{kind}{p}' for kind in ('down', 'long') for p in range(7)]}


# ---------------------------------------------------------------- the families
KIT = {'piano': piano, 'bells': bells, 'organ': organ, 'guitar': lute, 'bass': bass, 'violin': violin,
       'choir': singer, 'trumpet': trumpet, 'sax': sax, 'ocarina': ocarina, 'synth': synth, 'cloud': cloud,
       'stardust': glass_harp, 'kalimba': sitar, 'steeldrum': steeldrum, 'songbird': maestro, 'drumkit': drums}


SEATED = ('sax', 'ocarina')                                   # families that sit on a seat of their own (row 5)


def drawn(family: str, frame: str, base: Image.Image) -> Image.Image:
    """A frame as the game draws it: on its seat, if it sits."""
    im = KIT[family](frame).image(base)
    if family not in SEATED:
        return im
    seat = KIT[family]('seat').image(base)
    seat.alpha_composite(im)
    return seat


def ordered() -> list[str]:
    return sorted(KIT, key=lambda f: FAMILY[f][0])


def neighbour_hits(base: Image.Image) -> dict[tuple[str, str], int]:
    """A row of blocks is drawn left to right, so what a block spills to its left (x < 8) lands on its left
    neighbour at x + 16. For every pair of families (left, right): how many of the right one's spilled pixels, in
    any frame, cover the left one's face (GUARD) in any frame."""
    spill, face = {}, {}
    for f, draw in KIT.items():
        frames = [draw(fr) for fr in FRAMES + EXTRA.get(f, [])]
        spill[f] = set().union(*({(x + 16, y) for y in range(CH) for x in range(TILE) if im.getpixel((x, y))[3]}
                                 for im in (fr.image(base) for fr in frames)))
        face[f] = set().union(*({(x + fr.dx, y + fr.dy) for x, y in GUARD} for fr in frames))
    return {(a, b): n for a in KIT for b in KIT if (n := len(face[a] & spill[b]))}


def build(base: Image.Image) -> Image.Image:
    rows = len(FRAMES) + max(map(len, EXTRA.values()))
    sheet = Image.new('RGBA', (CW * len(FAMILY), CH * rows), (0, 0, 0, 0))
    for family, draw in KIT.items():
        for r, frame in enumerate(FRAMES + EXTRA.get(family, [])):
            sheet.alpha_composite(draw(frame).image(base), (FAMILY[family][0] * CW, r * CH))
    return sheet


# ---------------------------------------------------------------- previews
def frames_board(base: Image.Image, per_column: int = 9) -> Image.Image:
    """Every frame of each family, one family per row."""
    fams = ordered()
    cols, rows = (len(fams) + per_column - 1) // per_column, min(len(fams), per_column)
    cw = (CW + 2) * len(FRAMES) + 8
    board = Image.new('RGBA', (cw * cols + 2, (CH + 2) * rows + 2), GRASS)
    for i, f in enumerate(fams):
        for c, frame in enumerate(FRAMES):
            board.alpha_composite(drawn(f, frame, base),
                                  (2 + (i // per_column) * cw + c * (CW + 2), 2 + (i % per_column) * (CH + 2)))
    return zoom(board, 4)


def conductor(base: Image.Image) -> Image.Image:
    """The conductor's every frame: the five every family has, then the bow."""
    frames = FRAMES + EXTRA['songbird']
    board = Image.new('RGBA', ((CW + 2) * len(frames) + 2, CH + 4), GRASS)
    for i, frame in enumerate(frames):
        board.alpha_composite(maestro(frame).image(base), (2 + i * (CW + 2), 2))
    return zoom(board, 4)


def band(cells: list[Image.Image], ground=GRASS) -> Image.Image:
    """Blocks side by side on 16-pixel tiles as on the farm: each 32-wide cell spills into its neighbours, the later
    one drawn over the earlier (the game draws a row left to right)."""
    row = Image.new('RGBA', (16 * len(cells) + 16, CH), ground)
    for i, im in enumerate(cells):
        for sx in range(4, 12):
            row.putpixel((8 + i * 16 + sx, CH - 1), SHADOW)
        row.alpha_composite(im, (i * 16, 0))
    return row


def lineup(base: Image.Image, per_row: int = 9) -> Image.Image:
    """For each family its pixel-set icon on a tile, and below it the classical block (idle) in a row."""
    fams = ordered()
    out = Image.new('RGBA', (16 * per_row + 16, (16 + CH + 6) * len(rows_of(fams, per_row))), (0, 0, 0, 0))
    for r, group in enumerate(rows_of(fams, per_row)):
        y = r * (16 + CH + 6)
        icons = Image.new('RGBA', (16 * per_row + 16, 16), GRASS)
        for i, f in enumerate(group):
            icons.alpha_composite(pixel_block(FAMILY[f][0]), (8 + i * 16, 0))
        out.alpha_composite(icons, (0, y))
        out.alpha_composite(band([drawn(f, 'idle', base) for f in group]), (0, y + 18))
    return zoom(out, 4)


def gif(base: Image.Image, path: Path, z: int = 3, per_row: int = 9) -> None:
    """The whole band on rows of tiles, playing: A and B on alternate beats, a long note at the end of each phrase."""
    cache = {(f, fr): drawn(f, fr, base) for f in KIT for fr in FRAMES}
    groups = rows_of(ordered(), per_row)
    frames = []
    for t in range(48):
        beat, phase = divmod(t, 4)
        for_f = lambda f: ('blink' if (t + FAMILY[f][0] * 5) % 37 == 0 else 'idle') if beat >= 10 else \
            ('long' if beat % 5 == 4 else 'AB'[beat % 2]) if phase < 3 else 'idle'
        im = Image.new('RGBA', (16 * per_row + 16, CH * len(groups)), GRASS)
        for r, group in enumerate(groups):
            im.alpha_composite(band([cache[(f, for_f(f))] for f in group]), (0, r * CH))
        frames.append(zoom(im, z).convert('RGB'))
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=120, loop=0, optimize=False)


if __name__ == '__main__':
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(os.environ.get('JUNIMO_PNG', DEFAULT_JUNIMO))
    base = Image.open(path).convert('RGBA').crop((0, 0, 16, 16))
    bad = {(f, fr): hits for f, draw in KIT.items() for fr in FRAMES + EXTRA.get(f, []) if (hits := draw(fr).face_hits())}
    if bad:
        sys.exit(f'covers the face: {bad}')
    if bad := neighbour_hits(base):
        sys.exit(f"covers a left neighbour's face (left, right): {bad}")
    out = ROOT / 'assets' / 'textures' / 'junimo_classical.png'
    build(base).save(out)
    if KIT:
        prev = ROOT / 'art' / 'preview'
        frames_board(base).save(prev / 'junimo_classical_frames.png')
        lineup(base).save(prev / 'junimo_classical_icons.png')
        conductor(base).save(prev / 'junimo_classical_conductor.png')
        gif(base, prev / 'junimo_classical.gif')
    print('wrote', out, f'({len(KIT)} of {len(FAMILY)} families)')
