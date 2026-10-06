"""Mockup v5 — the thin version: a tile is a tiny score. Cells play left to
right; each cell = single note / chord / rest + a note value (时值)."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import (P, new, card, button, pill, slider_track, slider_fill, white_key, black_key,
                icon, grid, rr_layers, toggle, knob, hline, vline, put)
from px import ramp
from mockup import Canvas, backdrop, FAMILY_ACCENT, PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
from mockup2 import small_btn
import mockup3
from mockup3 import TINY
from mockup4 import DRUM_SHORT

INK3 = P['ink3']
OX, OY = 74, 23
NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B']


def nn(m):
    return f'{NAMES[m % 12]}{m // 12 - 1}'


def cell_card(cv, x, y, cell, acc, selected=False, playing=False):
    w, h = 15, 22
    im = new(w, h + 1)
    if selected:
        rr_layers(im, 0, 0, w, h, 3, [(0, acc['O']), (1, acc['h']), (2, P['white'])])
    else:
        rr_layers(im, 0, 1, w, h, 3, [(0, P['ging3'])])
        rr_layers(im, 0, 0, w, h, 3, [(0, P['milk4']), (1, P['white'])])
    cv.spr(im, x, y)
    notes, val, dotted, trip = cell
    g = icon('rest4' if not notes else val, acc['O'] if selected else 'ink')
    gx = x + (w - g.width) // 2 - (1 if dotted else 0)
    cv.spr(g, gx, y + 2)
    if dotted:
        cv.spr(icon('dot', acc['O'] if selected else 'ink'), gx + g.width + 1, y + 8)
    if trip:
        cv.spr(icon('three', acc['O']), x + 2, y + 2)
    # pitch bars: one row per note, sorted high -> low
    for i, m in enumerate(sorted(notes, reverse=True)[:4]):
        bar = new(w - 6, 1, acc['L'] if not selected else acc['O'])
        cv.spr(bar, x + 3, y + 13 + i * 2)


def phrase_strip(cv, cells, sel, acc):
    x0, y0 = OX + 5, OY + 4
    for i, c in enumerate(cells):
        cell_card(cv, x0 + i * 16, y0, c, acc, selected=(i == sel))
    gx = x0 + len(cells) * 16
    ghost = new(15, 22)
    ui.stitch_rr(ghost, 0, 0, 15, 22, 3, P['milk4'], 1, 1)
    cv.spr(ghost, gx, y0)
    cv.spr(icon('plus', P['milk4']), gx + 5, y0 + 9)
    # tiny play order arrow under the strip
    hline_im = new(len(cells) * 16 - 1, 1, P['milk3'])
    cv.spr(hline_im, x0, y0 + 24)
    cv.spr(icon('arrow_right', P['milk4']), x0 + len(cells) * 16 - 4, y0 + 22)


def value_picker(cv, cell, acc):
    y = OY + 30
    cv.text('时值', OX + 6, y, INK2, dy=4)
    _, val, dotted, trip = cell
    names = [('n1', '全'), ('n2', '二分'), ('n4', '四分'), ('n8', '八分'), ('n16', '十六分')]
    for i, (v, _) in enumerate(names):
        x = OX + 22 + i * 15
        sel = v == val
        cv.spr(button(14, 14, 'pink' if sel else 'milk'), x, y)
        g = icon(v, 'white' if sel else 'ink')
        cv.spr(g, x + (14 - g.width) // 2, y + 2)
    for j, (lab, on) in enumerate((('附点', dotted), ('三连音', trip))):
        x = OX + 99 + j * 23
        w = 21
        st = 'selected' if on else 'normal'
        cv.spr(pill(w, 12, st, acc), x, y + 1)
        cv.text(lab, x, y + 1, WHITE if on else INK, align='center', w=w, dy=8,
                shadow=(0, 2, acc['O']) if on else None)


def cell_row(cv, cell, acc, label_fn=nn, chord_name=''):
    y = OY + 47
    cv.text('这一格', OX + 6, y, INK2, dy=4)
    x = OX + 30
    for m in sorted(cell[0]):
        w = 17
        ch = new(w, 10)
        rr_layers(ch, 0, 0, w, 10, 3, [(0, acc['O']), (1, acc['h'])])
        cv.spr(ch, x, y + 1)
        cv.text(label_fn(m), x, y + 1, acc['O'], align='center', w=w, dy=5)
        x += w + 2
    if chord_name:
        cv.text(chord_name, x + 1, y, INK2, dy=4)
    # rest + delete
    cv.spr(pill(20, 12, 'normal', acc), OX + 110, y)
    cv.spr(icon('rest4', 'ink'), OX + 113, y + 1)
    cv.text('休止', OX + 117, y, INK, dy=8)
    small_btn(cv, OX + 134, y, 'trash', 11, 12)


def keys(cv, sel_notes, lo, acc, drums=False):
    kx, kw = OX + 5, 10
    ky, kh = OY + 61, 38
    whites = [0, 2, 4, 5, 7, 9, 11]
    pos = {}
    for oc in range(2):
        for i, semi in enumerate(whites):
            m = lo + oc * 12 + semi
            x = kx + (oc * 7 + i) * kw
            st = 'selected' if m in sel_notes else 'normal'
            cv.spr(white_key(kw + 1, kh, st, acc), x, ky)
            pos[m] = x
            if drums:
                lab = DRUM_SHORT.get(m, '')
                col = acc['O'] if st == 'selected' else INK2
                if len(lab) == 2 and not lab[1].isdigit():
                    cv.text(lab[0], x, ky + 24, col, align='center', w=kw + 1, dy=-3)
                    cv.text(lab[1], x, ky + 24, col, align='center', w=kw + 1, dy=16)
                else:
                    cv.text(lab, x, ky + 26, col, align='center', w=kw + 1, dy=2)
            elif semi == 0 and m not in sel_notes:
                cv.text(f'C{m // 12 - 1}', x, ky + kh - 12, INK3, align='center', w=kw + 1, dy=0)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            m = lo + oc * 12 + semi
            bx = pos[m - 1] + kw - 3
            st = 'selected' if m in sel_notes else 'normal'
            cv.spr(black_key(7, 23, st, acc), bx, ky)
            pos[m] = bx
            if drums:
                cv.text(DRUM_SHORT.get(m, ''), bx, ky + 13, WHITE, align='center', w=7, dy=2)
    if not drums:
        for m in sel_notes:
            if m in pos:
                cv.spr(ui.heart_badge(), pos[m] + (2 if m % 12 not in (1, 3, 6, 8, 10) else 0), ky + (27 if m % 12 not in (1, 3, 6, 8, 10) else 15))


def hint_row(cv, acc, range_text):
    y = OY + 103
    cv.spr(icon('mouse_l', 'ink2', 'white', acc['L']), OX + 7, y)
    cv.text('换成这个音', OX + 16, y - 2, INK2, dy=4)
    cv.spr(icon('mouse_r', 'ink2', 'white', acc['L']), OX + 58, y)
    cv.text('叠加 / 移除', OX + 67, y - 2, INK2, dy=4)
    small_btn(cv, OX + 104, y - 1, 'left', 9, 10)
    cv.text(range_text, OX + 113, y - 2, INK, align='center', w=22, dy=4)
    small_btn(cv, OX + 136, y - 1, 'right', 9, 10)


# ---------------------------------------------------------------- scenes
MELODY = [([60, 64, 67], 'n4', False, False), ([76], 'n8', False, False), ([74], 'n8', False, False),
          ([72, 76], 'n4', True, False), ([], 'n8', False, False), ([67], 'n8', False, True),
          ([69], 'n8', False, True), ([71], 'n8', False, True)]
DRUMS = [([36, 42], 'n8', False, False), ([42], 'n8', False, False), ([38, 42], 'n8', False, False),
         ([42], 'n8', False, False), ([36, 42], 'n8', False, False), ([36, 42], 'n8', False, False),
         ([38, 42], 'n8', False, False), ([46], 'n8', False, False)]


def tab_notes(cv, acc):
    sel = 3
    phrase_strip(cv, MELODY, sel, acc)
    value_picker(cv, MELODY[sel], acc)
    cell_row(cv, MELODY[sel], acc, chord_name='大三度')
    keys(cv, set(MELODY[sel][0]), 60, acc)
    hint_row(cv, acc, 'C4–B5')


def tab_drums(cv, acc):
    sel = 2
    phrase_strip(cv, DRUMS, sel, acc)
    value_picker(cv, DRUMS[sel], acc)
    names = {36: '底鼓', 38: '军鼓', 42: '闭镲', 46: '开镲'}
    cell_row(cv, DRUMS[sel], acc, label_fn=lambda m: names.get(m, str(m)))
    keys(cv, set(DRUMS[sel][0]), 36, acc, drums=True)
    hint_row(cv, acc, 'C2–B3')


def tab_global(cv, acc):
    ox, oy = OX, OY
    hdr = new(70, 12)
    rr_layers(hdr, 0, 0, 70, 12, 3, [(0, P['gold2']), (1, P['gold3'])])
    cv.spr(hdr, ox + 4, oy + 4)
    cv.spr(icon('globe', 'goldD', 'white'), ox + 7, oy + 6)
    cv.text('本存档 · 所有方块', ox + 17, oy + 4, P['goldD'], dy=6)
    hdr2 = new(68, 12)
    rr_layers(hdr2, 0, 0, 68, 12, 3, [(0, P['mint2']), (1, P['mint3'])])
    cv.spr(hdr2, ox + 78, oy + 4)
    cv.spr(icon('person', 'mintD'), ox + 82, oy + 6)
    cv.text('我的偏好 · 所有存档', ox + 90, oy + 4, P['mintD'], dy=6)
    cv.spr(new(1, 92, P['milk3']), ox + 75, oy + 18)

    L = ox + 6
    cv.text('速度', L, oy + 21, INK2, dy=2)
    small_btn(cv, L + 18, oy + 22, 'minus', 10, 10)
    cv.spr(icon('n4', 'ink'), L + 31, oy + 22)
    cv.text('=120', L + 36, oy + 21, INK, dy=2)
    small_btn(cv, L + 55, oy + 22, 'plus', 10, 10)
    cv.text('空间', L, oy + 37, INK2, dy=2)
    gold = ramp('#f2b640')
    for i, r in enumerate(['干声', '小屋', '房间', '大厅', '教堂', '山洞']):
        c, rr = i % 3, i // 3
        x, y = L + c * 22, oy + 48 + rr * 13
        st = 'selected' if r == '大厅' else 'normal'
        cv.spr(pill(20, 11, st, gold), x, y)
        cv.text(r, x, y, WHITE if st == 'selected' else INK, align='center', w=20, dy=8,
                shadow=(0, 2, gold['O']) if st == 'selected' else None)
    cv.text('移调', L, oy + 79, INK2, dy=2)
    small_btn(cv, L + 18, oy + 80, 'minus', 10, 10)
    cv.text('0 半音', L + 31, oy + 79, INK, dy=2)
    small_btn(cv, L + 55, oy + 80, 'plus', 10, 10)

    R = ox + 80
    cv.text('主音量', R, oy + 21, INK2, dy=2)
    cv.spr(slider_track(34), R + 26, oy + 23)
    mint = ramp('#62bf96')
    cv.spr(slider_fill(30, mint), R + 26, oy + 23)
    cv.spr(knob(mint), R + 26 + 25, oy + 23)
    for i, (on, t) in enumerate([(True, '远近有声音大小'), (True, '头顶显示音名'), (True, '飘出小音符')]):
        y = oy + 38 + i * 14
        cv.spr(toggle(on), R, y)
        cv.text(t, R + 22, y - 2, INK, dy=4)
    cv.text('这些也能在 GMCM 里改', R - 1, oy + 83, INK3, dy=4)


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
    summary = '8 格 · 14 下' if kind == 'drums' else '8 格 · 11 个音'
    mockup2.draw_shell(cv, fam, acc, prog, summary, 4 if kind == 'global' else 1)
    {'notes': tab_notes, 'drums': tab_drums, 'global': tab_global}[kind](cv, acc)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    for k in ['notes', 'drums', 'global']:
        render(k, outdir / f'v5_{k}.png')
    print('done')
