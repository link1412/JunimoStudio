"""Junimo blocks, modern set: the game's own Junimo playing each family's instrument - the second look for placed blocks.

    python3 junimo_blocks.py [Junimo.png]      # Junimo.png = the game's Characters/Junimo.png

Writes assets/textures/junimo_blocks.png: one 16x24 cell per family (column = Family.Index) and frame (row), drawn standing on
its tile like a big craftable - the bottom 16 rows are the tile, the top 8 rise above it (so a Junimo can stand behind
a big drum); nothing is ever wider than the tile:

    0 idle (also the icon)   1 blink   2 play A   3 play B   4 long note   5 what it stands on (STANDS), or nothing

A and B alternate on successive notes (strum down / up, left cymbal / right cymbal); on a long note the Junimo shuts its
eyes and savours it.

Rules:
- the Junimo is the game's own (Characters/Junimo frame 0 - the one that comes out of a Junimo Hut, as in the Voyage
  orchestra), the same size, tinted like the game tints it; only its arms (the game's own poses, or short one-pixel
  forearms) and eyes (open, or shut as in its cheering frames) change. It has no mouth, and its body never squashes,
  stretches, leans or hops. A few wear something small for character (WEAR: shades, a hat, a beret, a bow);
- most stand at one spot (dy=1, feet on the cell's row 16), so a row of blocks keeps one eye-line; the instrument goes
  in front of and below them, and one whose instrument doesn't reach the floor stands on something that does (STANDS:
  a speaker cabinet, a hay bale), or it hovers;
- the instruments are drawn like the game's own (the band's drum kit in Maps/samshowtiles, Sam's guitars): plain game
  pixels, every material outlined in a dark shade of its own colour, one-pixel stands and sticks;
- the face is sacred: nothing is drawn over the eyes (nor right next to them), their glints or the blush - the build
  fails if anything is (the trumpeter's shades are the one thing allowed there);
- nothing is wider than its tile, so blocks side by side never overlap; and there are no flashes: the instrument
  being played is the effect.
"""
from __future__ import annotations

import os
import re
import sys
from pathlib import Path
from PIL import Image

from px import hex2rgba, zoom

C = hex2rgba
T = 16                                         # tile width
H = 24                                         # cell height: the tile, and 8 rows above it
ROOT = Path(__file__).resolve().parent.parent
FRAMES = ['idle', 'blink', 'A', 'B', 'long']
DEFAULT_JUNIMO = Path.home() / 'Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS' \
    / 'Content (unpacked)/Characters/Junimo.png'

# the families' order and accents, straight from the mod
FAMILY = {m[0]: (int(m[1]), m[2]) for m in re.findall(
    r'new Family\("(\w+)", (\d+), \d+, "(#[0-9a-f]{6})"', (ROOT / 'src/Data/Families.cs').read_text())}


def lighten(hexcol: str, k: float = 0.3) -> tuple[int, int, int, int]:
    """The Junimo's tint: the family accent lifted towards white (the game multiplies its grey Junimo by the tint, and
    the plain accent comes out darker than the pastel blocks and the panel)."""
    r, g, b, _ = C(hexcol)
    return tuple(round(c + (255 - c) * k) for c in (r, g, b)) + (255,)


def tint(im: Image.Image, t) -> Image.Image:
    r, g, b, a = im.split()
    return Image.merge('RGBA', [ch.point(lambda v, k=k: v * t[k] // 255) for k, ch in enumerate((r, g, b))] + [a])


def grid(rows: list[str], pal: dict) -> Image.Image:
    im = Image.new('RGBA', (T, H), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch not in '. ':
                im.putpixel((x, y), pal[ch])
    return im


def put(rows: list[str], pts, ch: str) -> list[str]:
    out = [list(r.ljust(T, '.')) for r in rows]
    for x, y in pts:
        if 0 <= x < T and 0 <= y < len(out):
            out[y][x] = ch
    return [''.join(r) for r in out]


def cell(rows: list[str]) -> list[str]:
    """A full-cell drawing, checked: H rows of T."""
    assert len(rows) == H and all(len(r) == T for r in rows), [len(r) for r in rows]
    return rows


def at(top: int, lines: list[str]) -> list[str]:
    """A drawing that starts at row `top` of the cell (rows above and below it are empty)."""
    return cell(['.' * T] * top + lines + ['.' * T] * (H - top - len(lines)))


def mirror(pts):
    """Points mirrored across the tile."""
    return [(T - 1 - x, y) for x, y in pts]


def slide(rows: list[str], dx: int) -> list[str]:
    """The same drawing slid dx pixels sideways (nothing may fall off the tile)."""
    out = []
    for r in rows:
        assert not (dx < 0 and r[:-dx].strip('.')) and not (dx > 0 and r[T - dx:].strip('.')), 'slides off the tile'
        out.append(('.' * dx + r[:T - dx]) if dx >= 0 else (r[-dx:] + '.' * -dx))
    return out


def stamp(rows: list[str], sprite: list[str], x0: int, y0: int) -> list[str]:
    """Paint a small sprite (rows of chars, '.' = clear) onto a cell at (x0, y0), one colour at a time with put."""
    for ch in sorted({c for r in sprite for c in r} - {'.'}):
        rows = put(rows, [(x0 + i, y0 + j) for j, r in enumerate(sprite) for i, c in enumerate(r) if c == ch], ch)
    return rows


def eyes_for(frame: str) -> str:
    """Its eyes shut on a blink and through a long note."""
    return 'shut' if frame in ('blink', 'long') else 'open'


def eyes_rapt(frame: str) -> str:
    """Its eyes shut whenever it plays, open only at rest: a singer's or a wind player's (the tell for blowing, as it
    has no mouth to blow with)."""
    return 'open' if frame == 'idle' else 'shut'


EMPTY = ['.' * T] * H

# ---------------------------------------------------------------- the game's Junimo
JB, JA, JD = (55, 55, 81, 255), (196, 206, 206, 255), (134, 134, 153, 255)
ARMS = {                                       # frame 0's little arms, and frame 44's cheering ones
    'L': [(0, 9), (1, 10)], 'R': [(15, 9), (14, 10)],
    'upL': [(2, 2), (1, 3), (1, 4), (1, 5), (1, 6)], 'upR': [(13, 2), (14, 3), (14, 4), (14, 5), (14, 6)],
}
FACE = {(5, 11), (10, 11), (5, 12), (10, 12), (4, 12), (11, 12)}           # eyes, glints, blush
GUARD = FACE | {(4, 11), (6, 11), (9, 11), (11, 11), (5, 10), (10, 10)}     # ... and a pixel round each eye


def junimo16(base: Image.Image, eyes: str = 'open', arms: tuple[str, ...] = ()) -> Image.Image:
    """Frame 0 with only the given arms and its eyes open or shut (the short soft lines of its cheering frames)."""
    im = base.copy()
    for side in ('L', 'R'):
        if side not in arms:
            for p in ARMS[side]:
                im.putpixel(p, (0, 0, 0, 0))
    for a in arms:
        if a.startswith('up'):
            for p in ARMS[a]:
                im.putpixel(p, JB)
    if eyes == 'shut':
        for p in ((5, 11), (10, 11), (5, 12), (10, 12)):
            im.putpixel(p, JA)
        for p in ((4, 11), (5, 11), (10, 11), (11, 11)):
            im.putpixel(p, JD)
    return im


class Frame:
    """One frame: a back layer, the Junimo, a front layer (each H rows). The Junimo's 16x16 frame goes at (dx, dy) -
    by default standing on the tile's floor. 'X' in either layer = the Junimo's own arm colour."""

    def __init__(self, family: str, front: list[str], pal: dict, eyes: str = 'open', arms: tuple[str, ...] = (),
                 dx: int = 0, dy: int = H - T, back: list[str] | None = None):
        self.family, self.front, self.pal, self.eyes, self.arms, self.dx, self.dy = family, front, pal, eyes, arms, dx, dy
        self.back = back or EMPTY

    def image(self, base: Image.Image) -> Image.Image:
        colour = lighten(FAMILY[self.family][1])
        pal = dict(self.pal, X=tint(Image.new('RGBA', (1, 1), JB), colour).getpixel((0, 0)))
        im = grid(self.back, pal)
        j = tint(junimo16(base, self.eyes, self.arms), colour)
        im.alpha_composite(j, dest=(max(self.dx, 0), self.dy), source=(max(-self.dx, 0), 0))
        if self.family in WEAR:
            top, rows = WEAR[self.family]
            im.alpha_composite(grid(stamp(EMPTY, rows, self.dx, self.dy + top), WEAR_PAL))
        im.alpha_composite(grid(self.front, pal))
        return im

    def face_hits(self) -> list[tuple[int, int]]:
        return sorted((x, y) for x, y in GUARD
                      if 0 <= x + self.dx < T and self.front[y + self.dy][x + self.dx] not in '. ')


# ---------------------------------------------------------------- what a few of them wear: a touch of character
WEAR_PAL = {
    'S': C('#1b1b2e'), 'f': C('#2e2f48'), 'g': C('#a8dcff'),        # shades: lenses, frame, the sky in them
    'K': C('#26232f'), 'k': C('#4a4660'), 'm': C('#8a84a8'),        # top hat: black silk, its sheen, the lit edges
    'r': C('#c8323e'),                                              # and a red band (the straw hat's too)
    'T': C('#f2cf73'), 't': C('#a8762c'),                           # straw and its dark
    'B': C('#d8364a'), 'b': C('#7a1426'), 'h': C('#ff8a8a'),        # beret: red, its dark, its light
    'P': C('#ff7eb6'), 'p': C('#a3285e'),                           # ribbon pink and its dark
}
WEAR = {   # family: (the Junimo row it starts on, rows in the Junimo's own 16x16 frame); over it, under the instrument
    'trumpet': (11, ["...fgSSffgSSf...",          # shades: a jazz trumpeter (the only thing allowed over the eyes; on
                     ".....SS..SS....."]),        # the dark navy sax player they vanished)
    'piano': (-1, ["......KKKK......",            # a little top hat: the concert pianist (its brim lit on top, so it
                   "......mkKK......",            # doesn't melt into the head's dark outline; a sheen down its crown)
                   "......rrrr......",
                   "....mmmmmmmm....",]),
    'ocarina': (1, ["......tttt......",           # the farmer's straw hat: a shepherd with his pan pipes (on the tan
                    ".....tTTTTt.....",           # guitarist, hat, body and guitar ran together)
                    ".....trrrrt.....",
                    "..ttTTTTTTTTtt..",
                    "...tttttttttt..."]),
    'cloud': (1, ["........b.......",             # a beret, its stalk where the sprout was: the French accordionist
                  ".....bbbbbbb....",
                  "....bBhBBBBBbb..",
                  ".....bbBBBBBBBb.",
                  "........bbbbbb.."]),
    'choir': (2, [".........PP.PP..",             # a ribbon bow: the pop idol
                  ".........PPpPP..",
                  ".........P...P.."]),
}
EYEWEAR = {'trumpet'}                                  # everything else keeps clear of the face


# ================================================================ the first three: guitar, drum kit, choir
# ---------------------------------------------------------------- guitar: held across it, like the pixel set's
GUITAR_PAL = {
    'o': C('#5a2a14'), 'c': C('#ffd89a'), 'd': C('#e0a560'), 'h': C('#fff1d0'),   # cream top, its shade and light
    'k': C('#3a2014'), 'g': C('#7a4526'),                                         # sound hole, fingerboard
    'n': C('#5a2a14'), 'N': C('#b07a48'), 'e': C('#3a2014'), 'p': C('#e8ecf2'),   # neck, its light edge, head, pegs
}
GUITAR = ['.' * T] * 9 + [                # on the diagonal: the body low in front of it, the neck up past its cheek
    "...............p",
    "..............pe",
    "...............e",
    "..............Nn",
    ".............Nn.",
    "............Nn..",
    "......oooooNn...",
    "....oohhccgn....",
    "...ohccccggo....",
    "...ohcccggdo....",
    "..ohcckkgcdo....",
    "..occckkcdo.....",
    "..ocgcccdo......",
    "...occddoo......",
    "....oooo........",
]


def guitar(frame: str) -> Frame:
    """The pixel set's cream guitar held across it the way a guitarist does: the body low in front of it, the neck up
    past its right cheek to its right hand (it stands a step back and one pixel left, so the neck clears its face).
    Its left forearm rests on the body; on A it sweeps down across the strings (and the right hand slides down the
    neck), on B it swings back up beside it; on a long note it throws that arm up and shuts its eyes while the
    chord rings."""
    strum = {'A': [(1, 14), (2, 15), (3, 16), (4, 17), (5, 18), (6, 19)], 'B': [(0, 10), (0, 11), (0, 12), (1, 13)],
             'long': []}.get(frame, [(1, 14), (2, 15), (3, 16), (4, 17)])
    fret = [(13, 11), (14, 12)] if frame == 'A' else [(13, 11), (14, 11)]
    rows = put(put(GUITAR, fret, 'X'), strum, 'X')
    return Frame('guitar', rows, GUITAR_PAL, 'shut' if frame in ('blink', 'long') else 'open',
                 ('upL',) if frame == 'long' else (), dx=-1, dy=1)


# ---------------------------------------------------------------- drum kit: behind a snare, a cymbal either side
DRUM_PAL = {
    'o': C('#4a0e1e'), 'w': C('#f4f7fb'), 'W': C('#cdd8e8'), 's': C('#a9b4c8'),   # outline, head and its far half, hoop
    'r': C('#e8344c'), 'R': C('#a81c38'), 'l': C('#ffffff'),                      # red shell, its shade, lugs
    'B': C('#9a5418'), 'C': C('#fff4a0'), 'D': C('#f2c230'), 'E': C('#d8901c'),   # cymbal: rim, shine, gold, underside
    'F': C('#8a8f9c'),                                                            # stands
    'j': C('#9a5a2c'), 'v': C('#f6dcaa'),                                         # drumsticks (dark, not pale arms), tips
}
SNARE = ['.' * T] * 17 + [                # the pixel set's snare, seen from above and in front; it stands just in
    ".....oooooo.....",                   # front of the Junimo's feet (overlapping its body it read as a bucket)
    "...ooWWWWWWoo...",
    "..owwwwwwwwwwo..",
    "..osswwwwwwsso..",
    "..orlrrlrrlrro..",
    "...oRRRRRRRRo...",
    "....oooooooo....",
]
CYMBAL = {False: ["..BCB..", "BCCDDEB", ".BBBBB."],                 # a bell on a gold disc, its underside darker,
          True: ["BCB....", "BCDDB..", ".BEDDEB", "..BBBB."]}       # and tipped right over when it is struck


def drums(frame: str) -> Frame:
    """Behind a snare, a crash cymbal either side above its head, a stick in each hand resting on the head. On a note
    one arm goes up (the game's own cheering arm) and its stick crashes that cymbal, which tips, while the other
    stick strikes the snare; A and B swap arms, and a long note crashes both."""
    left_up, right_up = frame in ('B', 'long'), frame in ('A', 'long')
    back = list(EMPTY)
    for x0, hit, flip in ((0, left_up, False), (9, right_up, True)):
        for i, r in enumerate(CYMBAL[hit]):
            r = r[::-1] if flip else r
            for j, c in enumerate(r):
                if c != '.':
                    back = put(back, [(x0 + j, i)], c)
        back = put(back, [(12 if flip else 3, y) for y in range(3, 15)], 'F')       # its stand, down behind it
    front, arms = SNARE, []
    for up, x, side in ((left_up, lambda v: v, 'L'), (right_up, lambda v: 15 - v, 'R')):
        if up:
            arms.append('up' + side)
            front = put(front, [(x(2), 2), (x(3), 1)], 'j')
        else:                                   # hand at its side, the stick down and in, its tip on the head
            strike = frame == ('A' if side == 'L' else 'B')
            front = put(front, [(x(1), 11)], 'X')
            front = put(front, [(x(1), 12), (x(1), 13), (x(2), 14), (x(3), 15), (x(4), 16)], 'j')
            front = put(front, [(x(5), 18 if strike else 17)], 'v')
    return Frame('drumkit', front, DRUM_PAL, 'shut' if frame in ('blink', 'long') else 'open', tuple(arms), dy=1,
                 back=back)


# ---------------------------------------------------------------- choir: the singer at a stand mic
MIC_PAL = {
    'o': C('#2c2f3d'), 'W': C('#ffffff'), 'G': C('#d5dbe6'), 'g': C('#8a93a6'),   # the head: its dark, shine, silver
    'R': C('#d8364a'), 's': C('#5d6475'), 'k': C('#23242f'),                      # a red ring; the steel stand, base
}
MIC = at(7, [                      # a round head at its cheek, big and bright enough to read (a blind tester missed a
    ".............oo.",            # 2x2 grey one), on a tall steel stand down to a base at the front of the tile; it
    "............oWGo",            # stands where most of the set stands (dy=1), so a row keeps one eye-line
    "............oGgo",
    ".............oo.",
    "..............R.",
] + ["..............s."] * 11 + [
    "............kkkk",
])


def mic(frame: str) -> Frame:
    """A stand mic at its cheek, its right hand round the stand. It sings with its eyes shut, the free arm going up and
    down with the notes (up on B and through a long note). (Held in the hand instead, the mic read as a mouth or a
    beard under the face, or an ear beside it: its handle is too short at this size to say 'ball on a stick'.)"""
    rows = put(MIC, [(15, 13), (15, 14)], 'X')
    return Frame('choir', rows, MIC_PAL, eyes_rapt(frame), ('upL',) if frame in ('B', 'long') else ('L',), dy=1)


# ================================================================ keys: piano, organ, synth
# ---------------------------------------------------------------- piano: a toy keyboard on legs, like the pixel set's
PIANO_PAL = {
    'o': C('#1c1726'), 'P': C('#3b3552'),                          # black lacquer case: its outline, its face
    'Y': C('#ffe07a'),                                             # gold fallboard
    'W': C('#fffaf0'), 'w': C('#c9bdb0'), 'K': C('#1c1726'),       # ivory, a pressed key's shade, black keys
}
PIANO = cell(['.' * T] * 16 + [        # the icon's keyboard: gold board, black keys hanging from it, ivory fronts
    "oYYYYYYYYYYYYYYo",
    "oWWKWKWWKWKWKWWo",
    "oWWKWKWWKWKWKWWo",
    "oWWWWWWWWWWWWWWo",
    "oPPPPPPPPPPPPPPo",
    ".oooooooooooooo.",
    "..o..........o..",
    ".o............o.",
])
P_REST = [(0, 12), (1, 13), (1, 14), (2, 15), (3, 16)]       # elbow out, hand on the keys
P_PRESS = [(0, 13), (1, 14), (2, 15), (3, 16), (4, 17)]      # a pixel lower, pushing a key down
P_LIFT = [(1, 13), (0, 12), (0, 11), (0, 10)]                # forearm up off the keys


def _piano_keys(rows, xs):
    """Pressed keys sink: the ivory below the hand goes into shade, the key's front dropping over the case."""
    return put(rows, [(x, y) for x in xs for y in (18, 19, 20)], 'w')


def piano(frame: str) -> Frame:
    """Behind a little black-and-gold keyboard on legs (the pixel set's piano), its lower edge showing over the
    fallboard, elbows out and both hands on the keys. On A the left hand pushes a key down while the right lifts off;
    B is the mirror. On a long note it holds a chord with the left hand, throws the right arm up and shuts its eyes."""
    rows, arms = PIANO, ()
    if frame == 'A':
        rows = put(_piano_keys(rows, [4]), P_PRESS + mirror(P_LIFT), 'X')
    elif frame == 'B':
        rows = put(_piano_keys(rows, [11]), mirror(P_PRESS) + P_LIFT, 'X')
    elif frame == 'long':
        rows, arms = put(_piano_keys(rows, [2, 4, 6]), P_PRESS, 'X'), ('upR',)
    else:
        rows = put(rows, P_REST + mirror(P_REST), 'X')
    return Frame('piano', rows, PIANO_PAL, eyes_for(frame), arms, dy=1)


# ---------------------------------------------------------------- organ: a portative, pipes up behind its shoulder
ORGAN_PAL = {
    'O': C('#4a5270'), 'L': C('#eef1f6'), 'D': C('#959eb8'), 'M': C('#2a2e44'),   # tin: caps, light, shade, mouths
    'o': C('#4a1c14'), 'w': C('#9a3f26'), 'h': C('#c8683e'),                      # red walnut box: outline, face, light
    'W': C('#fff6e4'), 'K': C('#2a1a14'), 'k': C('#cdbba0'),                      # ivory, black keys, a pressed key
}
ORGAN_PIPES = cell(['.' * T] + [               # back layer: three pipes in a little fan, the middle one tallest, each
    "............OO..",   # 1            mouth at its own height (the pixel set's organ); they stand in the box,
    "............LD..",   # 2            hidden behind its body, and show past its right shoulder
    "..........OOLDOO",   # 3
    "..........LDLDLD",   # 4
    "..........MDLDLD",   # 5  left pipe's mouth (just above its shoulder)
    "..........LDMDLD",   # 6  middle
    "..........LDLDLD",   # 7
    "..........LDLDMD",   # 8  right
] + ["..........LDLDLD"] * 7 + ['.' * T] * 8)
ORGAN_BOX = cell(['.' * T] * 15 + [            # front layer: the portative's box held up below its face, keys to the front
    "...ooooooooooooo",   # 15
    "...owhwwwwwwwwwo",   # 16
    "...oWKWKWWKWKWWo",   # 17
    "...oWWWWWWWWWWWo",   # 18
    "...ooooooooooooo",   # 19
] + ['.' * T] * 4)
ORGAN_LEFT = {                                    # its left arm: under its body (above row 14 it would sit on the
    'rest': [(0, 14), (1, 15), (2, 16), (3, 16)],  # body's own outline at dx=-2 and vanish), hand on the keys
    'press': [(0, 14), (1, 15), (2, 16), (3, 17), (4, 17)],
    'drop': [(0, 14), (0, 15), (0, 16)],           # off the keys, hanging at its side
}
ORGAN_RIGHT = [(12, 12), (12, 13), (12, 14), (12, 15), (12, 16), (11, 17)]   # down across the pipes onto the keys


def organ(frame: str) -> Frame:
    """A little portative organ: a box of keys held up below its face and three tin pipes rising behind its right
    shoulder (it stands two pixels left to make room). At rest its left hand is on the keys and its right hand on the
    pipes. On A the left hand presses a low key while the right arm swings up across the pipes (the game's cheering
    arm); on B the left hand drops and the right comes down across the pipes to press a high key; on a long note both
    hands hold a chord and it shuts its eyes."""
    f = 'idle' if frame == 'blink' else frame
    left = {'A': 'press', 'B': 'drop', 'long': 'press'}.get(f, 'rest')
    right = ORGAN_RIGHT if f in ('B', 'long') else []
    keys = {'A': [(4, 18), (5, 18)], 'B': [(10, 18), (11, 18)], 'long': [(4, 18), (5, 18), (10, 18), (11, 18)]}
    rows = put(put(ORGAN_BOX, keys.get(f, []), 'k'), ORGAN_LEFT[left] + right, 'X')
    arms = {'idle': ('R',), 'A': ('upR',)}.get(f, ())
    return Frame('organ', rows, ORGAN_PAL, eyes_for(frame), arms, dx=-2, dy=1, back=ORGAN_PIPES)


# ---------------------------------------------------------------- synth: a keytar
SYNTH_PAL = {
    'o': C('#1d3b3a'), 'h': C('#86d4b8'), 'c': C('#2b6e68'),                      # teal body: outline, top, shade
    'W': C('#fff6e4'), 'K': C('#1f2430'), 'k': C('#c4b49c'),                      # ivory, black keys, a pressed key
    'n': C('#f2a33a'), 'r': C('#e8604a'),                                         # amber knob, red button
}
KEYTAR = cell(['.' * T] * 11 + [               # the keyboard across its front; the neck a short 45-degree stub out to the
    "...............o",   # 11 its tip    tile's edge, its right hand on the tip (run on up its side, the neck closed
    "..............ho",   # 12            into a teapot's handle), lit on its upper side, a pixel of air from the blush
    ".............ho.",   # 13
    "............ho..",   # 14
    "ooooooooooohoo..",   # 15
    "ohnhrhhhhhhho...",   # 16 a knob and a button on top
    "oWKWKWWKWKWWo...",   # 17 keys
    "oWWWWWWWWWWWo...",   # 18
    "occccccccccco...",   # 19
    ".oooooooooooo...",   # 20
] + ['.' * T] * 3)
KEYTAR_LEFT = {                                   # its left hand on the keys
    'rest': [(0, 13), (0, 14), (1, 15), (1, 16)],
    'low': [(0, 13), (0, 14), (1, 15), (2, 16), (2, 17)],          # pressing at the low end
    'run': [(1, 13), (2, 14), (3, 15), (4, 16), (5, 17)],          # reached in along the keys
}
KEYTAR_BEND = [(14, 12), (13, 13)]                                 # right hand slid down the neck


def synth(frame: str) -> Frame:
    """A teal keytar (no screen, so nothing to read as shades): the keyboard across its front below its face, the
    neck a short stub up to the right with its right hand on the tip (the game's own little arm). On A its left hand
    presses the low keys while the right slides down the neck; on B the left runs in along the keys and the right is
    back on the tip; on a long note it holds a chord, shuts its eyes and throws its right arm up."""
    f = 'idle' if frame == 'blink' else frame
    left = {'A': 'low', 'B': 'run', 'long': 'low'}.get(f, 'rest')
    keys = {'A': [(2, 18), (3, 18)], 'B': [(5, 18), (6, 18)], 'long': [(2, 18), (3, 18), (5, 18)]}
    rows = put(put(KEYTAR, keys.get(f, []), 'k'), KEYTAR_LEFT[left] + (KEYTAR_BEND if f == 'A' else []), 'X')
    arms = {'A': (), 'long': ('upR',)}.get(f, ('R',))
    return Frame('synth', rows, SYNTH_PAL, eyes_for(frame), arms, dx=0, dy=1)


# ================================================================ percussion: bells, steel drum, stardust
# ---------------------------------------------------------------- bells: a rainbow toy glockenspiel
GLOCK_PAL = {
    'o': C('#3d2a1e'),                                             # the frame's dark wood, a seam between the bars
    'R': C('#e8483f'), 'O': C('#f59a35'), 'Y': C('#f7d23e'),       # the bars: a rainbow, longest on the left
    'G': C('#5cc25a'), 'B': C('#4a9fe8'), 'P': C('#9b6ae0'),
    'k': C('#4a2a14'), 'm': C('#ffffff'),                          # mallets: dark sticks, white heads
}
GLOCK = at(17, [                  # on the floor just in front of it (its feet show over the frame), bars shortening
    ".ooooooo........",           # to the right; each bar outlined on its own reads as a xylophone (solid, the
    ".oRoOoYooooooo..",           # rainbow read as a flag or a box)
    ".oRoOoYoGoBoPo..",
    ".oRoOoYoGoBoPo..",
    ".oRoOoYoGoBoPo..",
    ".oRoOoYoGooooo..",
    ".ooooooooo......",
])
MALLET = {   # left hand: (arm, stick, head); the right mirrors it. The piano's arms: elbows out, hands down at the bars
    'rest': ([(0, 12), (1, 13), (1, 14), (2, 15)], [(3, 16)], [(4, 17), (5, 17)]),          # head resting on the frame
    'hit': ([(0, 13), (1, 14), (2, 15), (3, 16)], [(4, 17)], [(5, 18), (6, 18), (5, 19)]),  # down on the yellow bar
    'lift': ([(1, 13), (0, 12), (0, 11), (0, 10)], [(0, 9)], [(0, 8), (1, 8), (0, 7)]),     # forearm up by its side
}


def bells(frame: str) -> Frame:
    """The bells family (chromatic percussion: music box, glockenspiel, xylophone) plays a rainbow toy glockenspiel on
    the floor in front of it, a white-headed mallet in each hand, elbows out like the pianist's. On A the left mallet
    comes down on a bar while the right lifts up by its side, B the other way; on a long note both come down, eyes
    shut. (Handbells read as torches, horns - and, made rounder, as something less polite.)"""
    left, right = {'A': ('hit', 'lift'), 'B': ('lift', 'hit'), 'long': ('hit', 'hit')}.get(frame, ('rest', 'rest'))
    rows = GLOCK
    for pose, flip in ((left, False), (right, True)):
        arm, stick, head = (mirror(p) if flip else p for p in MALLET[pose])
        rows = put(put(put(rows, arm, 'X'), stick, 'k'), head, 'm')
    return Frame('bells', rows, GLOCK_PAL, eyes_for(frame), (), dy=1)


# ---------------------------------------------------------------- steel drum: a chrome pan on a stand in front
STEEL_PAL = {
    'o': C('#3d4560'), 'W': C('#f1f4f9'), 's': C('#d3dae6'), 'm': C('#9ea9bf'),   # chrome: outline, light, mid
    'k': C('#7d88a3'),                                                             # the hammered notes
    'K': C('#4a5270'), 'y': C('#ffe07a'), 'l': C('#2e3344'),                       # skirt band, its bolts; stand
    'f': C('#6a3a1c'), 'q': C('#ffd0d8'), 't': C('#ff4a5e'),                       # mallets: dark wood, red rubber tip
}
STEELPAN = ['.' * T] * 16 + [          # its far rim runs between the Junimo's feet, so they show just behind it
    ".....mWWWWm.....",
    "...omsksskWso...",
    ".ooWkssmmsskWoo.",
    ".oWssksssskssmo.",
    ".oWmsmmmmmmmkko.",
    "..oyKKyKKyKKyo..",
    "...l........l...",
    "..l..........l..",
]
assert all(len(r) == T for r in STEELPAN) and len(STEELPAN) == H
STEEL_MALLET = {   # left hand (the game's stub): its shaft, then its tip (shine, red rubber); the right mirrors it
    'rest': ([(1, 12), (1, 13), (2, 14), (2, 15), (3, 16)], [(4, 17), (4, 18)]),        # tip resting on the surface
    'hit': ([(1, 12), (2, 13), (3, 14), (4, 15), (5, 16)], [(6, 18), (6, 19)]),         # down on a note well inside
    'lift': ([(0, 9), (0, 8), (0, 7)], [(0, 6), (0, 5)]),                               # raised up beside its body
}


def steeldrum(frame: str) -> Frame:
    """Just behind a chrome steel pan on a stand (the pixel set's: hammered notes, a dark band with gold bolts), a
    red-tipped mallet in each hand resting on the pan. On a note one mallet comes down on a note further in (its dimple
    darkens) while the other lifts up beside its body - A strikes with the left, B with the right; a long note is a
    roll, both mallets down, eyes shut. (Raised over its head, the mallets read as antennae on a flying saucer.)"""
    left, right = {'A': ('hit', 'lift'), 'B': ('lift', 'hit'), 'long': ('hit', 'hit')}.get(frame, ('rest', 'rest'))
    rows = STEELPAN
    for pose, flip in ((left, False), (right, True)):
        shaft, tip = (mirror(p) if flip else p for p in STEEL_MALLET[pose])
        if pose == 'lift':
            tip = tip[::-1]                            # pointing up: the shine on its top
        rows = put(put(put(rows, shaft, 'f'), tip[:1], 'q'), tip[1:], 't')
        if pose == 'hit':
            rows = put(rows, [(9 if flip else 6, 20)], 'k')
    return Frame('steeldrum', rows, STEEL_PAL, eyes_for(frame), ('L', 'R'), dy=1)


# ---------------------------------------------------------------- stardust: a star wand
STAR_PAL = {
    'n': C('#2b2657'), 'c': C('#bfe4ff'), 'b': C('#7fa6e0'), 'W': C('#ffffff'),   # navy outline, pale cyan, shade, shine
    'w': C('#5e3c94'),                                                             # the wand
}
STAR = ["...n...",                # handle point (3, 6), between its legs
        "..ncn..",
        "nnnccnn",
        "nccccbn",
        ".nccbn.",
        ".ncnbn.",
        ".nn.nn."]
assert all(len(r) == 7 for r in STAR)
SHINE = {'idle': (3, 2), 'blink': (3, 2), 'A': (1, 3), 'B': (3, 1), 'long': (2, 3)}   # its glint, as it turns
WAND = {                          # (wand pixels, handle point of the star)
    'idle': ([(12, 11), (12, 12), (13, 13), (13, 14), (14, 15), (14, 16)], (12, 10)),
    'A': ([(11, 9), (10, 8)], (9, 7)),
    'B': ([(12, 9), (12, 8)], (12, 7)),
    'long': ([(12, 9), (12, 8), (12, 7)], (12, 6)),
}


def stardust(frame: str) -> Frame:
    """A star wand: a big pale star with a navy outline on a slim purple wand, waved like a little conductor. It holds
    it at its side; on A it raises it and tips it over to the left, on B swings it back up to the right, and on a long
    note it holds it up high, both arms up and eyes shut. Only the star's glint moves on it - no sparkles."""
    wand, (hx, hy) = WAND.get(frame, WAND['idle'])
    sx, sy = SHINE[frame]
    star = put(STAR, [(sx, sy)], 'W')
    rows = stamp(put(EMPTY, wand, 'w'), star, hx - 3, hy - 6)
    arms = {'idle': ('R',), 'blink': ('R',), 'A': ('upR',), 'B': ('upR',), 'long': ('upL', 'upR')}[frame]
    return Frame('stardust', rows, STAR_PAL, eyes_for(frame), arms, dx=-1, dy=8)


# ================================================================ winds: trumpet, sax, ocarina
# The Junimo has no mouth and its face sits at the very bottom of its body, so every wind instrument is
# played the way Hello Kitty plays one: the mouthpiece just touches the bottom centre of its face (junimo
# (6..9, 13)) and the instrument is held out to one side, below the cheek, a pixel of air kept from the blush
# and glints. Props centred under the face read as a bowl, a skirt or an ice-cream cone in tests.
# ---------------------------------------------------------------- trumpet: the pixel set's gold trumpet, to the right
TRUMPET_PAL = {
    'o': C('#6a3a10'), 'K': C('#c47a1e'), 'G': C('#f6b93b'), 'L': C('#ffe07a'),   # warm-dark outline, gold ramp
    'p': C('#fff8dc'),                                                            # valve caps, the bell's glint
}
# It stands two pixels left (dx=-2, dy=1): its mouth is cell (4..7, 14), its right cheek clears x>=11.
# The mouthpiece touches the bottom centre of its face, the leadpipe runs along its bottom edge under the cheek, the
# valves stand up past its lower right and the bell flares out at the tile's edge.
TRUMPET = at(11, [                 # level: idle and blink
    "...............o",   # 11
    "..............oL",   # 12  bell
    "...........p.pLp",   # 13  two valve caps; the bell's glint
    ".....GL....GoGLL",   # 14  mouthpiece at its mouth; valve casings
    ".......LLLLLLLGG",   # 15  leadpipe into the bell
    ".......oooKKKKKG",   # 16
    "..........oLLLoK",   # 17  the lower tube
    "...........ooo.K",   # 18
    "...............o",   # 19
])
TRUMPET_DIP = at(12, [             # A: tipped down a pixel from the mouthpiece (valves and bell drop)
    "...............o",   # 12
    "..............oL",   # 13
    ".....GL....p.pLp",   # 14
    ".......LLL.GoGLL",   # 15
    ".......oooLLLLGG",   # 16
    "..........oKKKKG",   # 17
    "..........oLLLoK",   # 18
    "...........ooo.K",   # 19
    "...............o",   # 20
])
TRUMPET_LIFT = at(10, [            # B and the long note: the bell lifted a pixel
    "...............o",   # 10
    "..............oL",   # 11
    "..............Lp",   # 12
    "...........p.pLL",   # 13
    ".....GL....GoGGG",   # 14
    ".......LLLLLLLKG",   # 15
    ".......oooKKKKoK",   # 16
    "..........oLLL.K",   # 17
    "...........ooo.o",   # 18
])


def trumpet(frame: str) -> Frame:
    """A gold trumpet held out to the right, its mouthpiece at the bottom centre of the face, its right arm reaching
    down to the valves (the other hand is round the far side). A and B rock the trumpet - the bell dips on A and lifts
    on B - while a finger presses the right valve, then the left one. On a long note it holds the bell up, shuts its
    eyes and throws its right arm up in a fanfare."""
    rows = {'A': TRUMPET_DIP, 'B': TRUMPET_LIFT, 'long': TRUMPET_LIFT}.get(frame, TRUMPET)
    hand = {'A': [(12, 12), (12, 13), (13, 14)],          # reaches down with the dip and presses the right valve
            'B': [(12, 12), (11, 13)],                     # presses the left valve
            'long': []}.get(frame, [(12, 12)])             # resting over both caps
    return Frame('trumpet', put(rows, hand, 'X'), TRUMPET_PAL, eyes_rapt(frame),
                 ('upR',) if frame == 'long' else ('R',), dx=-2, dy=1)


# ---------------------------------------------------------------- sax: the pixel set's gold alto sax, hung below it
SAX_PAL = {
    'o': C('#6a3a10'), 'K': C('#c47a1e'), 'G': C('#f6b93b'), 'L': C('#ffe07a'),   # same gold as the trumpet
    'p': C('#fff4c4'),                                                            # keys, the bell's glint
}
# It stands a step back (dx=-1, dy=1): its mouth is cell (5..8, 14). The mouthpiece touches the bottom centre of its
# face, the neck crooks down into the body, which runs down across its lower edge, round the bow and up into the bell.
# Three hangs, swung on the mouthpiece: the bell a pixel further in, at rest, or a pixel further out.
SAX = at(14, [                     # at rest: idle and blink
    "......K.........",   # 14  mouthpiece
    "......oL........",   # 15  neck
    ".......oLo......",   # 16
    ".......oLGo.....",   # 17
    ".......oLpo.oo..",   # 18  upper key; the bell's rim
    ".......oLGoopLo.",   # 19
    "........oLpoGKo.",   # 20  lower key
    "........oLGGGKo.",   # 21  the bow
    ".........oKKKo..",   # 22
    "..........ooo...",   # 23
])
SAX_IN = at(14, [                  # A: swung in, nearly upright
    "......K.........",   # 14
    "......oL........",   # 15
    "......oLo.......",   # 16
    "......oLGo......",   # 17
    "......oLpo.oo...",   # 18
    "......oLGoopLo..",   # 19
    ".......oLpoGKo..",   # 20
    ".......oLGGGKo..",   # 21
    "........oKKKo...",   # 22
    ".........ooo....",   # 23
])
SAX_OUT = at(14, [                 # B and the long note: swung out, the bell kicked out to the right
    "......K.........",   # 14
    "......oL........",   # 15
    ".......oLo......",   # 16
    ".......oLGo.....",   # 17
    ".......oLpGo.oo.",   # 18
    "........oLGoopLo",   # 19
    "........oLpGoGKo",   # 20
    ".........oLGGGKo",   # 21
    "..........oKKKo.",   # 22
    "...........ooo..",   # 23
])
SAX_HAND = {   # right arm down past its foot, a two-pixel hand over the upper key
    'rest': [(13, 14), (12, 15), (12, 16), (11, 17), (10, 18), (9, 18)],
    'in': [(13, 14), (12, 15), (11, 16), (10, 17), (9, 18), (8, 18)],
    'out': [(13, 14), (12, 15), (12, 16), (12, 17), (11, 18), (10, 18)],
}


def sax(frame: str) -> Frame:
    """A gold alto sax hung from its mouth: mouthpiece at the bottom centre of the face, the body down across its
    lower edge and the bell curling up at the right, its right hand on the keys. A and B swing the sax on its
    mouthpiece - in, then out, the bell swaying two pixels - and the hand rides along on the keys; on a long note it
    swings the bell out, shuts its eyes and lifts its free arm."""
    pose = {'A': 'in', 'B': 'out', 'long': 'out'}.get(frame, 'rest')
    rows = {'in': SAX_IN, 'out': SAX_OUT, 'rest': SAX}[pose]
    return Frame('sax', put(rows, SAX_HAND[pose], 'X'), SAX_PAL, eyes_rapt(frame),
                 ('upL',) if frame == 'long' else ('L',), dx=-1, dy=1)


# ---------------------------------------------------------------- ocarina family: a pan flute, slid along the lips
OCARINA_PAL = {
    'o': C('#3a2a0e'), 'L': C('#ffe27a'), 'h': C('#fff8dc'), 'k': C('#3a2a0e'),   # bamboo: outline, cane, rims, mouths
    'b': C('#3a2a0e'), 'B': C('#7a5a1e'),                                         # a dark binding (a blue cord read
}                                                                                 # as a belt; tan vanished on wood)
# A small ocarina could not be told from a blueberry, a fish or (held under the face) a bowl at game scale, so this
# family plays the brief's alternative: a pan flute. It stands a step back and a pixel right (dx=1, dy=3): its mouth is
# cell (7..10, 16). The pipes' rims run level along its bottom edge, the shortest pipe at its mouth and the longest
# hanging out past its left side, where its left hand holds the flute. It stands at dy=1, like most of the set.
PANFLUTE = at(15, [
    ".okhkhkhko......",   # 15  the pipes' mouths between pale cut rims, level under its mouth
    ".bBbBbBbBb......",   # 16  the cord
    ".oLoLoLoLo......",   # 17
    ".oLoLoLoLo......",   # 18
    ".oLoLoLo........",   # 19
    ".oLoLo..........",   # 20
    ".oLo............",   # 21
])
PAN_HAND = {-1: [(2, 12), (1, 13), (0, 14)], 0: [(2, 12), (1, 13), (1, 14)], 1: [(2, 12), (2, 13), (2, 14)]}


def ocarina(frame: str) -> Frame:
    """The ocarina family's Junimo plays a pan flute, held level along the bottom of its face and out past its left
    side, its left hand on the long end. It slides the flute along its lips to change pipes - a pixel left on A, a
    pixel right on B, the hand travelling with it; on a long note it shuts its eyes and lifts its right arm."""
    s = {'A': -1, 'B': 1}.get(frame, 0)
    rows = put(slide(PANFLUTE, s), PAN_HAND[s], 'X')
    return Frame('ocarina', rows, OCARINA_PAL, eyes_rapt(frame),
                 ('upR',) if frame == 'long' else ('R',), dx=1, dy=1)


# ================================================================ whimsy: songbird, cloud
# ---------------------------------------------------------------- songbird: a goldfinch on its raised hand
BIRD_PAL = {
    'o': C('#8a4512'),   # the yellow's outline: a dark of the yellow, never black
    'Y': C('#ffd630'),   # goldfinch yellow - the opposite of the sky-blue Junimo, light in grayscale
    'y': C('#eaa21f'),   # under-shade, warm like the pixel set's songbird block
    'K': C('#3b2a20'),   # cap, wing, tail and eye: a warm near-black brown
    'w': C('#fff4c8'),   # the wing bar
    'b': C('#ff8a2e'),   # beak and feet - the pixel set's songbird orange
}
BIRD = {                   # side-on, facing in towards the Junimo, its feet on the cheering hand's hook at (2,10)
    'idle': at(0, [         # perched, beak shut, tail hanging behind the hand
        "................",
        "................",
        "....KKK.........",
        "...KKKKo........",
        "...oYYKbb.......",
        "..ooYYYo........",
        ".oKwKYYo........",
        "oKKKKYyo........",
        "KK.ooooo........",
        "..b.b...........",
    ]),
    'A': at(0, [            # sings: head up, the beak open wide, chest out
        "................",
        "....KKK.b.......",
        "...KKKKb........",
        "...oYYK.........",
        "...oYYYb........",
        "..ooYYYYb.......",
        ".oKwKYYYo.......",
        "oKKKKYyo........",
        "KK.ooooo........",
        "..b.b...........",
    ]),
    'B': at(0, [            # between phrases: beak shut, wings up in a flutter
        "................",
        "................",
        "K...KKK.........",
        "KK.KKKKo........",
        ".KKoYYKbb.......",
        "..wKYYYo........",
        "..oKKYYo........",
        ".oyyYYyo........",
        "oK.ooooo........",
        "..b.b...........",
    ]),
    'long': at(0, [         # head thrown back a pixel, stretched tall, beak wide open to the upper right
        "................",
        "...KKK..b.......",
        "..KKKKob........",
        "..oYKY..........",
        "..oYYYYb........",
        ".ooYYYYYb.......",
        "oKwKYYYYo.......",
        "KKKKKYyo........",
        "K..ooooo........",
        "..b.b...........",
    ]),
}


def songbird(frame: str) -> Frame:
    """A goldfinch-yellow songbird perched on its raised left hand (the game's cheering arm), singing for it - a duet in
    turns. On A the bird sings, head up and beak wide open; on B it shuts its beak and flutters its wings while the
    Junimo answers with its free arm up; it sings along with its eyes shut, like the singer. On a long note the
    bird throws its head back and holds the note, and the Junimo cheers, both arms up."""
    rows = BIRD['idle' if frame == 'blink' else frame]
    return Frame('songbird', rows, BIRD_PAL, eyes_rapt(frame),
                 ('upL', 'upR') if frame in ('B', 'long') else ('upL', 'R'))


# ---------------------------------------------------------------- cloud: a squeezebox whose bellows are a cloud
CLOUD_PAL = {
    'o': C('#5d6fc0'),   # the cloud's line (the pixel set's cloud blue)
    'W': C('#ffffff'),   # white puff, also the keys on the right board
    'c': C('#c3cff7'),   # soft underside and folds, cool
    'R': C('#8a2430'),   # end-boards: a dark of their red, never black
    'r': C('#e0533f'),   # red board face
    'q': C('#ff9a78'),   # warm light on the left board's top-left
}
BOARD_L = ["RRR", "Rqr", "Rrr", "Rrr", "Rrr", "RRR"]       # 3x6 end-boards on rows 17..22, one in each hand
BOARD_R = ["RRR", "rWR", "rrR", "rWR", "rrR", "RRR"]       # the right one shows two white keys
CLOUD = {                  # staged like the piano: the Junimo (dy=0) just behind it, its feet showing over the
                           # cloud's top on row 16 (wrapped round its bottom, the white read as a nappy or a tutu)
    'idle': at(16, [         # three lobes and a soft, bumpy underside - a floating puff, not a flat tub of foam
        "......oooo......",
        "..oo.oWWWWo.oo..",
        "..oWWWcWWWWWco..",
        "..oWWWWWWWWWco..",
        "..oWWWWWWWWcco..",
        "..ocWWWWWWWcco..",
        "..occWWccWccco..",
        "...ooccooccoo...",
    ]),
    'A': at(16, [            # squeezed: the boards press in and the cloud puffs out above and below them
        "......oooo......",
        "....ooWWWWoo....",
        "...oWWWWWWWco...",
        "...oWWcWWcWco...",
        "...oWWWWWWWco...",
        "...oWWWWWWcco...",
        "...ocWWWWWcco...",
        "....occoocco....",
    ]),
    'B': at(17, [            # stretched: long and low, the boards out at the tile's edges
        "...oo..oo..oo...",
        ".ooWWooWWooWWoo.",
        "oWWWWcWWWcWWWcco",
        "oWWWWWWWWWWWWcco",
        "ocWWWWWWWWWWccco",
        ".occWWccWWccWco.",
        "..oooccooccooo..",
    ]),
}
CLOUD_HOLD = {             # the left board's x, and the left arm down its side to the board (the right mirrors both)
    'idle': (1, [(1, 12), (0, 13), (0, 14), (0, 15), (1, 16)]),
    'A': (3, [(1, 12), (1, 13), (2, 14), (2, 15), (3, 16)]),
    'B': (0, [(1, 12), (0, 13), (0, 14), (0, 15), (0, 16)]),
}


def cloud(frame: str) -> Frame:
    """The pixel set's white cloud played as a squeezebox: it is the bellows between two little red end-boards, and the
    Junimo (a step back, so the cloud sits below its face) holds a board in each hand. On A it squeezes - the boards
    come in 3 px a side and the cloud puffs up and down between them; on B it pulls the cloud out long and low to the
    tile's edges; on a long note it holds it stretched wide with its eyes shut."""
    key = {'blink': 'idle', 'long': 'B'}.get(frame, frame)
    lx, arm = CLOUD_HOLD[key]
    rows = stamp(stamp(CLOUD[key], BOARD_L, lx, 17), BOARD_R, T - 3 - lx, 17)
    rows = put(put(rows, arm, 'X'), mirror(arm), 'X')
    return Frame('cloud', rows, CLOUD_PAL, eyes_for(frame), (), dy=0)


# ================================================================ strings: bass, violin, kalimba
# ---------------------------------------------------------------- bass: the pixel set's red electric, slung low
BASS_PAL = {
    'o': C('#6e1420'), 'R': C('#e04a3a'), 'r': C('#ff8a5a'), 'd': C('#b8283a'),   # red body: outline, red, light, shade
    'W': C('#f6f3ec'),                                                            # pickguard, pegs, bridge
    'k': C('#3e2216'), 'N': C('#c98a52'),                                         # neck, headstock, pickups; neck's edge
}
BASS = cell([                    # steeper than the guitar (2:1), so the long neck clears its cheek and climbs past its head
    "................",   # 0
    "................",   # 1
    "...............k",   # 2  headstock, up in the overflow rows
    "..............Wk",   # 3  pegs
    "...............k",   # 4
    "..............Wk",   # 5
    "...............k",   # 6
    "...............k",   # 7  neck
    "...............k",   # 8
    "..............Nk",   # 9
    "..............Nk",   # 10
    ".............Nk.",   # 11
    ".............Nk.",   # 12
    "............Nk..",   # 13
    "............Nk..",   # 14
    "........oo.Nk...",   # 15 upper horn
    ".......oro.Nk.o.",   # 16
    "......orRoNkooRo",   # 17 lower horn
    ".....orWWWNkRRdo",   # 18 pickguard
    "....orWWWWkkRdo.",   # 19 pickup
    "....oRWWWWRRRdo.",   # 20
    "....oRRWkkkRdo..",   # 21 pickup
    ".....oRRRWRdo...",   # 22 bridge
    "......ooooooo...",   # 23
])
BASS_PLUCK = {
    'idle': [(1, 14), (2, 15), (3, 16), (4, 17), (5, 17), (6, 18), (7, 18)],   # forearm resting along the pickguard
    'A': [(1, 14), (2, 15), (3, 16), (4, 17), (5, 18), (6, 19), (7, 20)],      # digs down across it to pluck
    'B': [(0, 11), (0, 12), (0, 13), (1, 14)],                                 # pops back up off the strings
    'long': [(1, 14), (0, 15), (0, 16), (0, 17)],                              # drops away, letting it ring
}
BASS_FRET = {'A': [(13, 11), (13, 12)], 'long': [(13, 7), (14, 6)]}           # else (13, 9), (14, 9)


def bass(frame: str) -> Frame:
    """The pixel set's red electric bass, slung low and held steeper than the guitar so its long neck climbs past its
    right cheek and the headstock rises beside its head; double-cutaway body, white pickguard. Its left forearm rests
    along the pickguard; on A it digs down across the strings (and the fretting hand slides down the neck), on B it
    pops back up; on a long note it shuts its eyes, slides the fretting hand right up to the headstock and lets the
    plucking arm drop."""
    plucking = BASS_PLUCK.get(frame, BASS_PLUCK['idle'])
    fretting = BASS_FRET.get(frame, [(13, 9), (14, 9)])
    rows = put(put(BASS, fretting, 'X'), plucking, 'X')
    return Frame('bass', rows, BASS_PAL, eyes_for(frame), (), dx=-1, dy=1)


# ---------------------------------------------------------------- violin: a big violin played upright, like a cello
VIOLIN_PAL = {
    'o': C('#3e140c'), 'A': C('#c8572c'), 'a': C('#8e3418'), 'h': C('#f08a52'),   # varnished wood: dark, red-brown,
    'n': C('#24100c'), 'w': C('#ffe9c4'),                                         # shade, light; fingerboard; strings
    'b': C('#5a2410'), 'c': C('#f6eedc'),                                         # the bow: its stick, its hair
}
VIOLIN = cell([                    # stood at its front-left: neck beside its left cheek, the scroll up in the overflow rows
    "................",   # 0
    "................",   # 1
    "...oo...........",   # 2  scroll
    "..ohAo..........",   # 3
    "..oaAo..........",   # 4
    "..wan...........",   # 5  pegbox and pegs
    "...anw..........",   # 6
    "...an...........",   # 7  neck
    "...an...........",   # 8
    "...an...........",   # 9
    "...an...........",   # 10
    "...an...........",   # 11
    "...an...........",   # 12
    "...an...........",   # 13
    ".ooonooo........",   # 14 upper bout
    "ohAAnAAao.......",   # 15
    "ohAAnAAao.......",   # 16
    ".oAawaAo........",   # 17 waist (the bow crosses here); f-holes only a shade: dark slits and a white bridge made a
    ".oAawaAo........",   # 18 pumpkin's face, and the old orange its skin
    "ohAAwAAao.......",   # 19 a one-pixel bridge
    "ohAAnAAao.......",   # 20 tailpiece
    "ohAAnAaao.......",   # 21
    ".oaaaaao........",   # 22
    "....n...........",   # 23 endpin
])
BOW_ARM = {11: [(15, 13), (14, 14), (13, 15), (12, 16), (11, 17)],            # by where the bow hand (the frog) is
           13: [(15, 13), (15, 14), (14, 15), (14, 16), (13, 17)],
           14: [(15, 13), (15, 14), (15, 15), (15, 16), (14, 17)]}


def violin(frame: str) -> Frame:
    """A big violin stood upright like a cello at its front-left (the pixel set's violin: scroll, black fingerboard),
    the neck rising beside its left cheek, its left hand on it; its right hand holds the bow level across the strings
    below its face - a dark stick over a line of pale hair, which is what makes it read as a bow. The bow saws: pushed
    left on A, drawn right on B; on a long note it draws the bow right out, shuts its eyes and slides its left hand down
    the neck."""
    hand = {'A': 11, 'B': 14, 'long': 14}.get(frame, 13)
    rows = put(VIOLIN, [(x, 17) for x in range(hand - 12, hand)], 'b')            # the stick ...
    rows = put(rows, [(x, 18) for x in range(hand - 11, hand)], 'c')              # ... and the hair under it
    rows = put(rows, BOW_ARM[hand] + [(hand, 18)], 'X')
    rows = put(rows, [(2, 12), (3, 13)] if frame == 'long' else [(2, 9), (3, 10)], 'X')
    return Frame('violin', rows, VIOLIN_PAL, eyes_for(frame), (), dx=2, dy=0)


# ---------------------------------------------------------------- kalimba: a walnut board held up in both hands
KALIMBA_PAL = {
    'o': C('#2e140a'), 'B': C('#6e3a22'),                                         # walnut: its outline, its face
    's': C('#ffffff'), 'S': C('#c9d2e0'), 'n': C('#8d98ae'), 'K': C('#1a0a04'),   # tine tips, tines, bridge; hole
}
KALIMBA = cell(['.' * T] * 15 + [              # square, small and dark (it stood out from the wood floor and no longer
    "...oooooooooo...",   # 15           reads as a pot); air under it, so it is held, not stood in
    "...oBBBssBBBo...",   # 16 the middle pair's tips
    "...oBsBSSBsBo...",   # 17 the next pair's
    "...osSBSSBSso...",   # 18 the outer pair's: a fan, longest in the middle
    "...oSSBSSBSSo...",   # 19
    "...onnnnnnnno...",   # 20 the steel bridge
    "...oBBBKKBBBo...",   # 21 sound hole
    "...oooooooooo...",   # 22
    '.' * T])
KAL_HOLD = [(1, 13), (1, 14), (2, 15), (2, 16), (2, 17)]                 # down its side, hand on the board's edge
KAL_PRESS = [(1, 13), (1, 14), (2, 15), (3, 16), (4, 16), (4, 17)]      # over the top, the thumb on a tine
KAL_LIFT = [(1, 13), (0, 12), (0, 11), (0, 10)]                         # up off the board


def _tine_down(rows, x):
    """The pressed tine: its tip sinks a step under the thumb."""
    return put(put(rows, [(x, 17)], 'B'), [(x, 18)], 's')


def kalimba(frame: str) -> Frame:
    """A kalimba held up in both hands below its face: a small walnut board, a fan of white tines with the longest in
    the middle, a steel bridge and a sound hole (the pixel set's). At rest both hands hold its sides. On A the left
    thumb comes over the top and presses a tine down while the right hand lifts off; B is the mirror; on a long note
    both thumbs hold their tines down and it shuts its eyes."""
    rows, f = KALIMBA, frame
    if f == 'A':
        rows = put(_tine_down(rows, 5), KAL_PRESS + mirror(KAL_LIFT), 'X')
    elif f == 'B':
        rows = put(_tine_down(rows, 10), mirror(KAL_PRESS) + KAL_LIFT, 'X')
    elif f == 'long':
        rows = put(_tine_down(_tine_down(rows, 5), 10), KAL_PRESS + mirror(KAL_PRESS), 'X')
    else:
        rows = put(rows, KAL_HOLD + mirror(KAL_HOLD), 'X')
    return Frame('kalimba', rows, KALIMBA_PAL, eyes_for(frame), (), dx=0, dy=1)



# ---------------------------------------------------------------- what three of them stand on
# The trumpeter, the keytarist and the pan piper hold their instruments up at their faces with nothing below them: at the
# band's eye-line they hovered over their shadows. Each stands on something of its own instead, a row of its own
# (STAND) that the game draws under it, still as it breathes: a speaker cabinet for the two with the band, a hay bale for
# the shepherd in the straw hat. Its top is the cell's row 17, under the band's feet (row 16).
STAND_PAL = {
    'o': C('#1c1a24'), 'K': C('#3a3646'), 'm': C('#c9ccd6'), 'r': C('#e8344c'),   # cabinet, its panel, knobs, pilot light
    'g': C('#5e5648'), 'G': C('#7d735f'),                                         # the grille cloth and its weave
    'b': C('#5a3a12'), 'T': C('#b8913f'), 't': C('#8e6a2a'), 'h': C('#d2b064'),   # the bale: its edge, straw, dark, light
    'w': C('#6b2c14'),                                                            # (old hay, duller than the gold pipes
                                                                                  # in front of it), its twine
}
AMP = [
    ".oooooooooooooo.",
    ".oKmKmKmKKKKrKo.",   # the knobs and the pilot light
    ".oooooooooooooo.",
    ".ogGgggGgggGggo.",   # the grille
    ".oggggggggggggo.",
    ".oggGgggGgggGgo.",
    ".oooooooooooooo.",
]
BALE = [
    "..bbbbbbbbbbbb..",
    ".bhhThTwhThTwhb.",   # straw ends, two loops of twine round it
    ".bTtTTTwTTtTwTb.",
    ".btTTtTwTTTTwtb.",
    ".bTTtTTwtTTtwTb.",
    ".btTTTtwTTtTwTb.",
    "..bbbbbbbbbbbb..",
]
STANDS = {'trumpet': AMP, 'synth': AMP, 'ocarina': BALE}
STAND = len(FRAMES)                            # its row in the sheet, after the frames


def stand_image(family: str) -> Image.Image:
    """What a family stands on, alone in a cell (clear if it stands on its tile)."""
    im = Image.new('RGBA', (T, H), (0, 0, 0, 0))
    for y, r in enumerate(STANDS.get(family, [])):
        for x, ch in enumerate(r):
            if ch != '.':
                im.putpixel((x, H - len(STANDS[family]) + y), STAND_PAL[ch])
    return im


def drawn(family: str, frame: str, base: Image.Image) -> Image.Image:
    """A frame as the game shows it: on what it stands on."""
    im = stand_image(family)
    im.alpha_composite(KIT[family](frame).image(base))
    return im


KIT = {'piano': piano, 'bells': bells, 'organ': organ, 'guitar': guitar, 'bass': bass, 'violin': violin, 'choir': mic, 'trumpet': trumpet, 'sax': sax, 'ocarina': ocarina, 'synth': synth, 'cloud': cloud, 'stardust': stardust, 'kalimba': kalimba, 'steeldrum': steeldrum, 'songbird': songbird, 'drumkit': drums}


def build(base: Image.Image) -> Image.Image:
    sheet = Image.new('RGBA', (T * len(FAMILY), H * (len(FRAMES) + 1)), (0, 0, 0, 0))
    for family, draw in KIT.items():
        for r, frame in enumerate(FRAMES):
            sheet.alpha_composite(draw(frame).image(base), (FAMILY[family][0] * T, r * H))
        sheet.alpha_composite(stand_image(family), (FAMILY[family][0] * T, STAND * H))
    return sheet


# ---------------------------------------------------------------- previews
GRASS, SHADOW = (118, 160, 92, 255), (96, 138, 76, 255)


def ordered() -> list[str]:
    """The families drawn so far, in the sheet's order."""
    return sorted(KIT, key=lambda f: FAMILY[f][0])


def frames_board(base: Image.Image, per_column: int = 9) -> Image.Image:
    """Every frame of each family, one family per row, up to `per_column` rows to a column."""
    fams = ordered()
    cols = (len(fams) + per_column - 1) // per_column
    rows = min(len(fams), per_column)
    cw = (T + 2) * len(FRAMES) + 6
    board = Image.new('RGBA', (cw * cols + 2, (H + 2) * rows + 2), (0, 0, 0, 0))
    for i, f in enumerate(fams):
        for c, frame in enumerate(FRAMES):
            board.alpha_composite(drawn(f, frame, base),
                                  (2 + (i // per_column) * cw + c * (T + 2), 2 + (i % per_column) * (H + 2)))
    return zoom(board, 10 if len(fams) <= 4 else 6, bg=GRASS)


def row_on_tiles(cells: list[Image.Image]) -> Image.Image:
    """Cells standing on neighbouring tiles of grass (the tiles are the bottom 16 rows), a soft shadow under each."""
    row = Image.new('RGBA', (T * len(cells), H), GRASS)
    for i, im in enumerate(cells):
        for sx in range(4, 12):
            row.putpixel((i * T + sx, H - 1), SHADOW)
        row.alpha_composite(im, (i * T, H - im.height))
    return row


def pixel_block(index: int) -> Image.Image:
    return Image.open(ROOT / 'assets' / 'textures' / 'blocks.png').convert('RGBA').crop((index * T, 0, index * T + T, T))


def rows_of(items: list, n: int) -> list[list]:
    return [items[i:i + n] for i in range(0, len(items), n)]


ROLE = {'drumkit': 'beat', 'steeldrum': 'beat',                                  # eighths
        'choir': 'hold', 'cloud': 'hold', 'violin': 'hold', 'organ': 'hold',      # long notes
        'trumpet': 'hold', 'sax': 'hold', 'ocarina': 'hold', 'songbird': 'hold'}  # (the rest play quarters)


def performance() -> list[tuple[dict, int]]:
    """A little jam for the GIF in 60 ms steps, {family: frame}: drums play eighths, voices and winds long notes, the
    rest quarters, and the beat and quarter players end on a long one; before and after they wait, blinking now and
    then, each in its own time. A note held past 4 steps shows the long-note frame."""
    blink_at = {f: (FAMILY[f][0] * 7) % 28 for f in KIT}

    def idle(f, t):
        return 'blink' if t % 30 in (blink_at[f], blink_at[f] + 1) else 'idle'

    score = {'beat': [(t, 3) for t in range(0, 56, 4)] + [(56, 16)],                 # (start, length) in steps
             'quarter': [(t, 6) for t in range(0, 56, 8)] + [(56, 16)],
             'hold': [(0, 18), (24, 14), (40, 18)]}
    steps = [{f: idle(f, t) for f in KIT} for t in range(30)]
    for t in range(72):
        s = {}
        for f in KIT:
            notes = score[ROLE.get(f, 'quarter')]
            k = max(i for i, (m, _) in enumerate(notes) if m <= t)
            start, length = notes[k]
            if t - start >= length:
                s[f] = idle(f, t)
            elif length > 8 and t - start >= 4:
                s[f] = 'long'
            else:
                s[f] = 'AB'[k % 2]
        steps.append(s)
    steps += [{f: idle(f, t) for f in KIT} for t in range(30, 50)]
    return [(s, 60) for s in steps]


def gif(base: Image.Image, path: Path, z: int = 4, per_row: int = 9) -> None:
    """The whole band on rows of tiles, playing the jam."""
    cache = {(f, fr): drawn(f, fr, base) for f in KIT for fr in FRAMES}
    bands = rows_of(ordered(), per_row)
    out = []
    for state, ms in performance():
        frame = Image.new('RGBA', (T * per_row, H * len(bands)), GRASS)
        for r, band in enumerate(bands):
            frame.alpha_composite(row_on_tiles([cache[(f, state[f])] for f in band]), (0, r * H))
        out.append((zoom(frame, z).convert('RGB'), ms))
    out[0][0].save(path, save_all=True, append_images=[im for im, _ in out[1:]], duration=[ms for _, ms in out],
                   loop=0, optimize=False)


def icons_board(base: Image.Image, per_row: int = 9) -> Image.Image:
    """For each family, its pixel-set icon above and its Junimo block (idle) below."""
    bands = rows_of(ordered(), per_row)
    board = Image.new('RGBA', (T * per_row, (H * 2 + 4) * len(bands)), (0, 0, 0, 0))
    for r, band in enumerate(bands):
        y = r * (H * 2 + 4)
        board.alpha_composite(row_on_tiles([pixel_block(FAMILY[f][0]) for f in band]), (0, y))
        board.alpha_composite(row_on_tiles([drawn(f, 'idle', base) for f in band]), (0, y + H + 2))
    return zoom(board, 6)


if __name__ == '__main__':
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(os.environ.get('JUNIMO_PNG', DEFAULT_JUNIMO))
    base = Image.open(path).convert('RGBA').crop((0, 0, T, T))
    bad = {(f, fr): hits for f, draw in KIT.items() for fr in FRAMES if (hits := draw(fr).face_hits())}
    bad |= {(f, 'wear'): hits for f, (top, rows) in WEAR.items() if f not in EYEWEAR and (hits := sorted(
        (x, top + y) for y, r in enumerate(rows) for x, c in enumerate(r) if c != '.' and (x, top + y) in GUARD))}
    if bad:
        sys.exit(f'covers the face: {bad}')
    out = ROOT / 'assets' / 'textures' / 'junimo_blocks.png'
    sheet = build(base)
    sheet.save(out)
    prev = ROOT / 'art' / 'preview'
    frames_board(base).save(prev / 'junimo_blocks_frames.png')
    icons_board(base).save(prev / 'junimo_blocks_icons.png')
    gif(base, prev / 'junimo_blocks.gif')
    print('wrote', out, sheet.size)
