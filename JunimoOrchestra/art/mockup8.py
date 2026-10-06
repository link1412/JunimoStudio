"""Mockup v8 — outside only: range strip (octave), waterfall, 时值, 延迟.
All times are exact milliseconds; no beats, no grid choice, no dotted/triplet shortcuts.
Velocity lives in the per-note 更多 popover."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import P, new, pill, slider_track, slider_fill, icon, grid, rr_layers, knob
from px import ramp
from mockup import Canvas, backdrop, FAMILY_ACCENT, PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
from mockup2 import small_btn
import mockup3
from mockup3 import candy, help_btn, TINY
from mockup4 import keyboard_layout, popup_bubble
from mockup7 import range_strip, keyboard, chip
from px import hex2rgba as C

INK3 = P['ink3']
OX, OY = 74, 23
PX_PER_S = 20            # waterfall scale: 1 second = 20 art px


def waterfall_ms(cv, notes, sel, lo, acc, y0, h, offscreen=()):
    kx, kw, colx = keyboard_layout(lo)
    x, w = kx + 1, 14 * kw - 1
    bg = new(w, h, P['white'])
    px = bg.load()
    order = sorted(colx.items(), key=lambda kv: kv[1][0])
    used = {n[0] for n in notes}
    for m, (cx, cw, blk) in order:
        tint = C('#f6ecf2') if blk else None
        if m in used:
            tint = C('#f7e3ec') if blk else C('#fff0f5')
        if tint:
            for yy in range(h):
                for xx in range(cx - x, cx - x + cw):
                    if 0 <= xx < w:
                        px[xx, yy] = tint
        if not blk:
            xx = cx - x - 1
            if 0 <= xx < w:
                for yy in range(0, h, 2):
                    px[xx, yy] = P['milk2']
    # time ruler: dotted every 0.25 s, solid every 0.5 s
    q = PX_PER_S // 4
    for k in range(0, h // q + 1):
        yy = h - 1 - k * q
        if yy < 0:
            continue
        solid = k % 2 == 0
        for xx in range(0, w, 1 if solid else 2):
            px[xx, yy] = P['milk3'] if solid else P['ging2']
    frame = new(w + 2, h + 2)
    rr_layers(frame, 0, 0, w + 2, h + 2, 2, [(0, P['milk4'])])
    frame.alpha_composite(bg, (1, 1))
    cv.spr(frame, x - 1, y0 - 1)
    for k in range(2, h // q + 1, 2):
        yy = y0 + h - 1 - k * q
        sec = k * 0.25
        lab = f'{sec:g}'
        TINY.draw(cv.s, lab, cv.ox + (x + w - 3 - len(lab) * 2) * Z, cv.oy + yy * Z + 2, INK3)
    cv.text('秒', x + w - 7, y0 - 1, INK3, dy=0)
    for n in notes:
        m, d, l = n[0], n[1], n[2]
        if m not in colx:
            continue
        cx, cw, blk = colx[m]
        top = y0 + h - 1 - round((d + l) / 1000 * PX_PER_S)
        hgt = max(3, round(l / 1000 * PX_PER_S) - 1)
        if top < y0:
            hgt -= y0 - top
            top = y0
        is_sel = n == sel
        cv.spr(candy(cw - (0 if blk else 2), hgt, acc, selected=is_sel),
               cx + (0 if blk else 1) - (1 if is_sel else 0), top - (1 if is_sel else 0))
    for side, count in offscreen:
        ind = new(10, 9)
        rr_layers(ind, 0, 0, 10, 9, 3, [(0, acc['O']), (1, acc['h'])])
        xx = kx + 2 if side == 'left' else kx + 14 * kw - 12
        cv.spr(ind, xx, y0 + 2)
        cv.spr(icon(side, acc['O']), xx + 2, y0 + 3)
        TINY.draw(cv.s, str(count), cv.ox + (xx + 6) * Z, cv.oy + (y0 + 2) * Z - 2, acc['O'])


def ms_field(cv, x, y, label, value, w=62, editing=False, acc=None):
    cv.text(label, x, y, INK2, dy=6)
    fx = x + 12
    bg = new(w, 11)
    if editing:
        rr_layers(bg, 0, 0, w, 11, 3, [(0, acc['O']), (1, acc['h']), (2, P['white'])])
    else:
        rr_layers(bg, 0, 0, w, 11, 3, [(0, P['milk4']), (1, P['white'])])
    cv.spr(bg, fx, y)
    if not editing:
        for bx, ic in ((fx + 1, 'minus'), (fx + w - 10, 'plus')):
            b = new(9, 9)
            rr_layers(b, 0, 0, 9, 9, 3, [(0, P['rose2']), (1, P['blush'])])
            cv.spr(b, bx, y + 1)
            g = icon(ic, 'berry')
            cv.spr(g, bx + (9 - g.width) // 2, y + 1 + (9 - g.height) // 2)
        cv.text(f'{value} 毫秒', fx + 10, y, INK, align='center', w=w - 20, dy=6)
    else:
        tw = cv.text(value, fx + 5, y, INK, dy=6)
        cv.spr(new(1, 7, acc['O']), fx + 5 + (tw + 3) // 4, y + 2)
        cv.text('毫秒', fx + w - 14, y, INK2, dy=6)


def more_btn(cv, x, y, active=False, acc=None):
    w = 20
    cv.spr(pill(w, 11, 'selected' if active else 'normal', acc), x, y)
    cv.text('更多', x, y, WHITE if active else INK2, align='center', w=w, dy=6,
            shadow=(0, 2, acc['O']) if active else None)


NOTES = [(48, 0, 2000, 100), (64, 0, 500, 100), (67, 250, 250, 90), (72, 500, 500, 100),
         (76, 750, 375, 112), (79, 1000, 1000, 100), (74, 1510, 180, 84)]


def tab_notes(cv, acc, state='plain'):
    sel = NOTES[4]
    range_strip(cv, OX + 6, OY + 3, [n[0] for n in NOTES], 60, acc)
    help_btn(cv, OX + 137, OY + 3)
    waterfall_ms(cv, NOTES, sel, 60, acc, OY + 17, 46, offscreen=[('left', 1)])
    keyboard(cv, NOTES, sel, 60, acc, OY + 64, 24, 15)
    x = OX + 5
    y1, y2 = OY + 90, OY + 102
    chip(cv, x, y1, 'E5', acc)
    ms_field(cv, x + 24, y1, '延迟', '750' if state != 'typing' else '750', editing=(state == 'typing'), acc=acc)
    ms_field(cv, x + 24, y2, '时值', '375', acc=acc)
    more_btn(cv, OX + 111, y1, active=(state == 'more'), acc=acc)
    small_btn(cv, OX + 134, y1, 'trash', 11, 11)
    if state == 'more':
        bx, by, bw, bh = OX + 62, OY + 60, 84, 27
        popup_bubble(cv, bx, by, bw, bh, [], tail_x=OX + 117, tail_up=False)
        cv.text('这个音的更多设置', bx + 5, by + 3, INK2, dy=0)
        cv.text('力度', bx + 5, by + 12, INK, dy=4)
        cv.spr(slider_track(38), bx + 20, by + 14)
        fw = round(38 * 112 / 127)
        cv.spr(slider_fill(fw, acc), bx + 20, by + 14)
        cv.spr(knob(acc), bx + 20 + fw - 5, by + 14)
        f = new(17, 11)
        rr_layers(f, 0, 0, 17, 11, 2, [(0, P['milk4']), (1, P['white'])])
        cv.spr(f, bx + 62, by + 12)
        cv.text('112', bx + 62, by + 12, INK, align='center', w=17, dy=6)
    if state == 'typing':
        popup_bubble(cv, OX + 29, OY + 70, 76, 16, [(None, '输入毫秒数，Enter 确认', INK2)], tail_x=OX + 45, tail_up=False)


def render(state, out):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT['bells'])
    acc['hex'] = FAMILY_ACCENT['bells']
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.draw_shell(cv, 'bells', acc, PROGRAMS_ZH['bells'][2], '7 个音 · 2 秒', 1)
    tab_notes(cv, acc, state)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    for s in ['plain', 'more', 'typing']:
        render(s, outdir / f'v8_{s}.png')
    print('done')
