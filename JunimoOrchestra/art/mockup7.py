"""Mockup v7 — simple mode for everyone + 高级 mode for full MIDI.
Simple: rows are fixed eighth notes ("格"), delay = N 格, 力度 弱/中/强, one-click range strip.
Advanced: 每格 choice, 附点/三连音, exact ticks (480 per quarter), velocity 1-127."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import (P, new, card, button, pill, slider_track, slider_fill, white_key, black_key, icon, grid,
                rr_layers, toggle, knob, hline)
from px import ramp
from mockup import Canvas, backdrop, FAMILY_ACCENT, PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
from mockup2 import small_btn
import mockup3
from mockup3 import draw_grid, candy, help_btn, TINY
from mockup4 import keyboard_layout, popup_bubble
from mockup6 import glyph_btn, UNIT_ICON

INK3 = P['ink3']
OX, OY = 74, 23


# ---------------------------------------------------------------- pieces
def range_strip(cv, x, y, notes, lo, acc):
    """88-key strip; click anywhere to jump. Bracket = visible 2 octaves, ticks = notes in the tile."""
    cv.text('音域', x, y - 1, INK2, dy=2)
    sx = x + 16
    strip = new(88, 6)
    for i in range(88):
        m = 21 + i
        blk = (m % 12) in (1, 3, 6, 8, 10)
        for yy in range(6):
            strip.putpixel((i, yy), P['keyB'] if (blk and yy < 4) else P['keyW'])
        if m % 12 == 0:
            strip.putpixel((i, 5), P['ink3'])
    fr = new(90, 8)
    rr_layers(fr, 0, 0, 90, 8, 1, [(0, P['keyO'])])
    fr.alpha_composite(strip, (1, 1))
    cv.spr(fr, sx, y + 1)
    wx = sx + 1 + (lo - 21)
    br = new(26, 10)
    rr_layers(br, 0, 0, 26, 10, 1, [(0, acc['O'])])
    rr_layers(br, 1, 1, 24, 8, 0, [(0, (0, 0, 0, 0))])
    cv.spr(br, wx - 1, y)
    for m in notes:
        cv.spr(new(1, 2, acc['O']), sx + 1 + (m - 21), y - 2)
    return sx


def adv_toggle(cv, x, y, on, acc):
    w = 20
    st = 'selected' if on else 'normal'
    cv.spr(pill(w, 11, st, ramp('#f2b640') if on else acc), x, y)
    cv.text('高级', x, y, WHITE if on else INK2, align='center', w=w, dy=6,
            shadow=(0, 2, P['goldD']) if on else None)


def toolbar(cv, notes, lo, acc, advanced):
    range_strip(cv, OX + 6, OY + 3, notes, lo, acc)
    adv_toggle(cv, OX + 113, OY + 3, advanced, acc)
    help_btn(cv, OX + 137, OY + 3)


def waterfall(cv, notes, sel, lo, acc, rows, rp=5, top=17, offscreen=(), unit_label=None):
    kx, kw, colx = keyboard_layout(lo)
    wy, wh = OY + top, rows * rp + 1
    order = sorted(colx.items(), key=lambda kv: kv[1][0])
    lanes = [(v[0], v[1], 'black' if v[2] else 'white') for _, v in order]
    lit = {i for i, (m, _) in enumerate(order) if m in {n[0] for n in notes}}
    draw_grid(cv, kx + 1, wy, 14 * kw - 1, wh, rows // 2, rp * 2, lanes, lit, numbers=False)
    for r in range(1, rows + 1):
        yy = wy + wh - 1 - r * rp
        TINY.draw(cv.s, str(r), cv.ox + (kx + 14 * kw - 5) * Z, cv.oy + yy * Z + 2, INK3)
    for n in notes:
        m, d, l = n[0], n[1], n[2]
        if m not in colx:
            continue
        cx, cw, blk = colx[m]
        tp = wy + wh - 1 - round((d + l) * rp)
        hgt = max(3, round(l * rp) - 1)
        if tp < wy:
            hgt -= (wy - tp)
            tp = wy
        is_sel = n == sel
        cv.spr(candy(cw - (0 if blk else 2), hgt, acc, selected=is_sel),
               cx + (0 if blk else 1) - (1 if is_sel else 0), tp - (1 if is_sel else 0))
    for side, count in offscreen:
        ind = new(10, 9)
        rr_layers(ind, 0, 0, 10, 9, 3, [(0, acc['O']), (1, acc['h'])])
        x = kx + 2 if side == 'left' else kx + 14 * kw - 12
        cv.spr(ind, x, wy + 2)
        cv.spr(icon(side, acc['O']), x + 2, wy + 3)
        TINY.draw(cv.s, str(count), cv.ox + (x + 6) * Z, cv.oy + (wy + 2) * Z - 2, acc['O'])
    return wy + wh


def keyboard(cv, notes, sel, lo, acc, ky, kh, bh):
    kx, kw, colx = keyboard_layout(lo)
    has = {n[0] for n in notes}
    whites = [0, 2, 4, 5, 7, 9, 11]
    for oc in range(2):
        for i, semi in enumerate(whites):
            m = lo + oc * 12 + semi
            x = kx + (oc * 7 + i) * kw
            st = 'selected' if m == sel[0] else ('hover' if m in has else 'normal')
            cv.spr(white_key(kw + 1, kh, st, acc), x, ky)
            if semi == 0 and m != sel[0]:
                cv.text(f'C{m // 12 - 1}', x, ky + kh - 12, INK3, align='center', w=kw + 1, dy=0)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            m = lo + oc * 12 + semi
            cv.spr(black_key(7, bh, 'selected' if m == sel[0] else 'normal', acc), colx[m - 1][0] + kw - 4, ky)
    cv.spr(ui.heart_badge(), colx[sel[0]][0] + 1, ky + kh - 9)


def chip(cv, x, y, text, acc, w=20):
    ch = new(w, 11)
    rr_layers(ch, 0, 0, w, 11, 3, [(0, acc['O']), (1, acc['L'])])
    cv.spr(ch, x, y)
    cv.text(text, x, y, WHITE, align='center', w=w, dy=6, shadow=(0, 2, acc['O']))


def stepper(cv, x, y, text, w):
    bg = new(w, 11)
    rr_layers(bg, 0, 0, w, 11, 3, [(0, P['milk4']), (1, P['white'])])
    cv.spr(bg, x, y)
    for bx, ic in ((x + 1, 'minus'), (x + w - 10, 'plus')):
        b = new(9, 9)
        rr_layers(b, 0, 0, 9, 9, 3, [(0, P['rose2']), (1, P['blush'])])
        cv.spr(b, bx, y + 1)
        g = icon(ic, 'berry')
        cv.spr(g, bx + (9 - g.width) // 2, y + 1 + (9 - g.height) // 2)
    cv.text(text, x + 10, y, INK, align='center', w=w - 20, dy=6)


def vel3(cv, x, y, vel, acc):
    cv.text('力度', x, y, INK2, dy=6)
    for i, lab in enumerate(['弱', '中', '强']):
        sel = lab == vel
        cv.spr(pill(9, 11, 'selected' if sel else 'normal', acc), x + 11 + i * 10, y)
        cv.text(lab, x + 11 + i * 10, y, WHITE if sel else INK, align='center', w=9, dy=6,
                shadow=(0, 2, acc['O']) if sel else None)


def tick_field(cv, x, y, val, w=26, gold=True):
    bx = new(w, 11)
    edge = P['gold2'] if gold else P['milk4']
    rr_layers(bx, 0, 0, w, 11, 2, [(0, edge), (1, P['white'])])
    cv.spr(bx, x, y)
    cv.text(val, x, y, INK, align='center', w=w, dy=6)


def value_glyphs(cv, x, y, value, h=13):
    for i, v in enumerate(['n1', 'n2', 'n4', 'n8', 'n16']):
        glyph_btn(cv, x + i * 13, y, v, v == value, h=h)


# ---------------------------------------------------------------- scenes
# grid unit = eighth note in these examples: (pitch, delay 格, length 格, vel)
MELODY = [(48, 0, 8, '中'), (64, 0, 2, '中'), (67, 1, 1, '弱'), (72, 2, 2, '中'), (76, 3, 2, '强'), (79, 4, 4, '中'), (74, 6, 1, '弱')]


def tab_simple(cv, acc):
    sel = MELODY[4]
    toolbar(cv, [n[0] for n in MELODY], 60, acc, False)
    waterfall(cv, MELODY, sel, 60, acc, rows=8, offscreen=[('left', 1)])
    keyboard(cv, MELODY, sel, 60, acc, OY + 58, 26, 16)
    y1, y2 = OY + 86, OY + 99
    x = OX + 5
    chip(cv, x, y1, 'E5', acc)
    stepper(cv, x + 23, y1, '延迟 3 格', 50)
    vel3(cv, x + 77, y1, '强', acc)
    small_btn(cv, OX + 134, y1, 'trash', 11, 11)
    cv.text('时值', x, y2, INK2, dy=10)
    value_glyphs(cv, x + 16, y2, 'n4')
    cv.text('四分音符 · 长 2 格', x + 84, y2, INK3, dy=10)


def tab_advanced(cv, acc):
    gold = ramp('#f2b640')
    sel = (76, 3, 3, 101)
    notes = [(48, 0, 8, 64), (64, 0, 2, 83), (67, 1, 1, 77), (72, 2, 2, 91), sel, (79, 4, 4, 97), (74, 6, 1, 72)]
    toolbar(cv, [n[0] for n in notes], 60, acc, True)
    end = waterfall(cv, notes, sel, 60, acc, rows=6, offscreen=[('left', 1)])
    keyboard(cv, notes, sel, 60, acc, OY + 49, 24, 15)
    x = OX + 5
    y1, y2, y3 = OY + 75, OY + 88, OY + 101
    # row 1: note + delay (格 + tick)
    chip(cv, x, y1, 'E5', acc)
    stepper(cv, x + 23, y1, '延迟 3 格', 50)
    tick_field(cv, x + 76, y1, '720')
    cv.text('tick', x + 104, y1, P['gold2'], dy=6)
    small_btn(cv, OX + 134, y1, 'trash', 11, 11)
    # row 2: value + dotted/triplet + exact length
    cv.text('时值', x, y2, INK2, dy=6)
    for i, v in enumerate(['sn1', 'sn2', 'sn4', 'sn8', 'sn16']):
        glyph_btn(cv, x + 16 + i * 12, y2, v, v == 'sn4', w=11, h=11)
    for j, (lab, on) in enumerate((('附点', True), ('三连', False))):
        xx = x + 78 + j * 17
        cv.spr(pill(16, 11, 'selected' if on else 'normal', gold), xx, y2)
        cv.text(lab, xx, y2, WHITE if on else INK, align='center', w=16, dy=6,
                shadow=(0, 2, gold['O']) if on else None)
    tick_field(cv, x + 113, y2, '720', w=22)
    # row 3: exact velocity + grid unit
    cv.text('力度', x, y3, INK2, dy=6)
    cv.spr(slider_track(44), x + 17, y3 + 2)
    fw = round(44 * 101 / 127)
    cv.spr(slider_fill(fw, acc), x + 17, y3 + 2)
    cv.spr(knob(acc), x + 17 + fw - 5, y3 + 2)
    tick_field(cv, x + 64, y3, '101', w=17)
    cv.text('每格', x + 85, y3, P['gold2'], dy=6)
    for i, u in enumerate(['sn4', 'sn8', 'sn16', 't8']):
        glyph_btn(cv, x + 100 + i * 11, y3, 'sn8' if u == 't8' else u, u == 'sn8', w=10, h=11, trip=(u == 't8'))


def tab_drums(cv, acc):
    drums = [(42, i, 0.6, '强' if i % 2 == 0 else '弱') for i in range(7)] + [(46, 7, 1, '中')] + \
            [(36, 0, 0.6, '强'), (36, 4, 0.6, '强'), (36, 5, 0.6, '中'), (38, 2, 0.6, '强'), (38, 6, 0.6, '强'), (49, 0, 4, '中')]
    sel = (38, 6, 0.6, '强')
    toolbar(cv, [n[0] for n in drums], 36, acc, False)
    waterfall(cv, drums, sel, 36, acc, rows=8)
    keyboard(cv, drums, sel, 36, acc, OY + 58, 26, 16)
    y1, y2 = OY + 86, OY + 99
    x = OX + 5
    chip(cv, x, y1, 'D2 军鼓', acc, w=28)
    stepper(cv, x + 31, y1, '延迟 6 格', 48)
    vel3(cv, x + 82, y1, '强', acc)
    small_btn(cv, OX + 134, y1, 'trash', 11, 11)
    cv.text('时值', x, y2, INK2, dy=10)
    value_glyphs(cv, x + 16, y2, 'n8')
    cv.text('八分音符 · 长 1 格', x + 84, y2, INK3, dy=10)


def render(kind, out):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    fam = 'drumkit' if kind == 'drums' else 'bells'
    acc = ramp(FAMILY_ACCENT[fam])
    acc['hex'] = FAMILY_ACCENT[fam]
    prog = 'TR-808 鼓组' if fam == 'drumkit' else PROGRAMS_ZH['bells'][2]
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.draw_shell(cv, fam, acc, prog, '14 下鼓点' if kind == 'drums' else '7 个音', 1)
    {'simple': tab_simple, 'advanced': tab_advanced, 'drums': tab_drums}[kind](cv, acc)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    for k in ['simple', 'advanced', 'drums']:
        render(k, outdir / f'v7_{k}.png')
    print('done')
