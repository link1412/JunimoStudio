"""Render text with Stardew's own SpriteFonts (unpacked JSON + PNG), mirroring
MonoGame's SpriteFont layout rules so mockups match the game pixel-for-pixel."""
from __future__ import annotations

import json
import os
from functools import lru_cache
from PIL import Image

# the game's fonts, unpacked by StardewXnbHack (SDV_CONTENT: the unpacked Content folder)
CONTENT = os.environ.get('SDV_CONTENT', os.path.expanduser(
    '~/Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS/Content (unpacked)'))
FONT_DIR = os.environ.get('SDV_FONTS', os.path.join(CONTENT, 'Fonts'))


class SpriteFont:
    def __init__(self, name: str):
        with open(os.path.join(FONT_DIR, name + '.json'), encoding='utf-8') as f:
            d = json.load(f)
        self.line = d['LineSpacing']
        self.spacing = d['Spacing']
        self.glyphs = d['Glyphs']
        self.default = d.get('DefaultCharacter') or '*'
        self.tex = Image.open(os.path.join(FONT_DIR, name + '.png')).convert('RGBA')
        self._tint_cache: dict = {}

    def _glyph(self, ch):
        return self.glyphs.get(ch) or self.glyphs[self.default]

    def measure(self, text: str) -> tuple[int, int]:
        w = 0.0
        best = 0.0
        first = True
        lines = 1
        for ch in text:
            if ch == '\n':
                best = max(best, w)
                w, first, lines = 0.0, True, lines + 1
                continue
            g = self._glyph(ch)
            if first:
                w = max(g['LeftSideBearing'], 0)
                first = False
            else:
                w += self.spacing + g['LeftSideBearing']
            w += g['Width'] + g['RightSideBearing']
        return int(round(max(best, w))), lines * self.line

    def _tinted(self, colour):
        if colour not in self._tint_cache:
            r, g, b, a = colour
            src = self.tex
            solid = Image.new('RGBA', src.size, (r, g, b, 255))
            alpha = src.getchannel('A')
            if a != 255:
                alpha = alpha.point(lambda v: v * a // 255)
            solid.putalpha(alpha)
            self._tint_cache[colour] = solid
        return self._tint_cache[colour]

    def draw(self, dst: Image.Image, text: str, x: int, y: int, colour, shadow=None, scale: int = 1):
        if shadow is not None:
            self.draw(dst, text, x + shadow[0], y + shadow[1], shadow[2], None, scale)
        tex = self._tinted(tuple(colour))
        ox, oy = 0.0, 0.0
        first = True
        for ch in text:
            if ch == '\n':
                ox, oy, first = 0.0, oy + self.line, True
                continue
            g = self._glyph(ch)
            if first:
                ox = max(g['LeftSideBearing'], 0)
                first = False
            else:
                ox += self.spacing + g['LeftSideBearing']
            b = g['BoundsInTexture']
            c = g['Cropping']
            if b['Width'] > 0 and b['Height'] > 0:
                piece = tex.crop((b['X'], b['Y'], b['X'] + b['Width'], b['Y'] + b['Height']))
                if scale != 1:
                    piece = piece.resize((piece.width * scale, piece.height * scale), Image.NEAREST)
                dst.alpha_composite(piece, (int(x + (ox + c['X']) * scale), int(y + (oy + c['Y']) * scale)))
            ox += g['Width'] + g['RightSideBearing']


@lru_cache(maxsize=None)
def font(name: str) -> SpriteFont:
    return SpriteFont(name)
