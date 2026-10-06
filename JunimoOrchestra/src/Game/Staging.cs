using System;
using System.Collections.Generic;
using System.Globalization;

namespace JunimoOrchestra.Game
{
    /// <summary>Parsed staging directions of a block (see <see cref="Data.BlockSettings.Stage"/>), for film sets.</summary>
    internal sealed class Staging
    {
        public static readonly Staging None = new();
        private static readonly Dictionary<string, Staging> Cache = new();

        /// <summary>Invisible until it's first played.</summary>
        public bool Hide { get; private set; }

        /// <summary>Never seen, only heard ("ghost"): nothing drawn, no puffs, no notes floating up.</summary>
        public bool Ghost { get; private set; }

        /// <summary>Vanishes this many game ticks after it's first played (0 = never).</summary>
        public int Bye { get; private set; }

        /// <summary>A Junimo puts its instrument away this many game ticks after it's first played (0 = never)...</summary>
        public int Rest { get; private set; }

        /// <summary>Game ticks per beat of the music (0 = unknown): a Junimo sways to it while it plays and dances to it
        /// once it has put its instrument away ("dance:P" or "dance:P:O"); a classical one breathes and sways to it while
        /// it plays and while it waits for its next entry.</summary>
        public double Dance { get; private set; }

        /// <summary>Game ticks from its first note to a beat (so it moves on the beat; a little extra makes a wave).</summary>
        public double DanceOffset { get; private set; }

        /// <summary>Blocks to play in turn: (tile offset, game ticks later).</summary>
        public IReadOnlyList<(int Dx, int Dy, int Ticks)> Relays { get; private set; } = Array.Empty<(int, int, int)>();

        /// <summary>Stretches (game ticks after it's first played, from-to) when a Junimo holds still, as a band does in a
        /// break: no swaying, no beat, no blinking ("still:A:B"). It still plays any note of its own there.</summary>
        public IReadOnlyList<(int From, int To)> Still { get; private set; } = Array.Empty<(int, int)>();

        /// <summary>Stretches (game ticks after it's first played, from-to) when a classical conductor beats time to its
        /// dance's beat ("conduct:A:B"): baton raised the beat before, down on every beat, held high where it holds still
        /// and after the last.</summary>
        public IReadOnlyList<(int From, int To)> Conduct { get; private set; } = Array.Empty<(int, int)>();

        /// <summary>A classical conductor raises its top hat to us this many game ticks after it's first played (0 = never;
        /// "bow:T").</summary>
        public int Bow { get; private set; }

        /// <summary>Game ticks after it's first played when a Junimo star pops up over it and it jumps for joy ("star:T").</summary>
        public IReadOnlyList<int> Stars { get; private set; } = Array.Empty<int>();

        public bool Any => this.Hide || this.Ghost || this.Bye > 0 || this.Rest > 0 || this.Relays.Count > 0 || this.Still.Count > 0 || this.Conduct.Count > 0
            || this.Bow > 0 || this.Stars.Count > 0;

        /// <summary>Whether the block needs to remember when it was first played.</summary>
        public bool Timed => this.Hide || this.Bye > 0 || this.Rest > 0 || this.Still.Count > 0 || this.Conduct.Count > 0
            || this.Bow > 0 || this.Stars.Count > 0 || this.Dance > 0;

        /// <summary>Whether it holds still at this age (game ticks after it was first played).</summary>
        public bool IsStill(long age)
        {
            foreach ((int from, int to) in this.Still)
                if (age >= from && age < to)
                    return true;
            return false;
        }

        /// <summary>The age its swaying and dancing go by: frozen where it holds still, the true age elsewhere (so it
        /// comes back on the beat).</summary>
        public long MoveAge(long age)
        {
            foreach ((int from, int to) in this.Still)
                if (age >= from && age < to)
                    return from;
            return age;
        }

        public static Staging Parse(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return None;
            if (Cache.TryGetValue(raw, out Staging? cached))
                return cached;

            var stage = new Staging();
            var relays = new List<(int, int, int)>();
            var still = new List<(int, int)>();
            var conduct = new List<(int, int)>();
            var stars = new List<int>();
            foreach (string part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split(':');
                int I(int i) => int.Parse(f[i], CultureInfo.InvariantCulture);
                try
                {
                    switch (f[0])
                    {
                        case "hide":
                            stage.Hide = true;
                            break;
                        case "ghost":
                            stage.Ghost = true;
                            break;
                        case "bye" when f.Length == 2:
                            stage.Bye = Math.Max(0, I(1));
                            break;
                        case "rest" when f.Length == 2:
                            stage.Rest = Math.Max(0, I(1));
                            break;
                        case "dance" when f.Length is 2 or 3:
                            stage.Dance = Math.Max(0, double.Parse(f[1], CultureInfo.InvariantCulture));
                            stage.DanceOffset = f.Length == 3 ? double.Parse(f[2], CultureInfo.InvariantCulture) : 0;
                            break;
                        case "relay" when f.Length == 4:
                            relays.Add((I(1), I(2), Math.Max(0, I(3))));
                            break;
                        case "still" when f.Length == 3:
                            still.Add((Math.Max(0, I(1)), Math.Max(0, I(2))));
                            break;
                        case "conduct" when f.Length == 3:
                            conduct.Add((Math.Max(0, I(1)), Math.Max(0, I(2))));
                            break;
                        case "bow" when f.Length == 2:
                            stage.Bow = Math.Max(0, I(1));
                            break;
                        case "star" when f.Length == 2:
                            stars.Add(Math.Max(0, I(1)));
                            break;
                    }
                }
                catch (FormatException)
                {
                    // ignore a malformed direction, keep the rest
                }
            }
            stage.Relays = relays;
            stage.Still = still;
            stage.Conduct = conduct;
            stage.Stars = stars;
            if (Cache.Count < 4096)
                Cache[raw] = stage;
            return stage;
        }
    }
}
