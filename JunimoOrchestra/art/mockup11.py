"""Mockup v11 — 效果 tab with exact 0–127 values (sliders + typed numbers) and triggers."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import P, new, pill, slider_track, slider_fill, icon, rr_layers, toggle, knob
from px import ramp
from mockup import Canvas, backdrop, FAMILY_ACCENT, PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
import mockup3
from mockup4 import popup_bubble

INK3 = P['ink3']
OX, OY = 74, 23


def value_box(cv, x, y, text, w=24, editing=False, acc=None):
    b = new(w, 11)
    if editing:
        rr_layers(b, 0, 0, w, 11, 3, [(0, acc['O']), (1, acc['h']), (2, P['white'])])
    else:
        rr_layers(b, 0, 0, w, 11, 3, [(0, P['milk4']), (1, P['white'])])
    cv.spr(b, x, y)
    tw = cv.text(text, x, y, INK, align='center', w=w, dy=6)
    if editing:
        cv.spr(new(1, 7, acc['O']), x + (w * 4 + tw) // 8 + 1, y + 2)


def fx_row(cv, y, label, val, acc, center=False, text=None, editing=False):
    x = OX + 6
    cv.text(label, x, y, INK2, dy=6)
    tx, tw = x + 22, 84
    cv.spr(slider_track(tw), tx, y + 2)
    pos = tx + round(tw * val / 127)
    if center:
        mid = tx + tw // 2
        cv.spr(new(1, 9, P['ink3']), mid, y + 1)
        lo, hi = sorted([mid, pos])
        if hi - lo > 2:
            cv.spr(slider_fill(hi - lo + 3, acc), lo - 1, y + 2)
    else:
        cv.spr(slider_fill(max(6, pos - tx), acc), tx, y + 2)
    cv.spr(knob(acc), pos - 3, y + 2)
    value_box(cv, tx + tw + 4, y, text if text is not None else str(val), w=26, editing=editing, acc=acc)


def tab_fx(cv, acc, editing=False):
    cv.text('声音效果', OX + 6, OY + 3, INK2, dy=2)
    y = OY + 14
    fx_row(cv, y, '混响', 40, acc, editing=editing)
    fx_row(cv, y + 14, '合唱', 0, acc)
    fx_row(cv, y + 28, '颤音', 0, acc)
    fx_row(cv, y + 42, '声像', 80, acc, center=True, text='右 16')
    cv.text('左', OX + 28, y + 51, INK3, dy=0)
    cv.text('右', OX + 108, y + 51, INK3, dy=0)
    tip = new(138, 11)
    rr_layers(tip, 0, 0, 138, 11, 3, [(0, P['gold2']), (1, P['gold3'])])
    cv.spr(tip, OX + 6, OY + 73)
    cv.spr(icon('globe', 'goldD', 'white'), OX + 9, OY + 75)
    cv.text('混响的“空间”在全局里设定，当前：大厅', OX + 20, OY + 73, P['goldD'], dy=5)
    cv.spr(new(138, 1, P['milk3']), OX + 6, OY + 88)
    cv.text('触发方式', OX + 6, OY + 90, INK2, dy=2)
    for i, (on, ic, t) in enumerate([(True, 'person', '玩家路过'), (False, 'chicken', '村民和小动物路过')]):
        x = OX + 6 + i * 62
        yy = OY + 101
        cv.spr(toggle(on), x, yy)
        cv.spr(icon(ic, 'ink2'), x + 22, yy + 1)
        cv.text(t, x + 31, yy - 2, INK, dy=4)
    if editing:
        popup_bubble(cv, OX + 70, OY + 26, 76, 16, [(None, '输入 0–127，Enter 确认', INK2)], tail_x=OX + 129, tail_up=True)


def render(out, editing=False):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT['violin'])
    acc['hex'] = FAMILY_ACCENT['violin']
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.draw_shell(cv, 'violin', acc, '小提琴', '3 个音 · 速度 120', 2)
    tab_fx(cv, acc, editing)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    render(outdir / 'v11_fx.png')
    render(outdir / 'v11_fx_typing.png', editing=True)
    print('done')
