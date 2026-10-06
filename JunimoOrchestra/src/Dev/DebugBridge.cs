using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using JunimoOrchestra.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace JunimoOrchestra.Dev
{
    /// <summary>
    /// Development-only remote control. Active only when a <c>debug</c> folder exists next to the mod.
    /// Write commands (one per line) to <c>debug/cmd.txt</c>; results are appended to <c>debug/out.txt</c>.
    /// While active, in-game time is frozen so a test session can never reach the end of the day and save.
    /// </summary>
    internal sealed class DebugBridge
    {
        private static DebugBridge? instance;

        private readonly ModEntry mod;
        private readonly string dir;
        private readonly string cmdPath;
        private readonly string outPath;
        private readonly Queue<string> queue = new();
        private int waitTicks;
        private Func<bool>? waitUntil;
        private DateTime waitSince;
        private string? pendingShot;
        private string? lastRun;

        public static DebugBridge? TryCreate(ModEntry mod)
        {
            string dir = Path.Combine(mod.Helper.DirectoryPath, "debug");
            if (!Directory.Exists(dir))
                return null;
            instance = new DebugBridge(mod, dir);
            new Harmony(mod.ModManifest.UniqueID + ".debug").Patch(
                AccessTools.Method(typeof(Microsoft.Xna.Framework.Game), "EndDraw"),
                prefix: new HarmonyMethod(typeof(DebugBridge), nameof(EndDraw_Prefix)));
            mod.Monitor.Log("Debug bridge active (debug/cmd.txt).", LogLevel.Warn);
            return instance;
        }

        private DebugBridge(ModEntry mod, string dir)
        {
            this.mod = mod;
            this.dir = dir;
            this.cmdPath = Path.Combine(dir, "cmd.txt");
            this.outPath = Path.Combine(dir, "out.txt");
        }

        public void Update()
        {
            if (Context.IsWorldReady)
                Game1.gameTimeInterval = 0; // freeze the clock: tests must never reach bedtime/saving

            if (Game1.ticks % 10 == 0 && File.Exists(this.cmdPath))
            {
                string[] lines = File.ReadAllLines(this.cmdPath);
                File.Delete(this.cmdPath);
                foreach (string line in lines)
                    if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
                        this.queue.Enqueue(line.Trim());
            }

            if (this.waitTicks > 0)
            {
                this.waitTicks--;
                return;
            }
            if (this.waitUntil != null)
            {
                if (!this.waitUntil())
                {
                    if (DateTime.UtcNow - this.waitSince < TimeSpan.FromSeconds(90))
                        return;
                    this.Write("ERR wait timed out after 90 s");   // don't leave the bridge stuck: go on with the next command
                }
                this.waitUntil = null;
            }
            if (this.pendingShot != null)
                return;

            while (this.queue.Count > 0 && this.waitTicks == 0 && this.waitUntil == null && this.pendingShot == null)
            {
                string line = this.queue.Dequeue();
                try
                {
                    this.waitSince = DateTime.UtcNow;
                    string result = this.Run(line);
                    this.Write($"OK {line}{(result.Length > 0 ? " => " + result : "")}");
                }
                catch (Exception ex)
                {
                    this.Write($"ERR {line} => {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private void Write(string text)
        {
            File.AppendAllText(this.outPath, $"[{DateTime.Now:HH:mm:ss.fff}] {text}\n");
        }

        private string Run(string line)
        {
            string[] a = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int I(int i) => int.Parse(a[i], CultureInfo.InvariantCulture);
            string Rest(int i) => string.Join(' ', a.Skip(i));

            switch (a[0])
            {
                case "wait":
                    this.waitTicks = I(1);
                    return "";

                case "waittitle":
                    this.waitUntil = () => Game1.activeClickableMenu is StardewValley.Menus.TitleMenu && StardewValley.Menus.TitleMenu.subMenu == null;
                    return "";

                case "waitworld":
                    this.waitUntil = () => Context.IsWorldReady && Game1.activeClickableMenu == null && !Game1.fadeToBlack;
                    return "";

                case "load":
                    if (Context.IsWorldReady)
                        throw new InvalidOperationException("already in a save");
                    SaveGame.Load(a[1]);
                    Game1.exitActiveMenu();
                    return "";

                case "status":
                    return $"world={Context.IsWorldReady} loc={Game1.currentLocation?.NameOrUniqueName} tile={Game1.player?.TilePoint} "
                        + $"menu={Game1.activeClickableMenu?.GetType().Name} engine={this.mod.Engine.IsReady} err={this.mod.Engine.LoadError} "
                        + $"presets={this.mod.Engine.Presets.Count} voices={this.mod.Engine.ActiveVoices} viewport={Game1.viewport.Width}x{Game1.viewport.Height} "
                        + $"zoom={Game1.options.zoomLevel} ui={Game1.options.uiScale}";

                case "warp":
                    Game1.warpFarmer(a[1], I(2), I(3), false);
                    return "";

                case "tp": // teleport within the current location (triggers adjacency like walking)
                    Game1.player.setTileLocation(new Vector2(I(1), I(2)));
                    return "";

                case "face":
                    Game1.player.faceDirection(I(1));
                    return "";

                case "place":
                {
                    Family family = Families.All.First(f => f.Key == a[1]);
                    var tile = new Vector2(I(2), I(3));
                    GameLocation loc = Game1.currentLocation;
                    loc.objects.Remove(tile);
                    var obj = ItemRegistry.Create<SObject>(family.QualifiedItemId);
                    obj.TileLocation = tile;
                    if (a.Length > 4)
                        obj.modData[BlockSettings.ModDataKey] = Rest(4);
                    loc.objects.Add(tile, obj);
                    return obj.DisplayName;
                }

                case "remove":
                    Game1.currentLocation.objects.Remove(new Vector2(I(1), I(2)));
                    return "";

                case "get":
                {
                    SObject obj = Game1.currentLocation.objects[new Vector2(I(1), I(2))];
                    obj.modData.TryGetValue(BlockSettings.ModDataKey, out string? raw);
                    return $"{obj.QualifiedItemId} {raw ?? "(default)"}";
                }

                case "play":
                {
                    var tile = new Vector2(I(1), I(2));
                    SObject obj = Game1.currentLocation.objects[tile];
                    Family family = Game.Patches.BlockFamily(obj) ?? throw new InvalidOperationException("not a block");
                    this.mod.FireBlock(Game1.currentLocation, tile, obj, family, true);
                    return "";
                }

                case "preview":
                {
                    if (!BlockSettings.TryParse(Rest(1), out BlockSettings? s))
                        throw new FormatException("bad settings");
                    return string.Join(",", this.mod.Player.Preview(s!) ?? Array.Empty<double>());
                }

                case "zoom": // world and UI scale, e.g. "zoom 1 1" for crisp screenshots (in memory only)
                    Game1.options.desiredBaseZoomLevel = float.Parse(a[1], CultureInfo.InvariantCulture);
                    Game1.options.desiredUIScale = float.Parse(a.Length > 2 ? a[2] : a[1], CultureInfo.InvariantCulture);
                    Game1.game1.refreshWindowSettings();
                    return $"{Game1.options.zoomLevel} {Game1.options.uiScale}";

                case "hud": // hide or show the HUD (toolbar, clock), for screenshots
                    Game1.displayHUD = a[1] != "0";
                    return "";

                case "speed": // set the movement speed (percent), report what a tile takes her
                    if (a.Length > 1)
                        this.mod.Speed.Set(I(1));
                    return $"{JunimoOrchestra.Game.MoveSpeed.Percent}% {JunimoOrchestra.Game.MoveSpeed.SecondsPerTile(Game1.player):0.000} s/tile running={Game1.player.running} buff={Game1.player.temporarySpeedBuff}";

                case "slot": // pick a toolbar slot (an empty one: nothing held up)
                    Game1.player.CurrentToolIndex = I(1);
                    return Game1.player.CurrentItem?.DisplayName ?? "(empty)";

                case "give": // a block of a family, or a Junimo item ("classical", "musician")
                {
                    string id = Families.All.FirstOrDefault(f => f.Key == a[1])?.QualifiedItemId
                        ?? JunimoKinds.All.First(k => k.Key == a[1]).QualifiedItemId;
                    Game1.player.addItemToInventory(ItemRegistry.Create(id, a.Length > 2 ? I(2) : 1));
                    return "";
                }

                case "placeheld": // place the held item on a tile the way the player does (placementAction, stack goes down)
                {
                    SObject held = Game1.player.CurrentItem as SObject ?? throw new InvalidOperationException("no object held");
                    bool placed = Utility.tryToPlaceItem(Game1.currentLocation, held, I(1) * 64 + 32, I(2) * 64 + 32);
                    Game1.currentLocation.objects.TryGetValue(new Vector2(I(1), I(2)), out SObject? obj);
                    string? raw = null;
                    obj?.modData.TryGetValue(BlockSettings.ModDataKey, out raw);
                    return $"placed={placed} held={Game1.player.CurrentItem?.Stack ?? 0} tile={obj?.QualifiedItemId} {raw}";
                }

                case "craft": // what a recipe takes and makes
                {
                    var recipe = new StardewValley.CraftingRecipe(a[1]);
                    Item made = recipe.createItem();
                    return $"known={Game1.player.craftingRecipes.ContainsKey(a[1])} ingredients=[{string.Join(" ", recipe.recipeList.Select(p => p.Key + "x" + p.Value))}] "
                        + $"have={recipe.doesFarmerHaveIngredientsInInventory()} makes={made.QualifiedItemId}x{made.Stack} price={made.salePrice()}";
                }

                case "recipes": // learn the recipes due (as at the start of a day)
                    this.mod.LearnRecipes();
                    return string.Join(" ", Game1.player.craftingRecipes.Keys.Where(k => k.StartsWith(Family.ItemPrefix)).Select(k => k[Family.ItemPrefix.Length..]));

                case "ccdone": // pretend the Community Center was restored (in memory: never save after this)
                    foreach (string flag in new[] { "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank", "ccVault", "ccBulletin", "ccIsComplete" })
                        Game1.MasterPlayer.mailReceived.Add(flag);
                    return $"complete={GameStateQuery.CheckConditions("IS_COMMUNITY_CENTER_COMPLETE")}";

                case "newgame": // from the title: a new game on the standard farm, intro skipped: newgame <name> <farm> <favourite thing>
                {
                    if (Game1.activeClickableMenu is not StardewValley.Menus.TitleMenu)
                        throw new InvalidOperationException("not at the title");
                    Game1.resetPlayer();
                    var cc = new StardewValley.Menus.CharacterCustomization(StardewValley.Menus.CharacterCustomization.Source.NewGame);
                    StardewValley.Menus.TitleMenu.subMenu = cc;
                    var reflect = this.mod.Helper.Reflection;
                    reflect.GetField<StardewValley.Menus.TextBox>(cc, "nameBox").GetValue().Text = a[1];
                    reflect.GetField<StardewValley.Menus.TextBox>(cc, "farmnameBox").GetValue().Text = a[2];
                    reflect.GetField<StardewValley.Menus.TextBox>(cc, "favThingBox").GetValue().Text = a[3];
                    Game1.player.Name = a[1];   // what the menu's OK checks (the boxes set these as they're typed in)
                    Game1.player.displayName = a[1];
                    Game1.player.farmName.Value = a[2];
                    Game1.player.favoriteThing.Value = a[3];
                    Game1.whichFarm = 0;
                    Game1.whichModFarm = null;
                    Game1.startingCabins = 0;
                    reflect.GetField<bool>(cc, "skipIntro").SetValue(true);
                    reflect.GetMethod(cc, "optionButtonClick").Invoke("OK");
                    return $"{Game1.player.Name} farm={Game1.player.farmName.Value} whichFarm={Game1.whichFarm} menu={Game1.activeClickableMenu?.GetType().Name} sub={StardewValley.Menus.TitleMenu.subMenu?.GetType().Name}";
                }

                case "clearfarm": // clear the farm of debris, trees, grass and stumps (buildings and bushes stay)
                {
                    Farm farm = Game1.getFarm();
                    int objects = farm.objects.Count(), features = farm.terrainFeatures.Count(), clumps = farm.resourceClumps.Count;
                    farm.objects.Clear();
                    farm.terrainFeatures.Clear();
                    farm.resourceClumps.Clear();
                    farm.debris.Clear();
                    return $"{objects} objects, {features} terrain features, {clumps} stumps/logs/boulders";
                }

                case "map": // write the current location's tiles to a file: '.' free, 'o' object, 'f' floor, 'w' water, '#' blocked
                {
                    GameLocation loc = Game1.currentLocation;
                    int x0 = I(1), y0 = I(2), x1 = I(3), y1 = I(4);
                    var rows = new List<string>();
                    for (int y = y0; y <= y1; y++)
                    {
                        var row = new System.Text.StringBuilder($"{y,3} ");
                        for (int x = x0; x <= x1; x++)
                        {
                            var t = new Vector2(x, y);
                            row.Append(loc.objects.ContainsKey(t) ? 'o'
                                : loc.isWaterTile(x, y) ? 'w'
                                : loc.terrainFeatures.TryGetValue(t, out var f) && f is StardewValley.TerrainFeatures.Flooring ? 'f'
                                : loc.CanItemBePlacedHere(t) ? '.' : '#');
                        }
                        rows.Add(row.ToString());
                    }
                    File.WriteAllLines(this.Resolve(a[5]), rows);
                    return $"{x1 - x0 + 1}x{y1 - y0 + 1} from {x0},{y0}";
                }

                case "floor": // lay a floor or path over a rectangle: floor <id> x0 y0 x1 y1 (ids as Data/FloorsAndPaths)
                {
                    int n = 0;
                    for (int y = I(3); y <= I(5); y++)
                        for (int x = I(2); x <= I(4); x++, n++)
                            Game1.currentLocation.terrainFeatures[new Vector2(x, y)] = new StardewValley.TerrainFeatures.Flooring(a[1]);
                    return $"{n} tiles";
                }

                case "item": // put an item down as decoration: item <qualified id> x y
                {
                    var tile = new Vector2(I(2), I(3));
                    var obj = ItemRegistry.Create<SObject>(a[1]);
                    Game1.currentLocation.objects.Remove(tile);
                    bool placed = obj.placementAction(Game1.currentLocation, I(2) * 64, I(3) * 64, Game1.player);
                    return $"{obj.DisplayName} placed={placed}";
                }

                case "sign": // a text sign: sign x y <text> (game text tokens work, e.g. [LocalizedText asset:key])
                {
                    var tile = new Vector2(I(1), I(2));
                    var sign = ItemRegistry.Create<SObject>("(BC)TextSign");
                    sign.signText.Value = Rest(3);
                    Game1.currentLocation.objects.Remove(tile);
                    bool placed = sign.placementAction(Game1.currentLocation, I(1) * 64, I(2) * 64, Game1.player);
                    if (Game1.currentLocation.objects.TryGetValue(tile, out SObject? put))
                        put.signText.Value = Rest(3);
                    return $"placed={placed} {put?.SignText}";
                }

                case "chest": // a chest of items: chest x y <qualified id>:<count>,...
                {
                    var tile = new Vector2(I(1), I(2));
                    var chest = new StardewValley.Objects.Chest(true, tile);
                    foreach (string entry in a[3].Split(','))
                    {
                        string[] pair = entry.Split(':');
                        chest.addItem(ItemRegistry.Create(pair[0], int.Parse(pair[1], CultureInfo.InvariantCulture)));
                    }
                    Game1.currentLocation.objects.Remove(tile);
                    Game1.currentLocation.objects.Add(tile, chest);
                    return $"{chest.Items.Count} stacks";
                }

                case "run": // run across N tiles with the movement key held down (0 up, 1 right, 2 down, 3 left); "lastrun" reports
                {
                    int dir = I(1), tiles = I(2);
                    Point start = Game1.player.TilePoint;
                    var crossed = new List<(int Tick, double Seconds)>();
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    Point last = start;
                    var keys = dir switch { 0 => Game1.options.moveUpButton, 1 => Game1.options.moveRightButton, 2 => Game1.options.moveDownButton, _ => Game1.options.moveLeftButton };
                    SButton key = keys[0].key.ToSButton();
                    // SMAPI's own input override (what Suppress uses), pressed before each update: cleared again after it.
                    // Plain reflection: SMAPI's reflection helper won't reach its internals (fine for a dev-only tool)
                    var press = Game1.input.GetType().GetMethod("OverrideButton", System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("no OverrideButton in this SMAPI");
                    EventHandler<StardewModdingAPI.Events.UpdateTickingEventArgs> hold = (_, _) => press.Invoke(Game1.input, new object[] { key, true });
                    this.mod.Helper.Events.GameLoop.UpdateTicking += hold;
                    this.lastRun = "running";
                    this.waitUntil = () =>
                    {
                        Point now = Game1.player.TilePoint;
                        if (now != last)
                        {
                            crossed.Add((Game1.ticks, clock.Elapsed.TotalSeconds));
                            last = now;
                        }
                        bool there = Math.Abs(now.X - start.X) + Math.Abs(now.Y - start.Y) >= tiles;
                        if (!there && clock.Elapsed.TotalSeconds < 60)
                            return false;
                        this.mod.Helper.Events.GameLoop.UpdateTicking -= hold;
                        Game1.player.Halt();
                        var frames = crossed.Zip(crossed.Skip(1), (p, q) => q.Tick - p.Tick).ToList();
                        double wall = crossed.Count > 1 ? (crossed[^1].Seconds - crossed[0].Seconds) / (crossed.Count - 1) : 0;
                        this.lastRun = frames.Count == 0 ? "no tiles crossed"
                            : $"{crossed.Count} tiles, frames a tile {frames.Average():0.00} ({frames.Min()}-{frames.Max()}), "
                              + $"{wall:0.0000} s a tile by the clock, buff {Game1.player.temporarySpeedBuff}, at {Game1.player.TilePoint}";
                        return true;
                    };
                    return key.ToString();
                }

                case "lastrun":
                    return this.lastRun ?? "(none)";

                case "save": // save the game now (mid-day, as it stands)
                    Game1.activeClickableMenu = new StardewValley.Menus.SaveGameMenu();
                    this.waitUntil = () => Game1.activeClickableMenu is not StardewValley.Menus.SaveGameMenu;
                    return Constants.SaveFolderName ?? "";

                case "activate":
                {
                    var tile = new Vector2(I(1), I(2));
                    SObject obj = Game1.currentLocation.objects[tile];
                    obj.checkForAction(Game1.player);
                    return Game1.activeClickableMenu?.GetType().Name ?? "(no menu)";
                }

                case "gamemenu":
                    Game1.activeClickableMenu = new StardewValley.Menus.GameMenu(I(1));
                    return "";

                case "mousetile": // put the cursor over a world tile
                {
                    Vector2 local = Game1.GlobalToLocal(Game1.viewport, new Vector2(I(1) * 64 + 32, I(2) * 64 + 32));
                    Game1.setMousePosition((int)local.X, (int)local.Y, false);
                    return local.ToString();
                }

                case "pickaxe": // hit a tile with a pickaxe, then report what dropped
                {
                    var tile = new Vector2(I(1), I(2));
                    var tool = ItemRegistry.Create<StardewValley.Tools.Pickaxe>("(T)Pickaxe");
                    tool.lastUser = Game1.player;
                    SObject obj = Game1.currentLocation.objects[tile];
                    bool vanillaRemove = obj.performToolAction(tool);
                    var drops = Game1.currentLocation.debris.SelectMany(d => d.item != null ? new[] { d.item } : Array.Empty<Item>())
                        .Select(i => $"{i.QualifiedItemId}:{(i.modData.TryGetValue(BlockSettings.ModDataKey, out string? v) ? v : "(none)")}");
                    return $"vanillaRemove={vanillaRemove} stillThere={Game1.currentLocation.objects.ContainsKey(tile)} drops=[{string.Join(" ; ", drops)}]";
                }

                case "held": // what the open menu holds on the cursor (a crafting page's crafted item), or the player's cursor slot
                {
                    Item? held = (Game1.activeClickableMenu as StardewValley.Menus.GameMenu)?.GetCurrentPage() is StardewValley.Menus.CraftingPage page
                        ? this.mod.Helper.Reflection.GetField<Item?>(page, "heldItem").GetValue() : Game1.player.CursorSlotItem;
                    return held == null ? "(nothing)" : $"{held.QualifiedItemId} x{held.Stack}";
                }

                case "closemenu":
                    Game1.exitActiveMenu();
                    return "";

                case "click":
                    Game1.activeClickableMenu?.receiveLeftClick(I(1), I(2));
                    return "";

                case "rclick":
                    Game1.activeClickableMenu?.receiveRightClick(I(1), I(2));
                    return "";

                case "hover":
                    Game1.activeClickableMenu?.performHoverAction(I(1), I(2));
                    return "";

                // panel input in art pixels (see mockups: panel is 232x160)
                case "aclick":
                case "arclick":
                case "amouse":
                case "adrag":
                case "akey":
                {
                    if (Game1.activeClickableMenu is not UI.TunerMenu menu)
                        throw new InvalidOperationException("tuner not open");
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    switch (a[0])
                    {
                        case "aclick":
                        {
                            Point p = menu.ScreenOf(F(1), F(2));
                            Game1.setMousePosition(p.X, p.Y, true);
                            menu.receiveLeftClick(p.X, p.Y);
                            menu.releaseLeftClick(p.X, p.Y);
                            return p.ToString();
                        }
                        case "arclick":
                        {
                            Point p = menu.ScreenOf(F(1), F(2));
                            Game1.setMousePosition(p.X, p.Y, true);
                            menu.receiveRightClick(p.X, p.Y);
                            return p.ToString();
                        }
                        case "amouse":
                        {
                            Point p = menu.ScreenOf(F(1), F(2));
                            Game1.setMousePosition(p.X, p.Y, true);
                            return p.ToString();
                        }
                        case "adrag":
                        {
                            Point p1 = menu.ScreenOf(F(1), F(2)), p2 = menu.ScreenOf(F(3), F(4));
                            menu.receiveLeftClick(p1.X, p1.Y);
                            for (int step = 1; step <= 8; step++)
                                menu.leftClickHeld(p1.X + (p2.X - p1.X) * step / 8, p1.Y + (p2.Y - p1.Y) * step / 8);
                            menu.releaseLeftClick(p2.X, p2.Y);
                            Game1.setMousePosition(p2.X, p2.Y, true);
                            return "";
                        }
                        default:
                            menu.receiveKeyPress(Enum.Parse<Microsoft.Xna.Framework.Input.Keys>(a[1]));
                            return "";
                    }
                }

                case "capture":
                {
                    string path = this.Resolve(a[2]);
                    this.mod.Engine.StartCapture(path, double.Parse(a[1], CultureInfo.InvariantCulture));
                    return path;
                }

                case "shot":
                    this.pendingShot = this.Resolve(a[1]);
                    return this.pendingShot;

                case "type": // type into the focused text entry, then press Enter
                    Game1.keyboardDispatcher.Subscriber?.RecieveTextInput(Rest(1));
                    Game1.keyboardDispatcher.Subscriber?.RecieveCommandInput('\r');
                    return "";

                case "lang": // switch game language for this session (not saved to preferences)
                    LocalizedContentManager.CurrentLanguageCode = Enum.Parse<LocalizedContentManager.LanguageCode>(a[1]);
                    return LocalizedContentManager.CurrentLanguageCode.ToString();

                case "tab":
                    if (Game1.activeClickableMenu is UI.TunerMenu tm)
                        tm.DebugSetTab(int.Parse(a[1]));
                    return "";

                // song load <id> <blocks.json> <mix.json> [reach] | song arm <id> | song capture <dir|off>
                // | song source open | song source retro <smu_stream> <rom dir> | song status
                case "song":
                    switch (a[1])
                    {
                        case "load":
                            return this.mod.LoadSong(a[2], this.Resolve(a[3]), this.Resolve(a[4]), a.Length > 5 ? I(5) : 0);
                        case "arm":
                            return this.mod.ArmSong(a[2]);
                        case "capture":
                            this.mod.SongPlayer.CaptureDir = a[2] == "off" ? null : this.Resolve(a[2]);
                            return this.mod.SongPlayer.CaptureDir ?? "off";
                        case "source":
                            this.mod.Config.SongSource = a[2];
                            if (a.Length > 4)
                            {
                                this.mod.Config.RetroHelper = a[3];
                                this.mod.Config.RetroRoms = a[4];
                            }
                            return this.mod.Config.SongSource;
                        case "status":
                            return this.mod.SongPlayer.Status;
                        default:
                            throw new ArgumentException("song load|arm|capture|source|status");
                    }

                case "log":
                    this.mod.Monitor.Log(Rest(1), LogLevel.Info);
                    return "";

                case "exit":
                    Game1.game1.Exit();
                    return "";

                default:
                    throw new ArgumentException("unknown command");
            }
        }

        private string Resolve(string path) => Path.IsPathRooted(path) ? path : Path.Combine(this.dir, path);

        /// <summary>Grab the finished frame just before it is presented.</summary>
        private static void EndDraw_Prefix()
        {
            DebugBridge? self = instance;
            if (self?.pendingShot == null)
                return;
            string path = self.pendingShot;
            self.pendingShot = null;
            try
            {
                GraphicsDevice device = Game1.graphics.GraphicsDevice;
                int w = device.PresentationParameters.BackBufferWidth;
                int h = device.PresentationParameters.BackBufferHeight;
                var data = new Color[w * h];
                device.GetBackBufferData(data);
                for (int i = 0; i < data.Length; i++)
                    data[i].A = 255;
                using var tex = new Texture2D(device, w, h);
                tex.SetData(data);
                using FileStream stream = File.Create(path);
                tex.SaveAsPng(stream, w, h);
                self.Write($"SHOT {path} {w}x{h}");
            }
            catch (Exception ex)
            {
                self.Write($"ERR shot => {ex}");
            }
        }
    }
}
