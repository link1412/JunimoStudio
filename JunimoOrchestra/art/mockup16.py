"""Mockup v16 — two radio groups (the simplified notes tab, chosen in Global > 我的偏好).

One view for every instrument, all 128 pitches:
- 八度: -1 to 9 in one row, the same number as in C4 (中 marks 4, middle C's octave); 音名: C to B, the naturals in a row and the sharps above and between them. Picking either one
  moves the note there and plays it. In octave 9 the names above G9 are off (MIDI ends at 127);
- the row of chips above is the block's notes; the radio groups edit the one that's picked. + adds a note (a copy of the
  picked one, same moment), the bin takes the picked one away (the last one can't go). A block with one note is just
  one chip, so it reads as the two radio groups;
- pitch only: a note keeps its delay, length and velocity, so blocks made in 完整 or the showcase edit fine too."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import P, new, pill, icon, rr_layers, button
from px import ramp
from mockup import SMALL, Canvas, backdrop, FAMILY_ACCENT, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
from mockup2 import small_btn
import mockup3
from mockup3 import help_btn
from mockup9 import tempo_chip

INK3 = P['ink3']
OX, OY = 74, 23
L = OX + 6
NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B']
WHITES = [0, 2, 4, 5, 7, 9, 11]
SHARPS = [(1, 0), (3, 1), (6, 3), (8, 4), (10, 5)]      # semitone, the natural it sits after


def name(m):
    return f'{NAMES[m % 12]}{m // 12 - 1}'


def toolbar(cv, acc, bpm, follows):
    tempo_chip(cv, L, OY + 3, bpm, follows, acc)
    gold = ramp('#f2b640')
    cv.spr(pill(32, 11, 'normal', gold), OX + 102, OY + 3)
    cv.spr(icon('globe', 'goldD', 'gold3'), OX + 105, OY + 5)
    cv.text('完整', OX + 115, OY + 3, P['goldD'], dy=6)
    help_btn(cv, OX + 137, OY + 3)


def radio(cv, x, y, w, h, label, state, acc):
    """state: on / normal / off (MIDI has no such pitch)."""
    if state == 'on':
        cv.spr(pill(w, h, 'selected', acc), x, y)
    else:
        cv.spr(pill(w, h, 'normal', acc), x, y)
    col = WHITE if state == 'on' else (INK3 if state == 'off' else INK)
    cv.text(label, x, y, col, align='center', w=w, dy=(h - 7) * 2 - 2, shadow=(0, 2, acc['O']) if state == 'on' else None)
    if state == 'off':
        cv.spr(new(w, h, P['milk'][:3] + (170,)), x, y)


def chips(cv, notes, picked, acc):
    y = OY + 17
    x = L + cv.text('这个方块', L, y, INK2, dy=6) // Z + 4
    for i, m in enumerate(notes):
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
    # 八度
    cv.text('八度', L, OY + 32, INK2, dy=2)
    cv.text('低', L + 18, OY + 32, INK3, dy=2)
    cv.text('中', OX + 5 + 5 * 13, OY + 32, INK3, align='center', w=12, dy=2)     # over 4: middle C's octave
    cv.text('高', OX + 139, OY + 32, INK3, dy=2)
    for i, o in enumerate(range(-1, 10)):
        radio(cv, OX + 5 + i * 13, OY + 42, 12, 13, str(o), 'on' if o == octave else 'normal', acc)
    # 音名
    cv.text('音名', L, OY + 59, INK2, dy=2)

    def state(s):
        p = (octave + 1) * 12 + s
        return 'off' if p > 127 else ('on' if s == semi else 'normal')

    for s, after in SHARPS:
        radio(cv, OX + 5 + (after + 1) * 20 - 8, OY + 68, 16, 13, NAMES[s], state(s), acc)
    for i, s in enumerate(WHITES):
        radio(cv, OX + 5 + i * 20, OY + 83, 19, 17, NAMES[s], state(s), acc)


def hint(cv, text):
    cv.text(text, L, OY + 103, INK3, dy=4)


def render(out, fam, prog, summary, draw):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT[fam])
    acc['hex'] = FAMILY_ACCENT[fam]
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.draw_shell(cv, fam, acc, prog, summary, 1)
    draw(cv, acc)
    scr.convert('RGB').save(out)


def view(notes, picked, bpm=120, follows=True, tip='点一下就换过去，并且试听'):
    def draw(cv, acc):
        toolbar(cv, acc, bpm, follows)
        chips(cv, notes, picked, acc)
        groups(cv, notes[picked], acc)
        hint(cv, tip)
    return draw


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    render(outdir / 'v16_single.png', 'ocarina', '陶笛', '1 个音 · 速度 120', view([64], 0))
    render(outdir / 'v16_chord.png', 'piano', '大钢琴', '3 个音 · 速度 120', view([60, 64, 67], 2))
    render(outdir / 'v16_showcase.png', 'violin', '竖琴', '3 个音 · 速度 101',
           view([73, 76, 78], 1, 101, False, '只改音高：什么时候响、响多久都不变'))
    render(outdir / 'v16_top.png', 'songbird', '鸟鸣', '1 个音 · 速度 120', view([127], 0))
    print('done')
