"""General MIDI program names (en / zh), block families and drum kits.
Single source of truth; later exported to i18n json and C# tables."""

PROGRAMS = [
    # Piano
    ('Grand Piano', '原声大钢琴'), ('Bright Piano', '明亮大钢琴'), ('Electric Grand', '电子大钢琴'),
    ('Honky-tonk Piano', '酒吧钢琴'), ('E-Piano 1', '电钢琴 1'), ('E-Piano 2', '电钢琴 2'),
    ('Harpsichord', '大键琴'), ('Clavinet', '击弦古钢琴'),
    # Chromatic percussion
    ('Celesta', '钢片琴'), ('Glockenspiel', '钟琴'), ('Music Box', '八音盒'), ('Vibraphone', '颤音琴'),
    ('Marimba', '马林巴'), ('Xylophone', '木琴'), ('Tubular Bells', '管钟'), ('Dulcimer', '扬琴'),
    # Organ
    ('Drawbar Organ', '拉杆风琴'), ('Percussive Organ', '打击风琴'), ('Rock Organ', '摇滚风琴'), ('Church Organ', '管风琴'),
    ('Reed Organ', '簧风琴'), ('Accordion', '手风琴'), ('Harmonica', '口琴'), ('Tango Accordion', '探戈手风琴'),
    # Guitar
    ('Nylon Guitar', '尼龙弦吉他'), ('Steel Guitar', '钢弦吉他'), ('Jazz Guitar', '爵士电吉他'), ('Clean Guitar', '清音电吉他'),
    ('Muted Guitar', '闷音电吉他'), ('Overdriven Guitar', '过载吉他'), ('Distortion Guitar', '失真吉他'), ('Guitar Harmonics', '吉他泛音'),
    # Bass
    ('Acoustic Bass', '原声贝斯'), ('Finger Bass', '指弹电贝斯'), ('Pick Bass', '拨片电贝斯'), ('Fretless Bass', '无品贝斯'),
    ('Slap Bass 1', '击勾贝斯 1'), ('Slap Bass 2', '击勾贝斯 2'), ('Synth Bass 1', '合成贝斯 1'), ('Synth Bass 2', '合成贝斯 2'),
    # Strings
    ('Violin', '小提琴'), ('Viola', '中提琴'), ('Cello', '大提琴'), ('Contrabass', '低音提琴'),
    ('Tremolo Strings', '颤弓弦乐'), ('Pizzicato Strings', '拨弦'), ('Orchestral Harp', '竖琴'), ('Timpani', '定音鼓'),
    # Ensemble
    ('String Ensemble 1', '弦乐合奏 1'), ('String Ensemble 2', '弦乐合奏 2'), ('Synth Strings 1', '合成弦乐 1'), ('Synth Strings 2', '合成弦乐 2'),
    ('Choir Aahs', '合唱“啊”'), ('Voice Oohs', '人声“喔”'), ('Synth Voice', '合成人声'), ('Orchestra Hit', '管弦齐奏'),
    # Brass
    ('Trumpet', '小号'), ('Trombone', '长号'), ('Tuba', '大号'), ('Muted Trumpet', '弱音小号'),
    ('French Horn', '圆号'), ('Brass Section', '铜管组'), ('Synth Brass 1', '合成铜管 1'), ('Synth Brass 2', '合成铜管 2'),
    # Reed
    ('Soprano Sax', '高音萨克斯'), ('Alto Sax', '中音萨克斯'), ('Tenor Sax', '次中音萨克斯'), ('Baritone Sax', '上低音萨克斯'),
    ('Oboe', '双簧管'), ('English Horn', '英国管'), ('Bassoon', '巴松管'), ('Clarinet', '单簧管'),
    # Pipe
    ('Piccolo', '短笛'), ('Flute', '长笛'), ('Recorder', '竖笛'), ('Pan Flute', '排箫'),
    ('Blown Bottle', '吹瓶'), ('Shakuhachi', '尺八'), ('Whistle', '口哨'), ('Ocarina', '陶笛'),
    # Synth lead
    ('Square Lead', '方波主音'), ('Saw Lead', '锯齿波主音'), ('Calliope Lead', '汽笛风琴主音'), ('Chiff Lead', '吹管主音'),
    ('Charang Lead', '拨弦主音'), ('Voice Lead', '人声主音'), ('Fifths Lead', '五度主音'), ('Bass + Lead', '贝斯主音'),
    # Synth pad
    ('New Age Pad', '新世纪音垫'), ('Warm Pad', '温暖音垫'), ('Polysynth Pad', '复音合成音垫'), ('Choir Pad', '合唱音垫'),
    ('Bowed Pad', '弓弦音垫'), ('Metallic Pad', '金属音垫'), ('Halo Pad', '光环音垫'), ('Sweep Pad', '扫频音垫'),
    # Synth effects
    ('Rain', '雨声'), ('Soundtrack', '电影配乐'), ('Crystal', '水晶'), ('Atmosphere', '大气'),
    ('Brightness', '明亮'), ('Goblins', '小精灵'), ('Echoes', '回声'), ('Sci-Fi', '科幻'),
    # Ethnic
    ('Sitar', '西塔琴'), ('Banjo', '班卓琴'), ('Shamisen', '三味线'), ('Koto', '日本筝'),
    ('Kalimba', '拇指琴'), ('Bagpipe', '风笛'), ('Fiddle', '民间提琴'), ('Shanai', '唢呐'),
    # Percussive
    ('Tinkle Bell', '叮当铃'), ('Agogo', '阿哥哥铃'), ('Steel Drums', '钢鼓'), ('Woodblock', '木鱼'),
    ('Taiko Drum', '太鼓'), ('Melodic Tom', '旋律嗵鼓'), ('Synth Drum', '合成鼓'), ('Reverse Cymbal', '反向镲'),
    # Sound effects
    ('Guitar Fret Noise', '吉他擦弦声'), ('Breath Noise', '呼吸声'), ('Seashore', '海浪'), ('Bird Tweet', '鸟鸣'),
    ('Telephone Ring', '电话铃'), ('Helicopter', '直升机'), ('Applause', '掌声'), ('Gunshot', '枪声'),
]
assert len(PROGRAMS) == 128

# block families: key, friendly zh name, GM category zh, one-line description zh, default program
FAMILIES = [
    ('piano', '钢琴', '钢琴', '钢琴、电钢琴、大键琴', 0),
    ('bells', '铃铛', '半音打击乐', '八音盒、钟琴、木琴，叮叮当当', 10),
    ('organ', '风琴', '风琴', '管风琴、手风琴、口琴', 19),
    ('guitar', '吉他', '吉他', '木吉他、电吉他', 24),
    ('bass', '贝斯', '贝斯', '低沉的贝斯', 33),
    ('violin', '小提琴', '弦乐', '小提琴、大提琴、竖琴', 40),
    ('choir', '合唱', '合奏', '弦乐团、合唱团', 52),
    ('trumpet', '小号', '铜管', '小号、长号、圆号', 56),
    ('sax', '萨克斯', '簧管', '萨克斯、双簧管、单簧管', 65),
    ('ocarina', '陶笛', '吹管', '长笛、竖笛、陶笛', 79),
    ('synth', '合成器', '合成主音', '像电子游戏的主旋律', 80),
    ('cloud', '云朵', '合成音垫', '软绵绵的背景铺底', 88),
    ('stardust', '星尘', '合成效果', '雨声、水晶、科幻', 98),
    ('kalimba', '拇指琴', '民族乐器', '西塔琴、三味线、拇指琴', 108),
    ('steeldrum', '钢鼓', '打击乐', '钢鼓、木鱼、太鼓', 114),
    ('songbird', '小鸟', '音效', '鸟鸣、海浪、掌声', 123),
    ('drumkit', '架子鼓', '鼓组', '整套鼓，一个键一种鼓', None),
]

# GeneralUser GS 2.0.3 drum kits (bank 128)
DRUM_KITS = [(0, '标准鼓组 1'), (1, '标准鼓组 2'), (2, '标准鼓组 3'), (8, '房间鼓组'), (16, '力量鼓组'),
             (24, '电子鼓组'), (25, '808/909 鼓组'), (26, '舞曲鼓组'), (32, '爵士鼓组'), (40, '刷子鼓组'),
             (48, '管弦鼓组'), (56, '音效鼓组'), (127, 'CM-64 鼓组')]

# a few GeneralUser GS variation presets (bank, program) -> zh name, used by mockups
VARIANTS_ZH = {
    (0, 123): '鸟鸣', (3, 123): '鸟鸣 2', (1, 123): '狗叫', (2, 123): '马蹄声',
    (0, 122): '海浪', (1, 122): '雨', (2, 122): '雷声', (3, 122): '风', (4, 122): '溪流', (5, 122): '气泡',
    (0, 126): '掌声', (1, 126): '笑声', (2, 126): '尖叫', (3, 126): '出拳', (4, 126): '心跳', (5, 126): '脚步声',
    (0, 125): '直升机', (1, 125): '汽车发动', (5, 125): '警笛', (6, 125): '火车', (7, 125): '喷气机', (8, 125): '星舰',
}

# Chinese names for every GeneralUser GS v2.0.3 variation preset (bank 1-119; bank 120 only
# duplicates the bank-128 drum kits and is hidden). English falls back to the SoundFont's names.
VARIANTS_ZH.update({
    (11, 0): '钢琴与弦乐·渐弱', (12, 0): '钟声钢琴', (11, 1): '钢琴与弦乐·持续',
    (8, 4): '合唱电钢琴', (11, 4): '音叉与 FM 电钢琴', (12, 4): '钟声电钢琴',
    (8, 5): '合唱 FM 电钢琴', (11, 5): '钢琴与 FM 电钢琴',
    (8, 6): '双排大键琴', (11, 6): '大键琴·无力度', (12, 6): '双排大键琴·无力度',
    (11, 8): '叮当铃', (12, 10): '圣诞铃铛', (11, 11): '颤音琴·无颤音',
    (8, 14): '教堂钟', (9, 14): '排钟', (11, 14): '钟楼',
    (8, 16): '失谐音轮风琴', (11, 16): '音轮风琴·无力度', (12, 16): '失谐音轮风琴·无力度',
    (8, 17): '失谐打击风琴', (11, 17): '打击风琴·无力度', (12, 17): '失谐打击风琴·无力度',
    (11, 18): '摇滚风琴·无力度', (8, 19): '管风琴 2', (11, 19): '管风琴·无力度', (12, 19): '管风琴 2·无力度',
    (11, 20): '簧风琴·无力度', (8, 21): '意大利手风琴',
    (8, 24): '尤克里里', (8, 25): '十二弦吉他', (16, 25): '曼陀林', (8, 26): '夏威夷吉他',
    (8, 27): '合唱清音吉他', (12, 27): '清音吉他 2', (8, 28): '放克吉他', (11, 29): '哇音吉他',
    (8, 30): '反馈吉他', (8, 31): '吉他反馈',
    (1, 38): '合成贝斯 101', (8, 38): '酸性贝斯', (11, 38): '电子贝斯', (12, 38): '锯齿贝斯',
    (8, 39): '厚重 FM 贝斯', (11, 39): '脉冲贝斯',
    (1, 44): '颤弓弦乐·单声道', (1, 48): '快弦乐·单声道', (8, 48): '管弦音垫', (12, 48): '全管弦乐团',
    (13, 48): '木管合奏', (1, 49): '慢弦乐·单声道', (11, 49): '力度弦乐', (12, 49): '力度弦乐·单声道',
    (8, 50): '合成弦乐 3', (11, 50): '合成弦乐 4', (11, 51): '合成弦乐 5', (1, 52): '音乐会合唱·单声道',
    (1, 56): '小号 2', (1, 57): '长号 2', (1, 60): '独奏圆号', (1, 61): '铜管组·单声道',
    (8, 61): '铜管组 2', (11, 61): '铜管组 3', (8, 62): '合成铜管 3', (8, 63): '合成铜管 4',
    (24, 75): '锡口哨', (25, 75): '锡口哨·平直', (26, 75): '锡口哨·装饰音', (11, 78): '吹口哨',
    (1, 80): '方波', (8, 80): '正弦波', (12, 80): '方波主音 2', (13, 80): '方波主音 3',
    (1, 81): '锯齿波', (8, 81): '博士独奏', (11, 81): '锯齿短音', (12, 81): '锯齿主音 2', (13, 81): '锯齿主音 3',
    (11, 88): '大键琴音垫', (12, 88): '幻想曲 2', (13, 88): '夜视', (11, 89): '太阳风', (12, 89): '太阳风 2',
    (11, 96): '神秘音垫', (1, 98): '合成槌音', (11, 98): '合成风铃', (11, 100): '明亮锯齿叠音',
    (2, 102): '回声声像', (8, 107): '大正琴',
    (8, 115): '响板', (8, 116): '音乐会大鼓', (8, 117): '旋律嗵鼓 2', (8, 118): '808 嗵鼓',
    (11, 119): '镲片', (12, 119): '铃鼓',
    (1, 120): '切割噪声', (2, 120): '拍弦', (1, 121): '长笛按键声', (11, 121): '滤波脆响',
    (11, 122): '呼啸的风', (12, 122): '白噪声',
    (1, 124): '电话铃 2', (2, 124): '开门吱呀', (3, 124): '关门声', (4, 124): '刮擦声', (5, 124): '风铃',
    (2, 125): '汽车刹车', (3, 125): '汽车驶过', (4, 125): '撞车', (9, 125): '爆裂噪声',
    (1, 127): '机枪', (2, 127): '激光枪', (3, 127): '爆炸', (11, 127): '干扰声', (12, 127): '流星',
})
