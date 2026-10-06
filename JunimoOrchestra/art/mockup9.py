"""Mockup v9 — MIDI ticks (480 per quarter) as the exact unit, each tile has a
fixed tempo that defaults to the save-wide tempo; every tick value carries a
friendly translation (note glyph when exact + seconds at the tile's tempo)."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import P, new, pill, slider_track, slider_fill, icon, grid, rr_layers, knob, toggle
from px import ramp, hex2rgba as C
from mockup import Canvas, backdrop, FAMILY_ACCENT, PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
from mockup2 import small_btn
import mockup3
from mockup3 import candy, help_btn, TINY
from mockup4 import keyboard_layout, popup_bubble
from mockup7 import range_strip, keyboard, chip

INK3 = P['ink3']
OX, OY = 74, 23
PPQ = 480
PX_PER_Q = 10            # waterfall: one quarter note (480 tick) = 10 art px


def glyph_for(ticks):
    table = {1920: ('n1', False), 960: ('n2', False), 480: ('n4', False), 240: ('n8', False), 120: ('n16', False),
             2880: ('n1', True), 1440: ('n2', True), 720: ('n4', True), 360: ('n8', True), 180: ('n16', True)}
    return table.get(ticks)


def secs(ticks, bpm):
    return ticks / PPQ * 60 / bpm


def waterfall_ticks(cv, notes, sel, lo, acc, y0, h, offscreen=()):
    kx, kw, colx = keyboard_layout(lo)
    x, w = kx + 1, 14 * kw - 1
    bg = new(w, h, P['white'])
    px = bg.load()
    used = {n[0] for n in notes}
    for m, (cx, cw, blk) in sorted(colx.items(), key=lambda kv: kv[1][0]):
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
    half = PX_PER_Q // 2
    for k in range(0, h // half + 1):
        yy = h - 1 - k * half
        if yy < 0:
            continue
        solid = k % 2 == 0
        for xx in range(0, w, 1 if solid else 2):
            px[xx, yy] = P['milk3'] if solid else P['ging2']
    frame = new(w + 2, h + 2)
    rr_layers(frame, 0, 0, w + 2, h + 2, 2, [(0, P['milk4'])])
    frame.alpha_composite(bg, (1, 1))
    cv.spr(frame, x - 1, y0 - 1)
    for k in range(1, h // PX_PER_Q + 1):
        yy = y0 + h - 1 - k * PX_PER_Q
        lab = str(k * PPQ)
        TINY.draw(cv.s, lab, cv.ox + (x + w - 2 - len(lab) * 3) * Z, cv.oy + yy * Z + 2, INK3)
    cv.text('tick', x + w - 10, y0 - 1, INK3, dy=-2)
    for n in notes:
        m, d, l = n[0], n[1], n[2]
        if m not in colx:
            continue
        cx, cw, blk = colx[m]
        top = y0 + h - 1 - round((d + l) / PPQ * PX_PER_Q)
        hgt = max(3, round(l / PPQ * PX_PER_Q) - 1)
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


def tempo_chip(cv, x, y, bpm, follows_global, acc):
    w = 24
    gold = ramp('#f2b640')
    bg = new(w, 11)
    if follows_global:
        rr_layers(bg, 0, 0, w, 11, 3, [(0, P['gold2']), (1, P['gold3'])])
    else:
        rr_layers(bg, 0, 0, w, 11, 3, [(0, acc['O']), (1, acc['h'])])
    cv.spr(bg, x, y)
    col = P['goldD'] if follows_global else acc['O']
    cv.spr(icon('sn4', col), x + 3, y + 2)
    cv.text(f'={bpm}', x + 8, y, col, dy=6)


def tick_field(cv, x, y, label, ticks, bpm, w=36, editing=False, acc=None):
    cv.text(label, x, y, INK2, dy=6)
    fx = x + 12
    bg = new(w, 11)
    if editing:
        rr_layers(bg, 0, 0, w, 11, 3, [(0, acc['O']), (1, acc['h']), (2, P['white'])])
    else:
        rr_layers(bg, 0, 0, w, 11, 3, [(0, P['milk4']), (1, P['white'])])
    cv.spr(bg, fx, y)
    if editing:
        tw = cv.text(str(ticks), fx + 4, y, INK, dy=6)
        cv.spr(new(1, 7, acc['O']), fx + 4 + (tw + 3) // 4, y + 2)
    else:
        for bx, ic in ((fx + 1, 'minus'), (fx + w - 10, 'plus')):
            b = new(9, 9)
            rr_layers(b, 0, 0, 9, 9, 3, [(0, P['rose2']), (1, P['blush'])])
            cv.spr(b, bx, y + 1)
            g = icon(ic, 'berry')
            cv.spr(g, bx + (9 - g.width) // 2, y + 1 + (9 - g.height) // 2)
        cv.text(str(ticks), fx + 10, y, INK, align='center', w=w - 20, dy=6)
    # friendly translation: note glyph if exact, then seconds
    hx = fx + w + 3
    g = glyph_for(ticks)
    if g:
        im = icon('s' + g[0], INK3)
        cv.spr(im, hx, y + 2)
        hx += im.width + 1
        if g[1]:
            cv.spr(icon('dot', INK3), hx, y + 6)
            hx += 3
        hx += 2
    s = secs(ticks, bpm)
    cv.text(f'{s:.2f}'.rstrip('0').rstrip('.') + ' 秒', hx, y, INK3, dy=6)


def more_btn(cv, x, y, acc, active=False):
    cv.spr(pill(20, 11, 'selected' if active else 'normal', acc), x, y)
    cv.text('更多', x, y, WHITE if active else INK2, align='center', w=20, dy=6,
            shadow=(0, 2, acc['O']) if active else None)


NOTES = [(48, 0, 1920, 100), (64, 0, 480, 100), (67, 240, 240, 90), (72, 480, 480, 100),
         (76, 720, 360, 112), (79, 960, 960, 100), (74, 1450, 173, 84)]


def tab_notes(cv, acc, bpm=120, follows=True, state='plain'):
    sel = NOTES[4]
    range_strip(cv, OX + 6, OY + 3, [n[0] for n in NOTES], 60, acc)
    tempo_chip(cv, OX + 110, OY + 3, bpm, follows, acc)
    help_btn(cv, OX + 137, OY + 3)
    waterfall_ticks(cv, NOTES, sel, 60, acc, OY + 17, 46, offscreen=[('left', 1)])
    keyboard(cv, NOTES, sel, 60, acc, OY + 64, 24, 15)
    x = OX + 5
    y1, y2 = OY + 90, OY + 102
    chip(cv, x, y1, 'E5', acc)
    tick_field(cv, x + 23, y1, '延迟', 720, bpm, editing=(state == 'typing'), acc=acc)
    tick_field(cv, x + 23, y2, '时值', 360, bpm, acc=acc)
    more_btn(cv, OX + 113, y2, acc)
    small_btn(cv, OX + 135, y2, 'trash', 11, 11)
    if state == 'tempo':
        bx, by, bw, bh = OX + 60, OY + 16, 86, 34
        popup_bubble(cv, bx, by, bw, bh, [], tail_x=OX + 118, tail_up=True)
        cv.text('这个方块的速度', bx + 5, by + 3, INK2, dy=0)
        gold = ramp('#f2b640')
        # option 1: follow global
        cv.spr(toggle(not follows), bx + 5, by + 12)
        cv.text('自定义', bx + 26, by + 10, INK, dy=4)
        cv.spr(button_small(9), bx + 50, by + 12)
        cv.spr(icon('minus', 'berry'), bx + 52, by + 16)
        cv.spr(icon('sn4', INK), bx + 61, by + 13)
        cv.text(f'={bpm}', bx + 66, by + 11, INK, dy=4)
        cv.spr(button_small(9), bx + 76, by + 12)
        cv.spr(icon('plus', 'berry'), bx + 78, by + 14)
        cv.text('关掉就跟随全局默认速度 120', bx + 5, by + 23, INK3, dy=0)
    if state == 'typing':
        popup_bubble(cv, OX + 28, OY + 72, 92, 16, [(None, '输入 tick（四分音符 = 480）', INK2)], tail_x=OX + 44, tail_up=False)


def button_small(w):
    b = new(w, 9)
    rr_layers(b, 0, 0, w, 9, 3, [(0, P['rose2']), (1, P['blush'])])
    return b


def render(state, out, bpm=120, follows=True):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT['bells'])
    acc['hex'] = FAMILY_ACCENT['bells']
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.draw_shell(cv, 'bells', acc, PROGRAMS_ZH['bells'][2], f'7 个音 · 速度 {bpm}', 1)
    tab_notes(cv, acc, bpm, follows, state)
    scr.convert('RGB').save(out)


def render_global(out):
    import mockup5
    src = open(mockup5.__file__, encoding='utf-8').read()
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT['bells'])
    acc['hex'] = FAMILY_ACCENT['bells']
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.draw_shell(cv, 'bells', acc, PROGRAMS_ZH['bells'][2], '7 个音 · 速度 120', 4)
    mockup5.tab_global(cv, acc)
    L = OX + 6
    # relabel the tempo row: it is the default every tile follows
    cv.spr(new(68, 12, P['milk']), L - 1, OY + 20)
    cv.text('默认速度', L, OY + 21, INK2, dy=2)
    small_btn(cv, L + 30, OY + 22, 'minus', 9, 10)
    cv.spr(icon('sn4', INK), L + 41, OY + 23)
    cv.text('=120', L + 46, OY + 21, INK, dy=2)
    small_btn(cv, L + 60, OY + 22, 'plus', 9, 10)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    render('plain', outdir / 'v9_plain.png')
    render('typing', outdir / 'v9_typing.png')
    render('tempo', outdir / 'v9_tempo.png', bpm=96, follows=False)
    render_global(outdir / 'v9_global.png')
    print('done')
