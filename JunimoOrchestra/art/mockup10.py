"""Mockup v10 — 音色 tab: family grid + GM program list, soundfont variants
unfold under a program, exact bank/program entry lives in 更多."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import P, new, pill, icon, grid, rr_layers, button
from px import ramp
from mockup import Canvas, backdrop, FAMILY_ACCENT, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z
import mockup2
from mockup2 import small_btn
import mockup3
from mockup4 import popup_bubble
import blocks
import gm

INK3 = P['ink3']
OX, OY = 74, 23
FAM = {f[0]: f for f in gm.FAMILIES}


def family_grid(cv, sel_key, hover_key=None):
    cells = []
    for i, f in enumerate(blocks.FAMILIES):
        c, r = i % 3, i // 3
        cells.append((f, OX + 7 + c * 20, OY + 7 + r * 17))
    for f, x, y in cells:
        if f['key'] == sel_key:
            a = ramp(FAMILY_ACCENT[f['key']])
            plate = new(20, 19)
            rr_layers(plate, 0, 0, 20, 19, 5, [(0, a['O']), (1, a['h']), (2, P['white'])])
            cv.spr(plate, x - 2, y - 2)
        elif f['key'] == hover_key:
            plate = new(20, 19)
            rr_layers(plate, 0, 0, 20, 19, 5, [(0, P['rose2']), (1, P['blush'])])
            cv.spr(plate, x - 2, y - 2)
    for f, x, y in cells:
        lift = 1 if f['key'] in (sel_key, hover_key) else 0
        cv.spr(blocks.block_sprite(f), x, y - lift)
        if f['key'] == sel_key:
            cv.spr(ui.heart_badge(), x + 11, y - 5)
    dx, dy = OX + 7 + 2 * 20, OY + 7 + 5 * 17
    cv.spr(button(16, 15, 'milk'), dx, dy)
    cv.spr(icon('dice', 'ink2', 'white'), dx + 4, dy + 3)
    return {f['key']: (x, y) for f, x, y in cells}


def header(cv, fam_key, acc):
    k, name, cat, desc, _ = FAM[fam_key]
    x = OX + 68
    tw = cv.text(f'{name}族', x, OY + 3, INK, dy=2)
    cv.text(f'· {cat}', x + (tw + 8) // 4, OY + 3, INK3, dy=2)
    cv.spr(pill(20, 11, 'normal', acc), OX + 126, OY + 3)
    cv.text('更多', OX + 126, OY + 3, INK2, align='center', w=20, dy=6)


def row(cv, y, name, number, state, acc, badge=None, expanded=False):
    x, w = OX + 68, 78
    cv.spr(pill(w, 10, state, acc), x, y)
    sel = state == 'selected'
    cv.spr(icon('sn8', WHITE if sel else acc['m']), x + 3, y + 1)
    cv.text(name, x + 9, y, WHITE if sel else INK, dy=5, shadow=(0, 2, acc['O']) if sel else None)
    if badge:
        bw = 13
        b = new(bw, 7)
        rr_layers(b, 0, 0, bw, 7, 2, [(0, acc['O'] if not sel else P['white']), (1, acc['h'] if not sel else acc['O'])])
        cv.spr(b, x + w - 29, y + 1)
        cv.text(f'+{badge}', x + w - 29, y + 1, acc['O'] if not sel else WHITE, align='center', w=bw, dy=-2)
    cv.text(str(number), x, y, acc['h'] if sel else INK3, align='right', w=w - 3, dy=5)


def variant_chips(cv, y, chips, sel, acc):
    x = OX + 72
    cv.spr(new(1, 9, acc['L']), OX + 70, y)
    for c in chips:
        w = max(14, len(c) * 5 + 4)
        st = 'selected' if c == sel else 'normal'
        cv.spr(pill(w, 9, st, acc), x, y)
        cv.text(c, x, y, WHITE if st == 'selected' else INK, align='center', w=w, dy=4,
                shadow=(0, 2, acc['O']) if st == 'selected' else None)
        x += w + 1


def scroll_hint(cv, y):
    cv.spr(icon('down', INK3), OX + 104, y)


def tab_songbird(cv, acc):
    pos = family_grid(cv, 'songbird', hover_key='steeldrum')
    header(cv, 'songbird', acc)
    base = 120
    y = OY + 15
    variants = {122: 5, 123: 3, 125: 5, 126: 5}
    for i in range(8):
        p = base + i
        name = gm.PROGRAMS[p][1]
        sel = p == 123
        row(cv, y, name, p + 1, 'selected' if sel else 'normal', acc, badge=variants.get(p))
        y += 11
        if sel:
            variant_chips(cv, y, ['鸟鸣', '鸟鸣 2', '狗叫', '马蹄声'], '狗叫', acc)
            y += 11
    # tooltip over the hovered family
    hx, hy = pos['steeldrum']
    popup_bubble(cv, OX + 4, hy - 25, 62, 22, [(None, '钢鼓族 · 打击乐', INK), (None, '钢鼓、木鱼、太鼓', INK3)],
                 tail_x=hx + 5, tail_up=False)


def tab_piano_more(cv, acc):
    family_grid(cv, 'piano')
    header(cv, 'piano', acc)
    y = OY + 15
    variants = {0: 2, 1: 1, 4: 3, 5: 1, 6: 3}
    for p in range(8):
        row(cv, y, gm.PROGRAMS[p][1], p + 1, 'selected' if p == 0 else 'normal', acc, badge=variants.get(p))
        y += 11
    # 更多 popover
    gold = ramp('#f2b640')
    bx, by, bw, bh = OX + 70, OY + 16, 76, 46
    popup_bubble(cv, bx, by, bw, bh, [], tail_x=OX + 132, tail_up=True)
    cv.text('按编号选音色', bx + 5, by + 3, INK2, dy=0)
    for i, (lab, val) in enumerate((('音色库', '0'), ('音色号', '1'))):
        yy = by + 12 + i * 12
        cv.text(lab, bx + 5, yy, INK, dy=4)
        small_btn(cv, bx + 26, yy + 1, 'minus', 9, 9)
        f = new(20, 10)
        rr_layers(f, 0, 0, 20, 10, 2, [(0, P['milk4']), (1, P['white'])])
        cv.spr(f, bx + 36, yy + 1)
        cv.text(val, bx + 36, yy + 1, INK, align='center', w=20, dy=4)
        small_btn(cv, bx + 57, yy + 1, 'plus', 9, 9)
    cv.text('音色号按 GM 编号 1–128', bx + 5, by + 36, INK3, dy=0)


def tab_drums(cv, acc):
    family_grid(cv, 'drumkit')
    header(cv, 'drumkit', acc)
    y = OY + 15
    for i, (prog, name) in enumerate(gm.DRUM_KITS[:9]):
        row(cv, y, name, prog + 1, 'selected' if prog == 25 else 'normal', acc)
        y += 11
    scroll_hint(cv, y - 1)


SCENES = {
    'songbird': ('songbird', '狗叫', tab_songbird),
    'piano': ('piano', '原声大钢琴', tab_piano_more),
    'drums': ('drumkit', '808/909 鼓组', tab_drums),
}


def render(kind, out):
    fam, prog, fn = SCENES[kind]
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT[fam])
    acc['hex'] = FAMILY_ACCENT[fam]
    mockup2.TABS = mockup3.TABS
    mockup2.TAB_W, mockup2.TAB_STEP = 36, 38
    mockup2.FAMILY_ZH[fam] = FAM[fam][1]
    mockup2.draw_shell(cv, fam, acc, prog, '1 个音 · 速度 120', 0)
    fn(cv, acc)
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    for k in SCENES:
        render(k, outdir / f'v10_{k}.png')
    print('done')
