"""Mockup v3 — note waterfall (per-note pitch / delay / length / velocity),
full-range minimap, 4 tabs (relay moved to backlog)."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import (P, new, card, tab, button, pill, slider_track, slider_fill, heart_knob, white_key, black_key,
                icon, stage, ribbon, panel, grid, rr_layers, keycap, toggle, knob, fill_rr, hline, vline, put)
from px import ramp, hex2rgba as C
import blocks
from sfont import font
from mockup import (Canvas, backdrop, junimo, tab_instrument, SMALL, BIG, FAMILY_ACCENT, FAMILY_ZH,
                    PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z)
import mockup2
from mockup2 import small_btn, fx_slider

TINY = font('tinyFont')
INK3 = P['ink3']
TABS = [('音色', 'notes'), ('音符', 'keys'), ('效果', 'wave')]
NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B']


def nn(m):
    return f'{NAMES[m % 12]}{m // 12 - 1}'


# ---------------------------------------------------------------- waterfall helpers
def draw_grid(cv, x, y, w, h, beats, ppb, lanes, lit=(), numbers=True):
    """lanes: list of (x0, width, kind) with kind in {'white','black','pad'}; lit = lane indexes that hold notes."""
    bg = new(w, h, P['white'])
    px = bg.load()
    for li, (cx, cw, kind) in enumerate(lanes):
        tint = None
        if kind == 'black':
            tint = C('#f6ecf2')
        elif kind == 'pad' and li % 2 == 1:
            tint = C('#fbf3f6')
        if li in lit:
            tint = C('#fff0f5') if kind != 'black' else C('#f7e3ec')
        if tint:
            for yy in range(h):
                for xx in range(cx - x, cx - x + cw):
                    if 0 <= xx < w:
                        px[xx, yy] = tint
    for (cx, cw, kind) in lanes:
        if kind in ('white', 'pad'):
            xx = cx - x - 1
            if 0 <= xx < w:
                for yy in range(0, h, 2):
                    px[xx, yy] = P['milk2']
    for b in range(0, beats * 2 + 1):
        yy = h - 1 - b * ppb // 2
        if yy < 0:
            continue
        col = P['milk3'] if b % 2 == 0 else P['ging2']
        for xx in range(0, w, 1 if b % 2 == 0 else 2):
            px[xx, yy] = col
    frame = new(w + 2, h + 2)
    rr_layers(frame, 0, 0, w + 2, h + 2, 2, [(0, P['milk4'])])
    frame.alpha_composite(bg, (1, 1))
    cv.spr(frame, x - 1, y - 1)
    for b in range(1, beats + 1 if numbers else 1):
        yy = y + h - 1 - b * ppb
        TINY.draw(cv.s, str(b), cv.ox + (x + w - 4) * Z, cv.oy + yy * Z - 1, INK3)


def candy(w, h, acc, selected=False, dim=False):
    h = max(h, 3)
    im = new(w, h)
    if selected:
        im = new(w + 2, h + 2)
        rr_layers(im, 0, 0, w + 2, h + 2, 2, [(0, P['white'])])
        rr_layers(im, 1, 1, w, h, 1, [(0, P['berry']), (1, acc['L'])])
        vline(im, 2, 2, max(1, h - 3), P['white'])
        return im
    else:
        rr_layers(im, 0, 0, w, h, 1, [(0, acc['O']), (1, acc['L'])])
    if w > 3 and h > 3:
        vline(im, 1 + (1 if selected and w > 4 else 0), 1 + (1 if selected else 0), max(1, h - 3 - (1 if selected else 0)), acc['h'])
    return im


def minimap(cv, x, y, notes, lo, hi, acc, label='音域'):
    cv.text(label, x, y - 2, INK2, dy=2)
    bx = x + 16
    small_btn(cv, bx, y - 1, 'left', 8, 9)
    sx = bx + 10
    strip = new(88, 5)
    for i in range(88):
        m = 21 + i
        blk = (m % 12) in (1, 3, 6, 8, 10)
        for yy in range(5):
            strip.putpixel((i, yy), P['keyB'] if (blk and yy < 3) else P['keyW'])
        if m % 12 == 0:
            strip.putpixel((i, 4), P['ink3'])
    fr = new(90, 7)
    rr_layers(fr, 0, 0, 90, 7, 1, [(0, P['keyO'])])
    fr.alpha_composite(strip, (1, 1))
    cv.spr(fr, sx, y)
    # visible window bracket
    wx = sx + 1 + (lo - 21)
    br = new(hi - lo + 3, 9)
    rr_layers(br, 0, 0, hi - lo + 3, 9, 1, [(0, acc['O'])])
    rr_layers(br, 1, 1, hi - lo + 1, 7, 0, [(0, (0, 0, 0, 0))])
    cv.spr(br, wx - 1, y - 1)
    for mm in notes:
        tick = new(1, 3, acc['O'])
        cv.spr(tick, sx + 1 + (mm - 21), y - 3)
    small_btn(cv, sx + 92, y - 1, 'right', 8, 9)


def stepper(cv, x, y, text, w=42):
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


def vel_bars(cv, x, y, vel, acc):
    for i in range(5):
        h = 3 + i * 2
        b = new(2, h, acc['O'] if i < vel else P['milk3'])
        cv.spr(b, x + i * 3, y + 10 - h)


def inspector(cv, x, y, acc, chip, delay, length, vel, show_len=True):
    cw = 18
    ch = new(cw, 11)
    rr_layers(ch, 0, 0, cw, 11, 3, [(0, acc['O']), (1, acc['L'])])
    cv.spr(ch, x, y)
    cv.text(chip, x, y, WHITE, align='center', w=cw, dy=6, shadow=(0, 2, acc['O']))
    x += cw + 3
    stepper(cv, x, y, '延迟 ' + delay)
    x += 45
    if show_len:
        stepper(cv, x, y, '时长 ' + length)
        x += 45
    vel_bars(cv, x + 1, y, vel, acc)
    small_btn(cv, 74 + 150 - 16, y, 'trash', 11, 11)


def help_btn(cv, x, y):
    b = new(10, 10)
    rr_layers(b, 0, 0, 10, 10, 4, [(0, P['rose2']), (1, P['blush'])])
    cv.spr(b, x, y)
    cv.text('?', x, y, P['berry'], align='center', w=10, dy=3)


def toolbar(cv, ox, oy):
    cv.text('长度', ox + 6, oy + 3, INK2, dy=2)
    small_btn(cv, ox + 22, oy + 4, 'minus', 9, 10)
    cv.text('4 拍', ox + 32, oy + 3, INK, dy=2)
    small_btn(cv, ox + 47, oy + 4, 'plus', 9, 10)
    cv.text('对齐', ox + 62, oy + 3, INK2, dy=2)
    cv.spr(button(12, 11, 'milk'), ox + 78, oy + 3)
    cv.spr(icon('n8', 'ink'), ox + 81, oy + 4)
    cv.text('半拍', ox + 92, oy + 3, INK, dy=2)
    cv.spr(icon('n4', P['gold2']), ox + 108, oy + 4)
    cv.text('=120', ox + 113, oy + 3, P['gold2'], dy=2)
    help_btn(cv, ox + 136, oy + 4)


# ---------------------------------------------------------------- 音符 (melodic)
def tab_notes(cv, acc, fam):
    ox, oy = 74, 23
    toolbar(cv, ox, oy)

    # the tile's notes: (midi, delay_beats, length_beats, vel 1..5)
    notes = [(48, 0, 4, 3), (64, 0.5, 1, 3), (67, 1, 1, 3), (72, 1.5, 1, 4), (76, 2, 2, 5), (79, 2, 2, 4), (72, 3, 1, 3)]
    sel = (76, 2, 2, 5)
    lo, hi = 60, 83
    minimap(cv, ox + 6, oy + 17, [n[0] for n in notes], lo, hi, acc)

    kx, kw = ox + 5, 10
    wy, wh, ppb = oy + 27, 33, 8
    ky, kh = wy + wh + 1, 33
    whites = [0, 2, 4, 5, 7, 9, 11]
    colx = {}
    cols = []
    for oc in range(2):
        for i, semi in enumerate(whites):
            m = lo + oc * 12 + semi
            colx[m] = (kx + (oc * 7 + i) * kw + 1, kw - 1, False)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            m = lo + oc * 12 + semi
            left = colx[m - 1][0]
            colx[m] = (left + kw - 3, 5, True)
    order = sorted(colx.items(), key=lambda kv: kv[1][0])
    lanes = [(v[0], v[1], 'black' if v[2] else 'white') for _, v in order]
    lit_idx = {i for i, (mm, _) in enumerate(order) if mm in {n[0] for n in notes}}
    draw_grid(cv, kx + 1, wy, 14 * kw - 1, wh, 4, ppb, lanes, lit_idx)
    # notes in the waterfall
    for (m, d, l, v) in notes:
        if m not in colx:
            continue
        cx, cw, blk = colx[m]
        top = wy + wh - int((d + l) * ppb)
        hgt = int(l * ppb) - 1
        is_sel = (m, d, l, v) == sel
        cv.spr(candy(cw - (0 if blk else 2), hgt, acc, selected=is_sel), cx + (0 if blk else 1) - (1 if is_sel else 0), top - (1 if is_sel else 0))
    # keyboard
    has = {n[0] for n in notes}
    for oc in range(2):
        for i, semi in enumerate(whites):
            m = lo + oc * 12 + semi
            x = kx + (oc * 7 + i) * kw
            st = 'selected' if m == sel[0] else ('hover' if m in has else 'normal')
            cv.spr(white_key(kw + 1, kh, st, acc), x, ky)
            if semi == 0 and m != sel[0]:
                cv.text(f'C{4 + oc}', x, ky + kh - 12, INK3, align='center', w=kw + 1, dy=0)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            m = lo + oc * 12 + semi
            cv.spr(black_key(7, 20, 'selected' if m == sel[0] else 'normal', acc), colx[m - 1][0] + kw - 4, ky)
    cv.spr(ui.heart_badge(), colx[sel[0]][0] + 1, ky + 22)

    inspector(cv, ox + 5, oy + 101, acc, nn(sel[0]), '2 拍', '2 拍', sel[3])


# ---------------------------------------------------------------- 音符 (drum kit)
DRUMS = [(36, '底鼓'), (38, '军鼓'), (37, '边击'), (39, '拍手'), (42, '闭镲'), (46, '开镲'),
         (44, '脚镲'), (45, '低嗵'), (48, '中嗵'), (50, '高嗵'), (49, '吊镲'), (51, '叮叮')]


def drum_pad(w, h, state, acc):
    im = new(w, h)
    if state == 'selected':
        rr_layers(im, 0, 0, w, h, 2, [(0, acc['O']), (1, acc['L'])])
        hline(im, 2, 1, w - 4, acc['h'])
    elif state == 'lit':
        rr_layers(im, 0, 0, w, h, 2, [(0, P['keyO']), (1, acc['h'])])
        hline(im, 2, 1, w - 4, P['white'])
    else:
        rr_layers(im, 0, 0, w, h, 2, [(0, P['keyO']), (1, P['keyW'])])
        hline(im, 2, 1, w - 4, P['white'])
    fill_rr(im, 1, h - 4, w - 2, 3, 1, P['keyW2'] if state == 'normal' else acc['m'], only_bottom=True)
    return im


def tab_drums(cv, acc):
    ox, oy = 74, 23
    toolbar(cv, ox, oy)

    # category bar instead of pitch minimap
    cv.text('鼓组', ox + 6, oy + 15, INK2, dy=2)
    cats = ['常用', '嗵鼓', '镲片', '拉丁', '音效']
    for i, c in enumerate(cats):
        x = ox + 22 + i * 24
        st = 'selected' if i == 0 else 'normal'
        cv.spr(pill(22, 10, st, acc), x, oy + 15)
        cv.text(c, x, oy + 15, WHITE if st == 'selected' else INK, align='center', w=22, dy=5,
                shadow=(0, 2, acc['O']) if st == 'selected' else None)

    kx, pw = ox + 5, 11
    wy, wh, ppb = oy + 27, 33, 8
    ky, kh = wy + wh + 2, 32
    cols = [(kx + i * (pw + 1) + 1, pw - 1, False) for i in range(12)]
    lanes = [(kx + i * (pw + 1) + 1, pw, 'pad') for i in range(12)]
    used = {0, 1, 4, 5, 10}
    draw_grid(cv, kx + 1, wy, 12 * (pw + 1) - 1, wh, 4, ppb, lanes, used)
    # a classic back-beat
    hits = []
    for b in range(8):
        hits.append((42 if b != 7 else 46, b * 0.5, 3 if b % 2 else 4))
    hits += [(36, 0, 5), (36, 2, 5), (36, 2.5, 3), (38, 1, 5), (38, 3, 5), (49, 0, 4)]
    sel = (38, 3, 5)
    idx = {m: i for i, (m, _) in enumerate(DRUMS)}
    for (m, d, v) in hits:
        x = kx + idx[m] * (pw + 1) + 1
        top = wy + wh - int(d * ppb) - 4
        is_sel = (m, d, v) == sel
        cv.spr(candy(pw - 2, 3, acc, selected=is_sel), x + 1 - (1 if is_sel else 0), top - (1 if is_sel else 0))
    lit = {m for (m, _, _) in hits}
    for i, (m, name) in enumerate(DRUMS):
        x = kx + i * (pw + 1)
        st = 'selected' if m == sel[0] else ('lit' if m in lit else 'normal')
        cv.spr(drum_pad(pw, kh, st, acc), x, ky)
        col = WHITE if st == 'selected' else INK
        cv.text(name[0], x, ky + 7, col, align='center', w=pw, dy=0)
        cv.text(name[1], x, ky + 7, col, align='center', w=pw, dy=26)
    inspector(cv, ox + 5, oy + 101, acc, '军鼓', '3 拍', '', sel[2], show_len=False)


# ---------------------------------------------------------------- 效果 + 触发
def tab_fx(cv, acc):
    ox, oy = 74, 23
    cv.text('声音效果', ox + 6, oy + 3, INK2, dy=2)
    y0 = oy + 14
    fx_slider(cv, '混响', ox + 6, y0, 0.55, acc, w=70)
    fx_slider(cv, '合唱', ox + 6, y0 + 13, 0.20, acc, w=70)
    fx_slider(cv, '颤音', ox + 6, y0 + 26, 0.0, acc, text='关', w=70)
    fx_slider(cv, '声像', ox + 6, y0 + 39, 0.5, acc, text='居中', center=True, w=70)
    tip = new(138, 11)
    rr_layers(tip, 0, 0, 138, 11, 3, [(0, P['gold2']), (1, P['gold3'])])
    cv.spr(tip, ox + 6, oy + 67)
    cv.spr(icon('globe', 'goldD', 'white'), ox + 9, oy + 69)
    cv.text('混响的“空间”在全局里设定，当前：大厅', ox + 20, oy + 67, P['goldD'], dy=5)

    cv.spr(new(138, 1, P['milk3']), ox + 6, oy + 82)
    cv.text('触发方式', ox + 6, oy + 84, INK2, dy=2)
    rows = [(True, 'person', '玩家路过'), (False, 'chicken', '村民和小动物路过')]
    for i, (on, ic, t) in enumerate(rows):
        x = ox + 6 + i * 66
        y = oy + 96
        cv.spr(toggle(on), x, y)
        cv.spr(icon(ic, 'ink2'), x + 22, y + 1)
        cv.text(t, x + 31, y - 2, INK, dy=4)


def tab_global(cv, acc):
    mockup2.tab_global(cv, acc)
    ox, oy = 74, 23
    # world column: drop the relay-era "stop all" button (relay is in the backlog)
    cv.spr(new(66, 14, P['milk']), ox + 5, oy + 96)
    # local column: metronome belongs to the player's own preferences
    R = ox + 80
    cv.spr(new(68, 24, P['milk']), R - 2, oy + 86)
    cv.spr(toggle(True), R, oy + 86)
    cv.text('编辑时开节拍器', R + 22, oy + 84, INK, dy=4)
    cv.text('这些也能在 GMCM 里改', R - 1, oy + 99, INK3, dy=4)


def help_tooltip(cv):
    lines = [('mouse_l', '点琴键：把选中的音换成这个音高'),
             ('mouse_r', '右键琴键：同一时间再叠一个音'),
             ('mouse_l', '点瀑布空白处：在那个时间加一个音'),
             ('mouse_l', '拖糖果：改音高和延迟；拖上沿改时长'),
             ('mouse_r', '右键糖果：删掉这个音')]
    x, y, w, h = 118, 41, 106, 50
    bub = new(w, h)
    rr_layers(bub, 0, 0, w, h, 4, [(0, P['berry']), (1, P['white'])])
    rr_layers(bub, 2, 2, w - 4, h - 4, 3, [(0, P['blush'])])
    rr_layers(bub, 3, 3, w - 6, h - 6, 2, [(0, P['milk'])])
    cv.spr(bub, x, y)
    tail = grid(["OOOOOOO", ".OWWWO.", "..OWO..", "...O..."], {'O': P['berry'], 'W': P['white']})
    cv.spr(grid(["...O...", "..OWO..", ".OWWWO.", "OWWWWWO"], {'O': P['berry'], 'W': P['white']}), 74 + 137, y - 3)
    acc = ramp('#ff82ad')
    for i, (ic, t) in enumerate(lines):
        yy = y + 5 + i * 8
        cv.spr(icon(ic, 'ink2', 'white', acc['L']), x + 6, yy)
        cv.text(t, x + 15, yy - 1, INK, dy=0)


def render(tab_idx, fam, prog_idx, summary, out, drums=False, help=False):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT[fam])
    acc['hex'] = FAMILY_ACCENT[fam]
    prog = PROGRAMS_ZH.get(fam, ['标准鼓组'])[prog_idx] if fam in PROGRAMS_ZH else 'TR-808 鼓组'
    mockup2.TABS = TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    shell_tab = {0: 0, 1: 1, 2: 2, 3: 4}[tab_idx]
    mockup2.draw_shell(cv, fam, acc, prog, summary, shell_tab)
    if tab_idx == 0:
        tab_instrument(cv, fam, acc, prog_idx)
    elif tab_idx == 1:
        tab_drums(cv, acc) if drums else tab_notes(cv, acc, fam)
    elif tab_idx == 2:
        tab_fx(cv, acc)
    else:
        tab_global(cv, acc)
    if help:
        help_tooltip(cv)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    render(1, 'bells', 2, '4 拍 · 7 个音', outdir / 'v3_notes.png')
    render(1, 'drumkit', 0, '4 拍 · 15 下', outdir / 'v3_drums.png', drums=True)
    render(2, 'violin', 0, 'A4 · La', outdir / 'v3_fx.png')
    render(1, 'bells', 2, '4 拍 · 7 个音', outdir / 'v3_help.png', help=True)
    render(3, 'bells', 2, '4 拍 · 7 个音', outdir / 'v3_global.png')
    print('done')
