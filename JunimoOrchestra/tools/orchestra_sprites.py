"""Pixel art for the Junimo orchestra: instruments a Junimo actually plays (bows move, mallets strike, cymbals crash).

    python3 tools/orchestra_sprites.py <preview.png> <the game's Characters/Junimo.png>

Writes assets/textures/orchestra.png: one row per instrument, 32x32 cells: [back idle, back A, back B, front idle, front A, front B].
'Back' is drawn behind the Junimo, 'front' over it. The Junimo (16x16, the game's own sprite, tinted) stands with its
feet at (16, 31) of the cell. Frame A/B alternate on successive notes (down-bow / up-bow, left / right mallet...).
Row order must match OrchestraArt.Instruments in src/Game/OrchestraArt.cs.

Below the instruments, at y = 320, is the Junimos' concert dress (a top hat and a red bow tie), drawn over the Junimo:
16x20 cells [front (Junimo frames 0, 44, 46), side (16), back (32)], the Junimo's own 16x16 frame starting 4 rows down.
Next to them, at (48, 324) and (64, 324), Junimo frames 0 and 44 with happy closed eyes (^ ^), in the Junimo's own
palette so the game tints them like the real thing (for long notes, and blinking).
"""
import sys
from PIL import Image

W = H = 32
PAL = {
    'o': (58, 29, 18), 'O': (30, 16, 12),                              # outlines
    'W': (107, 50, 20), 'w': (156, 78, 34), 'l': (201, 118, 58), 'h': (232, 160, 96),   # wood dark..highlight
    'e': (43, 29, 22),                                                 # ebony
    's': (239, 227, 196), 'S': (190, 176, 150),                        # strings / bow hair
    't': (140, 100, 60),                                               # bow stick
    'b': (34, 28, 43), 'B': (74, 64, 96), 'x': (110, 100, 130),        # piano black, highlight
    'k': (243, 234, 214), 'K': (180, 170, 150),                        # ivory keys
    'g': (176, 122, 34), 'G': (232, 182, 64), 'y': (255, 224, 138),    # gold
    'c': (138, 74, 34), 'C': (196, 111, 53), 'q': (226, 150, 90),      # copper
    'd': (239, 226, 194), 'D': (205, 187, 148),                        # drum head
    'r': (192, 57, 43), 'R': (140, 30, 30),                            # felt
    'v': (255, 255, 255),                                              # shine
}
OUTLINE = {'W', 'w', 'l', 'h', 'e', 'b', 'B', 'x', 'k', 'K', 'g', 'G', 'y', 'c', 'C', 'q', 'd', 'D', 'r', 'R'}


class Canvas:
    def __init__(self):
        self.px = [[None] * W for _ in range(H)]

    def put(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = c

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < W and 0 <= y < H else None

    def ellipse(self, cx, cy, rx, ry, c):
        for y in range(H):
            for x in range(W):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    self.put(x, y, c)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.put(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def shade(self, base, light, dark):
        """Light from the upper left: edge pixels of a colour facing left/up get `light`, right/down get `dark`."""
        src = [row[:] for row in self.px]
        g = lambda x, y: src[y][x] if 0 <= x < W and 0 <= y < H else None
        for y in range(H):
            for x in range(W):
                if src[y][x] != base:
                    continue
                if g(x - 1, y) is None or g(x, y - 1) is None:
                    self.px[y][x] = light
                elif g(x + 1, y) is None or g(x, y + 1) is None:
                    self.px[y][x] = dark

    def outline(self, c='o'):
        src = [row[:] for row in self.px]
        for y in range(H):
            for x in range(W):
                if src[y][x] is None and any(
                        0 <= x + dx < W and 0 <= y + dy < H and src[y + dy][x + dx] in OUTLINE
                        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    self.px[y][x] = c
        return self

    def over(self, other):
        for y in range(H):
            for x in range(W):
                if other.px[y][x] is not None:
                    self.px[y][x] = other.px[y][x]
        return self

    def image(self):
        im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        for y in range(H):
            for x in range(W):
                if self.px[y][x] is not None:
                    im.putpixel((x, y), PAL[self.px[y][x]] + (255,))
        return im


def violin_rot(x0, y0, angle_deg, k=1.0):
    """A violin seen from the front: under the chin at (x0, y0) (the lower bout), neck rising to the right."""
    import math
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    c = Canvas()
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - x0, y + 0.5 - y0
            u = (dx * ca + dy * sa) / k                                # along the violin
            v = (-dx * sa + dy * ca) / k                               # across it
            col = None
            if (u / 2.7) ** 2 + (v / 2.6) ** 2 <= 1:                  # lower bout
                col = 'w'
            elif ((u - 4.6) / 2.2) ** 2 + (v / 2.1) ** 2 <= 1:        # upper bout
                col = 'w'
            elif 1.5 < u < 4.0 and abs(v) < 1.6:                      # waist
                col = 'w'
            if -1.0 < u < 9.6 and abs(v) < 0.55:                      # fingerboard and neck
                col = 'e'
            if 9.6 <= u < 11.2 and abs(v) < 0.95:                     # scroll
                col = 'W'
            if abs(u - 1.0) < 0.5 and abs(v) < 1.3:                   # bridge
                col = 's'
            if col:
                c.put(x, y, col)
    c.shade('w', 'l', 'W')
    return c.outline()


def bow_at(x, y, angle_deg, length, shift):
    """A bow crossing the strings at (x, y), perpendicular to a violin at angle_deg, slid along itself by `shift`."""
    import math
    a = math.radians(angle_deg + 90)
    ca, sa = math.cos(a), math.sin(a)
    c = Canvas()
    for i in range(-length // 2, length // 2 + 1):
        t = i + shift
        px, py = x + ca * t, y + sa * t
        c.put(int(round(px)), int(round(py)), 't')
        c.put(int(round(px)) + 1, int(round(py)), 'S')
    t = length // 2 + shift
    c.put(int(round(x + ca * t)), int(round(y + sa * t)), 'O')     # frog at the Junimo's end
    return c


def violin(x0, y0, big=False):
    """Held under the chin, pointing to the Junimo's left (our right): lower bout at (x0, y0)."""
    c = Canvas()
    s = 1.15 if big else 1.0
    c.ellipse(x0 + 2.5 * s, y0 + 2.5, 2.6 * s, 2.5, 'w')           # lower bout
    c.ellipse(x0 + 7.2 * s, y0 + 2.5, 2.1 * s, 2.0, 'w')           # upper bout
    c.rect(int(x0 + 4 * s), y0 + 1, int(x0 + 5.5 * s), y0 + 3, 'w')  # waist
    c.shade('w', 'l', 'W')
    c.rect(int(x0 + 3 * s), y0 + 2, int(x0 + 13 * s), y0 + 2, 'e')   # fingerboard over the body and the neck
    c.put(int(x0 + 4 * s), y0 + 2, 's')                              # bridge
    c.rect(int(x0 + 13 * s) + 1, y0 + 1, int(x0 + 13 * s) + 2, y0 + 2, 'W')  # scroll
    return c.outline()


def bow_v(x, y0, length, shift):
    """A bow held upright across a violin's strings; `shift` slides it along its length (down-bow / up-bow)."""
    c = Canvas()
    for i in range(length):
        c.put(x + (i // 6), y0 + shift + i, 't')
        c.put(x + (i // 6) + 1, y0 + shift + i, 'S')
    c.put(x + (length - 1) // 6, y0 + shift + length - 1, 'O')        # frog
    return c


def bow_h(y, x0, length, shift):
    """A bow drawn across a cello or bass, sliding sideways."""
    c = Canvas()
    for i in range(length):
        c.put(x0 + shift + i, y, 't')
        c.put(x0 + shift + i, y + 1, 'S')
    c.put(x0 + shift, y, 'O')
    c.put(x0 + shift, y + 1, 'O')
    return c


def cello(cx, bottom, tall=False):
    """Upright, endpin on the ground at (cx, bottom)."""
    c = Canvas()
    k = 1.25 if tall else 1.0
    lb = bottom - 1 - 4 * k                                         # lower bout centre
    c.ellipse(cx + 0.5, lb, 4.2 * k, 4.0 * k, 'w')
    ub = lb - 6.5 * k
    c.ellipse(cx + 0.5, ub, 3.4 * k, 3.2 * k, 'w')
    c.rect(int(cx - 2 * k) + 1, int(ub), int(cx + 2 * k), int(lb), 'w')
    c.shade('w', 'l', 'W')
    top = int(ub - 3.2 * k - 6 * k)
    c.rect(cx, top + 2, cx, int(lb + 2), 'e')                       # neck + fingerboard
    c.rect(cx - 1, top, cx + 1, top + 1, 'W')                      # scroll
    c.put(cx, int(lb + 1.5 * k), 's')                                # bridge
    c.rect(cx, bottom - 1, cx, bottom, 'O')                          # endpin
    return c.outline()


def harp(x0, bottom):
    c = Canvas()
    top = bottom - 25
    c.rect(x0, top + 2, x0 + 1, bottom - 1, 'G')                    # pillar
    for i in range(12):                                             # neck: a gentle S curve along the top
        c.put(x0 + 1 + i, top + 1 + int(2.2 * (i / 11) ** 1.5) + (1 if 3 < i < 8 else 0), 'G')
        c.put(x0 + 1 + i, top + 2 + int(2.2 * (i / 11) ** 1.5) + (1 if 3 < i < 8 else 0), 'g')
    for i in range(22):                                             # soundboard: diagonal from the foot to the neck
        x = x0 + 2 + int(i * 10 / 21)
        y = bottom - 2 - i
        c.put(x, y, 'w')
        c.put(x + 1, y, 'l')
    c.rect(x0 - 1, bottom - 1, x0 + 5, bottom, 'g')                 # base
    c.shade('G', 'y', 'g')
    c = c.outline()
    for sx in range(x0 + 2, x0 + 12, 2):                            # strings between the neck and the soundboard
        y_top = top + 4 + int(2.2 * ((sx - x0 - 1) / 11) ** 1.5)
        y_bot = bottom - 2 - int((sx - x0 - 2) * 21 / 10)
        for y in range(y_top, y_bot):
            if c.get(sx, y) is None:
                c.put(sx, y, 's')
    return c


def piano(x0, bottom):
    """A little grand piano, lid up, keys toward us; the Junimo sits behind the keys."""
    c = Canvas()
    c.rect(x0, bottom - 8, x0 + 21, bottom - 4, 'b')               # case
    c.ellipse(x0 + 21, bottom - 6, 3, 2.6, 'b')                     # curved tail
    c.rect(x0 + 1, bottom - 3, x0 + 2, bottom, 'b')                 # legs
    c.rect(x0 + 18, bottom - 3, x0 + 19, bottom, 'b')
    for i in range(12):                                             # the lid, propped open toward the back
        c.rect(x0 + 6 + i, bottom - 9 - i // 2, x0 + 21, bottom - 9 - i // 2, 'b')
    c.shade('b', 'B', 'b')
    c = c.outline('O')
    c.rect(x0 + 1, bottom - 8, x0 + 13, bottom - 7, 'k')            # keyboard
    for i in range(1, 13, 2):
        c.put(x0 + i + (1 if i % 4 == 1 else 0), bottom - 8, 'b')
    return c


def piano_keys_down(x0, bottom, which):
    c = Canvas()
    for i in which:
        c.put(x0 + i, bottom - 8, 'K')
        c.put(x0 + i, bottom - 7, 'K')
    return c


def timpani(cx, bottom):
    c = Canvas()
    c.ellipse(cx + 0.5, bottom - 6, 7.5, 5.5, 'C')                  # kettle
    c.rect(cx - 8, bottom - 13, cx + 9, bottom - 7, None)           # (cut the top half off)
    for y in range(H):
        for x in range(W):
            if y < bottom - 7 and c.get(x, y) == 'C':
                c.put(x, y, None)
    c.shade('C', 'q', 'c')
    c.ellipse(cx + 0.5, bottom - 7, 7.5, 1.8, 'd')                  # head
    c.shade('d', 'd', 'D')
    c.rect(cx - 5, bottom - 1, cx - 5, bottom, 'c')                 # legs
    c.rect(cx + 6, bottom - 1, cx + 6, bottom, 'c')
    return c.outline()


def mallets(cx, head_y, down, side):
    """Two timpani sticks: raised, or (down) one of them striking the head."""
    c = Canvas()
    for s, hand in ((-1, 'L'), (1, 'R')):
        striking = down and ((hand == 'L') == (side == 0))
        tip = (cx + s * 4, head_y - (0 if striking else 7))
        c.line(cx + s * 7, head_y - 4, tip[0], tip[1], 't')
        c.put(tip[0], tip[1], 'r')
        c.put(tip[0], tip[1] - 1, 'r')
        c.put(tip[0] + (1 if s > 0 else -1), tip[1], 'R')
    return c


def cymbals(cx, y, crash):
    c = Canvas()
    if crash:
        c.ellipse(cx - 1, y, 1.6, 4.5, 'G')
        c.ellipse(cx + 2, y, 1.6, 4.5, 'G')
    else:
        c.ellipse(cx - 9, y, 1.6, 4.5, 'G')
        c.ellipse(cx + 10, y, 1.6, 4.5, 'G')
    c.shade('G', 'y', 'g')
    c = c.outline('O')
    if crash:
        for dx, dy in ((0, -7), (-3, -6), (3, -6), (-5, -3), (5, -3)):
            c.put(cx + dx, y + dy, 'v')
    return c


def shaker(x, y, up):
    c = Canvas()
    yy = y - (3 if up else 0)
    c.ellipse(x + 0.5, yy, 2.2, 1.8, 'G')
    c.rect(x, yy + 2, x, yy + 3, 't')
    c.shade('G', 'y', 'g')
    c = c.outline('O')
    if up:
        c.put(x - 3, yy - 2, 'v')
        c.put(x + 4, yy - 2, 'v')
    return c


def congas(cx, bottom, hit):
    c = Canvas()
    for dx, h in ((-6, 8), (5, 9)):
        c.rect(cx + dx - 2, bottom - h, cx + dx + 2, bottom - 1, 'w')
        c.rect(cx + dx - 2, bottom - h + 2, cx + dx + 2, bottom - h + 2, 'g')
        c.rect(cx + dx - 2, bottom - 3, cx + dx + 2, bottom - 3, 'g')
    c.shade('w', 'l', 'W')
    for dx, h in ((-6, 8), (5, 9)):
        c.rect(cx + dx - 2, bottom - h - 1, cx + dx + 2, bottom - h - 1, 'd' if not hit else 'v')
    return c.outline()


# ---------------------------------------------------------------------------------------------- the sheet
FEET = 31
ROWS = []                       # (name, back frames, front frames)
e = Canvas


def row(name, back, front):
    ROWS.append((name, back, front))


# violin / viola: on the shoulder, pointing up to the right; cello / bass: upright. Their bows are drawn separately
# (BOWED below) so they can slide the whole length of each note.
VIOLIN_AT, VIOLIN_ANGLE = (19, 24), -30
for name, k in (('violin', 1.0), ('viola', 1.15)):
    v = violin_rot(*VIOLIN_AT, VIOLIN_ANGLE, k)
    row(name, [e(), e(), e()], [v, v, v])
cel = cello(11, FEET)
row('cello', [e(), e(), e()], [cel, cel, cel])
bas = cello(20, FEET, tall=True)
row('bass', [e(), e(), e()], [bas, bas, bas])
hp = harp(3, FEET)
row('harp', [hp, hp, hp], [e(), Canvas().over(piano_keys_down(0, 0, [])), e()])
pn = piano(4, FEET)
row('piano', [e(), e(), e()], [pn, Canvas().over(pn).over(piano_keys_down(4, FEET, [2, 5, 9])),
                               Canvas().over(pn).over(piano_keys_down(4, FEET, [3, 7, 11]))])
tp = timpani(16, FEET)
row('timpani', [e(), e(), e()], [Canvas().over(tp).over(mallets(16, FEET - 8, False, 0)),
                                 Canvas().over(tp).over(mallets(16, FEET - 8, True, 0)),
                                 Canvas().over(tp).over(mallets(16, FEET - 8, True, 1))])
row('cymbals', [e(), e(), e()], [cymbals(16, 22, False), cymbals(16, 20, True), cymbals(16, 20, True)])
row('shaker', [e(), e(), e()], [shaker(23, 22, False), shaker(23, 22, True), shaker(24, 21, True)])
row('congas', [e(), e(), e()], [congas(16, FEET, False), congas(16, FEET, True), congas(16, FEET, True)])

# where the Junimo stands for each instrument: offset of its feet from (16, 31), and whether it sits behind the front layer
JUNIMO_AT = {'violin': (-3, 0), 'viola': (-3, 0), 'cello': (4, 0), 'bass': (-5, 0), 'harp': (6, 0), 'piano': (-2, -9),
             'timpani': (0, -5), 'cymbals': (0, 0), 'shaker': (-2, 0), 'congas': (0, -6)}

# concert dress: a top hat (on the sprout) and a red bow tie (under the chin), in Junimo-sprite coordinates
HAT_K, HAT_HI, TIE, KNOT = (24, 20, 32, 255), (70, 66, 90, 255), (200, 40, 56, 255), (120, 20, 36, 255)


def dress(hat_x, tie):
    # worn, not perched: the brim sits a row down on the head (row 4), so the crown takes in its top and the sprout
    px = {}
    for y in range(0, 4):
        for x in range(hat_x - 1, hat_x + 3):
            px[(x, y)] = HAT_K
    for x in range(hat_x - 3, hat_x + 5):
        px[(x, 4)] = HAT_K                                                 # brim
    for x in range(hat_x - 1, hat_x + 3):
        px[(x, 3)] = TIE                                                   # hat band
    px[(hat_x + 2, 0)] = px[(hat_x + 2, 1)] = HAT_HI                       # sheen
    px.update(tie)
    return px


def bow_front(y=13):
    pat = ['#....#', '##oo##', '#....#']
    return {(5 + i, y + dy): TIE if ch == '#' else KNOT for dy, r in enumerate(pat) for i, ch in enumerate(r) if ch != '.'}


DRESS = [dress(7, bow_front()),                                            # front
         dress(8, {(12, 13): TIE, (11, 14): KNOT, (12, 14): TIE, (13, 14): TIE, (12, 15): TIE}),   # side (facing right)
         dress(7, {})]                                                     # back
DRESS_Y = H * len(ROWS)

BOW_FRAMES = 9
BOWED_Y = DRESS_Y + 32                   # rows of bows, their hands, and fingering hands (see BOWED)
sheet = Image.new('RGBA', (W * BOW_FRAMES, BOWED_Y + 10 * H), (0, 0, 0, 0))
for r, (name, back, front) in enumerate(ROWS):
    for i, cv in enumerate(back + front):
        sheet.alpha_composite(cv.image(), (i * W, r * H))
for i, px in enumerate(DRESS):
    for (x, y), c in px.items():
        sheet.putpixel((i * 16 + x, DRESS_Y + 4 + y), c)


def closed_eyes(junimo, frame, eye_row):
    body, line = (196, 206, 206, 255), (55, 55, 81, 255)
    fr = junimo.crop((frame % 8 * 16, frame // 8 * 16, frame % 8 * 16 + 16, frame // 8 * 16 + 16)).copy()
    for ex in (5, 10):
        fr.putpixel((ex, eye_row + 1), body)                               # the eye's shine
        fr.putpixel((ex - 1, eye_row + 1), line)                           # ^
        fr.putpixel((ex, eye_row), line)
        fr.putpixel((ex + 1, eye_row + 1), line)
    return fr


JUNIMO_PNG = sys.argv[2] if len(sys.argv) > 2 else 'Junimo.png'
_junimo = Image.open(JUNIMO_PNG).convert('RGBA')
for i, (frame, eye_row) in enumerate([(0, 11), (44, 10)]):
    sheet.alpha_composite(closed_eyes(_junimo, frame, eye_row), (48 + i * 16, DRESS_Y + 4))

# the same Junimo without its little arms (for players whose hands are drawn on the instrument): open, closed eyes
ARMS = [(0, 9), (1, 10), (15, 9), (14, 10)]
for i, fr in enumerate([_junimo.crop((0, 0, 16, 16)).copy(), closed_eyes(_junimo, 0, 11)]):
    for p in ARMS:
        fr.putpixel(p, (0, 0, 0, 0))
    sheet.alpha_composite(fr, (80 + i * 16, DRESS_Y + 4))

# ---------------------------------------------------------------------------------------------- hands and bows
# Hands are drawn in the Junimo's own palette so the game tints them to match. A hand is a little round mitten.
J_BODY, J_LINE = (196, 206, 206, 255), (55, 55, 81, 255)


def mitten(im, hx, hy):
    for dx, dy in ((-1, 0), (-1, 1), (2, 0), (2, 1), (0, -1), (1, -1), (0, 2), (1, 2)):
        if 0 <= hx + dx < W and 0 <= hy + dy < H:
            im.putpixel((hx + dx, hy + dy), J_LINE)
    for dx in (0, 1):
        for dy in (0, 1):
            if 0 <= hx + dx < W and 0 <= hy + dy < H:
                im.putpixel((hx + dx, hy + dy), J_BODY)


def arm_line(im, x0, y0, x1, y1):
    c = Canvas()
    c.line(x0, y0, x1, y1, 'o')
    for y in range(H):
        for x in range(W):
            if c.px[y][x]:
                im.putpixel((x, y), J_LINE)


def bow_line(frog, theta_deg, length):
    import math
    d = (math.cos(math.radians(theta_deg)), math.sin(math.radians(theta_deg)))
    c = Canvas()
    for i in range(length + 1):
        x, y = int(round(frog[0] - d[0] * i)), int(round(frog[1] - d[1] * i))
        c.put(x, y, 't')
        c.put(x, y + 1, 'S')
    return c


# Each bowed instrument: where its bow crosses the strings, the direction from there to the frog, the bow's length, how
# far the frog is from the strings at the start and end of a stroke, and (violin, viola) where the fingering hand goes.
# BOW_FRAMES positions from "frog at the strings" (0) to "tip at the strings" (8); a note is one whole stroke.
BOWED = {
    'violin': dict(contact=(20.6, 23.2), theta=200, length=14, travel=(3, 10), finger=(VIOLIN_AT, VIOLIN_ANGLE, 1.0, (18, 26))),
    'viola': dict(contact=(20.6, 23.4), theta=200, length=15, travel=(3, 10), finger=(VIOLIN_AT, VIOLIN_ANGLE, 1.15, (18, 26))),
    'cello': dict(contact=(11, 25), theta=0, length=12, travel=(2, 8)),
    'bass': dict(contact=(20, 25), theta=180, length=12, travel=(2, 8)),
}
FINGER_FRAMES = 5                         # fingering hand from low notes (near the scroll) to high (near the body)
for r, (name, spec) in enumerate(BOWED.items()):
    import math
    d = (math.cos(math.radians(spec['theta'])), math.sin(math.radians(spec['theta'])))
    for k in range(BOW_FRAMES):
        dist = spec['travel'][0] + (spec['travel'][1] - spec['travel'][0]) * k / (BOW_FRAMES - 1)
        frog = (spec['contact'][0] + d[0] * dist, spec['contact'][1] + d[1] * dist)
        sheet.alpha_composite(bow_line(frog, spec['theta'], spec['length']).image(), (k * W, BOWED_Y + r * 2 * H))
        grip = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        mitten(grip, int(round(frog[0])) - (1 if d[0] > 0 else 0), int(round(frog[1])))
        sheet.alpha_composite(grip, (k * W, BOWED_Y + (r * 2 + 1) * H))
for r, name in enumerate(('violin', 'viola')):
    (vx, vy), angle, kk, shoulder = BOWED[name]['finger']
    a = math.radians(angle)
    for k in range(FINGER_FRAMES):
        u = (9.0 - 3.8 * k / (FINGER_FRAMES - 1)) * kk
        hx, hy = int(round(vx + math.cos(a) * u)), int(round(vy + math.sin(a) * u)) + 1
        im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        arm_line(im, shoulder[0], shoulder[1], hx - 1, hy + 1)
        mitten(im, hx - 1, hy)
        sheet.alpha_composite(im, (k * W, BOWED_Y + (8 + r) * H))
out = __file__.rsplit('/', 2)[0] + '/assets/textures/orchestra.png'
sheet.save(out)
print('rows:', [r[0] for r in ROWS], sheet.size)

if len(sys.argv) > 1:
    # preview: each instrument with a tinted Junimo, idle / A / B, at 6x on grass
    junimo = Image.open(sys.argv[2] if len(sys.argv) > 2 else 'Junimo.png').convert('RGBA')
    tints = [(255, 107, 138), (180, 140, 255), (128, 112, 255), (91, 168, 255), (255, 165, 61), (255, 224, 102),
             (124, 224, 124), (124, 224, 124), (124, 224, 124), (124, 224, 124)]
    S = 6
    prev = Image.new('RGBA', (W * 3 * S, H * len(ROWS) * S), (196, 120, 60, 255))
    for r, (name, back, front) in enumerate(ROWS):
        for i in range(3):
            cell = Image.new('RGBA', (W, H), (0, 0, 0, 0))
            cell.alpha_composite(back[i].image())
            frame = 44 if (name in ('cymbals', 'shaker') and i > 0) else 0
            j = junimo.crop((frame % 8 * 16, frame // 8 * 16, frame % 8 * 16 + 16, frame // 8 * 16 + 16))
            t = tints[r]
            j = Image.merge('RGBA', [ch.point(lambda v, k=k: v * t[k] // 255) for k, ch in enumerate(j.split()[:3])] + [j.split()[3]])
            hop = -1 if i > 0 else 0
            jx, jy = JUNIMO_AT[name]
            cell.alpha_composite(j, (8 + jx, FEET - 15 + hop + jy))
            d = sheet.crop((0, DRESS_Y, 16, DRESS_Y + 20))
            cell.alpha_composite(d, (8 + jx, FEET - 19 + hop + jy)) if FEET - 19 + hop + jy >= 0 else None
            cell.alpha_composite(front[i].image())
            prev.alpha_composite(cell.resize((W * S, H * S), Image.NEAREST), (i * W * S, r * H * S))
    prev.save(sys.argv[1])
