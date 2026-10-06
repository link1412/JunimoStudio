"""Tiny pixel-art toolkit used to author every sprite in this mod as code.

Sprites are written as ASCII grids; each character maps to a colour in a palette
dict ('.' and ' ' are transparent). Colour ramps are built with hue-shifting so
shadows lean cool and highlights lean warm, which keeps small sprites lively.
"""
from __future__ import annotations

import colorsys
from PIL import Image

RGBA = tuple[int, int, int, int]


def hex2rgba(h: str, a: int = 255) -> RGBA:
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def _shift(rgb: RGBA, dl: float, ds: float, dh: float) -> RGBA:
    r, g, b, a = rgb
    h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
    h = (h + dh) % 1.0
    l = min(1, max(0, l + dl))
    s = min(1, max(0, s + ds))
    r2, g2, b2 = colorsys.hls_to_rgb(h, l, s)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


def toward(c: RGBA, target_hue_deg: float, amount: float) -> float:
    """Return a hue delta (0..1 units) nudging c's hue toward target by amount."""
    r, g, b, _ = c
    h, _, _ = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
    t = target_hue_deg / 360
    d = ((t - h + 0.5) % 1.0) - 0.5
    return d * amount


def ramp(base: str) -> dict[str, RGBA]:
    """5-step hue-shifted ramp: O(outline) d(dark) m(mid) L(base) h(light) H(highlight)."""
    c = hex2rgba(base)
    cool = lambda amt: toward(c, 250, amt)   # shadows drift toward blue-violet
    warm = lambda amt: toward(c, 55, amt)    # highlights drift toward yellow
    return {
        'O': _shift(c, -0.42, -0.05, cool(0.22)),
        'd': _shift(c, -0.20, -0.04, cool(0.10)),
        'm': _shift(c, -0.08, 0.00, cool(0.05)),
        'L': c,
        'h': _shift(c, +0.12, -0.05, warm(0.10)),
        'H': _shift(c, +0.24, -0.10, warm(0.16)),
    }


def grid(rows: list[str], pal: dict[str, RGBA], strict: bool = True) -> Image.Image:
    w = max(len(r) for r in rows)
    im = Image.new('RGBA', (w, len(rows)), (0, 0, 0, 0))
    px = im.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in '. ':
                continue
            if ch not in pal:
                if strict:
                    raise KeyError(f'palette has no {ch!r} (row {y}: {row!r})')
                continue
            px[x, y] = pal[ch]
    return im


def paste(dst: Image.Image, src: Image.Image, x: int, y: int) -> None:
    dst.alpha_composite(src, (x, y))


def silhouette(im: Image.Image, colour: RGBA = (255, 255, 255, 255)) -> Image.Image:
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    sp, dp = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            if sp[x, y][3] > 0:
                dp[x, y] = colour
    return out


def outline(im: Image.Image, colour: RGBA, diagonal: bool = False) -> Image.Image:
    """Add a 1px outline around opaque pixels (drawn beneath the sprite)."""
    w, h = im.size
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    sp, dp = im.load(), out.load()
    offs = [(1, 0), (-1, 0), (0, 1), (0, -1)]
    if diagonal:
        offs += [(1, 1), (1, -1), (-1, 1), (-1, -1)]
    for y in range(h):
        for x in range(w):
            if sp[x, y][3] == 0:
                for dx, dy in offs:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and sp[nx, ny][3] > 0:
                        dp[x, y] = colour
                        break
    out.alpha_composite(im)
    return out


def zoom(im: Image.Image, z: int, bg: RGBA | None = None, gridline: RGBA | None = None) -> Image.Image:
    big = im.resize((im.width * z, im.height * z), Image.NEAREST)
    if bg is not None:
        b = Image.new('RGBA', big.size, bg)
        b.alpha_composite(big)
        big = b
    if gridline is not None:
        from PIL import ImageDraw
        d = ImageDraw.Draw(big)
        for x in range(0, big.width, z):
            d.line([(x, 0), (x, big.height)], fill=gridline)
        for y in range(0, big.height, z):
            d.line([(0, y), (big.width, y)], fill=gridline)
    return big
