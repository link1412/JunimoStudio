"""Mockup v6 — waterfall + per-note delay, expressed only in note values:
grid unit is a note glyph, delay = N grid units, duration = note value, velocity = 弱/中/强."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import (P, new, card, button, pill, white_key, black_key, icon, grid, rr_layers, hline, vline, put)
from px import ramp
from mockup import Canvas, backdrop, FAMILY_ACCENT, PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
from mockup2 import small_btn
import mockup3
from mockup3 import draw_grid, candy, help_btn, TINY
from mockup4 import DRUM_SHORT, keyboard_layout

INK3 = P['ink3']
OX, OY = 74, 23
NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B']
UNIT_ICON = {'n4': 'n4', 'n8': 'n8', 'n16': 'n16', 't8': 'n8'}


def nn(m):
    return f'{NAMES[m % 12]}{m // 12 - 1}'


def glyph_btn(cv, x, y, g, sel, w=12, h=12, trip=False):
    cv.spr(button(w, h, 'pink' if sel else 'milk'), x, y)
    im = icon(g, 'white' if sel else 'ink')
    gx = x + (w - im.width) // 2 + (1 if trip else 0)
    cv.spr(im, gx, y + 1 + max(0, (h - 3 - im.height) // 2))
    if trip:
        cv.spr(icon('three', 'white' if sel else 'ink2'), x + 1, y + 1)


def toolbar(cv, unit, rng):
    ox, oy = OX, OY
    cv.text('网格', ox + 6, oy + 3, INK2, dy=2)
    for i, u in enumerate(['n4', 'n8', 'n16', 't8']):
        glyph_btn(cv, ox + 22 + i * 13, oy + 2, UNIT_ICON[u], u == unit, trip=(u == 't8'))
    cv.text('八度', ox + 78, oy + 3, INK2, dy=2)
    small_btn(cv, ox + 94, oy + 3, 'left', 9, 10)
    cv.text(rng, ox + 103, oy + 3, INK, align='center', w=22, dy=2)
    small_btn(cv, ox + 125, oy + 3, 'right', 9, 10)
    help_btn(cv, ox + 137, oy + 3)


def waterfall(cv, notes, sel, lo, acc, rows=8, rp=5, unit='n8', offscreen=()):
    kx, kw, colx = keyboard_layout(lo)
    wy, wh = OY + 17, rows * rp + 1
    order = sorted(colx.items(), key=lambda kv: kv[1][0])
    lanes = [(v[0], v[1], 'black' if v[2] else 'white') for _, v in order]
    lit = {i for i, (m, _) in enumerate(order) if m in {n[0] for n in notes}}
    # rows: draw_grid draws beat lines every ppb/2; use rows as 'beats' with ppb = 2*rp for single lines
    draw_grid(cv, kx + 1, wy, 14 * kw - 1, wh, rows // 2, rp * 2, lanes, lit, numbers=False)
    # replace beat numbers with row counters (1..8) at the left in tiny font
    for r in range(1, rows + 1):
        yy = wy + wh - 1 - r * rp
        TINY.draw(cv.s, str(r), cv.ox + (kx + 14 * kw - 5) * Z, cv.oy + yy * Z + 2, INK3)
    for n in notes:
        m, d, l = n[0], n[1], n[2]
        if m not in colx:
            continue
        cx, cw, blk = colx[m]
        top = wy + wh - 1 - round((d + l) * rp)
        hgt = max(3, round(l * rp) - 1)
        is_sel = n == sel
        cv.spr(candy(cw - (0 if blk else 2), hgt, acc, selected=is_sel),
               cx + (0 if blk else 1) - (1 if is_sel else 0), top - (1 if is_sel else 0))
    for side, count in offscreen:
        ind = new(10, 9)
        rr_layers(ind, 0, 0, 10, 9, 3, [(0, acc['O']), (1, acc['h'])])
        x = kx + 2 if side == 'left' else kx + 14 * kw - 12
        cv.spr(ind, x, wy + 2)
        cv.spr(icon(side, acc['O']), x + 2, wy + 3)
        TINY.draw(cv.s, str(count), cv.ox + (x + 6) * Z, cv.oy + (wy + 2) * Z - 2, acc['O'])


def keyboard(cv, notes, sel, lo, acc, drums=False):
    kx, kw, colx = keyboard_layout(lo)
    ky, kh = OY + 58, 26
    has = {n[0] for n in notes}
    whites = [0, 2, 4, 5, 7, 9, 11]
    for oc in range(2):
        for i, semi in enumerate(whites):
            m = lo + oc * 12 + semi
            x = kx + (oc * 7 + i) * kw
            st = 'selected' if m == sel[0] else ('hover' if m in has else 'normal')
            cv.spr(white_key(kw + 1, kh, st, acc), x, ky)
            if drums:
                lab = DRUM_SHORT.get(m, '')
                col = acc['O'] if st == 'selected' else INK2
                if len(lab) == 2 and not lab[1].isdigit():
                    cv.text(lab[0], x, ky + 16, col, align='center', w=kw + 1, dy=-5)
                    cv.text(lab[1], x, ky + 16, col, align='center', w=kw + 1, dy=12)
                else:
                    cv.text(lab, x, ky + 17, col, align='center', w=kw + 1, dy=0)
            elif semi == 0 and m != sel[0]:
                cv.text(f'C{m // 12 - 1}', x, ky + kh - 12, INK3, align='center', w=kw + 1, dy=0)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            m = lo + oc * 12 + semi
            bx = colx[m - 1][0] + kw - 4
            st = 'selected' if m == sel[0] else 'normal'
            cv.spr(black_key(7, 16, st, acc), bx, ky)
            if drums:
                cv.text(DRUM_SHORT.get(m, ''), bx, ky + 6, WHITE, align='center', w=7, dy=2)
    if not drums:
        cv.spr(ui.heart_badge(), colx[sel[0]][0] + 1, ky + 18)


def inspector(cv, acc, chip, delay_n, unit, value, dotted, trip, vel, cw=20, dw=54):
    x0, y1, y2 = OX + 5, OY + 86, OY + 99
    ch = new(cw, 11)
    rr_layers(ch, 0, 0, cw, 11, 3, [(0, acc['O']), (1, acc['L'])])
    cv.spr(ch, x0, y1)
    cv.text(chip, x0, y1, WHITE, align='center', w=cw, dy=6, shadow=(0, 2, acc['O']))
    # delay stepper: "延迟 3 个♪"
    x = x0 + cw + 3
    w = dw
    bg = new(w, 11)
    rr_layers(bg, 0, 0, w, 11, 3, [(0, P['milk4']), (1, P['white'])])
    cv.spr(bg, x, y1)
    for bx, ic in ((x + 1, 'minus'), (x + w - 10, 'plus')):
        b = new(9, 9)
        rr_layers(b, 0, 0, 9, 9, 3, [(0, P['rose2']), (1, P['blush'])])
        cv.spr(b, bx, y1 + 1)
        g = icon(ic, 'berry')
        cv.spr(g, bx + (9 - g.width) // 2, y1 + 1 + (9 - g.height) // 2)
    txt = '无延迟' if delay_n == 0 else f'延迟 {delay_n} 个'
    tw = cv.text(txt, x + 11, y1, INK, dy=6)
    if delay_n:
        g = icon(UNIT_ICON[unit], 'ink')
        cv.spr(g, x + 11 + (tw + 6) // 4, y1 + 1)
    # velocity: 弱 中 强
    x += w + 4
    cv.text('力度', x, y1, INK2, dy=6)
    x += 11
    for i, lab in enumerate(['弱', '中', '强']):
        sel = lab == vel
        pw = 9
        cv.spr(pill(pw, 11, 'selected' if sel else 'normal', acc), x + i * (pw + 1), y1)
        cv.text(lab, x + i * (pw + 1), y1, WHITE if sel else INK, align='center', w=pw, dy=6,
                shadow=(0, 2, acc['O']) if sel else None)
    small_btn(cv, OX + 150 - 16, y1, 'trash', 11, 11)
    # row 2: note value
    cv.text('时值', x0, y2, INK2, dy=10)
    for i, v in enumerate(['n1', 'n2', 'n4', 'n8', 'n16']):
        glyph_btn(cv, x0 + 16 + i * 13, y2, v, v == value, h=13)
    for j, (lab, on) in enumerate((('附点', dotted), ('三连音', trip))):
        xx = x0 + 84 + j * 28
        pw = 26
        cv.spr(pill(pw, 12, 'selected' if on else 'normal', acc), xx, y2 + 1)
        cv.text(lab, xx, y2 + 1, WHITE if on else INK, align='center', w=pw, dy=8,
                shadow=(0, 2, acc['O']) if on else None)


# (pitch, delay units, length units, vel)  — grid unit = eighth note
MELODY = [(48, 0, 8, '中'), (64, 0, 2, '中'), (67, 1, 1, '弱'), (72, 2, 2, '中'), (76, 3, 3, '强'), (79, 4, 4, '中'), (74, 6, 1, '弱')]
DRUMS = [(42, i, 0.6, '强' if i % 2 == 0 else '弱') for i in range(7)] + [(46, 7, 1, '中')] + \
        [(36, 0, 0.6, '强'), (36, 4, 0.6, '强'), (36, 5, 0.6, '中'), (38, 2, 0.6, '强'), (38, 6, 0.6, '强'), (49, 0, 4, '中')]


def tab_notes(cv, acc):
    sel = MELODY[4]
    toolbar(cv, 'n8', 'C4–B5')
    waterfall(cv, MELODY, sel, 60, acc, offscreen=[('left', 1)])
    keyboard(cv, MELODY, sel, 60, acc)
    inspector(cv, acc, 'E5', 3, 'n8', 'n4', True, False, '强')


def tab_drums(cv, acc):
    sel = (38, 6, 0.6, '强')
    toolbar(cv, 'n8', 'C2–B3')
    waterfall(cv, DRUMS, sel, 36, acc)
    keyboard(cv, DRUMS, sel, 36, acc, drums=True)
    inspector(cv, acc, '军鼓', 6, 'n8', 'n8', False, False, '强')


def help_tip(cv):
    from mockup4 import popup_bubble
    popup_bubble(cv, OX + 50, OY + 16, 96, 56, [
        ('mouse_l', '点琴键：把选中的音换成这个音高', None),
        ('mouse_r', '右键琴键：同一时刻再叠一个音', None),
        ('mouse_l', '点瀑布空白：在那一格加一个音', None),
        ('mouse_l', '拖糖果：上下改延迟，左右改音高', None),
        ('mouse_r', '右键糖果：删掉这个音', None),
        (None, '时值用下方的音符按钮来选', INK3)], tail_x=OX + 139, tail_up=True)


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
    summary = '14 下鼓点' if kind == 'drums' else '7 个音'
    mockup2.draw_shell(cv, fam, acc, prog, summary, 1)
    if kind == 'drums':
        tab_drums(cv, acc)
    else:
        tab_notes(cv, acc)
        if kind == 'help':
            help_tip(cv)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    for k in ['notes', 'drums', 'help']:
        render(k, outdir / f'v6_{k}.png')
    print('done')


# ---------------------------------------------------------------- v6b: drums use the plain piano UI
def inspector_named(cv, acc, chip, sub, delay_n, unit, value, dotted, trip, vel):
    """Same editor row; the chip just reads "D2 军鼓"."""
    inspector(cv, acc, f"{chip} {sub}", delay_n, unit, value, dotted, trip, vel, cw=28, dw=50)


def key_tooltip(cv, x, y, text):
    from mockup4 import popup_bubble
    w = 42
    popup_bubble(cv, x - w // 2, y, w, 12, [(None, text, INK)], tail_x=x - 3, tail_up=False)


def render_drums_plain(out):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT['drumkit'])
    acc['hex'] = FAMILY_ACCENT['drumkit']
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.draw_shell(cv, 'drumkit', acc, 'TR-808 鼓组', '14 下鼓点', 1)
    sel = (38, 6, 0.6, '强')
    toolbar(cv, 'n8', 'C2–B3')
    waterfall(cv, DRUMS, sel, 36, acc)
    keyboard(cv, DRUMS, sel, 36, acc, drums=False)   # identical to the piano
    inspector_named(cv, acc, 'D2', '军鼓', 6, 'n8', 'n8', False, False, '强')
    # hovering the F#2 black key shows its drum name
    from mockup4 import keyboard_layout as kl
    kx, kw, colx = kl(36)
    fx = colx[42][0] + 2
    key_tooltip(cv, fx + 1, OY + 44, 'F#2 · 闭合踩镲')
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    render_drums_plain(Path('preview') / 'v6b_drums_plain.png')
