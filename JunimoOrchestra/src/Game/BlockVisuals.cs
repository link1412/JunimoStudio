using System;
using System.Collections.Generic;
using System.Linq;
using JunimoOrchestra.Audio;
using JunimoOrchestra.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using SObject = StardewValley.Object;

namespace JunimoOrchestra.Game
{
    /// <summary>Draws placed blocks: a little hop on every note. No squash &amp; stretch (scaled off its whole pixels the
    /// art shimmers, and with a note on every step that read as shaking) and no white flash (dozens of blocks flashing
    /// a few times a second is hard on the eyes; the hop shows which one sounds).</summary>
    internal sealed class BlockVisuals
    {
        private const double HopSeconds = 0.28;
        private const double HopPixels = 3;   // at the top of the hop (it was 6, with 16% squash: too much)

        /// <summary>When each block's notes start and end (seconds of game time, as heard), and their pitches.</summary>
        private readonly Dictionary<(string Location, Vector2 Tile), PlayedNotes> notes = new();

        /// <summary>Game tick each staged block (hide/bye) was first played on.</summary>
        private readonly Dictionary<(string Location, Vector2 Tile), long> firstPlayed = new();
        private readonly Dictionary<(string Look, int X, int Y), Crop> crops = new();
        private readonly Func<Texture2D> texture;
        private readonly Func<Texture2D> orchestra;
        private readonly Func<Texture2D> classical;
        private readonly Func<Texture2D> band;

        public BlockVisuals(Func<Texture2D> texture, Func<Texture2D> orchestra, Func<Texture2D> classical, Func<Texture2D> band)
        {
            this.texture = texture;
            this.orchestra = orchestra;
            this.classical = classical;
            this.band = band;
        }

        private static double Now => Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0;

        /// <summary>Remember when a block's notes start (and end, and what they are) so it can move in time with the audio.</summary>
        /// <param name="latencyMs">How long after now the audio of time 0 is heard (default: the block synth's).</param>
        /// <param name="beat">A beat of its tempo (seconds; 0 if unknown): a wind player's phrase ends where a beat's rest
        /// follows.</param>
        public void Pulse(GameLocation location, Vector2 tile, IReadOnlyList<double> starts, IReadOnlyList<double>? lengths = null,
            IReadOnlyList<int>? pitches = null, double? latencyMs = null, double beat = 0)
        {
            double t0 = Now + (latencyMs ?? SynthEngine.LatencyMs) / 1000.0;
            int[] order = Enumerable.Range(0, starts.Count).OrderBy(i => starts[i]).ToArray();
            var times = new double[order.Length];
            var ends = new double[order.Length];
            var keys = new int[order.Length];
            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k];
                times[k] = t0 + starts[i];
                ends[k] = times[k] + (lengths != null && i < lengths.Count ? lengths[i] : 0);
                keys[k] = pitches != null && i < pitches.Count ? pitches[i] : 60;
            }
            this.notes[(location.NameOrUniqueName, tile)] = new PlayedNotes(times, ends, keys, beat);
        }

        /// <summary>Little tinted notes that float up from the block as each note starts.</summary>
        public void Particles(GameLocation location, Vector2 tile, IReadOnlyList<double> starts, Color accent, double? latencyMs = null)
        {
            double latency = (latencyMs ?? SynthEngine.LatencyMs) / 1000.0;
            int count = Math.Min(starts.Count, 12);
            for (int i = 0; i < count; i++)
            {
                int kind = Game1.random.NextDouble() < 0.7 ? 0 : 1;
                float drift = (float)(Game1.random.NextDouble() - 0.5) * 1.2f;
                var sprite = new TemporaryAnimatedSprite(ContentHooks.FxTexture, new Rectangle(kind * 9, 0, 9, 9), 9999f, 1, 1,
                    tile * 64f + new Vector2(18 + Game1.random.Next(12), -8), false, drift < 0, (tile.Y * 64 + 80) / 10000f,
                    0.012f, Color.Lerp(accent, Color.White, 0.15f), 3f, 0f, 0f, 0f)
                {
                    motion = new Vector2(drift, -1.6f),
                    acceleration = new Vector2(0, 0.02f),
                    delayBeforeAnimationStart = SpriteDelay(starts[i] + latency)
                };
                location.temporarySprites.Add(sprite);
            }
        }

        /// <summary>Whether a look is one of the Junimo items' (the classical set's, a Junimo musician's, or a band Junimo's):
        /// its instrument is the effect, so no notes float up from it.</summary>
        public static bool IsJunimo(string? look) => ClassicalArt.IsLook(look) || look == OrchestraArt.MusicianLook || ModernArt.IsLook(look);

        public void Clear()
        {
            this.notes.Clear();
            this.firstPlayed.Clear();
            this.crops.Clear();
        }

        /// <summary>A block was played: staged blocks appear (or schedule their exit) with a puff; a Junimo's instrument
        /// gathers out of swirling leaves, and blows away as leaves again when it rests.</summary>
        public void Played(GameLocation location, Vector2 tile, SObject obj, Color accent)
        {
            Staging stage = Staging.Parse(BlockSettings.StageOf(obj));
            if (stage.Ghost || !stage.Timed || !this.firstPlayed.TryAdd((location.NameOrUniqueName, tile), Game1.ticks))
                return;
            if (Patches.BlockFamily(obj) is not Family own)
                return;
            string? look = BlockSettings.LookOf(obj);
            var junimo = OrchestraArt.Resolve(look, obj, own);
            var classical = ClassicalArt.Resolve(look, obj, own);
            Color tint = junimo?.Tint ?? accent;
            bool musician = junimo?.Instrument != null || classical != null;
            Vector2 seat = tile + (junimo?.Offset ?? classical?.Offset ?? Vector2.Zero) / 64f;
            if (stage.Hide)
            {
                if (musician)
                    OrchestraArt.Leaves(location, seat, true, 0);
                else
                    Puff(location, tile, tint, 0);
            }
            if (stage.Rest > 0 && musician)
                OrchestraArt.Leaves(location, seat, false, SpriteDelay(stage.Rest / 60.0));
            foreach (int at in stage.Stars)
                OrchestraArt.Star(location, seat, SpriteDelay(at / 60.0));
            if (stage.Bye > 0)
                Puff(location, tile, tint, SpriteDelay(stage.Bye / 60.0));
        }

        /// <summary>A temporary sprite's delay for something due this many seconds from now. The sprite counts its delay
        /// down by each frame's whole milliseconds (16 for a 16.67 ms frame), so a delay in true milliseconds runs 4% slow
        /// (a star due 11 s on came 28 frames late): this one is in the sprite's own milliseconds, whole frames of them.</summary>
        internal static int SpriteDelay(double seconds)
        {
            TimeSpan frame = Game1.currentGameTime?.ElapsedGameTime is { Ticks: > 0 } t ? t : TimeSpan.FromTicks(166667);
            return (int)Math.Round(seconds / frame.TotalSeconds) * frame.Milliseconds;
        }

        /// <summary>Game ticks since a staged block was first played, or null if it hasn't been.</summary>
        private long? Age(SObject obj, Vector2 tile)
        {
            string location = (obj.Location ?? Game1.currentLocation)?.NameOrUniqueName ?? "";
            return this.firstPlayed.TryGetValue((location, tile), out long at) ? Game1.ticks - at : null;
        }

        /// <summary>Whether a staged block is on stage right now (hidden before it's played, gone after its exit, never if
        /// it's a ghost).</summary>
        private bool OnStage(SObject obj, Vector2 tile)
        {
            Staging stage = Staging.Parse(BlockSettings.StageOf(obj));
            if (stage.Ghost)
                return false;
            if (!(stage.Hide || stage.Bye > 0))
                return true;
            string location = (obj.Location ?? Game1.currentLocation)?.NameOrUniqueName ?? "";
            if (!this.firstPlayed.TryGetValue((location, tile), out long at))
                return !stage.Hide;
            return stage.Bye <= 0 || Game1.ticks - at < stage.Bye;
        }

        /// <summary>A little puff of smoke and stars in the block's colour (no sound).</summary>
        private static void Puff(GameLocation location, Vector2 tile, Color tint, int delayMs)
        {
            Vector2 at = tile * 64f;
            location.temporarySprites.Add(new TemporaryAnimatedSprite(5, at, Color.Lerp(tint, Color.White, 0.5f), 8, false, 60f, 0, -1, (tile.Y * 64 + 70) / 10000f, -1, delayMs));
            for (int i = 0; i < 3; i++)
            {
                var star = new TemporaryAnimatedSprite("Characters\\Junimo", new Rectangle(0, 112, 16, 16), 50f, 8, 0,
                    at + new Vector2(Game1.random.Next(-8, 40), Game1.random.Next(-24, 16)), false, false, (tile.Y * 64 + 72) / 10000f,
                    0.02f, Color.White, 2f, 0f, 0f, 0f)
                {
                    motion = new Vector2((float)(Game1.random.NextDouble() - 0.5) * 2f, -1.5f),
                    delayBeforeAnimationStart = delayMs + i * 40
                };
                location.temporarySprites.Add(star);
            }
        }

        /// <summary>What a block's notes are doing right now (for a Junimo musician).</summary>
        private NoteState Latest(GameLocation? location, Vector2 tile)
        {
            if (location == null || !this.notes.TryGetValue((location.NameOrUniqueName, tile), out var played))
                return NoteState.None;
            double now = Now;
            for (int i = played.Starts.Length - 1; i >= 0; i--)
            {
                if (played.Starts[i] <= now)
                    return new NoteState(played, now, i);
            }
            return new NoteState(played, now, -1);
        }

        /// <summary>Drop finished animations (call occasionally).</summary>
        public void Prune()
        {
            double now = Now;
            List<(string, Vector2)>? dead = null;
            foreach (var pair in this.notes)
            {
                double[] starts = pair.Value.Starts;
                if (starts.Length == 0 || (now - starts[^1] > HopSeconds && now > pair.Value.LastEnd + 3))   // its flourish over
                    (dead ??= new()).Add(pair.Key);
            }
            if (dead != null)
                foreach (var key in dead)
                    this.notes.Remove(key);
        }

        /// <summary>Hop offset (pixels, negative = up) for a block right now.</summary>
        public float Sample(GameLocation? location, Vector2 tile)
        {
            if (location == null || !this.notes.TryGetValue((location.NameOrUniqueName, tile), out var played))
                return 0;
            double[] times = played.Starts;

            // the most recent note that has started drives the animation
            double now = Now;
            double dt = -1;
            for (int i = times.Length - 1; i >= 0; i--)
            {
                if (times[i] <= now)
                {
                    dt = now - times[i];
                    break;
                }
            }
            if (dt < 0 || dt > HopSeconds)
                return 0;
            return (float)(-Math.Sin(Math.PI * dt / HopSeconds) * HopPixels);
        }

        /// <summary>The grown crop a "crop:<seed id>" look draws (made once per tile).</summary>
        private Crop? CropLook(string? look, GameLocation? location, int x, int y)
        {
            if (look == null || !look.StartsWith("crop:", StringComparison.Ordinal))
                return null;
            var key = (look, x, y);
            if (!this.crops.TryGetValue(key, out Crop? crop))
            {
                crop = new Crop(look[5..], x, y, location ?? Game1.currentLocation);
                crop.growCompletely();
                this.crops[key] = crop;
            }
            return crop;
        }

        /// <summary>Replacement for <see cref="SObject.draw(SpriteBatch,int,int,float)"/> for a placed block.</summary>
        public void Draw(SObject obj, Family family, SpriteBatch b, int x, int y, float alpha)
        {
            if (!this.OnStage(obj, new Vector2(x, y)))
                return;
            Rectangle box = obj.GetBoundingBoxAt(x, y);
            string? look = BlockSettings.LookOf(obj);
            var junimo = OrchestraArt.Resolve(look, obj, family);
            Crop? crop = this.CropLook(look, obj.Location, x, y);
            var classical = ClassicalArt.Resolve(look, obj, family);
            bool band = ModernArt.IsLook(look);
            if (obj.Fragility != 2 && junimo == null && crop == null && classical == null && !band)
            {
                b.Draw(Game1.shadowTexture, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 55)),
                    Game1.shadowTexture.Bounds, Color.White * alpha, 0f,
                    new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), 4f,
                    SpriteEffects.None, box.Bottom / 15000f);
            }

            float hop = this.Sample(obj.Location, new Vector2(x, y));
            Vector2 shake = obj.shakeTimer > 0 ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2)) : Vector2.Zero;
            Vector2 bottom = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 64 + hop)) + shake;
            Vector2 scale = new(4f, 4f);
            float layer = (obj.isPassable() ? box.Top : box.Center.Y) / 10000f;
            var effects = obj.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // a Junimo in concert dress playing its family's classical instrument as its notes sound (or dancing once it's
            // put it away)
            if (classical is var (cellFamily, offset))
            {
                Staging stage = Staging.Parse(BlockSettings.StageOf(obj));
                long age = this.Age(obj, new Vector2(x, y)) ?? 0;
                float grow = stage.Hide ? Math.Min(1f, 0.35f + age / 10f) : 1f;
                NoteState note = this.Latest(obj.Location, new Vector2(x, y));
                layer += offset.Y / 10000f;
                if (stage.Rest > 0 && age >= stage.Rest)
                {
                    OrchestraArt.Draw(b, this.orchestra(), x, y, ClassicalArt.Seat(cellFamily) + offset, alpha, layer,
                        ClassicalArt.Tint(cellFamily), null, note, 0f, age, stage, grow);
                }
                else
                {
                    // blinks go by its own clock when staged, so every take of a scene blinks alike
                    long clock = stage.Timed ? age : Game1.ticks;
                    bool blink = !stage.IsStill(age) && (clock + x * 37 + y * 91) % 197 < 7;
                    int frame = ClassicalArt.ConductFrame(stage, age) ?? ClassicalArt.FrameOf(note, blink, cellFamily, stage.Dance > 0);
                    var (lean, lift, squash) = ClassicalArt.Motion(cellFamily, stage, age, note, x, y);
                    ClassicalArt.Draw(b, this.classical(), cellFamily, x, y, offset, alpha, layer, frame, grow, lean, lift, squash);
                }
                return;
            }

            // the game's own Junimo with its family's instrument, playing it as its notes sound
            if (band)
            {
                Staging stage = Staging.Parse(BlockSettings.StageOf(obj));
                long age = this.Age(obj, new Vector2(x, y)) ?? 0;
                long clock = stage.Timed ? age : Game1.ticks;
                bool blink = (clock + x * 37 + y * 91) % 197 < 7;
                NoteState note = this.Latest(obj.Location, new Vector2(x, y));
                int frame = ClassicalArt.FrameOf(note, blink);
                var (lean, lift) = ModernArt.Motion(family, stage, age, note, x, y);
                ModernArt.Draw(b, this.band(), family, x, y, alpha, layer, frame, lean, lift);
                return;
            }

            // a Junimo playing a real instrument (or dancing once it's put it away)
            if (junimo is var (junimoTint, instrument, seat))
            {
                Staging stage = Staging.Parse(BlockSettings.StageOf(obj));
                long age = this.Age(obj, new Vector2(x, y)) ?? 0;
                float grow = stage.Hide ? Math.Min(1f, 0.35f + age / 10f) : 1f;
                OrchestraArt.Draw(b, this.orchestra(), x, y, seat, alpha, layer, junimoTint, instrument,
                    this.Latest(obj.Location, new Vector2(x, y)), hop, age, stage, grow);
                return;
            }

            // set dressing: a crop grown in the ground ("crop:<seed id>"), hopping with its notes
            if (crop != null)
            {
                crop.drawWithOffset(b, new Vector2(x, y), Color.White * alpha, 0f, new Vector2(32, 32 + hop));
                return;
            }

            // set dressing: draw another item's sprite (it still hops with its notes)
            if (look != null && ItemRegistry.GetData(look) is { } lookData)
            {
                Rectangle src = lookData.GetSourceRect();
                b.Draw(lookData.GetTexture(), bottom, src, Color.White * alpha, 0f,
                    new Vector2(src.Width / 2f, src.Height), scale * (16f / Math.Max(16, src.Width)), effects, layer);
                return;
            }

            b.Draw(this.texture(), bottom, new Rectangle(family.Index * 16, 0, 16, 16), Color.White * alpha, 0f,
                new Vector2(8, 16), scale, effects, layer);
        }
    }
}
