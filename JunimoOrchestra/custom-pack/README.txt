Junimo Orchestra: Custom

Your own SoundFonts and pictures for Junimo Orchestra go in this folder. Updating the mod replaces the
JunimoOrchestra folder, never this one, so what you put here stays. (Updating with the release zip puts this
folder's manifest.json and README.txt back; your files stay as they are.)

SoundFonts: soundfonts/
- Put .sf2 files in soundfonts/, then pick one in the block panel's Global tab (SoundFont ‹ ›) or in Generic Mod
  Config Menu. It plays as soon as it has loaded. The built-in GeneralUser GS is always the first in the list.
- SF2 only: SF3 (compressed) files can't be read. Polyphone (free) converts SF3 to SF2.
- Instrument names come from the SoundFont. Bank 0 is General MIDI; variations are banks 1-119, drum kits bank 128.
  A preset the SoundFont doesn't have is greyed out, and the panel says what plays instead.
- If a SoundFont can't be loaded, the blocks play the built-in GeneralUser GS and a message says why.
- Big SoundFonts take as much memory as their file size.
- Each player picks their own: in multiplayer, everyone hears the SoundFont they picked.

Pictures: textures/
- Copy a picture from Mods/JunimoOrchestra/assets/textures/, change it, and save it in textures/ under the same name.
  It's used from the next time you start the game.
- Keep it the same size as ours, with everything in the same place. One of another size isn't used (the SMAPI log
  says so) and the game keeps ours.

    blocks.png             272x32    the 17 blocks, 16x16 each; the second row is their white flash
    items.png              48x16     the three Junimo items' icons, 16x16 each
    junimo_classical.png   544x1024  classical Junimos: a 32x32 cell per instrument family (column) and frame (row)
    junimo_blocks.png      272x144   band Junimos: a 16x24 cell per family (column) and frame (row); the last row is
                                     what the trumpeter, the keytarist and the pan piper stand on
    orchestra.png          288x672   Junimo musicians' instruments, 32x32 cells, and their concert dress
    ui.png                 232x236   the panel's pieces (change their colours: where each piece is can't change)
    fx.png                 36x9      the note particles, 9x9 each (white: the game tints them)

- Content Patcher packs can edit them too: they're the game assets Mods/link1412.JunimoOrchestra/Blocks, Items,
  Classical, Band, Orchestra, UI and Fx.

Sharing yours
- Copy this folder and give it a name of its own (e.g. "[JO] 8-bit Sounds"). In its manifest.json, change Name,
  Author and UniqueID (one nobody else uses, e.g. "YourName.JO8bitSounds"). Any number of these can be installed
  side by side: their SoundFonts are all in the list; of a picture more than one has, one is used (the SMAPI log
  says which).


祝尼魔乐团：自定义

把你自己的音色库和贴图放在这个文件夹里。更新模组时替换的是 JunimoOrchestra 文件夹，不会动这里，放进来的东西会一直
在。（用新版的压缩包更新时，这个文件夹里的 manifest.json 和 README.txt 会被换成新的，你的文件原样保留。）

音色库：soundfonts/
- 把 .sf2 文件放进 soundfonts/，然后在方块面板的「全局」页（音色库 ‹ ›）或者 GMCM 里选，加载好就能听到。
  自带的 GeneralUser GS 永远是列表里的第一个。
- 只支持 SF2：SF3（压缩格式）读不了，可以用免费的 Polyphone 转成 SF2。
- 音色名来自音色库自己。bank 0 是 GM 标准音色；bank 1–119 是变体；bank 128 是鼓组。
  音色库里没有的音色显示成灰色，面板会告诉你实际会用哪个。
- 音色库加载不了时，方块会用自带的 GeneralUser GS，并提示原因。
- 大的音色库占用的内存和文件一样大。
- 音色库是每个玩家自己选的：联机时每个人听到的是自己选的那个。

贴图：textures/
- 从 Mods/JunimoOrchestra/assets/textures/ 里复制一张图出来，改好以后用同样的文件名放进 textures/，
  下次启动游戏就换上了。
- 尺寸要和原图一样，每样东西的位置也不能变。尺寸不对的图不会用（SMAPI 日志里会说），游戏继续用原图。

    blocks.png             272x32    17 种方块，每个 16x16；第二行是它们发光时的白色那一帧
    items.png              48x16     三种祝尼魔物品的图标，每个 16x16
    junimo_classical.png   544x1024  古典祝尼魔：每个乐器家族一列、每一帧一行，一格 32x32
    junimo_blocks.png      272x144   乐队祝尼魔：每个家族一列、每一帧一行，一格 16x24；最后一行是小号手、
                                     键盘吉他手和排箫手脚下站的东西
    orchestra.png          288x672   祝尼魔乐手的乐器（一格 32x32）和它们的礼服
    ui.png                 232x236   面板的各个部件（可以改颜色，部件的位置不能变）
    fx.png                 36x9      飘出的小音符，每个 9x9（白色：游戏会给它们上色）

- 也可以用 Content Patcher 改，它们是游戏资源 Mods/link1412.JunimoOrchestra/Blocks、Items、Classical、Band、
  Orchestra、UI、Fx。

分享你做的
- 复制一份这个文件夹，起个自己的名字（比如「[JO] 8-bit Sounds」），再改 manifest.json 里的 Name、Author 和
  UniqueID（别人没用过的，比如 "YourName.JO8bitSounds"）。这样的文件夹可以同时装好几个：音色库都会出现在列表里；
  同一张图有好几个的话只用其中一个（SMAPI 日志里会说用的是哪个）。
