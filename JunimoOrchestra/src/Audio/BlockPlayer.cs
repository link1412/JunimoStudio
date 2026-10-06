using System;
using System.Collections.Generic;
using JunimoOrchestra.Data;
using Microsoft.Xna.Framework;
using StardewValley;

namespace JunimoOrchestra.Audio
{
    /// <summary>Turns a block's settings into scheduled synth notes (tempo, transpose, spatial mix).</summary>
    internal sealed class BlockPlayer
    {
        /// <summary>Group id for panel previews, so a preview cuts off the previous preview.</summary>
        public const long PreviewGroup = long.MinValue + 1;

        private readonly SynthEngine engine;
        private readonly Func<ModConfig> config;
        private readonly Func<WorldSettings> world;

        public BlockPlayer(SynthEngine engine, Func<ModConfig> config, Func<WorldSettings> world)
        {
            this.engine = engine;
            this.config = config;
            this.world = world;
        }

        /// <summary>A stable id for the block at a tile in a location.</summary>
        public static long GroupFor(GameLocation location, Vector2 tile)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                foreach (char c in location.NameOrUniqueName)
                    h = (h ^ c) * 1099511628211L;
                h = (h ^ (long)tile.X) * 1099511628211L;
                h = (h ^ (long)tile.Y) * 1099511628211L;
                return h;
            }
        }

        /// <summary>Play a placed block, mixed for where the local player is standing.</summary>
        /// <returns>The note start times in seconds (for visuals), or null if nothing was played.</returns>
        public IReadOnlyList<double>? PlayBlock(GameLocation location, Vector2 tile, BlockSettings settings)
        {
            (float gain, float pan) = this.Spatial(location, tile);
            if (gain <= 0.01f)
                return null;
            return this.Play(GroupFor(location, tile), settings, gain, pan, Game1.ticks);
        }

        /// <summary>Play settings without spatial mixing (panel preview).</summary>
        public IReadOnlyList<double>? Preview(BlockSettings settings)
            => this.Play(PreviewGroup, settings, 1f, 0f);

        public void StopPreview() => this.engine.StopGroup(PreviewGroup);

        public void StopBlock(GameLocation location, Vector2 tile) => this.engine.StopGroup(GroupFor(location, tile));

        public IReadOnlyList<double>? Play(long group, BlockSettings settings, float gain, float pan, long? gameTick = null)
        {
            if (!this.engine.IsReady || settings.Notes.Count == 0)
                return null;

            WorldSettings world = this.world();
            int tempo = settings.EffectiveTempo(world.DefaultTempo);
            double secondsPerTick = 60.0 / tempo / BlockSettings.Ppq;
            int transpose = settings.IsDrumKit ? 0 : world.Transpose;

            var notes = new List<ScheduledNote>(settings.Notes.Count);
            var starts = new List<double>(settings.Notes.Count);
            foreach (BlockNote n in settings.Notes)
            {
                int key = Math.Clamp(n.Pitch + transpose, 0, 127);
                double start = n.Delay * secondsPerTick;
                double length = n.Duration * secondsPerTick;
                notes.Add(new ScheduledNote(key, n.Velocity,
                    (long)Math.Round(start * SynthEngine.SampleRate),
                    (long)Math.Round(length * SynthEngine.SampleRate)));
                starts.Add(start);
            }

            // expression carries the distance fade; quantised so nearby blocks can share synth channels
            int expression = Math.Clamp((int)Math.Round(gain * 127 / 8f) * 8, 8, 127);
            int panValue = Math.Clamp(settings.Pan + (int)Math.Round(pan * 48), 0, 127);
            var parameters = new ChannelParams(
                Bank: settings.Bank,
                Program: settings.Program,
                Volume: settings.Volume,
                Expression: expression,
                Reverb: settings.Reverb,
                Chorus: settings.Chorus,
                Vibrato: settings.Vibrato,
                Pan: panValue);

            this.engine.PlayPhrase(group, parameters, notes, gameTick);
            return starts;
        }

        /// <summary>Distance fade and stereo position relative to the local player.</summary>
        private (float gain, float pan) Spatial(GameLocation location, Vector2 tile)
        {
            if (!this.config().SpatialAudio || Game1.player == null || Game1.currentLocation != location)
                return (1f, 0f);
            Vector2 listener = Game1.player.getStandingPosition() / 64f;
            Vector2 source = tile + new Vector2(0.5f, 0.5f);
            float distance = Vector2.Distance(listener, source);
            const float full = 6f, silent = 24f;
            float gain = distance <= full ? 1f : Math.Max(0f, 1f - (distance - full) / (silent - full));
            gain *= gain; // perceptually smoother fade
            float pan = Math.Clamp((source.X - listener.X) / 10f, -1f, 1f);
            return (gain, pan);
        }
    }
}
