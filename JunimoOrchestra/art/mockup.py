"""Style mockup of the tuning panel, composed at real in-game scale (1280x720,
UI sprites at 4x, text in Stardew's own zh-CN SpriteFonts)."""
from __future__ import annotations

import os
import sys
from pathlib import Path
from PIL import Image, ImageEnhance

import ui
from ui import P, new, card, tab, button, pill, slider_track, slider_fill, heart_knob, white_key, black_key
from ui import icon, stage, ribbon, panel, grid, TREBLE, NOTEHEAD, SHARP, hline, put, fill_rr, rr_layers, keycap, toggle
from px import ramp, hex2rgba as C
from sfont import font, CONTENT
import blocks

Z = 4
SCR_W, SCR_H = 1280, 720
PW, PH = 232, 160
# a screenshot of the farm for the backdrop (MOCKUP_REF/farm_full.png; without it the backdrop is plain)
REF = Path(os.environ.get('MOCKUP_REF', '.'))

SMALL = font('SmallFont.zh-CN')
BIG = font('SpriteFont1.zh-CN')

FAMILY_ACCENT = {
    'piano': '#8a7ce0', 'bells': '#ff82ad', 'organ': '#ff8a66', 'guitar': '#ffa83d', 'bass': '#5b8def',
    'violin': '#e0506f', 'choir': '#b88cf0', 'trumpet': '#43b6ec', 'sax': '#6272e0', 'ocarina': '#4fcb86',
    'synth': '#c65cf0', 'cloud': '#8fa3ff', 'stardust': '#f2b640', 'kalimba': '#98c943', 'steeldrum': '#2fbfb2',
    'songbird': '#46a6f5', 'drumkit': '#ff5a6a',
}
FAMILY_ZH = {
    'piano': '钢琴', 'bells': '铃铛', 'organ': '风琴', 'guitar': '吉他', 'bass': '贝斯', 'violin': '小提琴',
    'choir': '合唱', 'trumpet': '小号', 'sax': '萨克斯', 'ocarina': '陶笛', 'synth': '合成器', 'cloud': '云朵',
    'stardust': '星尘', 'kalimba': '拇指琴', 'steeldrum': '钢鼓', 'songbird': '小鸟', 'drumkit': '架子鼓',
}
PROGRAMS_ZH = {
    'bells': ['钢片琴', '钟琴', '八音盒', '颤音琴', '马林巴', '木琴', '管钟', '扬琴'],
    'violin': ['小提琴', '中提琴', '大提琴', '低音提琴', '颤弓弦乐', '拨弦', '竖琴', '定音鼓'],
    'guitar': ['尼龙弦吉他', '钢弦吉他', '爵士吉他', '清音电吉他', '闷音电吉他', '过载吉他', '失真吉他', '吉他泛音'],
}

INK = P['ink']
INK2 = P['ink2']
WHITE = P['white']


class Canvas:
    """Draws in panel art-px coordinates onto the 1280x720 screen image."""

    def __init__(self, screen: Image.Image, ox: int, oy: int):
        self.s, self.ox, self.oy = screen, ox, oy

    def spr(self, im, x, y, z=Z):
        big = im.resize((im.width * z, im.height * z), Image.NEAREST)
        self.s.alpha_composite(big, (self.ox + x * Z, self.oy + y * Z))

    def spr_px(self, im, sx, sy, z=Z):
        big = im.resize((im.width * z, im.height * z), Image.NEAREST)
        self.s.alpha_composite(big, (int(sx), int(sy)))

    def text(self, s, x, y, col=INK, f=SMALL, shadow=None, align='left', w=None, dx=0, dy=0):
        tw, th = f.measure(s)
        X = self.ox + x * Z + dx
        if align == 'center':
            X = self.ox + x * Z + (w * Z - tw) // 2 + dx
        elif align == 'right':
            X = self.ox + x * Z + w * Z - tw + dx
        f.draw(self.s, s, X, self.oy + y * Z + dy, col, shadow)
        return tw


def backdrop():
    if not (REF / 'farm_full.png').exists():
        return Image.new('RGBA', (SCR_W, SCR_H), (58, 40, 52, 255))
    farm = Image.open(REF / 'farm_full.png').convert('RGBA')
    tx, ty = 56, 2
    crop = farm.crop((tx * 16, ty * 16, tx * 16 + 320, ty * 16 + 180))
    # a little music path of blocks in the world
    fams = [f for f in blocks.FAMILIES]
    row_y = 7
    for i, f in enumerate(fams[:9]):
        crop.alpha_composite(blocks.block_sprite(f), ((3 + i) * 16, row_y * 16 + 4))
    for i, f in enumerate(fams[9:]):
        crop.alpha_composite(blocks.block_sprite(f), ((4 + i) * 16, (row_y + 2) * 16 + 4))
    scr = crop.resize((SCR_W, SCR_H), Image.NEAREST)
    dark = Image.new('RGBA', scr.size, (20, 8, 24, 115))
    scr.alpha_composite(dark)
    return scr


def junimo(tint_hex, frame=44):
    j = Image.open(Path(CONTENT) / 'Characters' / 'Junimo.png').convert('RGBA')
    f = j.crop(((frame % 8) * 16, (frame // 8) * 16, (frame % 8) * 16 + 16, (frame // 8) * 16 + 16))
    t = C(tint_hex)
    t = tuple(int(c + (255 - c) * 0.35) for c in t[:3]) + (255,)
    px = f.load()
    for y in range(16):
        for x in range(16):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (r * t[0] // 255, g * t[1] // 255, b * t[2] // 255, a)
    return f


def draw_shell(cv: Canvas, fam: str, acc: dict, prog_name: str, note_str: str, tab_idx: int):
    cv.spr(panel(PW, PH), 0, 0)

    # --- title ribbon + close button
    rb = ribbon(104, 15)
    cv.spr(rb, (PW - 104) // 2, -8)
    title = FAMILY_ZH[fam] + '方块'
    cv.text(title, (PW - 104) // 2, -8, WHITE, BIG, shadow=(0, 3, P['berry']), align='center', w=104, dy=4)

    cb = ui.round_button(13, 'pink')
    cv.spr(cb, PW - 16, -5)
    cv.spr(icon('x', 'white'), PW - 13, -1)

    # --- left column: stage
    st = stage(60, 54)
    cv.spr(st, 8, 14)
    blk = blocks.block_sprite(next(f for f in blocks.FAMILIES if f['key'] == fam))
    cv.spr(blk, 8 + 22, 14 + 30)
    sh = new(10, 2, (40, 10, 40, 110))
    cv.spr(sh, 8 + 25, 14 + 31)
    cv.spr(junimo(acc['hex']), 8 + 22, 14 + 16)
    cv.spr(icon('note', 'gold3', 'white'), 8 + 13, 14 + 17)
    cv.spr(icon('notes', 'blush', 'white'), 8 + 41, 14 + 13)
    cv.spr(icon('sparkle', 'gold3', 'white'), 8 + 44, 14 + 26)

    # plaque: now playing
    pl = card(60, 23, r=3, fill='milk', edge='gold2', lace=False)
    cv.spr(pl, 8, 71)
    hline_im = new(56, 1, P['gold3'])
    cv.spr(hline_im, 10, 72)
    cv.text(prog_name, 8, 72, INK, align='center', w=60, dy=2)
    cv.text(note_str, 8, 82, acc['d'], align='center', w=60, dy=0)

    # play button
    cv.spr(button(60, 20, 'pink'), 8, 97)
    cv.spr(icon('play', 'white', 'white'), 24, 103)
    cv.text('试听', 32, 97, WHITE, BIG, shadow=(0, 3, P['berry']), dy=10)

    # copy / paste
    cv.spr(button(29, 15, 'milk'), 8, 121)
    cv.spr(icon('copy', 'ink', 'milk'), 12, 124)
    cv.text('复制', 20, 121, INK, dy=12)
    cv.spr(button(29, 15, 'milk'), 39, 121)
    cv.spr(icon('paste', 'ink', 'milk'), 43, 124)
    cv.text('粘贴', 51, 121, INK, dy=12)

    # --- tabs + card
    names = [('音色', 'notes'), ('音符', 'keys'), ('演奏', 'star')]
    for i, (nm, ic) in enumerate(names):
        x = 76 + i * 50
        sel = i == tab_idx
        cv.spr(tab(48, 13, sel), x, 11)
        icn = icon(ic, 'ink' if sel else 'ink2', 'white' if sel else 'blush')
        cv.spr(icn, x + 7, 15 if sel else 16)
        cv.text(nm, x + 17, 11, INK if sel else INK2, dy=14 if sel else 18)
    cv.spr(card(150, 114, r=4, lace=True), 74, 23)
    # re-open the selected tab into the card
    sx = 76 + tab_idx * 50
    cv.spr(new(46, 2, P['milk']), sx + 1, 23)

    # --- bottom bar: volume + done
    bar = card(216, 14, r=4, fill='milk2', edge='rose2', lace=False)
    cv.spr(bar, 8, 141)
    cv.spr(icon('speaker', 'ink', 'white'), 13, 144)
    cv.text('音量', 23, 141, INK, dy=12)
    tr = slider_track(116)
    cv.spr(tr, 42, 144)
    vol = 0.8
    fill_w = int(116 * vol)
    cv.spr(slider_fill(fill_w, acc), 42, 144)
    kn = heart_knob()
    cv.spr(kn, 42 + fill_w - 6, 142)
    cv.text('80%', 161, 141, INK, dy=12)
    cv.spr(button(38, 12, 'mint'), 184, 142)
    cv.spr(icon('check', 'mintD'), 192, 145)
    cv.text('完成', 200, 142, P['mintD'], dy=9)


def tab_note(cv: Canvas, acc, note=76):
    ox, oy = 74, 23
    # staff box
    box = card(54, 38, r=3, fill='white', edge='milk4', lace=False)
    cv.spr(box, ox + 5, oy + 6)
    for i in range(5):
        cv.spr(new(46, 1, P['ink3']), ox + 9, oy + 13 + i * 5)
    cv.spr(grid(TREBLE, {'O': P['ink']}), ox + 10, oy + 9)
    # E5 = top space of treble staff -> between line 0 and 1 (y 13..18) center 15.5
    head = grid(NOTEHEAD, {'O': acc['O']})
    headf = grid(NOTEHEAD, {'O': acc['L']})
    cv.spr(head, ox + 32, oy + 14)
    cv.spr(grid([".OO.", "OOOO", ".OO."], {'O': acc['L']}), ox + 32 + 0, oy + 14 + 0)
    cv.spr(new(1, 13, acc['O']), ox + 32, oy + 16)  # stem down (note above middle line)
    cv.spr(icon('sparkle', 'gold', 'white'), ox + 44, oy + 9)

    # note name
    cv.text('E5', ox + 64, oy + 5, INK, BIG, dx=0, dy=2)
    cv.text('Mi', ox + 64 + 16, oy + 5, acc['d'], BIG, dy=2)
    cv.text('第 5 八度 · 音高 76', ox + 64, oy + 17, INK2, dy=0)
    cv.text('简谱', ox + 64, oy + 26, INK2, dy=4)
    cv.text('3', ox + 84, oy + 25, acc['d'], BIG, dy=2)
    cv.spr(icon('dot', acc['d']), ox + 85, oy + 25)

    # octave control
    cv.text('八度', ox + 119, oy + 5, INK2, dy=1)
    cv.spr(button(9, 11, 'milk'), ox + 113, oy + 14)
    cv.spr(icon('left', 'ink'), ox + 115, oy + 16)
    cv.text('5', ox + 123, oy + 13, INK, BIG, dy=2)
    cv.spr(button(9, 11, 'milk'), ox + 131, oy + 14)
    cv.spr(icon('right', 'ink'), ox + 134, oy + 16)

    # keyboard: C4..B5
    kx, ky, kw, kh = ox + 5, oy + 49, 10, 48
    whites = [0, 2, 4, 5, 7, 9, 11]
    pos = {}
    for oc in range(2):
        for i, semi in enumerate(whites):
            midi = 60 + oc * 12 + semi
            x = kx + (oc * 7 + i) * kw
            state = 'selected' if midi == note else ('hover' if midi == 67 else 'normal')
            cv.spr(white_key(kw + 1, kh, state, acc), x, ky)
            pos[midi] = x
            if semi == 0:
                cv.text(f'C{4 + oc}', x, ky + kh - 13, P['ink3'], align='center', w=kw + 1, dy=0)
    for oc in range(2):
        for i, semi in enumerate([1, 3, 6, 8, 10]):
            midi = 60 + oc * 12 + semi
            left_white = pos[midi - 1]
            state = 'selected' if midi == note else 'normal'
            cv.spr(black_key(7, 31, state, acc), left_white + kw - 3, ky)
    # heart on selected key
    sx = pos[note]
    cv.spr(icon('heart', acc['O'], 'white'), sx + 2, ky + 34)
    # floating note above the pressed key
    cv.spr(icon('note', acc['O'], 'white'), sx + 3, ky - 9)

    hx, hy = ox + 14, oy + 100
    cv.spr(keycap('arrow_left'), hx, hy); cv.spr(keycap('arrow_right'), hx + 12, hy)
    cv.text('半音', hx + 25, hy - 2, P['ink2'], dy=4)
    hx += 50
    cv.spr(keycap('arrow_up'), hx, hy); cv.spr(keycap('arrow_down'), hx + 12, hy)
    cv.text('八度', hx + 25, hy - 2, P['ink2'], dy=4)
    hx += 50
    cv.spr(keycap('space', 19), hx, hy)
    cv.text('试听', hx + 22, hy - 2, P['ink2'], dy=4)


def tab_instrument(cv: Canvas, fam, acc, sel_prog=0):
    ox, oy = 74, 23
    cells = []
    for i, f in enumerate(blocks.FAMILIES):
        c, r = i % 3, i // 3
        cells.append((f, ox + 7 + c * 20, oy + 7 + r * 17))
    for f, x, y in cells:  # selection plate first so neighbours never get covered
        if f['key'] == fam:
            a = ramp(FAMILY_ACCENT[f['key']])
            plate = new(20, 19)
            rr_layers(plate, 0, 0, 20, 19, 5, [(0, a['O']), (1, a['h']), (2, P['white'])])
            cv.spr(plate, x - 2, y - 2)
    for f, x, y in cells:
        is_sel = f['key'] == fam
        cv.spr(blocks.block_sprite(f), x, y - (1 if is_sel else 0))
        if is_sel:
            cv.spr(ui.heart_badge(), x + 11, y - 5)
    # dice (random) in 18th slot
    dx_, dy_ = ox + 7 + 2 * 20, oy + 7 + 5 * 17
    cv.spr(ui.button(16, 15, 'milk'), dx_, dy_)
    cv.spr(icon('dice', 'ink2', 'white'), dx_ + 4, dy_ + 3)

    # right: programs
    px0 = ox + 68
    cv.text(FAMILY_ZH[fam] + '族 · 8 种音色', px0, oy + 4, INK2, dy=2)
    progs = PROGRAMS_ZH[fam]
    for i, nm in enumerate(progs):
        y = oy + 14 + i * 12
        st = 'selected' if i == sel_prog else ('hover' if i == 3 else 'normal')
        cv.spr(pill(76, 11, st, acc), px0, y)
        col = WHITE if st == 'selected' else INK
        num = f'{8 * 5 + i + 1:>3}'
        cv.spr(icon('note', WHITE if st == 'selected' else acc['m'], acc['O']), px0 + 4, y + 2)
        cv.text(nm, px0 + 11, y, col, dy=8, shadow=(0, 2, acc['O']) if st == 'selected' else None)
        cv.text(str(40 + i + 1), px0, y, P['ink3'] if st != 'selected' else acc['h'], align='right', w=73, dy=8)


def tab_play(cv: Canvas, acc):
    ox, oy = 74, 23

    def label(s, y):
        cv.text(s, ox + 7, oy + y, INK2, dy=2)

    # note length
    label('时值', 6)
    vals = ['n16', 'n8', 'n4', 'n2', 'n1', 'hold']
    for i, v in enumerate(vals):
        x = ox + 30 + i * 18
        sel = v == 'n4'
        cv.spr(button(16, 14, 'pink' if sel else 'milk'), x, oy + 4)
        g = icon(v, 'white' if sel else 'ink')
        cv.spr(g, x + (16 - g.width) // 2, oy + 6)

    # chord
    label('和弦', 24)
    chords = ['单音', '大三', '小三', '属七', '大七', '小七', '挂四', '强力', '八度']
    for i, ch in enumerate(chords):
        c, r = i % 5, i // 5
        x, y = ox + 30 + c * 23, oy + 22 + r * 12
        st = 'selected' if ch == '大三' else 'normal'
        cv.spr(pill(21, 11, st, acc), x, y)
        cv.text(ch, x, y, WHITE if st == 'selected' else INK, align='center', w=21, dy=8,
                shadow=(0, 2, acc['O']) if st == 'selected' else None)

    # strum + walk trigger
    label('扫弦', 50)
    for i, (s, ic) in enumerate([('关', None), ('上扫', 'arrow_up'), ('下扫', 'arrow_down')]):
        x = ox + 30 + i * 23
        st = 'selected' if i == 1 else 'normal'
        cv.spr(pill(21, 11, st, acc), x, oy + 48)
        if ic:
            cv.spr(icon(ic, 'white' if st == 'selected' else 'ink'), x + 3, oy + 50)
            cv.text(s, x + 9, oy + 48, WHITE if st == 'selected' else INK, dy=8,
                    shadow=(0, 2, acc['O']) if st == 'selected' else None)
        else:
            cv.text(s, x, oy + 48, INK, align='center', w=21, dy=8)
    # toggle: walk-by trigger
    cv.spr(toggle(True), ox + 102, oy + 48)
    cv.text('走过就响', ox + 102, oy + 58, INK2, align='center', w=19, dy=0)

    # relay compass
    label('传递', 74)
    cxx, cyy = ox + 44, oy + 84
    sub = new(38, 38)
    cv.spr(card(38, 36, r=4, fill='white', edge='milk4', lace=False), cxx - 19, cyy - 16)
    mini = blocks.block_sprite(next(f for f in blocks.FAMILIES if f['key'] == 'guitar'))
    cv.spr(mini, cxx - 8, cyy - 6)
    arrows = {'up': (cxx - 3, cyy - 13), 'down': (cxx - 3, cyy + 12), 'left': (cxx - 17, cyy - 1), 'right': (cxx + 14, cyy - 1)}
    on = {'right', 'down'}
    for k, (x, y) in arrows.items():
        cv.spr(icon(k, acc['O'] if k in on else 'ink3', 'white'), x, y)
    cv.text('延迟', ox + 70, oy + 70, INK2, dy=2)
    for i, v in enumerate(['n8', 'n4', 'n2', 'n1']):
        x = ox + 70 + i * 18
        sel = v == 'n4'
        cv.spr(button(16, 14, 'pink' if sel else 'milk'), x, oy + 79)
        g = icon(v, 'white' if sel else 'ink')
        cv.spr(g, x + (16 - g.width) // 2, oy + 81)
    cv.text('速度', ox + 70, oy + 96, INK2, dy=2)
    cv.spr(button(11, 11, 'milk'), ox + 88, oy + 97)
    cv.spr(icon('minus', 'ink'), ox + 91, oy + 101)
    cv.spr(icon('n4', 'ink'), ox + 102, oy + 97)
    cv.text('= 120', ox + 107, oy + 96, INK, dy=2)
    cv.spr(button(11, 11, 'milk'), ox + 133, oy + 97)
    cv.spr(icon('plus', 'ink'), ox + 136, oy + 99)


def render(tab_idx, fam, prog_idx, note_str, out):
    scr = backdrop()
    ox = (SCR_W - PW * Z) // 2
    oy = (SCR_H - PH * Z) // 2 + 8
    cv = Canvas(scr, ox, oy)
    acc = ramp(FAMILY_ACCENT[fam])
    acc['hex'] = FAMILY_ACCENT[fam]
    prog = PROGRAMS_ZH[fam][prog_idx]
    draw_shell(cv, fam, acc, prog, note_str, tab_idx)
    if tab_idx == 0:
        tab_instrument(cv, fam, acc, prog_idx)
    elif tab_idx == 1:
        tab_note(cv, acc)
    else:
        tab_play(cv, acc)
    scr.convert('RGB').save(out)
    return scr


if __name__ == '__main__':
    outdir = Path(sys.argv[1] if len(sys.argv) > 1 else 'preview')
    outdir.mkdir(parents=True, exist_ok=True)
    render(1, 'bells', 2, 'E5 · Mi', outdir / 'mockup_note.png')
    render(0, 'violin', 0, 'A4 · La', outdir / 'mockup_instrument.png')
    render(2, 'guitar', 0, 'G3 · Sol', outdir / 'mockup_play.png')
    print('done')
