using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Junimo.Engine.Performance;
using JunimoOrchestra.Audio;
using JunimoOrchestra.Data;
using JunimoOrchestra.Dev;
using JunimoOrchestra.Game;
using JunimoOrchestra.Songs;
using JunimoOrchestra.UI;
using JunimoOrchestra.UI.Pix;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SObject = StardewValley.Object;

namespace JunimoOrchestra
{
    internal sealed class ModEntry : Mod
    {
        public const string SoundFontFile = "assets/soundfonts/GeneralUser-GS.sf2";

        internal ModConfig Config = null!;
        internal WorldSettings World = new();
        internal SynthEngine Engine = null!;
        internal BlockPlayer Player = null!;
        internal BlockVisuals Visuals = null!;
        internal ContentHooks ContentHooks = null!;
        internal CustomPacks Packs = null!;
        internal SoundNames Names = null!;
        internal SongLibrary Songs = new();
        internal SongPlayer SongPlayer = null!;

        private BlockTriggers triggers = null!;
        private HoverLabel label = null!;
        internal MoveSpeed Speed = null!;
        private DebugBridge? debug;
        private Texture2D? blocksTexture;
        private Texture2D? orchestraTexture;
        private Texture2D? classicalTexture;
        private Texture2D? bandTexture;
        private Texture2D? uiAtlas;
        private Room? appliedRoom;
        private float appliedGain = -1;
        private bool reportedLoadError;
        private FontProblem? reportedFontProblem;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.Packs = new CustomPacks(helper, this.Monitor);

            this.Engine = new SynthEngine(message => this.Monitor.Log(message, LogLevel.Debug));
            this.ApplySoundFont();
            this.Player = new BlockPlayer(this.Engine, () => this.Config, () => this.World);
            this.SongPlayer = new SongPlayer(() => this.Config, () => this.Engine.Font, () => this.Engine.LoadError,
                message => this.Monitor.Log(message, LogLevel.Info));
            AppDomain.CurrentDomain.ProcessExit += (_, _) => this.SongPlayer.StopAll();   // the retro helper goes too
            this.ContentHooks = new ContentHooks(helper, this.Packs);
            this.Names = new SoundNames(helper.Translation, this.Engine);
            this.Visuals = new BlockVisuals(() => this.BlocksTexture, () => this.OrchestraTexture, () => this.ClassicalTexture, () => this.BandTexture);
            this.triggers = new BlockTriggers((l, t, o, f, p) => this.FireBlock(l, t, o, f, p), this.FireSongBlock, this.Songs, () => this.World.DefaultReach);
            this.label = new HoverLabel(this.Names);
            this.Speed = new MoveSpeed(() => this.Config, this.Names, helper.Input);
            Patches.Apply(this);
            this.debug = DebugBridge.TryCreate(this);
            if (this.debug != null)
                helper.Events.GameLoop.UpdateTicked += (_, _) => this.debug.Update();

            helper.Events.GameLoop.GameLaunched += (_, _) => Integrations.GenericModConfigMenu.Register(this);
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += (_, _) => this.LearnRecipes();
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Player.Warped += (_, e) =>
            {
                if (e.IsLocalPlayer)
                    this.ArmFor(e.NewLocation);
            };
            helper.Events.Display.RenderedWorld += (_, e) =>
            {
                if (this.Config.ShowLabels && Context.IsWorldReady)
                    this.label.DrawReach(e.SpriteBatch, this.World.DefaultReach);
            };
            helper.Events.Display.RenderedHud += (_, e) =>
            {
                if (!Context.IsWorldReady)
                    return;
                if (this.Config.ShowLabels)
                    this.label.Draw(e.SpriteBatch);
                this.Speed.Draw(e.SpriteBatch);
            };
            helper.Events.Input.ButtonsChanged += (_, _) => this.Speed.OnButtonsChanged();
            helper.Events.Content.AssetsInvalidated += (_, e) =>
            {
                if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(ContentHooks.BlocksTexture)))
                    this.blocksTexture = null;
                if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(ContentHooks.UiTexture)))
                    this.uiAtlas = null;
                if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(ContentHooks.OrchestraTexture)))
                    this.orchestraTexture = null;
                if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(ContentHooks.ClassicalTexture)))
                    this.classicalTexture = null;
                if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(ContentHooks.BandTexture)))
                    this.bandTexture = null;
            };
        }

        /// <summary>Staged blocks waiting to be played by a neighbour's relay: (game tick, location, tile).</summary>
        private readonly List<(long Due, GameLocation Location, Vector2 Tile)> relays = new();

        internal Texture2D BlocksTexture => this.blocksTexture ??= this.Helper.GameContent.Load<Texture2D>(ContentHooks.BlocksTexture);
        /// <summary>Junimo musicians' instruments (film sets only; see <see cref="OrchestraArt"/>).</summary>
        internal Texture2D OrchestraTexture => this.orchestraTexture ??= this.Helper.GameContent.Load<Texture2D>(ContentHooks.OrchestraTexture);
        /// <summary>Junimos in concert dress playing each family's classical instrument (the "classical" look; see <see cref="ClassicalArt"/>).</summary>
        internal Texture2D ClassicalTexture => this.classicalTexture ??= this.Helper.GameContent.Load<Texture2D>(ContentHooks.ClassicalTexture);
        /// <summary>The game's own Junimo with each family's instrument (the "band" look; see <see cref="ModernArt"/>).</summary>
        internal Texture2D BandTexture => this.bandTexture ??= this.Helper.GameContent.Load<Texture2D>(ContentHooks.BandTexture);
        internal Texture2D UiAtlas => this.uiAtlas ??= this.Helper.GameContent.Load<Texture2D>(ContentHooks.UiTexture);

        internal void SaveConfig() => this.Helper.WriteConfig(this.Config);

        /****
        ** SoundFonts
        ****/
        /// <summary>The file a <see cref="ModConfig.SoundFont"/> setting means: a content pack's, a full path, or else
        /// one in the mod's folder (for a pack that has gone, a file that isn't there: the built-in one plays and the
        /// panel says why).</summary>
        internal string SoundFontPath(string choice)
            => string.IsNullOrWhiteSpace(choice) ? Path.Combine(this.Helper.DirectoryPath, SoundFontFile)
                : Path.IsPathRooted(choice) ? choice
                : this.Packs.SoundFontPath(choice) ?? Path.Combine(this.Helper.DirectoryPath, choice);

        /// <summary>The SoundFonts to choose from: the built-in one ("") and the content packs' ("{pack}/{file}").</summary>
        internal List<string> SoundFontChoices()
        {
            var choices = new List<string> { "" };
            choices.AddRange(this.Packs.SoundFonts());
            if (!choices.Contains(this.Config.SoundFont))
                choices.Add(this.Config.SoundFont);   // a full path, or a file that has gone
            return choices;
        }

        /// <summary>Play the SoundFont the config names, if it isn't the one playing (or loading) already.</summary>
        internal void ApplySoundFont()
        {
            string path = this.SoundFontPath(this.Config.SoundFont);
            if (path == this.requestedFont)
                return;
            this.requestedFont = path;
            this.reportedFontProblem = null;
            this.Engine.BeginLoad(path, this.SoundFontPath(""));
        }

        private string? requestedFont;

        /// <summary>A SoundFont problem in the player's language.</summary>
        internal string Describe(FontProblem problem)
            => this.Helper.Translation.Get($"font.problem.{problem.Kind}", new { detail = problem.Detail });

        /****
        ** Blocks
        ****/
        /// <summary>A block was stepped next to (or triggered some other way).</summary>
        /// <param name="relayed">A staged block passed it its turn: it plays whoever may set it off.</param>
        internal void FireBlock(GameLocation location, Vector2 tile, SObject obj, Family family, bool byPlayer, bool relayed = false)
        {
            BlockSettings settings = BlockSettings.Read(obj, family);
            if (!relayed && (byPlayer ? !settings.TriggerByPlayers : !settings.TriggerByCreatures))
                return;
            var starts = this.Player.PlayBlock(location, tile, settings);
            if (location == Game1.currentLocation)
                this.Visuals.Played(location, tile, obj, family.Accent);
            if (starts != null && location == Game1.currentLocation)
                this.ShowPlaying(location, tile, family, starts, settings);
            foreach ((int dx, int dy, int ticks) in Staging.Parse(settings.Stage).Relays)
                this.relays.Add((Game1.ticks + ticks, location, tile + new Vector2(dx, dy)));
        }

        /// <summary>Play the blocks that staged blocks passed their turn to, once their moment comes.</summary>
        private void RunRelays()
        {
            for (int guard = 0; guard < 64 && this.relays.Count > 0; guard++)
            {
                var due = this.relays.FindAll(r => r.Due <= Game1.ticks);
                if (due.Count == 0)
                    return;
                this.relays.RemoveAll(r => r.Due <= Game1.ticks);
                foreach ((long _, GameLocation location, Vector2 tile) in due)
                {
                    if (location.objects.TryGetValue(tile, out SObject? obj) && !obj.bigCraftable.Value
                        && Families.FromItemId(obj.ItemId) is Family family)
                        this.FireBlock(location, tile, obj, family, true, relayed: true);
                }
            }
        }

        /// <summary>A song block was passed: it plays its part of the song.</summary>
        internal void FireSongBlock(GameLocation location, Vector2 tile, SObject obj, (string Song, int Block) at)
        {
            Song? song = this.Songs.Get(at.Song);
            if (song == null || !song.ById.TryGetValue(at.Block, out Block? block))
            {
                this.Monitor.LogOnce($"A block plays {at.Song} #{at.Block}, which isn't loaded.", LogLevel.Warn);
                return;
            }
            this.SongPlayer.Trigger(song, block, Game1.ticks);
            foreach ((int dx, int dy, int ticks) in Staging.Parse(BlockSettings.StageOf(obj)).Relays)
                this.relays.Add((Game1.ticks + ticks, location, tile + new Vector2(dx, dy)));
            if (location != Game1.currentLocation || Patches.BlockFamily(obj) is not Family family)
                return;
            this.Visuals.Played(location, tile, obj, family.Accent);
            // its notes, in seconds after now: the song's output plays the sample of the frame on screen
            var clock = new ExactClock(song.Blocks.Ppq, block.Tempo);
            double Seconds(long dt) => (block.Lead + clock.Numerator(dt) / (double)clock.Denominator) / ExactClock.Rate;
            var notes = block.Parts.Values.SelectMany(p => p.Notes).OrderBy(n => n.Dt).ToList();
            if (notes.Count == 0)
                return;
            var starts = notes.Select(n => Seconds(n.Dt)).ToList();
            this.Visuals.Pulse(location, tile, starts, notes.Select(n => Seconds(n.Dt + n.Length) - Seconds(n.Dt)).ToList(),
                notes.Select(n => n.Key).ToList(), latencyMs: 0, beat: Seconds(song.Blocks.Ppq) - Seconds(0));
            if (this.Config.NoteParticles && !BlockVisuals.IsJunimo(BlockSettings.LookOf(obj))   // its instrument is the effect
                && !Staging.Parse(BlockSettings.StageOf(obj)).Ghost)                            // (a ghost has none)
                this.Visuals.Particles(location, tile, starts, family.Accent, latencyMs: 0);
        }

        /// <summary>Load a song from its blocks and mix (dev, until songs can be made in the game).</summary>
        internal string LoadSong(string id, string blocksPath, string mixPath, int reach = 0)
        {
            Song song = this.Songs.Load(id, blocksPath, mixPath, reach);
            return $"{song.Id}: {song.Blocks.Blocks.Count} blocks, {song.Mix.Strips.Count} strips, reach {song.Reach}";
        }

        /// <summary>Get a fresh run of a song ready (the retro source powers its MU2000s on).</summary>
        internal string ArmSong(string id)
        {
            Song song = this.Songs.Get(id) ?? throw new ArgumentException($"no song {id}");
            this.SongPlayer.Arm(song);
            return this.SongPlayer.Status;
        }

        /// <summary>Arm a run of the first song whose blocks are in a location, so she can start whenever she likes.</summary>
        private void ArmFor(GameLocation? location)
        {
            if (location == null || this.Songs.MaxReach == 0)
                return;
            foreach (SObject obj in location.objects.Values)
            {
                if (SongLibrary.RefOf(obj) is { } at && this.Songs.Get(at.Song) is Song song)
                {
                    this.SongPlayer.Arm(song);
                    return;
                }
            }
        }

        /// <summary>The player right-clicked a block: open the tuner.</summary>
        internal void OnBlockActivated(SObject obj, Family family, Farmer who)
        {
            if (who != Game1.player || Game1.activeClickableMenu != null || SongLibrary.IsSongBlock(obj))
                return;
            GameLocation location = obj.Location ?? Game1.currentLocation;
            Game1.activeClickableMenu = new TunerMenu(this, location, obj, family);
        }

        private void ShowPlaying(GameLocation location, Vector2 tile, Family family, IReadOnlyList<double> starts, BlockSettings settings)
        {
            double secondsPerTick = 60.0 / settings.EffectiveTempo(this.World.DefaultTempo) / BlockSettings.Ppq;
            this.Visuals.Pulse(location, tile, starts, settings.Notes.Select(n => n.Duration * secondsPerTick).ToList(),
                settings.Notes.Select(n => n.Pitch).ToList(), beat: secondsPerTick * BlockSettings.Ppq);
            if (this.Config.NoteParticles && !BlockVisuals.IsJunimo(settings.Look) && !Staging.Parse(settings.Stage).Ghost)
                this.Visuals.Particles(location, tile, starts, family.Accent);
        }

        /// <summary>The blocks' recipes from the start; the Junimos' once the Community Center is restored (they're the
        /// Junimos' thanks), with a word that they've come.</summary>
        internal void LearnRecipes()
        {
            if (!this.Config.AutoLearnRecipes || !Context.IsWorldReady)
                return;
            foreach (Family family in Families.All)
                Game1.player.craftingRecipes.TryAdd(family.ItemId, 0);
            if (!GameStateQuery.CheckConditions("IS_COMMUNITY_CENTER_COMPLETE"))
                return;
            bool learned = false;
            foreach (JunimoKind kind in JunimoKinds.All)
                learned |= Game1.player.craftingRecipes.TryAdd(kind.ItemId, 0);
            if (learned)
                Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.junimos-learned")) { messageSubject = ItemRegistry.Create(JunimoKinds.Musician.QualifiedItemId) });
        }

        /****
        ** Events
        ****/
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!this.Engine.IsReady && this.Engine.LoadError == null)
                this.Engine.TryStart();
            else if (this.Engine.LoadError != null && !this.reportedLoadError)
            {
                this.reportedLoadError = true;
                this.Monitor.Log($"Instrument blocks will be silent: {this.Engine.LoadError}", LogLevel.Error);
            }
            // the chosen SoundFont couldn't be loaded and the built-in one plays: say so once, where the player sees it
            if (!this.Engine.IsLoading && this.Engine.FontProblem is FontProblem problem && problem != this.reportedFontProblem && Context.IsWorldReady)
            {
                this.reportedFontProblem = problem;
                this.Monitor.Log($"Playing the built-in SoundFont instead: {problem}", LogLevel.Warn);
                Game1.addHUDMessage(new HUDMessage(this.Helper.Translation.Get("hud.font-problem",
                    new { file = problem.File, problem = this.Describe(problem) }), HUDMessage.error_type));
            }

            if (this.Engine.IsReady)
            {
                float gain = this.Config.MasterVolume / 127f * Game1.options.soundVolumeLevel;
                if (gain != this.appliedGain)
                {
                    this.Engine.SetGain(gain);
                    this.appliedGain = gain;
                }
                if (this.appliedRoom != this.World.Room)
                {
                    this.Engine.SetRoom(this.World.Room);
                    this.appliedRoom = this.World.Room;
                }
            }

            if (Context.IsWorldReady)
            {
                this.triggers.Update();
                if (this.relays.Count > 0)
                    this.RunRelays();
                if (e.IsMultipleOf(60))
                    this.Visuals.Prune();
                // after this frame's blocks: the songs' audio may go on up to the next frame
                this.SongPlayer.Update(Game1.ticks, this.Config.MasterVolume / 127f * Game1.options.soundVolumeLevel);
                if (this.SongPlayer.RunEnded)
                    this.ArmFor(Game1.currentLocation);
            }
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.World = (Context.IsMainPlayer ? this.Helper.Data.ReadSaveData<WorldSettings>(WorldSettings.SaveKey) : null) ?? new WorldSettings();
            this.World.Normalize();
            this.LearnRecipes();
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            if (Context.IsMainPlayer)
                this.Helper.Data.WriteSaveData(WorldSettings.SaveKey, this.World);
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.Engine.StopAll();
            this.SongPlayer.StopAll();
            this.triggers.Reset();
            this.Speed.Reset();
            this.relays.Clear();
            this.Visuals.Clear();
            this.World = new WorldSettings();
            SpriteCache.Clear();
        }
    }
}
