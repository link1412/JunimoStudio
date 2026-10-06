"""Export gm.py tables + UI strings to i18n/default.json and i18n/zh.json."""
from __future__ import annotations

import json
from pathlib import Path

import gm

ROOT = Path(__file__).resolve().parent.parent

FAMILY_EN = {
    'piano': ('Piano', 'Piano', 'Grand piano, e-piano, harpsichord'),
    'bells': ('Bells', 'Chromatic Percussion', 'Music box, glockenspiel, xylophone'),
    'organ': ('Organ', 'Organ', 'Pipe organ, accordion, harmonica'),
    'guitar': ('Guitar', 'Guitar', 'Acoustic and electric guitars'),
    'bass': ('Bass', 'Bass', 'Deep, round basses'),
    'violin': ('Violin', 'Strings', 'Violin, cello, harp'),
    'choir': ('Choir', 'Ensemble', 'String sections and choirs'),
    'trumpet': ('Trumpet', 'Brass', 'Trumpet, trombone, horn'),
    'sax': ('Sax', 'Reed', 'Saxophones, oboe, clarinet'),
    'ocarina': ('Ocarina', 'Pipe', 'Flute, recorder, ocarina'),
    'synth': ('Synth', 'Synth Lead', 'Video-game style lead melodies'),
    'cloud': ('Cloud', 'Synth Pad', 'Soft, fluffy background pads'),
    'stardust': ('Stardust', 'Synth Effects', 'Rain, crystals, sci-fi'),
    'kalimba': ('Kalimba', 'Ethnic', 'Sitar, shamisen, kalimba'),
    'steeldrum': ('Steel Drum', 'Percussive', 'Steel drum, woodblock, taiko'),
    'songbird': ('Songbird', 'Sound Effects', 'Birds, seashore, applause'),
    'drumkit': ('Drum Kit', 'Drum Kits', 'A whole kit, one drum per key'),
}

DRUM_KITS_EN = {
    0: 'Standard 1', 1: 'Standard 2', 2: 'Standard 3', 8: 'Room', 16: 'Power', 24: 'Electronic',
    25: 'TR-808/909', 26: 'Dance', 32: 'Jazz', 40: 'Brush', 48: 'Orchestral', 56: 'SFX', 127: 'CM-64',
}

# GM percussion key map (35-81), zh names for the note chip / tooltip
DRUMS = {
    35: ('Acoustic Bass Drum', '原声底鼓'), 36: ('Bass Drum', '底鼓'), 37: ('Side Stick', '鼓边'),
    38: ('Snare', '军鼓'), 39: ('Hand Clap', '拍手'), 40: ('Electric Snare', '电军鼓'),
    41: ('Low Floor Tom', '低落地嗵'), 42: ('Closed Hi-Hat', '闭镲'), 43: ('High Floor Tom', '高落地嗵'),
    44: ('Pedal Hi-Hat', '踩镲'), 45: ('Low Tom', '低嗵鼓'), 46: ('Open Hi-Hat', '开镲'),
    47: ('Low-Mid Tom', '中低嗵鼓'), 48: ('Hi-Mid Tom', '中高嗵鼓'), 49: ('Crash Cymbal 1', '强音镲 1'),
    50: ('High Tom', '高嗵鼓'), 51: ('Ride Cymbal 1', '叮叮镲 1'), 52: ('Chinese Cymbal', '中国镲'),
    53: ('Ride Bell', '叮叮镲帽'), 54: ('Tambourine', '铃鼓'), 55: ('Splash Cymbal', '水镲'),
    56: ('Cowbell', '牛铃'), 57: ('Crash Cymbal 2', '强音镲 2'), 58: ('Vibraslap', '颤音器'),
    59: ('Ride Cymbal 2', '叮叮镲 2'), 60: ('Hi Bongo', '高音邦戈'), 61: ('Low Bongo', '低音邦戈'),
    62: ('Mute Hi Conga', '闷高康加'), 63: ('Open Hi Conga', '开高康加'), 64: ('Low Conga', '低康加'),
    65: ('High Timbale', '高天巴鼓'), 66: ('Low Timbale', '低天巴鼓'), 67: ('High Agogo', '高阿哥哥铃'),
    68: ('Low Agogo', '低阿哥哥铃'), 69: ('Cabasa', '卡巴萨'), 70: ('Maracas', '沙锤'),
    71: ('Short Whistle', '短哨'), 72: ('Long Whistle', '长哨'), 73: ('Short Guiro', '短刮葫'),
    74: ('Long Guiro', '长刮葫'), 75: ('Claves', '响棒'), 76: ('Hi Wood Block', '高木鱼'),
    77: ('Low Wood Block', '低木鱼'), 78: ('Mute Cuica', '闷鼓'), 79: ('Open Cuica', '开鼓'),
    80: ('Mute Triangle', '闷三角铁'), 81: ('Open Triangle', '开三角铁'),
}

UI = {
    'mod.name': ('Junimo Orchestra', '祝尼魔乐团'),
    'item.desc': ('A Junimo-tuned instrument block. Walk past it to play; right-click to tune it.',
                  '祝尼魔调过音的乐器方块。走过就会响，右键可以调音。'),
    'item.name': ('{{family}} Block', '{{family}}方块'),
    'item.junimo.classical.name': ('Classical Junimo', '古典祝尼魔'),
    'item.junimo.classical.desc': ('A Junimo in concert dress with a grand classical instrument. Place it and it plays like a block; right-click to tune it, and it takes up the instrument you pick.',
                                   '穿礼服的祝尼魔，抱着一件气派的古典乐器。放下就像方块一样演奏；右键调音，选什么乐器它就换什么乐器。'),
    'item.junimo.musician.name': ('Junimo Musician', '祝尼魔乐手'),
    'item.junimo.musician.desc': ('A Junimo from the orchestra. Place it and it plays like a block, really playing its instrument; right-click to tune it. Winds and voices it dances to.',
                                  '乐团里的祝尼魔。放下就像方块一样演奏，真的在拉琴、弹琴、敲鼓；右键调音。管乐和人声它就跟着跳舞。'),
    'item.junimo.band.name': ('Band Junimo', '乐队祝尼魔'),
    'item.junimo.band.desc': ('A Junimo just as it comes out of the hut, with its family\'s instrument: a keyboard, an electric guitar, a mic, a drum kit... Place it and it plays like a block; right-click to tune it, and it takes up the instrument you pick.',
                              '从祝尼魔小屋里跑出来的那种祝尼魔，拿着它这一族的乐器：键盘、电吉他、麦克风、架子鼓……放下就像方块一样演奏；右键调音，选什么乐器它就换什么乐器。'),
    # the showcase save's signs, one at the start of each demo row (shown through the Strings asset)
    'sign.elise.merged': ('Für Elise - both hands in one row of piano blocks. Run right along the path ->',
                          '《致爱丽丝》：两只手放在同一排钢琴方块里。沿着小路向右跑 →'),
    'sign.elise.split': ('Für Elise - each hand its own row: music box above, harp below. A block plays a whole bar. Run right ->',
                         '《致爱丽丝》：两只手各一排，上面八音盒，下面竖琴；一个方块弹一整小节。向右跑 →'),
    'sign.arabesque': ('Debussy, Arabesque No. 1 - Classical Junimos: harp above, piano below. Run right ->',
                       '德彪西《第一阿拉伯风格曲》：古典祝尼魔，上面竖琴，下面钢琴。向右跑 →'),
    'sign.nachtmusik': ('Mozart, Eine kleine Nachtmusik - Junimo musicians: violins above, viola, cellos and basses below. The far rows reach farther. Run right ->',
                        '莫扎特《弦乐小夜曲》：祝尼魔乐手，上面小提琴，下面中提琴、大提琴、低音提琴；离小路远的几排触发距离更长。向右跑 →'),
    'sign.chest': ('Blocks to try: put one down and right-click it to tune it. Hover a block to see where it can be set off from.',
                   '拿去试试：放下方块，右键调音。鼠标指着方块能看到从哪些格子能触发它。'),
    'hud.junimos-learned': ('The Junimos left you three recipes: Classical Junimo, Junimo Musician and Band Junimo', '祝尼魔留给你三个配方：古典祝尼魔、祝尼魔乐手、乐队祝尼魔'),
    'config.master-volume': ('Master volume', '总音量'),
    'config.spatial': ('Distance fade & stereo', '距离衰减与左右声像'),
    'config.labels': ('Show note labels', '显示音名标签'),
    'config.particles': ('Note particles', '音符粒子'),
    'config.simple-notes': ('Simple notes tab', '简化音符页'),
    'config.simple-notes.tip': ('Pick each note by octave and name, and its length and delay as the score draws them, '
                                'instead of the piano roll. Off: the full roll, with velocity and finer timing.',
                                '用“八度 + 音名”选音，时值和延迟照乐谱上音符的样子选，不用钢琴卷帘。关掉就是完整卷帘，'
                                '可以改力度和更细的节奏。'),
    'config.auto-learn': ('Learn the recipes', '自动学会配方'),
    'hud.loading': ('Junimo Orchestra is tuning up...', '祝尼魔乐团正在调音……'),
    'hud.no-soundfont': ('Junimo Orchestra: sound bank missing', '祝尼魔乐团：找不到音色库'),

    # tuner panel — shell
    'ui.tab.instrument': ('Sound', '音色'),
    'ui.tab.notes': ('Notes', '音符'),
    'ui.tab.effects': ('Effects', '效果'),
    'ui.tab.global': ('Global', '全局'),
    'ui.preview': ('Play', '试听'),
    'ui.stop': ('Stop', '停止'),
    'ui.copy': ('Copy', '复制'),
    'ui.paste': ('Paste', '粘贴'),
    'ui.copied': ('Copied! Paste it onto another block.', '已复制，可以粘贴到别的方块'),
    'ui.pasted': ('Pasted', '已粘贴'),
    'ui.paste-empty': ('Nothing to paste yet', '还没有复制过方块'),
    'ui.volume': ('Volume', '方块音量'),
    'ui.done': ('Done', '完成'),
    'ui.summary': ('{{count}} notes · {{tempo}} bpm', '{{count}} 个音 · 速度 {{tempo}}'),
    'ui.summary.one': ('1 note · {{tempo}} bpm', '1 个音 · 速度 {{tempo}}'),
    'ui.more': ('More', '更多'),
    'ui.label.notes': ('{{count}} notes', '{{count}} 个音'),

    # 音色
    'ui.family-header': ('{{name}}', '{{name}}族'),
    'ui.more.title': ('Pick by number', '按编号选音色'),
    'ui.more.bank': ('Bank', '音色库'),
    'ui.more.program': ('Prog.', '音色号'),
    'ui.more.hint': ('Programs use GM numbers 1-128', '音色号按 GM 编号 1–128'),
    'ui.more.missing': ('Not in this SoundFont, plays {{name}}', '音色库里没有，会用{{name}}'),

    # 音符
    'ui.range': ('Range', '音域'),
    'ui.range.tip': ('Go to {{from}} ~ {{to}}', '跳到 {{from}} ~ {{to}}'),   # the English font has no en dash
    'ui.delay': ('Delay', '延迟'),
    'ui.duration': ('Length', '时值'),
    'ui.seconds': ('{{s}}s', '{{s}} 秒'),
    'ui.notes.empty': ('Click a key to add a note', '点一下琴键，加一个音'),
    'ui.notes.none': ('Click a note to edit it', '点一个音来编辑它'),
    'ui.notes.full': ('A block holds up to {{max}} notes', '一个方块最多 {{max}} 个音'),
    'ui.tempo.title': ("This block's tempo", '这个方块的速度'),
    'ui.tempo.global': ('Global', '全局'),
    'ui.tempo.own': ('Its own', '自己的'),
    'ui.tempo.follow': ('- or + gives it a tempo of its own', '按 - + 设成它自己的速度'),
    'ui.tempo.back': ('The arrow: back to the global {{tempo}}', '点箭头回到全局的 {{tempo}}'),
    'ui.velocity': ('Velocity', '力度'),
    'ui.velocity.hint': ('How hard the key is hit', '下手的轻重，也会改变音色'),
    'ui.help.1': ('Left-click a key: change the note', '左键琴键：把选中的音换成这个音'),
    'ui.help.2': ('Right-click a key: add/remove a chord note', '右键琴键：同一时刻叠一个音，再点移除'),
    'ui.help.3': ('Click the waterfall: add a note there', '点瀑布空白处：在那里加一个音'),
    'ui.help.4': ('Drag a note: delay & pitch; its top: length', '拖糖果：改延迟和音高；拖上沿：改时值'),
    'ui.help.5': ('Right-click a note: delete it', '右键糖果：删除'),
    'ui.help.6': ('Arrow keys: semitone / octave', '方向键：左右移半音，上下移八度'),
    'ui.help.7': ('Space: play · Shift: exact ticks', '空格：试听　按住 Shift：精确到 1 tick'),

    # 音符：简化（八度 + 音名两组单选）
    'ui.simple.block': ('Notes', '这个方块'),
    'ui.simple.empty': ('No notes yet: pick one below', '还没有音：在下面选一个'),
    'ui.simple.octave': ('Octave', '八度'),
    'ui.simple.low': ('low', '低'),
    'ui.simple.middle': ('mid', '中'),
    'ui.simple.high': ('high', '高'),
    'ui.simple.full': ('Full', '完整'),
    'ui.simple.full.tip': ('Velocity, finer timing: the full roll', '力度、更细的节奏：在全局里换成完整卷帘'),
    'ui.simple.last': ('A block keeps at least one note', '方块至少留一个音'),
    'ui.simple.v1': ('Whole note', '全音符'),
    'ui.simple.v2': ('Half note', '二分音符'),
    'ui.simple.v4': ('Quarter note', '四分音符'),
    'ui.simple.v8': ('Eighth note', '八分音符'),
    'ui.simple.v16': ('Sixteenth note', '十六分音符'),
    'ui.simple.beats.1': ('4 beats', '4 拍'),
    'ui.simple.beats.2': ('2 beats', '2 拍'),
    'ui.simple.beats.4': ('1 beat', '1 拍'),
    'ui.simple.beats.8': ('1/2 beat', '半拍'),
    'ui.simple.beats.16': ('1/4 beat', '1/4 拍'),
    'ui.simple.length': ('{{name}} · {{beats}}', '{{name}} · {{beats}}'),
    'ui.simple.later': ('{{name}} later · {{beats}}', '晚一个{{name}}响 · {{beats}}'),
    'ui.simple.now': ('Right away', '马上响'),
    'ui.simple.dot': ('Dotted: half as long again', '附点：再长一半'),
    'ui.simple.help.1': ('Octave: bigger is higher, 4 is middle', '八度：数字越大越高，4 是中间那组'),
    'ui.simple.help.2': ('C D E: click one to switch and hear it', 'C D E：点一下就换过去，并且试听'),
    'ui.simple.help.3': ('Length: the note as the score draws it', '时值：照乐谱上音符的样子选，带点的再点小圆点'),
    'ui.simple.help.4': ('Delay: how much later it sounds', '延迟：走到方块旁边后，晚多久才响'),
    'ui.simple.help.5': ('+: another note at the same time (a chord)', '+：再加一个同时响的音（和弦）'),
    'ui.simple.help.6': ('Click a note at the top to change that one', '点上面的音，下面改的就是它'),
    'ui.simple.help.7': ('Velocity, finer timing: Full, in Global', '力度、更细的节奏：在全局里换成完整卷帘'),

    # 效果
    'ui.fx.title': ('Sound effects', '声音效果'),
    'ui.fx.reverb': ('Reverb', '混响'),
    'ui.fx.chorus': ('Chorus', '合唱'),
    'ui.fx.vibrato': ('Vibrato', '颤音'),
    'ui.fx.pan': ('Pan', '声像'),
    'ui.fx.left': ('L', '左'),
    'ui.fx.right': ('R', '右'),
    'ui.fx.center': ('Mid', '居中'),
    'ui.fx.pan-left': ('L {{n}}', '左 {{n}}'),
    'ui.fx.pan-right': ('R {{n}}', '右 {{n}}'),
    'ui.fx.room-tip': ('Reverb space is set in Global: {{room}}', '混响的“空间”在全局里设定，当前：{{room}}'),
    'ui.trigger': ('Plays when', '触发方式'),
    'ui.trigger.players': ('Players', '玩家路过'),
    'ui.trigger.creatures': ('NPCs & animals', '村民和小动物路过'),

    # 全局
    'ui.global.save': ('This save', '本存档 · 所有方块'),
    'ui.global.prefs': ('Just for me', '我的偏好 · 所有存档'),
    'ui.global.tempo': ('Tempo', '默认速度'),
    'ui.global.room': ('Space', '空间'),
    'ui.global.transpose': ('Key', '移调'),
    'ui.global.reach': ('Reach', '触发距离'),
    'ui.global.semitones': ('{{n}} st', '{{n}} 半音'),
    'ui.global.master': ('Master', '主音量'),
    'ui.global.spatial': ('Distance fade', '远近有声音大小'),
    'ui.global.labels': ('Note labels', '头顶显示音名'),
    'ui.global.particles': ('Note particles', '飘出小音符'),
    'ui.global.simple': ('Simple notes', '简化音符页'),
    'ui.global.gmcm': ('Also editable in GMCM', '这些也能在 GMCM 里改'),
    'ui.global.font': ('SoundFont', '音色库'),
    'ui.global.font.builtin': ('Built-in', '自带'),
    'ui.global.font.loading': ('Loading…', '加载中…'),
    'config.soundfont': ('SoundFont', '音色库'),
    'config.soundfont.tip': ('SF2 files in Mods/[JO] Custom/soundfonts (a new one shows up here after restarting the game, '
                             "and right away in the panel's Global tab)",
                             'Mods/[JO] Custom/soundfonts 里的 SF2（新放进去的，重启游戏后出现在这里，面板的全局页里马上就有）'),
    'config.soundfont.builtin': ('GeneralUser GS (built-in)', 'GeneralUser GS（自带）'),
    'font.problem.missing': ('file not found', '找不到文件'),
    'font.problem.sf3': ("SF3 isn't supported, use an SF2", '不支持 SF3，请用 SF2'),
    'font.problem.invalid': ('not a valid SF2 ({{detail}})', '不是有效的 SF2（{{detail}}）'),
    'hud.speed': ('Speed {{percent}}%', '移动速度 {{percent}}%'),
    'hud.speed.run': ('Running: {{seconds}} s a tile (16ths at {{bpm}} BPM)', '跑一格 {{seconds}} 秒（十六分音符 {{bpm}} BPM）'),
    'hud.speed.walk': ('Walking: {{seconds}} s a tile (16ths at {{bpm}} BPM)', '走一格 {{seconds}} 秒（十六分音符 {{bpm}} BPM）'),
    'config.speed-slower': ('Walk slower', '走慢一点'),
    'config.speed-faster': ('Walk faster', '走快一点'),
    'config.speed-reset': ('Normal speed', '恢复正常速度'),
    'config.speed.tip': ('Steps through 25-200%; hold Shift to change it by 5%. One tile is one step of the music, so the speed '
                         'is the tempo.', '在 25–200% 之间换挡，按住 Shift 每次 5%。走一格就是音乐的一步，所以速度就是节奏。'),
    'ui.trigger.reach': ('Reach', '距离'),
    'ui.trigger.reach.n': ('{{n}}', '{{n}} 格'),
    'ui.trigger.reach.inherited': ('Up to {{n}} tiles away, straight across (global)', '横竖 {{n}} 格以内有人就响（跟随全局）'),
    'ui.trigger.reach.own': ('Up to {{n}} tiles away, straight across (its own)', '横竖 {{n}} 格以内有人就响（自己的）'),
    'ui.trigger.reach.global': ('Follows the global reach, {{n}}: set it in Global', '跟随全局的 {{n}} 格，在全局页里设定'),
    'ui.trigger.reach.back': ('Back to the global reach, {{n}}', '回到全局的 {{n}} 格'),
    'hud.font-problem': ('{{file}}: {{problem}}. The blocks play the built-in SoundFont.', '{{file}}：{{problem}}。方块先用自带的音色库。'),
    'ui.room.dry': ('Dry', '干声'),
    'ui.room.cabin': ('Cabin', '小屋'),
    'ui.room.room': ('Room', '房间'),
    'ui.room.hall': ('Hall', '大厅'),
    'ui.room.church': ('Church', '教堂'),
    'ui.room.cave': ('Cave', '山洞'),
}


def build(lang: int) -> dict[str, str]:
    out: dict[str, str] = {}
    for k, (en, zh) in UI.items():
        out[k] = (en, zh)[lang]
    for key, zh_name, zh_cat, zh_desc, _ in gm.FAMILIES:
        en_name, en_cat, en_desc = FAMILY_EN[key]
        out[f'family.{key}.name'] = (en_name, zh_name)[lang]
        out[f'family.{key}.category'] = (en_cat, zh_cat)[lang]
        out[f'family.{key}.desc'] = (en_desc, zh_desc)[lang]
    for i, (en, zh) in enumerate(gm.PROGRAMS):
        out[f'program.{i}'] = (en, zh)[lang]
    for prog, zh in gm.DRUM_KITS:
        out[f'drumkit.{prog}'] = (DRUM_KITS_EN[prog], zh)[lang]
    for key, (en, zh) in DRUMS.items():
        out[f'drum.{key}'] = (en, zh)[lang]
    if lang == 1:
        for (bank, prog), zh in gm.VARIANTS_ZH.items():
            out[f'variant.{bank}.{prog}'] = zh
    return out


if __name__ == '__main__':
    (ROOT / 'i18n').mkdir(exist_ok=True)
    for lang, name in ((0, 'default.json'), (1, 'zh.json')):
        path = ROOT / 'i18n' / name
        path.write_text(json.dumps(build(lang), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print('wrote', path)
