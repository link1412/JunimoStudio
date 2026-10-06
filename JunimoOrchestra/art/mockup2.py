"""Mockup v2 — settings split into tile / global / local-preference layers,
multi-note tiles (chords + up-to-16-step phrases)."""
from __future__ import annotations

import sys
from pathlib import Path

import ui
from ui import (P, new, card, tab, button, pill, slider_track, slider_fill, heart_knob, white_key, black_key,
                icon, stage, ribbon, panel, grid, rr_layers, keycap, toggle, knob, slot, fill_rr, hline)
from px import ramp
import blocks
from mockup import (Canvas, backdrop, junimo, tab_instrument, SMALL, BIG, FAMILY_ACCENT, FAMILY_ZH,
                    PROGRAMS_ZH, INK, INK2, WHITE, SCR_W, SCR_H, PW, PH, Z)

INK3 = P['ink3']
TABS = [('音色', 'notes'), ('音符', 'keys'), ('效果', 'wave'), ('联动', 'link')]
TAB_W, TAB_STEP = 27, 29
CARD = (74, 23, 150, 114)


def draw_shell(cv, fam, acc, prog_name, summary, tab_idx):
    cv.spr(panel(PW, PH), 0, 0)
    rb = ribbon(104, 15)
    cv.spr(rb, (PW - 104) // 2, -8)
    cv.text(FAMILY_ZH[fam] + '方块', (PW - 104) // 2, -8, WHITE, BIG, shadow=(0, 3, P['berry']), align='center', w=104, dy=4)
    cv.spr(ui.round_button(13, 'pink'), PW - 16, -5)
    cv.spr(icon('x', 'white'), PW - 13, -1)

    # stage
    cv.spr(stage(60, 54), 8, 14)
    blk = blocks.block_sprite(next(f for f in blocks.FAMILIES if f['key'] == fam))
    cv.spr(blk, 30, 44)
    cv.spr(new(10, 2, (40, 10, 40, 110)), 33, 45)
    cv.spr(junimo(acc['hex']), 30, 30)
    cv.spr(icon('note', 'gold3', 'white'), 21, 31)
    cv.spr(icon('notes', 'blush', 'white'), 49, 27)
    cv.spr(icon('sparkle', 'gold3', 'white'), 52, 40)

    # plaque
    cv.spr(card(60, 23, r=3, fill='milk', edge='gold2', lace=False), 8, 71)
    cv.spr(new(56, 1, P['gold3']), 10, 72)
    cv.text(prog_name, 8, 72, INK, align='center', w=60, dy=2)
    cv.text(summary, 8, 82, acc['d'], align='center', w=60, dy=0)

    cv.spr(button(60, 20, 'pink'), 8, 97)
    cv.spr(icon('play', 'white', 'white'), 24, 103)
    cv.text('试听', 32, 97, WHITE, BIG, shadow=(0, 3, P['berry']), dy=10)
    cv.spr(button(29, 15, 'milk'), 8, 121)
    cv.spr(icon('copy', 'ink', 'milk'), 12, 124)
    cv.text('复制', 20, 121, INK, dy=12)
    cv.spr(button(29, 15, 'milk'), 39, 121)
    cv.spr(icon('paste', 'ink', 'milk'), 43, 124)
    cv.text('粘贴', 51, 121, INK, dy=12)

    # tabs: 4 tile tabs (pink) + global tab (gold), separated
    for i, (nm, ic) in enumerate(TABS):
        x = 76 + i * TAB_STEP
        sel = i == tab_idx
        cv.spr(tab(TAB_W, 13, sel), x, 11)
        pad = (TAB_W - 23) // 2
        cv.spr(icon(ic, 'ink' if sel else 'ink2', 'white' if sel else 'blush'), x + 3 + pad, 15 if sel else 16)
        cv.text(nm, x + 13 + pad, 11, INK if sel else INK2, dy=14 if sel else 18)
    gsel = tab_idx == 4
    gx = 196
    gt = new(27, 13)
    if gsel:
        rr_layers(gt, 0, 0, 27, 16, 3, [(0, P['goldD']), (1, P['gold3'])], only_top=True)
    else:
        rr_layers(gt, 0, 2, 27, 13, 3, [(0, P['goldD']), (1, P['gold'])], only_top=True)
        hline(gt, 3, 3, 21, P['gold3'])
    cv.spr(gt, gx, 11)
    cv.spr(icon('globe', 'goldD', 'gold3' if not gsel else 'white'), gx + 3, 15 if gsel else 16)
    cv.text('全局', gx + 13, 11, P['goldD'], dy=14 if gsel else 18)

    if gsel:
        c = card(150, 114, r=4, fill='milk', edge='gold2', lace=False)
        cv.spr(c, 74, 23)
        cv.spr(new(25, 2, P['gold3']), gx + 1, 23)
    else:
        cv.spr(card(150, 114, r=4, lace=True), 74, 23)
        cv.spr(new(TAB_W - 2, 2, P['milk']), 77 + tab_idx * TAB_STEP, 23)

    # bottom bar — this tile's volume
    cv.spr(card(216, 14, r=4, fill='milk2', edge='rose2', lace=False), 8, 141)
    cv.spr(icon('speaker', 'ink', 'white'), 13, 144)
    cv.text('方块音量', 23, 141, INK, dy=12)
    cv.spr(slider_track(104), 54, 144)
    fw = int(104 * 100 / 127)
    cv.spr(slider_fill(fw, acc), 54, 144)
    cv.spr(heart_knob(), 54 + fw - 6, 142)
    cv.text('100', 161, 141, INK, dy=12)
    cv.spr(button(38, 12, 'mint'), 184, 142)
    cv.spr(icon('check', 'mintD'), 192, 145)
    cv.text('完成', 200, 142, P['mintD'], dy=9)


def small_btn(cv, x, y, ic, w=11, h=11):
    cv.spr(button(w, h, 'milk'), x, y)
    g = icon(ic, 'ink')
    cv.spr(g, x + (w - g.width) // 2, y + (h - 3 - g.height) // 2 + 1)


# ---------------------------------------------------------------- 音符 (notes / phrase)
def tab_notes(cv, acc, fam):
    ox, oy = CARD[0], CARD[1]
    # phrase controls
    cv.text('乐句', ox + 6, oy + 4, INK2, dy=2)
    small_btn(cv, ox + 24, oy + 5, 'minus', 10, 10)
    cv.text('8 步', ox + 35, oy + 4, INK, dy=2)
    small_btn(cv, ox + 52, oy + 5, 'plus', 10, 10)
    cv.text('每步', ox + 68, oy + 4, INK2, dy=2)
    cv.spr(button(12, 11, 'milk'), ox + 86, oy + 4)
    cv.spr(icon('n8', 'ink'), ox + 89, oy + 5)
    cv.spr(icon('n4', INK3), ox + 103, oy + 5)
    cv.text('=120 全局', ox + 108, oy + 4, INK3, dy=2)

    # step strip: 16 slots, 8 active; notes shown as candy bars by pitch
    steps = {0: [60, 64, 67], 2: [64], 3: [67], 4: [65, 69, 72], 6: [69], 7: [72, 76]}
    sel_step = 4
    n_steps = 8
    sx, sy, sh = ox + 5, oy + 17, 18
    sw = 14
    for i in range(n_steps):
        x = sx + i * (sw + 1)
        state = 'selected' if i == sel_step else 'normal'
        cv.spr(slot(sw, sh, state, acc, beat=(i // 4) % 2 == 1), x, sy)
        ns = steps.get(i, [])
        for n in ns:
            yy = sy + sh - 4 - int((n - 60) * (sh - 7) / 16)
            bar = new(sw - 6, 2, acc['L'] if state != 'selected' else acc['O'])
            cv.spr(bar, x + 3, yy)
        cv.text(str(i + 1), x, sy + sh, INK3 if i % 4 else INK2, align='center', w=sw, dy=-2)
    gx_ = sx + n_steps * (sw + 1)
    ghost = new(sw, sh)
    ui.stitch_rr(ghost, 0, 0, sw, sh, 2, P['milk4'], 1, 1)
    cv.spr(ghost, gx_, sy)
    cv.spr(icon('plus', P['milk4']), gx_ + 5, sy + 7)

    # current step chips
    cy = oy + 41
    cv.text(f'第 {sel_step + 1} 步', ox + 6, cy, INK, dy=0)
    chips = ['F4', 'A4', 'C5']
    x = ox + 32
    for c in chips:
        w = 17
        ch = new(w, 9)
        rr_layers(ch, 0, 0, w, 9, 3, [(0, acc['O']), (1, acc['h'])])
        cv.spr(ch, x, cy + 1)
        cv.text(c, x, cy + 1, acc['O'], align='center', w=w, dy=4)
        x += w + 2
    cv.text('F 大三和弦', x + 2, cy, INK2, dy=0)
    small_btn(cv, ox + 134, cy, 'trash', 11, 11)

    # keyboard (C4..B5) — multiple notes lit
    kx, ky, kw, kh = ox + 5, oy + 55, 10, 42
    lit = {65, 69, 72}
    whites = [0, 2, 4, 5, 7, 9, 11]
    pos = {}
    for oc in range(2):
        for i, semi in enumerate(whites):
            midi = 60 + oc * 12 + semi
            x = kx + (oc * 7 + i) * kw
            cv.spr(white_key(kw + 1, kh, 'selected' if midi in lit else 'normal', acc), x, ky)
            pos[midi] = x
            if semi == 0 and midi not in lit:
                cv.text(f'C{4 + oc}', x, ky + kh - 12, INK3, align='center', w=kw + 1, dy=0)
    for oc in range(2):
        for semi in [1, 3, 6, 8, 10]:
            midi = 60 + oc * 12 + semi
            cv.spr(black_key(7, 26, 'selected' if midi in lit else 'normal', acc), pos[midi - 1] + kw - 3, ky)
    for n in lit:
        cv.spr(ui.heart_badge(), pos[n] + 2, ky + 29)

    # hints
    hy = oy + 101
    cv.spr(icon('mouse_l', 'ink2', 'white', acc['L']), ox + 8, hy)
    cv.text('换成这个音', ox + 17, hy - 2, INK2, dy=4)
    cv.spr(icon('mouse_r', 'ink2', 'white', acc['L']), ox + 62, hy)
    cv.text('叠加 / 移除', ox + 71, hy - 2, INK2, dy=4)
    cv.spr(keycap('space', 17), ox + 114, hy)
    cv.text('试听', ox + 133, hy - 2, INK2, dy=4)


# ---------------------------------------------------------------- 效果 (sound)
def fx_slider(cv, label, x, y, val, acc, text=None, w=72, center=False):
    cv.text(label, x, y, INK2, dy=2)
    tx = x + 24
    cv.spr(slider_track(w), tx, y + 2)
    if center:
        cv.spr(new(1, 9, P['ink3']), tx + w // 2, y + 1)
        fx0 = tx + w // 2
        fx1 = tx + int(w * val)
        lo, hi = sorted([fx0, fx1])
        if hi - lo > 2:
            cv.spr(slider_fill(hi - lo + 3, acc), lo - 1, y + 2)
        cv.spr(knob(acc), tx + int(w * val) - 3, y + 2)
    else:
        fw = max(6, int(w * val))
        cv.spr(slider_fill(fw, acc), tx, y + 2)
        cv.spr(knob(acc), tx + fw - 5, y + 2)
    cv.text(text or f'{int(val * 100)}%', tx + w + 4, y, INK, dy=2)


def tab_fx(cv, acc):
    ox, oy = CARD[0], CARD[1]
    cv.text('音长', ox + 6, oy + 6, INK2, dy=2)
    for i, v in enumerate(['n16', 'n8', 'n4', 'n2', 'n1', 'hold']):
        x = ox + 30 + i * 18
        sel = v == 'n2'
        cv.spr(button(16, 14, 'pink' if sel else 'milk'), x, oy + 4)
        g = icon(v, 'white' if sel else 'ink')
        cv.spr(g, x + (16 - g.width) // 2, oy + 6)
    y0 = oy + 25
    fx_slider(cv, '混响', ox + 6, y0, 0.55, acc)
    fx_slider(cv, '合唱', ox + 6, y0 + 15, 0.20, acc)
    fx_slider(cv, '颤音', ox + 6, y0 + 30, 0.0, acc, text='关')
    fx_slider(cv, '声像', ox + 6, y0 + 45, 0.62, acc, text='右 24', center=True)
    cv.text('左', ox + 30, y0 + 53, INK3, dy=0)
    cv.text('中', ox + 30 + 34, y0 + 53, INK3, dy=0)
    cv.text('右', ox + 30 + 68, y0 + 53, INK3, dy=0)
    cv.text('提示：本机偏好里开启“立体声场”后，声像会叠加方块的左右位置', ox + 6, y0 + 66, INK3, dy=0)
    # cross-link to global room
    tip = new(138, 11)
    rr_layers(tip, 0, 0, 138, 11, 3, [(0, P['gold2']), (1, P['gold3'])])
    cv.spr(tip, ox + 6, oy + 101)
    cv.spr(icon('globe', 'goldD', 'white'), ox + 9, oy + 103)
    cv.text('混响的“空间”在全局里设定，当前：大厅', ox + 20, oy + 101, P['goldD'], dy=5)


# ---------------------------------------------------------------- 联动 (trigger & relay)
def tab_link(cv, acc, fam):
    ox, oy = CARD[0], CARD[1]
    cv.text('谁能触发', ox + 6, oy + 4, INK2, dy=2)
    rows = [(True, 'person', '玩家路过'), (True, 'chicken', '村民和小动物路过')]
    for i, (on, ic, t) in enumerate(rows):
        y = oy + 15 + i * 13
        cv.spr(toggle(on), ox + 6, y)
        cv.spr(icon(ic, 'ink2'), ox + 28, y + 1)
        cv.text(t, ox + 38, y - 2, INK, dy=4)
    cv.text('收到上一个方块的传递时总会响', ox + 6, oy + 42, INK3, dy=0)

    # relay: neighbour map
    cv.text('传递给', ox + 88, oy + 4, INK2, dy=2)
    mx, my = ox + 86, oy + 14
    cv.spr(card(58, 58, r=4, fill='white', edge='milk4', lace=False), mx, my)
    cxx, cyy = mx + 21, my + 21  # center tile top-left (16px)
    me = blocks.block_sprite(next(f for f in blocks.FAMILIES if f['key'] == fam))
    cv.spr(me, cxx, cyy)
    neigh = {'right': 'drumkit', 'down': 'piano', 'left': None, 'up': None}
    npos = {'up': (cxx, cyy - 19), 'down': (cxx, cyy + 19), 'left': (cxx - 19, cyy), 'right': (cxx + 19, cyy)}
    for d, (x, y) in npos.items():
        k = neigh[d]
        if k:
            cv.spr(blocks.block_sprite(next(f for f in blocks.FAMILIES if f['key'] == k)), x, y)
        else:
            e = new(14, 14)
            ui.stitch_rr(e, 0, 0, 14, 14, 2, P['milk4'], 1, 1)
            cv.spr(e, x + 1, y + 1)
    on = {'right': True, 'down': False}
    arr = {'right': ('arrow_right', cxx + 16, cyy + 6), 'down': ('arrow_down', cxx + 6, cyy + 16),
           'left': ('arrow_left', cxx - 6, cyy + 6), 'up': ('arrow_up', cxx + 6, cyy - 6)}
    for d, (ic, x, y) in arr.items():
        col = acc['O'] if on.get(d) else P['ink3']
        cv.spr(icon(ic, col), x, y)

    # timing
    cv.text('什么时候传', ox + 6, oy + 54, INK2, dy=2)
    opts = ['立刻', '乐句结束后']
    for i, t in enumerate(opts):
        x = ox + 6 + i * 30
        st = 'selected' if i == 1 else 'normal'
        cv.spr(pill(28, 11, st, acc), x, oy + 65)
        cv.text(t, x, oy + 65, WHITE if st == 'selected' else INK, align='center', w=28, dy=8,
                shadow=(0, 2, acc['O']) if st == 'selected' else None)
    cv.text('再多等', ox + 6, oy + 80, INK2, dy=2)
    small_btn(cv, ox + 30, oy + 81, 'minus', 10, 10)
    cv.text('0 步', ox + 43, oy + 80, INK, dy=2)
    small_btn(cv, ox + 58, oy + 81, 'plus', 10, 10)

    tip = new(138, 11)
    rr_layers(tip, 0, 0, 138, 11, 3, [(0, P['milk4']), (1, P['milk2'])])
    cv.spr(tip, ox + 6, oy + 101)
    cv.text('方块首尾相连会循环演奏，可在“全局”里一键停止', ox + 6, oy + 101, INK2, align='center', w=138, dy=5)


# ---------------------------------------------------------------- 全局 (world + local prefs)
def tab_global(cv, acc):
    ox, oy = CARD[0], CARD[1]
    # left: world (shared) — right: local prefs
    hdr = new(70, 12)
    rr_layers(hdr, 0, 0, 70, 12, 3, [(0, P['gold2']), (1, P['gold3'])])
    cv.spr(hdr, ox + 4, oy + 4)
    cv.spr(icon('globe', 'goldD', 'white'), ox + 7, oy + 6)
    cv.text('整个存档 · 多人同步', ox + 17, oy + 4, P['goldD'], dy=6)
    hdr2 = new(68, 12)
    rr_layers(hdr2, 0, 0, 68, 12, 3, [(0, P['mint2']), (1, P['mint3'])])
    cv.spr(hdr2, ox + 78, oy + 4)
    cv.spr(icon('person', 'mintD'), ox + 82, oy + 6)
    cv.text('本机偏好 · 只影响你', ox + 90, oy + 4, P['mintD'], dy=6)
    cv.spr(new(1, 92, P['milk3']), ox + 75, oy + 18)

    # world column
    L = ox + 6
    cv.text('速度', L, oy + 19, INK2, dy=2)
    small_btn(cv, L + 18, oy + 20, 'minus', 10, 10)
    cv.spr(icon('n4', 'ink'), L + 31, oy + 20)
    cv.text('=120', L + 36, oy + 19, INK, dy=2)
    small_btn(cv, L + 55, oy + 20, 'plus', 10, 10)
    cv.text('摇摆', L, oy + 33, INK2, dy=2)
    cv.spr(slider_track(32), L + 18, oy + 35)
    cv.spr(slider_fill(10, ramp('#f2b640')), L + 18, oy + 35)
    cv.spr(knob(ramp('#f2b640')), L + 18 + 6, oy + 35)
    cv.text('15%', L + 53, oy + 33, INK, dy=2)
    cv.text('空间', L, oy + 47, INK2, dy=2)
    rooms = ['干声', '小屋', '房间', '大厅', '教堂', '山洞']
    gold = ramp('#f2b640')
    for i, r in enumerate(rooms):
        c, rr = i % 3, i // 3
        x, y = L + 12 + c * 18 + (6 if False else 0), oy + 58 + rr * 12
        x = L + c * 22
        st = 'selected' if r == '大厅' else 'normal'
        cv.spr(pill(20, 11, st, gold), x, y)
        cv.text(r, x, y, WHITE if st == 'selected' else INK, align='center', w=20, dy=8,
                shadow=(0, 2, gold['O']) if st == 'selected' else None)
    cv.text('移调', L, oy + 83, INK2, dy=2)
    small_btn(cv, L + 18, oy + 84, 'minus', 10, 10)
    cv.text('0 半音', L + 31, oy + 83, INK, dy=2)
    small_btn(cv, L + 55, oy + 84, 'plus', 10, 10)
    stop = button(64, 12, 'pink')
    cv.spr(stop, L, oy + 97)
    cv.spr(icon('stop', 'white'), L + 6, oy + 100)
    cv.text('停止所有演奏', L + 13, oy + 97, WHITE, dy=9, shadow=(0, 2, P['berry']))

    # local column
    R = ox + 80
    cv.text('主音量', R, oy + 19, INK2, dy=2)
    cv.spr(slider_track(34), R + 26, oy + 21)
    cv.spr(slider_fill(30, ramp('#62bf96')), R + 26, oy + 21)
    cv.spr(knob(ramp('#62bf96')), R + 26 + 25, oy + 21)
    items = [(True, '远近有声音大小'), (True, '头顶显示音名'), (True, '飘出小音符'), (False, '低延迟模式')]
    for i, (on, t) in enumerate(items):
        y = oy + 34 + i * 13
        cv.spr(toggle(on), R, y)
        cv.text(t, R + 22, y - 2, INK, dy=4)
    tip = new(66, 21)
    rr_layers(tip, 0, 0, 66, 21, 3, [(0, P['milk4']), (1, P['milk2'])])
    cv.spr(tip, R - 1, oy + 88)
    cv.text('也可在模组配置', R - 1, oy + 88, INK2, align='center', w=66, dy=5)
    cv.text('菜单 (GMCM) 中改', R - 1, oy + 88, INK2, align='center', w=66, dy=5 + 36)


def render(tab_idx, fam, prog_idx, summary, out):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT[fam])
    acc['hex'] = FAMILY_ACCENT[fam]
    draw_shell(cv, fam, acc, PROGRAMS_ZH[fam][prog_idx], summary, tab_idx)
    [lambda: tab_instrument(cv, fam, acc, prog_idx), lambda: tab_notes(cv, acc, fam), lambda: tab_fx(cv, acc),
     lambda: tab_link(cv, acc, fam), lambda: tab_global(cv, acc)][tab_idx]()
    scr.convert('RGB').save(out)


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    render(1, 'bells', 2, '8 步 · 11 个音', outdir / 'v2_notes.png')
    render(2, 'violin', 0, 'A4 · La', outdir / 'v2_fx.png')
    render(3, 'guitar', 0, 'C 大三和弦', outdir / 'v2_link.png')
    render(4, 'bells', 2, '8 步 · 11 个音', outdir / 'v2_global.png')
    render(0, 'violin', 0, 'A4 · La', outdir / 'v2_instrument.png')
    print('done')
