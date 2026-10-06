"""Mockup v17 — v16 plus 时值 and 延迟 (the simplified notes tab).

The two radio groups, a little tighter (音名 needs no label: C D E say it), and two rows under them, drawn the way the
score draws them:
- 时值: whole, half, quarter, eighth, sixteenth, and a dot (附点, half as long again). New notes are a quarter;
- 延迟: how long after the step the note sounds. 0 (right away, the default), then the same five values and the dot, in
  the same columns as 时值;
- every row edits the picked note ("click a note at the top, the rows below change that one"); + copies its length and
  delay, so a chord stays one moment. A value that isn't one of these (made in 完整) shows none picked;
- the chips go in the order they sound, a little arrow between moments: "C4 E4 › G4";
- no hint line any more: hovering a value says what it is in a bubble ("二分音符 · 2 拍")."""
from __future__ import annotations

import sys
from pathlib import Path

from ui import P, pill, icon, new
from mockup import SMALL, INK, INK2, WHITE, Z
from mockup2 import small_btn
from mockup4 import popup_bubble
from mockup16 import OX, OY, L, NAMES, WHITES, SHARPS, INK3, radio, toolbar, render, name

VALUES = [('n1', 1920), ('n2', 960), ('n4', 480), ('n8', 240), ('n16', 120)]
COL0 = OX + 28                      # the 0 of 延迟; the values start one column on
COLS = [OX + 43 + i * 17 for i in range(5)]
DOT = OX + 130


def chips(cv, notes, picked, acc):
    """notes: (pitch, delay); shown in the order they sound."""
    y = OY + 17
    x = L + cv.text('这个方块', L, y, INK2, dy=6) // Z + 4
    order = sorted(range(len(notes)), key=lambda i: (notes[i][1], notes[i][0]))
    prev = None
    for i in order:
        m, d = notes[i]
        if prev is not None and d != prev:
            cv.spr(icon('right', INK3), x, y + 2)
            x += 6
        prev = d
        lab = name(m)
        w = SMALL.measure(lab)[0] // Z + 7
        on = i == picked
        cv.spr(pill(w, 11, 'selected' if on else 'normal', acc), x, y)
        cv.text(lab, x, y, WHITE if on else INK, align='center', w=w, dy=6, shadow=(0, 2, acc['O']) if on else None)
        x += w + 2
    small_btn(cv, x + 1, y, 'plus', 11, 11)
    small_btn(cv, OX + 134, y, 'trash', 11, 11)


def groups(cv, m, acc):
    octave, semi = m // 12 - 1, m % 12
    cv.text('八度', L, OY + 30, INK2, dy=2)
    cv.text('低', L + 18, OY + 30, INK3, dy=2)
    cv.text('中', OX + 5 + 5 * 13, OY + 30, INK3, align='center', w=12, dy=2)
    cv.text('高', OX + 139, OY + 30, INK3, dy=2)
    for i, o in enumerate(range(-1, 10)):
        radio(cv, OX + 5 + i * 13, OY + 38, 12, 12, str(o), 'on' if o == octave else 'normal', acc)

    def state(s):
        p = (octave + 1) * 12 + s
        return 'off' if p > 127 else ('on' if s == semi else 'normal')

    for s, after in SHARPS:
        radio(cv, OX + 5 + (after + 1) * 20 - 8, OY + 53, 16, 12, NAMES[s], state(s), acc)
    for i, s in enumerate(WHITES):
        radio(cv, OX + 5 + i * 20, OY + 66, 19, 14, NAMES[s], state(s), acc)


def value_row(cv, label, y, ticks, acc, zero):
    h = 12
    cv.text(label, L, y, INK2, dy=(h - 7) * 2 - 2)
    plain = [v for _, v in VALUES]
    dotted = ticks * 2 % 3 == 0 and ticks * 2 // 3 in plain
    base = ticks * 2 // 3 if dotted else ticks
    if zero:
        on = ticks == 0
        radio(cv, COL0, y, 13, h, '0', 'on' if on else 'normal', acc)
    for (glyph, v), x in zip(VALUES, COLS):
        on = v == base
        cv.spr(pill(15, h, 'selected' if on else 'normal', acc), x, y)
        g = icon(glyph, WHITE if on else INK)
        cv.spr(g, x + (15 - g.width) // 2, y + 1)
    cv.spr(pill(13, h, 'selected' if dotted else 'normal', acc), DOT, y)
    cv.spr(icon('dot', WHITE if dotted else INK), DOT + 5, y + 4)
    if base not in plain:
        cv.spr(new(13, h, P['milk'][:3] + (170,)), DOT, y)


def view(notes, picked, bpm=120, follows=True, length=480, tip=None):
    """tip: (text, column x, row y) of a hover bubble."""
    def draw(cv, acc):
        toolbar(cv, acc, bpm, follows)
        chips(cv, notes, picked, acc)
        groups(cv, notes[picked][0], acc)
        value_row(cv, '时值', OY + 84, length, acc, False)
        value_row(cv, '延迟', OY + 98, notes[picked][1], acc, True)
        if tip:
            text, tx, ty = tip
            w = SMALL.measure(text)[0] // Z + 12
            bx = max(OX + 4, min(tx + 7 - w // 2, OX + 146 - w))
            popup_bubble(cv, bx, ty - 17, w, 14, [(None, text, INK)], tail_x=tx + 4)
    return draw


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    render(outdir / 'v17_quarter.png', 'ocarina', '陶笛', '1 个音 · 速度 120', view([(64, 0)], 0))
    render(outdir / 'v17_chord.png', 'piano', '大钢琴', '3 个音 · 速度 120',
           view([(60, 0), (64, 0), (67, 0)], 0, length=960, tip=('二分音符 · 2 拍', COLS[1], OY + 84)))
    # two eighths in one block: the tile is a beat
    render(outdir / 'v17_delay.png', 'violin', '小提琴', '2 个音 · 速度 90',
           view([(69, 0), (71, 240)], 1, 90, False, length=240, tip=('晚一个八分音符响 · 半拍', COLS[3], OY + 98)))
    render(outdir / 'v17_free.png', 'violin', '竖琴', '3 个音 · 速度 101',
           view([(73, 0), (76, 160), (78, 320)], 1, 101, False, length=437))
    print('done')
