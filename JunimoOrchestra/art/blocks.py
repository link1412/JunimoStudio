"""Instrument block sprites: 16x16 each, built on the vanilla Flute/Drum Block skeleton.

    python3 blocks.py [preview dir]

Writes assets/textures/blocks.png (row 0: the 17 blocks in Families.cs order; row 1: their white flash frames) and previews.

A block is a painted toy block with its family's instrument lying on the top face:
- the body is the vanilla block's skeleton pixel for pixel (a 10-row top face, a lit lip, a 3-row front). It is painted
  in the family's accent from Families.cs - the colour of its Junimo, its notes and its panel highlight - a step deeper,
  so the instrument on it stands out. The ramp is built in OKLCH, so every family's face sits at the same perceived
  depth: lights lean warm, darks lean blue-violet and give up chroma, nothing reaches black;
- the instrument fills the 12x10 face and is drawn in its own materials (ivory, brass, steel, spruce, ebony...) with
  no outline: lit from the top left, it casts a one-pixel shadow (the body's front colour) down onto the face, so it
  reads as a little object on the block rather than a sticker. At this size an outline would eat a third of it;
- each instrument is the one that still reads at game scale (all were tried at 1x, 2x and 4x, on grass, in the
  inventory, on the panel and in greyscale): the bells family plays a rainbow toy glockenspiel, as its Junimo does (a
  bell reads as a notification); the drum kit is the vanilla Drum Block's own drum in the family's colour,
  and the steel drum its shallow chrome cousin; the pipe family plays a pan flute, as its Junimo does (a small ocarina
  reads as a potato or a fish); the choir is the stand mic its Junimo sings at; the synth families are symbols: a
  square wave (lead), a cloud (pad), the star wand its Junimo waves (effects);
- the guitar, the bass and the violin each have their own wood and lie their own way, so they don't read as one shape:
  a spruce guitar with a round sound hole and an electric bass in red lacquer, both up to the right, and a violin in
  honey varnish up to the left.

The build fails if a colour is black-ish, an instrument leaves the top face, an instrument doesn't stand out from its
face, or a block's silhouette isn't the skeleton's.
"""
from __future__ import annotations

import colorsys
import math
import re
import sys
from pathlib import Path
from PIL import Image, ImageOps

from px import grid, hex2rgba, silhouette, zoom

C = hex2rgba
ROOT = Path(__file__).resolve().parent.parent
SPRINGOBJECTS = Path.home() / 'Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS' \
    / 'Content (unpacked)/Maps/springobjects.png'   # optional: vanilla Flute/Drum Block in the review sheet

# the families' order and accents, straight from the mod
ACCENT = dict(re.findall(r'new Family\("(\w+)", \d+, \d+, "(#[0-9a-f]{6})"', (ROOT / 'src/Data/Families.cs').read_text()))

# ---------------------------------------------------------------- block body
BODY = [
    "................",
    "...aaaaaaaaaa...",
    "..aHHLLLLLLLLa..",
    ".acHLLLLLLLLLLa.",
    ".aLLLLLLLLLLLLa.",
    ".aLLLLLLLLLLLLa.",
    ".aLLLLLLLLLLLLa.",
    ".aLLLLLLLLLLLLa.",
    ".aLLLLLLLLLLLLa.",
    ".aLLLLLLLLLLLLa.",
    ".acLLLLLLLLLLca.",
    ".aeLLLLLLLLLLea.",
    ".DceeeeeeeeeecD.",
    ".DaaaaaaaaaaaaD.",
    "..DaaaaaaaaaaD..",
    "...DDDDDDDDDD...",
]
FACE = {(x, y) for y, row in enumerate(BODY) for x, ch in enumerate(row) if ch in 'LHce' and y <= 11}
FACE_X, FACE_Y = 2, 2                          # an emblem's 12x10 grid starts here (the top face's box)


# ---------------------------------------------------------------- colour: OKLCH
def _lin(v: float) -> float:
    v /= 255
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def _gam(v: float) -> int:
    v = min(1.0, max(0.0, v))
    return round(255 * (12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055))


def to_lch(c) -> tuple[float, float, float]:
    r, g, b = (_lin(v) for v in c[:3])
    l, m, s = (math.copysign(abs(v) ** (1 / 3), v) for v in (
        0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b,
        0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b,
        0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b))
    a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s
    bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s
    return 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s, math.hypot(a, bb), math.degrees(math.atan2(bb, a)) % 360


def _linear_rgb(L: float, ch: float, h: float) -> tuple[float, float, float]:
    a, b = ch * math.cos(math.radians(h)), ch * math.sin(math.radians(h))
    l, m, s = (v ** 3 for v in (L + 0.3963377774 * a + 0.2158037573 * b,
                               L - 0.1055613458 * a - 0.0638541728 * b,
                               L - 0.0894841775 * a - 1.2914855480 * b))
    return (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s)


def lch(L: float, ch: float, h: float):
    """OKLCH to sRGBA, giving up chroma (never lightness or hue) until the colour fits in sRGB."""
    while ch > 0 and not all(-0.0005 <= v <= 1.0005 for v in _linear_rgb(L, ch, h)):
        ch -= 0.002
    return tuple(_gam(v) for v in _linear_rgb(L, max(ch, 0.0), h)) + (255,)


def _toward(h: float, target: float, most: float) -> float:
    """h turned toward the target hue, by at most `most` degrees."""
    d = ((target - h + 180) % 360) - 180
    return h + max(-most, min(most, d))


DEPTH, CHROMA = 0.07, 0.14      # the face is the accent this much deeper (OKLCH lightness), at most this saturated


def body_ramp(accent: str) -> dict:
    L, ch, h = to_lch(C(accent))
    L, ch = L - DEPTH, min(ch, CHROMA)
    return {
        'L': lch(L, ch, h),                                            # top face
        'H': lch(L + 0.09, ch * 0.8, _toward(h, 90, 8)),               # glint
        'c': lch(L - 0.035, ch, _toward(h, 280, 6)),                   # face corners
        'e': lch(L + 0.06, ch * 0.9, _toward(h, 90, 6)),               # lit lip
        'a': lch(L - 0.25, ch * 0.72, _toward(h, 280, 14)),            # front, the face's outline, the emblem's shadow
        'D': lch(max(L - 0.40, 0.24), ch * 0.5, _toward(h, 280, 20)),  # bottom outline
    }


# ---------------------------------------------------------------- materials (lit from the top left: light, base, shade)
IVORY = {'W': C('#fffbf2'), 'I': C('#f4e6cc'), 'i': C('#d9c2a2')}
BRASS = {'Y': C('#fff3b0'), 'y': C('#ffd25a'), 'z': C('#e39a2f'), 'Z': C('#a8601e')}
STEEL = {'S': C('#ffffff'), 's': C('#dfe7f1'), 't': C('#aab6cc'), 'T': C('#6c7795')}
SPRUCE = {'P': C('#ffe2a9'), 'p': C('#f2b366'), 'q': C('#c97b3c')}
ROSEWOOD = {'n': C('#8c4a2b'), 'N': C('#5c2c1c')}
EBONY = {'k': C('#2c2236'), 'K': C('#4b3f5c')}
FELT = {'r': C('#e84a5f')}

FAMILIES: list[dict] = []


def fam(key: str, emblem: list[str], *materials: dict):
    """emblem: the 12x10 top face ('.' leaves the paint showing); it may also use the body's own a (front), L (face) and
    D (darkest) - the drums are cut from the block like the vanilla Drum Block's."""
    pal = {}
    for m in materials:
        pal.update(m)
    FAMILIES.append({'key': key, 'emblem': emblem, 'pal': pal})


# 0 Piano: the keys from C to A under a black fallboard and a red felt strip, the face showing either side (a whole
#   octave, edge to edge with its three and two black keys, turns the block into a white bar; the margins keep the
#   keyboard a thing lying on it). The black keys run two thirds down, as on a piano. No joins are drawn between the
#   white keys: at a pixel a semitone there is no column for the one between E and F, and with the others drawn under
#   the black keys, E and F read as one wide key
fam('piano', [
    "............",
    ".KKKKKKKKKK.",
    ".rrrrrrrrrr.",
    ".WkWkWWkWkW.",
    ".WkWkWWkWkW.",
    ".WkWkWWkWkW.",
    ".WkWkWWkWkW.",
    ".WWWWWWWWWW.",
    ".iiiiiiiiii.",
    "............",
], IVORY, EBONY, FELT)

# 1 Chromatic percussion (music box, glockenspiel, xylophone): the rainbow toy glockenspiel its modern Junimo plays,
#   in the same paint: six bars shortening to the right, each seamed in the frame's dark wood, a white-headed mallet
#   resting on the blue bar. Solid, the rainbow reads as a flag; without the mallet, as a chart
GLOCK = {'R': C('#e8483f'), 'O': C('#f59a35'), 'Y': C('#f7d23e'), 'G': C('#5cc25a'), 'B': C('#4a9fe8'), 'P': C('#9b6ae0'),
         'o': C('#3d2a1e'), 'k': C('#4a2a14'), 'm': C('#ffffff')}   # the frame; the mallet's stick and head
fam('bells', [
    "............",
    "RoOo........",
    "RoOoYoGo....",
    "RoOoYoGoBoPo",
    "RoOoYoGoBoPo",
    "RoOoYoGommPo",
    "RoOoYoGommPo",
    "RoOoYoGo..k.",
    "RoOo.......k",
    "............",
], GLOCK)

# 2 Organ: four steel pipes on a wooden chest, the tall pair in the middle; the gaps keep them pipes (side by side
#   they read as a fence or a comb), the outer pair's mouths a row above the inner pair's
fam('organ', [
    "....St.St...",
    "....St.St...",
    ".St.St.St.St",
    ".St.St.St.St",
    ".TT.St.St.TT",
    ".St.TT.TT.St",
    ".St.St.St.St",
    ".St.St.St.St",
    "PPPPPPPPPPPP",
    "qqqqqqqqqqqq",
], STEEL, SPRUCE)

# 3 Guitar: acoustic, up to the right: a spruce top, the fingerboard running down to the sound hole, the bridge below
#   (with the hole on its own, the body reads as a bird's head)
fam('guitar', [
    "..........N.",
    ".........NN.",
    "........nn..",
    ".....PPnn...",
    "...PPPnnPq..",
    "..PPPnnPPq..",
    ".PPPkkPPq...",
    ".PPPkkPq....",
    ".PPnPPq.....",
    "..qqqq......",
], SPRUCE, ROSEWOOD, EBONY)

# 4 Bass: electric, up to the right: a red lacquer body with its horns and an ivory pickguard, the long neck
LACQUER = {'R': C('#ff8a7a'), 'r': C('#e8463c'), 'x': C('#a82a2e'), 'X': C('#6e1a24')}
fam('bass', [
    ".........NS.",
    "........NN..",
    ".......nS...",
    "......n.....",
    "...R.n.R....",
    "...RnrrX....",
    "..RrnWWrx...",
    "..rrWWrrx...",
    "..rrrrrx....",
    "...xxxx.....",
], ROSEWOOD, STEEL, IVORY, LACQUER)

# 5 Strings: a violin up to the left: the scroll, the ebony fingerboard down to the waist, f-holes either side of
#   the bridge, the tailpiece; in honey varnish (a red-brown violin melts into its rose block)
VARNISH = {'P': C('#ffe3a6'), 'p': C('#f7b55c'), 'q': C('#d27a34'),
           'N': C('#fff6e0'), 'n': C('#7a3418'), 'k': C('#2a1420')}   # bridge, f-holes, fingerboard and tailpiece
fam('violin', [
    "............",
    "pP..........",
    "qnk.........",
    "...kkPPq....",
    "..PPkkppq...",
    "..qpppkpq...",
    "...qqnpNPq..",
    "....PpNnppPq",
    "....Ppppkkpq",
    ".....qqqqqq.",
], VARNISH)

# 6 Ensemble: a stand mic with a note (its modern Junimo sings at one; a tiny Junimo on the block reads as a cat)
fam('choir', [
    "............",
    "....sSSs..W.",
    "...sStStt.WW",
    "...StStSt.W.",
    "...stststWW.",
    "....yyzz.WW.",
    ".....kk.....",
    ".....kk.....",
    "....kkkk....",
    "............",
], {'S': C('#ffffff'), 's': C('#d6dde9'), 't': C('#9aa6bd'), 'k': C('#3a3048'),   # a darker steel than the organ's,
    'y': C('#ffd25a'), 'z': C('#d9952f'), 'W': C('#ffffff')})                     # so the grille's checks show

# 7 Brass: a trumpet, three valves on top, the bell flaring to the right, the tuning slide underneath
fam('trumpet', [
    "............",
    "....Y.Y.Y...",
    "....y.y.y.Y.",
    "..YYyYyYyYYy",
    "Yyyyyyyyyyyz",
    "..zzzzzzzzYz",
    "..Yz....zzyz",
    "...zzzzzz..z",
    "............",
    "............",
], BRASS)

# 8 Reed: an alto sax, the mouthpiece at the top left, the keys down its body, the bell up to the right
fam('sax', [
    "............",
    ".k..........",
    "..yy........",
    "...yY...YYYY",
    "...yYz..yyyz",
    "...yYZ...yz.",
    "...yYz...yz.",
    "...yYZz.yyz.",
    "....zyyyyz..",
    ".....zzzz...",
], BRASS, EBONY)

# 9 Pipe: a bamboo pan flute bound with a cord, the pipes shortening to the right, as its modern Junimo plays (a small
#   ocarina reads as a potato or a fish)
BAMBOO = {'Y': C('#fff3c4'), 'y': C('#ffe27a'), 'z': C('#d9a441'),
          'B': C('#c0703a'), 'b': C('#7a3e1f'), 'k': C('#4a2a10')}   # the cord, the pipes' mouths
fam('ocarina', [
    "............",
    ".kYkYkYkYkY.",
    ".yzyzyzyzyz.",
    ".BbBbBbBbBb.",
    ".yzyzyzyzyz.",
    ".yzyzyzyz...",
    ".yzyzyz.....",
    ".yzyz.......",
    ".yz.........",
    "............",
], BAMBOO)

# 10 Synth lead: a square wave (the family's first sound)
fam('synth', [
    "............",
    "............",
    ".WWWW..WWWW.",
    ".W..W..W..W.",
    ".W..W..W..W.",
    ".W..W..W..W.",
    ".W..W..W..W.",
    "WW..WWWW..WW",
    "............",
    "............",
], IVORY)

# 11 Synth pad: a soft cloud
fam('cloud', [
    "............",
    "....WWW.....",
    "...WWWWW.WW.",
    ".WWWWWWWWWWW",
    "WWWWWWWWWWWI",
    "WWWWWWWWWWII",
    ".IIIIIIIIIi.",
    "............",
    "............",
    "............",
], IVORY)

# 12 Synth effects: a star wand casting sparkles (its modern Junimo waves one)
fam('stardust', [
    "........W...",
    "........W...",
    "......WWYWW.",
    ".......WYW..",
    "......nW.W..",
    ".....n......",
    "....n....W..",
    "...n....WYW.",
    "..n......W..",
    ".n..........",
], IVORY, BRASS, {'n': C('#7a3e1f')})

# 13 Ethnic: a walnut kalimba, the sound hole above the steel bridge, the tines hanging from it in a pair either side
#    of the middle, the inner pair longest (four evenly spaced tines can't sit centred in the box's even width); its
#    lit rim stays darker than the lime face (a lighter one melts into it without hue)
WALNUT = {'P': C('#a66a3e'), 'B': C('#7a4228'), 'b': C('#9a5a34'), 'K': C('#2a120a'),
          'S': C('#ffffff'), 's': C('#cfd7e4'), 'n': C('#f4f7fb'), 'N': C('#a9b4c8')}   # tines, bridge
fam('kalimba', [
    "..PPPPPPPP..",
    ".PBBBKKBBBb.",
    ".PnnnnnnnNb.",
    ".PSBSBBSBSB.",
    ".PSBSBBSBSB.",
    ".PsBSBBSBsB.",
    ".PBBSBBSBBB.",
    ".PBBsBBsBBB.",
    ".PBBBBBBBBB.",
    "..bBBBBBBb..",
], WALNUT)

# 14 Percussive: a steel pan, the vanilla Drum Block's drum made wide and shallow: a chrome dish hammered into notes,
#    a short skirt cut from the block's own darks
fam('steeldrum', [
    "............",
    "...aaaaaa...",
    "..amsWWsma..",
    ".aWsmWWmsWa.",
    ".aWWsmmsWWa.",
    ".DaWWWWWWaD.",
    ".DLaaaaaaLD.",
    ".DsDLLLLDsD.",
    "..aDDDDDDa..",
    "............",
], {'W': C('#ffffff'), 's': C('#dfe6f0'), 'm': C('#a9b4c8')})

# 15 Sound effects: a goldfinch singing a note
fam('songbird', [
    "..........W.",
    "....YYY...WW",
    "...YYYYy..W.",
    "...YkYyyUWW.",
    "..YYYYyyUUW.",
    "YYYYzzyy....",
    ".yyzzzzyz...",
    "..yyyyyz....",
    "....U.U.....",
    "............",
], BRASS, EBONY, IVORY, {'U': C('#ff8a2e')})

# 16 Drum kit: the vanilla Drum Block's drum, pixel for pixel: the shell in the family's colour, the head in ivory
#    (shaded underneath like the other instruments)
fam('drumkit', [
    "............",
    "....aaaa....",
    "...aWWWWa...",
    "..aWWWWWIa..",
    "..DaIIIIaD..",
    "..DLaaaaLD..",
    "..DWDLLDWD..",
    "..aDDLWDDa..",
    "...aDDDDa...",
    "............",
], IVORY)

assert [f['key'] for f in FAMILIES] == list(ACCENT), 'FAMILIES must follow Families.cs'


def emblem_layer(f: dict) -> Image.Image:
    r = body_ramp(ACCENT[f['key']])
    em = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    em.alpha_composite(grid(f['emblem'], {'a': r['a'], 'L': r['L'], 'D': r['D'], **f['pal']}), (FACE_X, FACE_Y))
    return em


def block_sprite(f: dict) -> Image.Image:
    r = body_ramp(ACCENT[f['key']])
    im = grid(BODY, r)
    em = emblem_layer(f)
    src = em.load()
    for y in range(15):                        # the shadow: a pixel down, onto the face
        for x in range(16):
            if src[x, y][3] and (x, y + 1) in FACE:
                im.putpixel((x, y + 1), r['a'])
    im.alpha_composite(em)
    return im


# ---------------------------------------------------------------- checks
def _lum(c) -> float:
    return 0.2126 * _lin(c[0]) + 0.7152 * _lin(c[1]) + 0.0722 * _lin(c[2])


def _contrast(a, b) -> float:
    la, lb = sorted((_lum(a), _lum(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


MIN_EDGE_CONTRAST = 1.6    # median contrast of an emblem's edge against what's around it on the face


def check(f: dict, spr: Image.Image) -> list[str]:
    errors = []
    px = spr.load()
    shape = {(x, y) for y, row in enumerate(BODY) for x, ch in enumerate(row) if ch != '.'}
    if {(x, y) for y in range(16) for x in range(16) if px[x, y][3]} != shape:
        errors.append("its silhouette isn't the block skeleton's")
    for y in range(16):
        for x in range(16):
            if px[x, y][3] and colorsys.rgb_to_hls(*(v / 255 for v in px[x, y][:3]))[1] < 0.08:
                errors.append(f'({x},{y}) is black-ish: #%02x%02x%02x' % px[x, y][:3])
    em = emblem_layer(f).load()
    drawn = {(x, y) for y in range(16) for x in range(16) if em[x, y][3]}
    if drawn - FACE:
        errors.append(f'the emblem leaves the top face at {sorted(drawn - FACE)}')
    edges = sorted(_contrast(px[x, y], px[nx, ny]) for x, y in drawn
                   for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
                   if (nx, ny) in FACE and (nx, ny) not in drawn)
    if edges and edges[len(edges) // 2] < MIN_EDGE_CONTRAST:
        errors.append(f'the emblem hardly stands out from its face (median edge contrast {edges[len(edges) // 2]:.2f})')
    return errors


def build(out_dir: Path) -> Image.Image:
    n = len(FAMILIES)
    sheet = Image.new('RGBA', (16 * n, 32), (0, 0, 0, 0))
    errors = []
    for i, f in enumerate(FAMILIES):
        spr = block_sprite(f)
        errors += [f"{f['key']}: {e}" for e in check(f, spr)]
        sheet.alpha_composite(spr, (i * 16, 0))
        sheet.alpha_composite(silhouette(spr), (i * 16, 16))  # flash frame
    if errors:
        sys.exit('blocks.py: ' + '\n  '.join(errors))
    sheet.save(out_dir / 'blocks.png')
    return sheet


# ---------------------------------------------------------------- previews
INVENTORY, GRASS = (255, 210, 132, 255), (118, 160, 92, 255)


def strip(sprites: list[Image.Image], bg, gap: int = 2) -> Image.Image:
    im = Image.new('RGBA', (2 + len(sprites) * (16 + gap), 20), bg)
    for i, s in enumerate(sprites):
        im.alpha_composite(s, (2 + i * (16 + gap), 2))
    return im


def review(sprites: list[Image.Image]) -> Image.Image:
    """In the inventory at the game's 4x with the vanilla Flute and Drum Blocks, the same in greyscale (values must hold
    without hue), at 2x (zoomed out), and in a row on the grass."""
    vanilla = []
    if SPRINGOBJECTS.exists():
        so = Image.open(SPRINGOBJECTS).convert('RGBA')
        vanilla = [so.crop(((i % 24) * 16, (i // 24) * 16, (i % 24) * 16 + 16, (i // 24) * 16 + 16)) for i in (464, 463)]
    inv = zoom(strip(sprites + vanilla, INVENTORY), 4)
    grey = ImageOps.grayscale(inv.convert('RGB')).convert('RGBA')
    rows = [inv, grey, zoom(strip(sprites + vanilla, INVENTORY), 2), zoom(strip(sprites, GRASS, gap=0), 4)]
    out = Image.new('RGBA', (max(r.width for r in rows), sum(r.height for r in rows) + 8 * (len(rows) - 1)), (50, 50, 50, 255))
    y = 0
    for r in rows:
        out.alpha_composite(r, (0, y))
        y += r.height + 8
    return out


if __name__ == '__main__':
    sheet = build(ROOT / 'assets' / 'textures')
    prev = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'art' / 'preview'
    prev.mkdir(parents=True, exist_ok=True)
    sprites = [sheet.crop((i * 16, 0, i * 16 + 16, 16)) for i in range(len(FAMILIES))]
    # 9 per row at 10x on the grass
    per = 9
    tiles = Image.new('RGBA', (per * 18 + 2, ((len(FAMILIES) + per - 1) // per) * 18 + 2), (0, 0, 0, 0))
    for i, s in enumerate(sprites):
        tiles.alpha_composite(s, (2 + (i % per) * 18, 2 + (i // per) * 18))
    zoom(tiles, 10, bg=GRASS).save(prev / 'blocks_x10.png')
    zoom(tiles, 4, bg=GRASS).save(prev / 'blocks_x4.png')
    review(sprites).save(prev / 'blocks_review.png')
    print('ok', sheet.size)
