"""The showcase farm: four demo rows below the farmhouse of a fresh standard farm, each a path to run along from left
to right with its blocks beside it and a sign at its start, and a chest of blocks to try by the house. Writes debug
bridge commands (docs/DEVELOPMENT.md), a line each.

    python3 gen.py > showcase.txt

    Für Elise (Beethoven)                  regular blocks, both hands in one row of piano blocks, a tile a sixteenth
    Für Elise                              regular blocks, a row per hand (music box, harp), a block a bar
    Arabesque No. 1 (Debussy), bars 1-5    classical Junimos (harp, grand piano), a block a beat
    Eine kleine Nachtmusik (Mozart), 1-8   Junimo musicians (two violins, viola, cello, bass), a block a half bar

Running on a path on the farm takes 0.1983 s a tile (speed 5 plus the floor's 0.1, measured), so a tile is a sixteenth
at 76 bpm (Für Elise), a triplet eighth at 101 (Arabesque) or an eighth at 151 (Nachtmusik). No block is in reach of
another row's path. Notes from the Mutopia Project's public-domain editions (arabesque.txt, nachtmusik.txt).
"""
import re
from fractions import Fraction
from pathlib import Path

HERE = Path(__file__).resolve().parent
PPQ = 480
PATH_FLOOR = '8'          # cobblestone path
STRINGS = 'Mods\\link1412.JunimoOrchestra\\Strings'
cmds = ['warp Farm 64 18', 'wait 60', 'clearfarm']


def settings(program, notes, tempo, vol=100, rev=40, cho=0, pan=64, bank=0, reach=1, look=None):
    n = '|'.join(f'{p},{d},{l},{v}' for p, d, l, v in sorted(notes, key=lambda n: (n[1], n[0])))
    # the reach always written down: a block without one takes the save's, and a player who turns that up mustn't
    # have one row heard from the next row's path
    s = f'v1;b={bank};p={program};t={tempo};vol={vol};rev={rev};cho={cho};mod=0;pan={pan};tp=1;tc=0;r={reach}'
    if look:
        s += f';look={look}'
    return s + f';n={n}'


def band(x0, path_y, last, sign_key):
    """The path (from two tiles before the first block to two after the last) and the sign at its start."""
    cmds.append(f'floor {PATH_FLOOR} {x0 - 2} {path_y} {x0 + last + 2} {path_y}')
    cmds.append(f'sign {x0 - 3} {path_y} [LocalizedText {STRINGS}:{sign_key}]')


# ---------------------------------------------------------------- Für Elise (WoO 59): the upbeat and bars 1-8
SIX = PPQ // 4
E2, E3, GS3, A2, A3, C4, E4, GS4, A4, B4, C5, D5, DS5, E5 = 40, 52, 56, 45, 57, 60, 64, 68, 69, 71, 72, 74, 75, 76
ev = []   # (sixteenth, pitch, ticks, velocity, hand)
def run(s, pitches):                      # the E-D#-E figure: plain sixteenths
    for i, p in enumerate(pitches):
        ev.append((s + i, p, SIX + 10, 74, 'R'))
def arpeggio(s, top, bass, lh, rh):       # the downbeat over the bass, the left hand's broken chord, the right's answer
    ev.append((s, top, 2 * SIX, 80, 'R'))
    ev.append((s, bass, 6 * SIX, 62, 'L'))
    for i, p in enumerate(lh, 1):
        ev.append((s + i, p, (6 - i) * SIX, 56, 'L'))
    for i, p in enumerate(rh, 3):
        ev.append((s + i, p, (6 - i) * SIX, 70, 'R'))
run(0, [E5, DS5])
run(2, [E5, DS5, E5, B4, D5, C5])
arpeggio(8, A4, A2, [E3, A3], [C4, E4, A4])
arpeggio(14, B4, E2, [E3, GS3], [E4, GS4, B4])
arpeggio(20, C5, A2, [E3, A3], [E4, E5, DS5])
run(26, [E5, DS5, E5, B4, D5, C5])
arpeggio(32, A4, A2, [E3, A3], [C4, E4, A4])
arpeggio(38, B4, E2, [E3, GS3], [E4, C5, B4])
ev.append((44, A4, 12 * SIX, 76, 'R'))    # bar 8: let it ring
ev.append((44, A2, 12 * SIX, 60, 'L'))
ev.append((45, E3, 11 * SIX, 54, 'L'))
ev.append((46, A3, 10 * SIX, 54, 'L'))
ELISE_T = 76
last = max(s for s, *_ in ev)

# both hands in one row of piano blocks, a tile a sixteenth: what starts then
X0, LANE, PATH = 10, 18, 19
for s in range(last + 1):
    notes = [(p, 0, l, v) for t, p, l, v, h in ev if t == s]
    if notes:
        cmds.append(f'place piano {X0 + s} {LANE} ' + settings(0, notes, ELISE_T))
band(X0, PATH, last, 'sign.elise.merged')

# a row per hand and instrument, a block a bar (its notes timed inside it): music box above, harp below
X0, TOP, PATH, BOTTOM = 10, 22, 23, 24
bars = [0, 2, 8, 14, 20, 26, 32, 38, 44]
for i, start in enumerate(bars):
    end = bars[i + 1] if i + 1 < len(bars) else last + 1
    for hand, family, program, y, vol, pan in (('R', 'bells', 10, TOP, 96, 70), ('L', 'violin', 46, BOTTOM, 112, 58)):
        notes = [(p, (s - start) * SIX, l, v) for s, p, l, v, h in ev if h == hand and start <= s < end]
        if notes:
            cmds.append(f'place {family} {X0 + start} {y} ' + settings(program, notes, ELISE_T, vol, 48, 0, pan))
band(X0, PATH, last, 'sign.elise.split')

# ---------------------------------------------------------------- Arabesque No. 1: bars 1-5, then the E major chord
# Classical Junimos, a block a beat (three tiles of a triplet eighth): the right hand's harp above, the left's piano below
arabesque = []              # (beat, pitch, delay, ticks, hand)
for line in open(HERE / 'arabesque.txt'):
    if line.startswith('#') or not line.strip():
        continue
    hand, pitch, start, length = line.split()
    start, length = Fraction(start), Fraction(length)
    beat = int(start)
    arabesque.append((beat, int(pitch), round((start - beat) * PPQ), round(length * PPQ), hand))
ARAB_T, BEAT_TILES = 101, 3
X0, TOP, PATH, BOTTOM = 10, 34, 35, 36
def velocity(beat, hand):
    base = {0: 66, 1: 66, 2: 70, 3: 78, 4: 82}[beat // 4]     # pp, swelling to the A5 in bars 4-5
    return base + (6 if hand == 'R' else -4)
for beat in range(20):
    for hand, family, program, y, vol, pan in (('R', 'violin', 46, TOP, 118, 74), ('L', 'piano', 0, BOTTOM, 100, 54)):
        mine = [(p, d, l, velocity(beat, hand)) for b, p, d, l, h in arabesque if b == beat and h == hand]
        if mine:
            cmds.append(f'place {family} {X0 + beat * BEAT_TILES} {y} '
                        + settings(program, mine, ARAB_T, vol, 72, 20, pan, look='classical'))
end = 20 * BEAT_TILES       # bar 6's downbeat: the harp rolls E major upwards over the piano's open low E
cmds.append(f'place violin {X0 + end} {TOP} ' + settings(46, [(p, i * 50, 4 * PPQ, 70) for i, p in enumerate([64, 68, 71, 76, 80])],
                                                           ARAB_T, 118, 80, 20, 74, look='classical'))
cmds.append(f'place piano {X0 + end} {BOTTOM} ' + settings(0, [(40, 0, 4 * PPQ, 62), (47, 0, 4 * PPQ, 56), (52, 0, 4 * PPQ, 54)],
                                                           ARAB_T, 100, 80, 20, 54, look='classical'))
band(X0, PATH, end, 'sign.arabesque')

# ---------------------------------------------------------------- Eine kleine Nachtmusik: bars 1-8 and bar 9's downbeat
# Junimo musicians, a block a half bar (four tiles of an eighth), five rows: the violins above the path, the viola,
# cello and bass (the cello's line an octave down) below; the rows further out reach further.
parts, current = {}, None
for line in open(HERE / 'nachtmusik.txt', encoding='utf-8'):
    m = re.match(r'=== (.+?)\s+\(MIDI track', line)
    if m:
        current = parts.setdefault(m.group(1), [])
        continue
    m = re.match(r'\s+(\d+)\s+([\d/]+)\s+(\S+)\s+(\S+)\s+([\d/]+)\s*(.*)', line)
    if current is None or not m or m.group(3) == '-':
        continue
    bar, beat, midi, _, dur, note = m.groups()
    start, length, pitches = (int(bar) - 1) * 4 + Fraction(beat), Fraction(dur), [int(p) for p in midi.split('+')]
    if ':16' in note:       # written tremolo: 16ths for the note's length
        current.extend((start + Fraction(i, 4), pitches, Fraction(1, 4)) for i in range(int(length * 4)))
    else:
        current.append((start, pitches, length))
cello = next(v for k, v in parts.items() if k.startswith('Violoncello'))
NACHT_T, HALF_BAR, END = 151, 4, 8 * 4
X0, PATH = 6, 42


def half_bars(notes, transpose=0, loud=96, final=()):
    out = {}
    for start, pitches, length in notes:
        if start >= END:
            continue
        block = int(start * 2) // HALF_BAR * HALF_BAR                   # in eighths, i.e. tiles
        delay = round((start - Fraction(block, 2)) * PPQ)
        # detached, as an Allegro's strings play: the short notes a bit shorter than written
        ticks = round(length * PPQ * (Fraction(7, 10) if length <= 1 else Fraction(9, 10)))
        v = loud - (14 if length <= Fraction(1, 4) else 0)
        for p in pitches:
            out.setdefault(block, []).append((p + transpose, delay, max(ticks, 60), v))
    out[END * 2] = [(p, 0, 3 * PPQ, loud) for p in final]              # G major, held
    return sorted(out.items())


for notes, program, row, reach, vol, pan, transpose, loud, final in (
        (parts['Violin I'], 40, PATH - 3, 3, 112, 40, 0, 100, (79,)),
        (parts['Violin II'], 40, PATH - 1, 1, 100, 54, 0, 92, (71, 74)),
        (parts['Viola'], 41, PATH + 1, 1, 104, 76, 0, 92, (67,)),
        (cello, 42, PATH + 3, 3, 108, 86, 0, 96, (55,)),
        (cello, 43, PATH + 5, 5, 112, 70, -12, 96, (43,))):
    for tile, block in half_bars(notes, transpose, loud, final):
        cmds.append(f'place violin {X0 + tile} {row} '
                    + settings(program, block, NACHT_T, vol, 56, 10, pan, reach=reach, look='musician'))
band(X0, PATH, END * 2, 'sign.nachtmusik')

# ---------------------------------------------------------------- by the house: blocks to try
FAMILIES = ['piano', 'bells', 'organ', 'guitar', 'bass', 'violin', 'choir', 'trumpet', 'sax', 'ocarina', 'synth', 'cloud',
            'stardust', 'kalimba', 'steeldrum', 'songbird', 'drumkit']
stock = [f'(O)link1412.JunimoOrchestra_{f}:100' for f in FAMILIES]
stock += ['(O)link1412.JunimoOrchestra_junimo_classical:20', '(O)link1412.JunimoOrchestra_junimo_musician:20']
cmds.append('chest 61 18 ' + ','.join(stock))
cmds.append(f'sign 62 18 [LocalizedText {STRINGS}:sign.chest]')
cmds.append('tp 64 18')

print('\n'.join(cmds))
