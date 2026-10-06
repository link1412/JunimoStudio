"""Mockup v4 — precise timing (1/960 beat, velocity 1-127), snap is only an
editing aid ("自由" allowed), 时值 naming, drum kit keeps the keyboard, single-player."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import (P, new, card, button, pill, slider_track, slider_fill, white_key, black_key,
                icon, grid, rr_layers, toggle, knob, hline, vline, put)
from px import ramp, hex2rgba as C
from mockup import (Canvas, backdrop, SMALL, BIG, FAMILY_ACCENT, PROGRAMS_ZH, INK, INK2, WHITE,
                    SCR_W, SCR_H, PW, PH, Z)
import mockup2
from mockup2 import small_btn, fx_slider
import mockup3
from mockup3 import draw_grid, candy, minimap, stepper, help_btn, TINY, nn

INK3 = P['ink3']
OX, OY = 74, 23

# GM percussion names: 2-char for white keys, 1-char for black keys (full name in tooltip / chip)
DRUM_SHORT = {36: '底鼓', 37: '边', 38: '军鼓', 39: '拍', 40: '电军', 41: '嗵1', 42: '闭', 43: '嗵2', 44: '脚',
              45: '嗵3', 46: '开', 47: '嗵4', 48: '嗵5', 49: '吊', 50: '嗵6', 51: '叮', 52: '中镲', 53: '叮铃',
              54: '铃', 55: '水镲', 56: '牛', 57: '吊2', 58: '颤', 59: '叮2'}
DRUM_FULL = {36: '底鼓', 38: '军鼓', 42: '闭合踩镲', 46: '开放踩镲', 49: '吊镲', 39: '拍手'}


def toolbar(cv, snap='1/2 拍'):
    ox, oy = OX, OY
    cv.text('对齐', ox + 6, oy + 3, INK2, dy=2)
    dd = new(36, 11)
    rr_layers(dd, 0, 0, 36, 11, 3, [(0, P['milk4']), (1, P['white'])])
    cv.spr(dd, ox + 22, oy + 3)
    cv.text(snap, ox + 22, oy + 3, INK, align='center', w=30, dy=6)
    cv.spr(icon('down', 'ink2'), ox + 50, oy + 7)
    cv.text('缩放', ox + 64, oy + 3, INK2, dy=2)
    small_btn(cv, ox + 80, oy + 4, 'minus', 9, 10)
    small_btn(cv, ox + 91, oy + 4, 'plus', 9, 10)
    cv.spr(icon('n4', P['gold2']), ox + 106, oy + 4)
    cv.text('=120', ox + 111, oy + 3, P['gold2'], dy=2)
    help_btn(cv, ox + 136, oy + 4)


def keyboard_layout(lo):
    kx, kw = OX + 5, 10
    whites = [0, 2, 4, 5, 7, 9, 11]
    colx = {}
    for oc in range(2):
        for i, semi in enumerate(whites):
            colx[lo + oc * 12 + semi] = (kx + (oc * 7 + i) * kw + 1, kw - 1, False)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            m = lo + oc * 12 + semi
            colx[m] = (colx[m - 1][0] + kw - 3, 5, True)
    return kx, kw, colx


def waterfall(cv, notes, sel, lo, acc, beats=4, ppb=7):
    kx, kw, colx = keyboard_layout(lo)
    wy, wh = OY + 27, 30
    order = sorted(colx.items(), key=lambda kv: kv[1][0])
    lanes = [(v[0], v[1], 'black' if v[2] else 'white') for _, v in order]
    lit = {i for i, (m, _) in enumerate(order) if m in {n[0] for n in notes}}
    draw_grid(cv, kx + 1, wy, 14 * kw - 1, wh, beats, ppb, lanes, lit)
    for n in notes:
        m, d, l = n[0], n[1], n[2]
        if m not in colx:
            continue
        cx, cw, blk = colx[m]
        top = wy + wh - round((d + l) * ppb)
        hgt = max(3, round(l * ppb) - 1)
        is_sel = n == sel
        cv.spr(candy(cw - (0 if blk else 2), hgt, acc, selected=is_sel),
               cx + (0 if blk else 1) - (1 if is_sel else 0), top - (1 if is_sel else 0))
    return kx, kw, colx


def keyboard(cv, notes, sel, lo, acc, drums=False):
    kx, kw, colx = keyboard_layout(lo)
    ky, kh = OY + 58, 28
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
                    cv.text(lab[0], x, ky + 17, col, align='center', w=kw + 1, dy=-3)
                    cv.text(lab[1], x, ky + 17, col, align='center', w=kw + 1, dy=16)
                else:
                    cv.text(lab, x, ky + 18, col, align='center', w=kw + 1, dy=4)
            elif semi == 0 and m != sel[0]:
                cv.text(f'C{m // 12 - 1}', x, ky + kh - 12, INK3, align='center', w=kw + 1, dy=0)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            m = lo + oc * 12 + semi
            bx = colx[m - 1][0] + kw - 4
            st = 'selected' if m == sel[0] else 'normal'
            cv.spr(black_key(7, 17, st, acc), bx, ky)
            if drums:
                cv.text(DRUM_SHORT.get(m, ''), bx, ky + 7, P['white'] if st != 'selected' else WHITE, align='center', w=7, dy=2)
    if not drums:
        cv.spr(ui.heart_badge(), colx[sel[0]][0] + 1, ky + 19)


def vel_slider(cv, x, y, v, acc, w=58):
    cv.text('力度', x, y, INK2, dy=6)
    tx = x + 17
    cv.spr(slider_track(w), tx, y + 2)
    fw = max(6, round(w * v / 127))
    cv.spr(slider_fill(fw, acc), tx, y + 2)
    cv.spr(knob(acc), tx + fw - 5, y + 2)
    cv.text(str(v), tx + w + 3, y, INK, dy=6)


def inspector(cv, acc, chip, delay, dur, vel, ms, editing=None):
    ox, y1, y2 = OX + 5, OY + 88, OY + 101
    cw = 19
    ch = new(cw, 11)
    rr_layers(ch, 0, 0, cw, 11, 3, [(0, acc['O']), (1, acc['L'])])
    cv.spr(ch, ox, y1)
    cv.text(chip, ox, y1, WHITE, align='center', w=cw, dy=6, shadow=(0, 2, acc['O']))
    x = ox + cw + 3
    for label, val, w in (('延迟', delay, 50), ('时值', dur, 58)):
        if editing == label:
            box = new(w, 11)
            rr_layers(box, 0, 0, w, 11, 3, [(0, acc['O']), (1, P['white'])])
            rr_layers(box, 1, 1, w - 2, 9, 2, [(0, acc['h'])])
            rr_layers(box, 2, 2, w - 4, 7, 1, [(0, P['white'])])
            cv.spr(box, x, y1)
            cv.text(label, x + 3, y1, INK2, dy=6)
            tw = cv.text(val, x + 14, y1, INK, dy=6)
            caret = new(1, 7, acc['O'])
            cv.spr(caret, x + 14 + (tw + 3) // 4, y1 + 2)
            cv.text('拍', x + w - 9, y1, INK2, dy=6)
        else:
            stepper(cv, x, y1, f'{label} {val}', w=w)
        x += w + 2
    vel_slider(cv, ox, y2, vel, acc)
    cv.text(ms, ox + 88, y2, INK3, dy=6)
    small_btn(cv, OX + 150 - 16, y2, 'trash', 11, 11)


def popup_bubble(cv, x, y, w, h, lines, tail_x=None, tail_up=False):
    b = new(w, h)
    rr_layers(b, 0, 0, w, h, 4, [(0, P['berry']), (1, P['white'])])
    rr_layers(b, 2, 2, w - 4, h - 4, 3, [(0, P['blush'])])
    rr_layers(b, 3, 3, w - 6, h - 6, 2, [(0, P['milk'])])
    cv.spr(b, x, y)
    if tail_x is not None:
        if tail_up:
            t = grid(["...O...", "..OWO..", ".OWWWO.", "OWWWWWO"], {'O': P['berry'], 'W': P['white']})
            cv.spr(t, tail_x, y - 3)
        else:
            t = grid(["OWWWWWO", ".OWWWO.", "..OWO..", "...O..."], {'O': P['berry'], 'W': P['white']})
            cv.spr(t, tail_x, y + h - 1)
    for i, (ic, text, col) in enumerate(lines):
        yy = y + 4 + i * 8
        if ic:
            cv.spr(icon(ic, 'ink2'), x + 6, yy + 1)
        cv.text(text, x + (14 if ic else 6), yy - 1, col or INK, dy=0)


def snap_menu(cv, acc):
    x, y, w = OX + 22, OY + 14, 36
    items = ['自由', '1 拍', '1/2 拍', '1/3 拍', '1/4 拍', '1/6 拍', '1/8 拍']
    h = len(items) * 9 + 4
    b = new(w, h)
    rr_layers(b, 0, 0, w, h, 3, [(0, P['berry']), (1, P['white'])])
    cv.spr(b, x, y)
    for i, it in enumerate(items):
        yy = y + 2 + i * 9
        sel = it == '自由'
        if sel:
            hl = new(w - 4, 9)
            rr_layers(hl, 0, 0, w - 4, 9, 2, [(0, acc['L'])])
            cv.spr(hl, x + 2, yy)
        cv.text(it, x + 2, yy, WHITE if sel else INK, align='center', w=w - 4, dy=4,
                shadow=(0, 2, acc['O']) if sel else None)


# ---------------------------------------------------------------- tabs
MELODY = [(48, 0, 4, 70), (64, 0.5, 1, 88), (67, 1, 1, 90), (72, 1.5, 1, 96), (76, 2, 0.5, 112), (79, 2, 2, 100), (72, 3, 1, 84)]
# an "imported MIDI" flavoured phrase: off-grid onsets, odd lengths, triplets
MIDI_ISH = [(48, 0, 3.9, 64), (64, 0.03, 0.47, 83), (67, 0.36, 0.44, 77), (72, 0.69, 0.5, 91), (76, 1.02, 0.47, 101),
            (79, 1.52, 1.21, 97), (74, 2.81, 0.33, 72), (72, 3.14, 0.8, 88)]


def tab_notes(cv, acc, notes, sel, snap, ms, delay_txt, dur_txt, editing=None, lo=60):
    toolbar(cv, snap)
    minimap(cv, OX + 6, OY + 17, [n[0] for n in notes], lo, lo + 23, acc)
    waterfall(cv, notes, sel, lo, acc)
    keyboard(cv, notes, sel, lo, acc)
    inspector(cv, acc, nn(sel[0]), delay_txt, dur_txt, sel[3], ms, editing)


DRUM_BEAT = [(42, b * 0.5, 0.25, 80 if b % 2 else 104) for b in range(7)] + [(46, 3.5, 0.5, 96)] + \
            [(36, 0, 0.5, 120), (36, 2, 0.5, 118), (36, 2.5, 0.25, 90), (38, 1, 0.5, 112), (38, 3, 0.5, 116), (49, 0, 1, 100)]


def tab_drums(cv, acc):
    lo = 36
    toolbar(cv, '1/2 拍')
    minimap(cv, OX + 6, OY + 17, [n[0] for n in DRUM_BEAT], lo, lo + 23, acc)
    sel = (38, 3, 0.5, 116)
    waterfall(cv, DRUM_BEAT, sel, lo, acc)
    keyboard(cv, DRUM_BEAT, sel, lo, acc, drums=True)
    inspector(cv, acc, '军鼓', '3 拍', '1/2 拍', sel[3], '原声军鼓 · D2 · 38')


def tab_global(cv, acc):
    mockup2.tab_global(cv, acc)
    ox, oy = OX, OY
    # single-player wording
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
    cv.spr(new(66, 14, P['milk']), ox + 5, oy + 96)
    R = ox + 80
    cv.spr(new(68, 24, P['milk']), R - 2, oy + 86)
    cv.spr(toggle(True), R, oy + 86)
    cv.text('编辑时开节拍器', R + 22, oy + 84, INK, dy=4)
    cv.text('这些也能在 GMCM 里改', R - 1, oy + 99, INK3, dy=4)


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
    summary = {'drums': '4 拍 · 14 下', 'global': '4 拍 · 7 个音', 'midi': '4 拍 · 8 个音'}.get(kind, '4 拍 · 7 个音')
    mockup2.draw_shell(cv, fam, acc, prog, summary, 4 if kind == 'global' else 1)
    if kind == 'notes':
        sel = MELODY[4]
        tab_notes(cv, acc, MELODY, sel, '1/2 拍', '约 0.25 秒', '2 拍', '1/2 拍')
    elif kind == 'precise':
        sel = MIDI_ISH[4]
        tab_notes(cv, acc, MIDI_ISH, sel, '自由', '约 0.235 秒', '1.02 拍', '0.47', editing='时值')
        popup_bubble(cv, OX + 60, OY + 58, 88, 27,
                     [(None, '输入拍数，下面几种都可以：', INK2), (None, '0.47    3/8    1+1/3', INK), (None, 'Enter 确认 · Esc 取消', INK3)],
                     tail_x=OX + 108, tail_up=False)
    elif kind == 'snap':
        sel = MIDI_ISH[4]
        tab_notes(cv, acc, MIDI_ISH, sel, '自由', '约 0.235 秒', '1.02 拍', '0.47 拍')
        snap_menu(cv, acc)
    elif kind == 'drums':
        tab_drums(cv, acc)
    elif kind == 'global':
        tab_global(cv, acc)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    for k in ['notes', 'precise', 'snap', 'drums', 'global']:
        render(k, outdir / f'v4_{k}.png')
    print('done')
