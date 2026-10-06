# 开发

## 从源码构建

需要 .NET 6+ SDK 和已安装 SMAPI 的游戏（ModBuildConfig 会自己找游戏目录）。

发声引擎（演奏器、调音台、改过的 MeltySynth）在 `../music/engine/src`，项目引用，编出来是模组文件夹里的
`Junimo.Engine.dll`。

自带的音色库不进仓库：从 [GeneralUser GS](https://github.com/mrbumpy409/GeneralUser-GS) 下载 v2.0.3，放到
`../music/engine/assets/GeneralUser-GS.sf2`，再在模组的 `assets/soundfonts/` 里链一份（ModBuildConfig 打包时只收项目文件夹里也有的子目录文件）：

```sh
mkdir -p assets/soundfonts
ln -s ../../../music/engine/assets/GeneralUser-GS.sf2 assets/soundfonts/
dotnet build -c Release   # 部署到游戏的 Mods 文件夹，并在 bin/Release/net6.0/ 生成发布包 zip
```

Release 构建时缺这个文件会直接报错，免得发出一个没有声音的包。

只想打包、不部署：`dotnet build -c Release -p:EnableModDeploy=false`。发布包里不会有 `art/`、`docs/`、`mockups/`、
`.py`、`.md` 和 Junimo 美术的工作稿（见 csproj 的 `IgnoreModFilePatterns`）。

发布包里是并排的两个文件夹：
- `JunimoOrchestra/`：模组本体。图在 `assets/textures/`，自带音色库在 `assets/soundfonts/`，许可证在 `assets/licenses/`。
  玩家更新时整个替换。
- `[JO] Custom/`：玩家自己的音色库（`soundfonts/`）和替换的图（`textures/`），来自 `custom-pack/`，由 csproj 的
  `BundleCustomPack` 在 Release 打包时放进 zip。它是这个模组的内容包（`src/Game/CustomPacks.cs`），放在模组文件夹外面，
  删掉 `JunimoOrchestra` 重装也不会丢。（ModBuildConfig 自带的 `ContentPacks` 会把两个包套进同一个父文件夹，
  一更新就一起被替换，所以没用它。）

## 美术流水线

Python 3 + Pillow。字体和参考图从本机游戏解包出来的 `Content (unpacked)` 读（`art/sfont.py`、`art/mockup.py` 顶上的路径）。

```sh
cd art
python3 blocks.py      # assets/textures/blocks.png：17 个方块 + 闪光层
python3 junimo_classical.py   # assets/textures/junimo_classical.png：古典祝尼魔（要游戏的 Characters/Junimo.png）
python3 junimo_items.py       # assets/textures/items.png：三种祝尼魔物品的图标
python3 fx.py          # assets/textures/fx.png：飘出的小音符
python3 export_ui.py   # assets/textures/ui.png + src/UI/Pix/PixData.g.cs：面板底图、调色板、图标
python3 i18n_gen.py    # i18n/default.json、i18n/zh.json（音色名在 gm.py）
python3 mockup11.py    # 各版本的界面示意图（mockup*.py），不用开游戏就能看界面
```

面板的绘制代码（`src/UI/Pix`）是 `art/ui.py` 的逐像素移植，所以游戏里和示意图一样。

## 调试桥

在已部署的模组文件夹里建一个 `debug/` 文件夹，模组就会开启调试桥（`src/Dev/DebugBridge.cs`）：读
`debug/cmd.txt` 里的命令（一行一条，读完删掉），结果追加到 `debug/out.txt`。调试桥开着时游戏时间是冻结的，不会走到睡觉存档。
**发布包里不能带这个文件夹**（打包本来就不会带）。

同一批命令在同一帧里执行；切页后马上点击要先 `wait` 几帧。会等待的命令（`waitworld`、`run`、`save`…）最多等 90 秒，
超时写一行 ERR 接着往下执行。

| 命令 | 作用 |
|---|---|
| `waittitle` / `load 存档` / `waitworld` / `wait 帧数` | 读档与等待 |
| `status` | 地点、位置、菜单、引擎状态、音色数、正在响的声部、视口和缩放 |
| `warp 地点 x y` / `tp x y` | 传送；`tp` 是同一地图内移动，会像走路一样触发相邻的方块 |
| `place 家族 x y [设置]` / `remove x y` / `get x y` | 摆方块（设置是 `v1;b=…;p=…;…;n=音高,延迟,时值,力度\|…`）、移除、读设置 |
| `play x y` / `preview 设置` | 演奏一个方块 / 按设置试听 |
| `give 家族 [数量]` | 给方块物品（家族写 `classical` / `musician` / `band` 就是祝尼魔物品） |
| `placeheld x y` | 像玩家一样把手里的东西放到某格（要站在旁边） |
| `craft 配方` / `recipes` / `ccdone` | 看配方材料和产出 / 按当天开始时的规则学配方 / 假装社区中心修好了（只在内存里，之后别存档） |
| `held` | 制作页面上拿在鼠标上的东西 |
| `activate x y` | 右键方块（打开面板） |
| `tab N` | 面板切到第 N 页（0 音色、1 音符、2 效果、3 全局） |
| `aclick` / `arclick` / `amouse` / `adrag` / `akey` | 面板里的点击、右键、移动、拖动、按键，坐标是面板的美术像素 |
| `mousetile x y` | 鼠标移到世界里的某一格（看悬停标签） |
| `closemenu` | 关菜单 |
| `lang en\|zh` | 本次切换语言（不写进设置） |
| `slot N` | 选工具栏第 N 格（选空格子就是空手） |
| `speed [百分比]` | 走路速度，并报告现在走一格多少秒、地板加速 |
| `run 方向 格数` / `lastrun` | 按住方向键跑 N 格（0 上 1 右 2 下 3 左，真的走，有碰撞和地板加速），报告每格几帧、几秒 |
| `newgame 名字 农场名 最喜欢的东西` | 标题画面上开新游戏（标准农场，跳过开场），会马上存一次 |
| `clearfarm` | 清空农场的杂物、树、草、树桩和大石头（建筑和灌木留着） |
| `map x0 y0 x1 y1 文件` | 把一片格子写进文件：`.` 空、`o` 物体、`f` 地板、`w` 水、`#` 不能放 |
| `floor 编号 x0 y0 x1 y1` | 铺地板或小路（编号见 Data/FloorsAndPaths） |
| `sign x y 文字` / `chest x y 物品:数量,…` / `item 物品 x y` | 摆可以写字的牌子（文字里能用游戏的 `[LocalizedText …]`）/ 装好东西的箱子 / 其他物品 |
| `save` | 现在就存档（白天中途） |
| `hud 0\|1` | 隐藏 / 显示 HUD |
| `zoom 世界 [界面]` | 改缩放（只在内存里），例如 `zoom 1 1` 截出不模糊的像素图；截完改回原来的值 |
| `shot 文件.png` | 截取整帧（后备缓冲区原尺寸） |
| `capture 秒 文件.wav` | 录模组的实时输出 |
| `exit` | 退出游戏 |

## 示例存档

`tools/showcase/`：四排示例曲子的生成脚本和曲谱，怎么重新生成见那里的 README。

存档不进仓库：打成 `JunimoOrchestra-showcase-save.zip`（解压出 `Orchestra_<id>/`），挂在 GitHub Release 上，
README 里的链接指向 v1.0.0 那份。重新生成以后，在新的 Release 上挂新的一份，再把链接改过去。

## 文档

- 设计：`docs/UX-DESIGN.md`
- 后续计划和已知限制：`docs/BACKLOG.md`
