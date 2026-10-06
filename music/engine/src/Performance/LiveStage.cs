using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Junimo.Engine.Mixing;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// A <see cref="Stage"/> played live: the game's thread triggers blocks as she passes them and says when it has
    /// finished each frame, the audio thread renders. Once frame f is finished no block can be triggered before frame
    /// f + 1, and every block starts at least its lead after its frame, so everything before frame f + 1's first sample
    /// plus <see cref="Ahead"/> (no more than the shortest lead of the song) is safe: the audio thread renders up to
    /// there and no further. However the two threads run — the game hitching, the audio thread falling behind — every
    /// message lands on its sample, and what is rendered is what a headless run of the same triggers renders.
    ///
    /// The first block she triggers places the song: game frame t for a block planned at frame f puts the plan's frame
    /// 0 at game frame t - f, and frames are counted from there. A performance from the top (a block planned before
    /// the song's first sample) starts the sources at the performance's sample 0, as a headless run does; one picked
    /// up halfway starts them where that block was triggered. The mix follows her place in the song, which the triggers
    /// alone decide (<see cref="SongClock"/>), so a replay of them renders the same samples off the plan as on it. The
    /// performance ends when the master has come out, that is when her place has passed the mix's end, or when she
    /// has stood still for <see cref="Stage.GiveUpAfter"/> (one picked up after the master's end is over at once).
    /// </summary>
    public sealed class LiveStage : IDisposable
    {
        private readonly SongBlocks song;
        private readonly MixSettings mix;
        private readonly ISourceRack rack;
        private readonly ConcurrentQueue<(Block Block, long Frame)> triggers = new();
        private readonly List<(long Frame, int Block)> log = new();
        private readonly int latency;
        private Stage? stage;
        private MasterWriter? writer;
        private long origin;
        private bool placed;
        private long finished = long.MinValue;   // the last frame the game finished, counted from the plan's frame 0
        private long position;
        private volatile bool done;
        private volatile int late;

        /// <param name="ahead">How far past a finished frame to render at most (samples); the song's shortest lead
        /// caps it.</param>
        public LiveStage(SongBlocks song, MixSettings mix, ISourceRack rack, int ahead)
        {
            if (song.Blocks.Count == 0)
                throw new ArgumentException("a song with no blocks");
            this.song = song;
            this.mix = mix;
            this.rack = rack;
            Ahead = (int)Math.Min(ahead, song.Blocks.Min(b => b.Lead));
            latency = new Mixer(mix).Latency;
        }

        /// <summary>Samples rendered past a finished frame's end at most.</summary>
        public int Ahead { get; }

        /// <summary>The game frame of the plan's frame 0, once the first block has been triggered.</summary>
        public long? Origin => placed ? origin : null;

        /// <summary>The performance's sample the sources start on (known once the first block has been triggered).</summary>
        public long Start { get; private set; }

        /// <summary>Where the master's first sample comes out of the stage: the mix's start plus the mixer's latency.</summary>
        public long First => mix.Start + latency;

        /// <summary>Where the performance ends, once it's known (audio thread): the master's last sample has come out,
        /// or the run was given up.</summary>
        public long? End => stage?.End;

        /// <summary>Where a performance of this mix played as planned ends: a block whose sound starts after it is never
        /// heard (a run it would start is over at once).</summary>
        public static long EndOf(MixSettings mix) => mix.Start + new Mixer(mix).Latency + mix.End;

        /// <summary>The performance's next sample to render.</summary>
        public long Position => placed ? Volatile.Read(ref position) : Start;

        /// <summary>The master has come out completely.</summary>
        public bool Done => done;

        /// <summary>Messages that came after their sample had been rendered (should stay 0).</summary>
        public int Late => late;

        /// <summary>The blocks triggered, in order: (frame counted from the plan's frame 0, block id). Game thread.</summary>
        public IReadOnlyList<(long Frame, int Block)> Triggers => log;

        /****
        ** Game thread
        ****/
        /// <summary>A block was triggered on game frame <paramref name="tick"/>.</summary>
        public void Trigger(Block b, long tick)
        {
            if (!placed)
            {
                origin = tick - b.Frame;
                Start = b.Frame <= 0 ? 0 : b.Frame * Performer.FrameSamples / rack.Grid * rack.Grid;
                position = Start;
                placed = true;
                done = Start >= First + mix.End;   // picked up after the master: nothing to play
            }
            long frame = tick - origin;
            log.Add((frame, b.Id));
            triggers.Enqueue((b, frame));
        }

        /// <summary>Every block of game frame <paramref name="tick"/> has been triggered.</summary>
        public void FrameDone(long tick)
        {
            if (placed)
                Volatile.Write(ref finished, tick - origin);
        }

        /// <summary>Write the master as it is rendered (24-bit WAV, optionally float64 too). Call before the first
        /// render.</summary>
        public void Capture(string path, string? rawPath = null)
        {
            writer?.Dispose();
            writer = new MasterWriter(path, mix.Rate, rawPath);
        }

        /// <summary>The triggers as a text file a headless run can replay (music/engine/cli replay).</summary>
        public void WriteTriggers(string path)
        {
            var lines = new List<string>
            {
                "# Junimo live triggers: <frame from the plan's frame 0> <block id>",
                FormattableString.Invariant($"start {Start}"),
                FormattableString.Invariant($"ahead {Ahead}"),
                FormattableString.Invariant($"origin {origin}"),
            };
            lines.AddRange(log.Select(t => FormattableString.Invariant($"{t.Frame} {t.Block}")));
            File.WriteAllLines(path, lines);
        }

        public static (long Start, int Ahead, List<(long Frame, int Block)> Triggers) ReadTriggers(string path)
        {
            long start = 0;
            int ahead = 0;
            var list = new List<(long, int)>();
            foreach (string line in File.ReadLines(path))
            {
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                string[] f = line.Split(' ');
                switch (f[0])
                {
                    case "start": start = long.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "ahead": ahead = int.Parse(f[1], CultureInfo.InvariantCulture); break;
                    case "origin": break;
                    default: list.Add((long.Parse(f[0], CultureInfo.InvariantCulture), int.Parse(f[1], CultureInfo.InvariantCulture))); break;
                }
            }
            return (start, ahead, list);
        }

        /****
        ** Audio thread
        ****/
        /// <summary>Render the next samples of the performance that are safe to render, at most
        /// <paramref name="max"/>; returns how many (0 while it has to wait for the game, and once it's done).</summary>
        /// <param name="at">The performance's sample the first of them is.</param>
        public int Render(int max, double[] outL, double[] outR, out long at)
        {
            at = Position;
            long frame = Volatile.Read(ref finished);   // read before taking the triggers: all of its frame's are in
            while (triggers.TryDequeue(out var t))
            {
                stage ??= new Stage(song, mix, rack, Start);
                stage.Trigger(t.Block, t.Frame);
            }
            if (stage == null || done || frame == long.MinValue)
                return 0;
            long limit = (frame + 1) * Performer.FrameSamples + Ahead;
            int n = (int)Math.Clamp(limit - stage.Position, 0, max);
            if (n == 0)
                return 0;
            at = stage.Position;
            stage.Render(n, outL, outR);
            if (stage.End is long end && end <= at + n)
            {
                n = (int)Math.Max(0, end - at);
                done = true;
            }
            writer?.Write(at - First, outL, outR, 0, n);
            late = stage.Late;
            Volatile.Write(ref position, at + n);
            if (done)
            {
                writer?.Dispose();
                writer = null;
            }
            return n;
        }

        public void Dispose()
        {
            writer?.Dispose();
            writer = null;
            rack.Dispose();
        }
    }
}
