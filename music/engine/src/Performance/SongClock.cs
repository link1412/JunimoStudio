using System;
using System.Collections.Generic;
using System.Linq;
using Junimo.Engine.Mixing;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// Her place in the song, which the mix follows: its rides, section EQs, widths and fade go by where she is in the
    /// song rather than by the clock. The blocks she triggers say where she is: a block the plan has at frame F,
    /// triggered at frame f, puts her at the plan's frame F at frame f. Between triggers her place moves on with the
    /// clock, but not past the next frame of the plan with a block on it until one of its blocks is triggered: when she
    /// is late the mix waits for her, when she stops it stops with her. A jump (early, a block skipped, a stretch played
    /// again) is made up over <see cref="Glide"/> samples rather than at once. Only blocks heard in the mix count: one
    /// whose sound starts after the mix's end (a song's last reset) says nothing about the mix. A frame's sound comes at
    /// least <see cref="Delay"/> samples after it (the song's shortest lead), so the place of the sound at the
    /// performance's sample p is her place at p - Delay, plus Delay.
    ///
    /// Played as planned, the place is the performance's sample itself. Whatever she does, it follows from the triggers
    /// alone: the place at p needs only the triggers of the frames before (p - Delay) / 735 + 1, which a live stage has
    /// by the time it renders p (it renders no further than <see cref="LiveStage.Ahead"/> past a finished frame, and
    /// that's no more than the shortest lead), so a replay of them gives the very same place on every sample.
    /// </summary>
    public sealed class SongClock
    {
        /// <summary>Samples a jump is made up over (50 ms).</summary>
        public const int Glide = 2205;

        private const long FrameSamples = Performer.FrameSamples;

        private readonly long[] frames;   // the plan's frames with a block heard in the mix, ascending
        private readonly long heardBefore;
        private readonly Queue<(long Frame, long Plan)> pending = new();
        private bool placed;
        private long t0, u0;              // the last trigger: when (game time, in samples) and her place then
        private long cap;                 // her place can't pass it until the next trigger
        private long jump;                // her place just before the last trigger, less u0: made up over the glide
        private long asked = long.MinValue;

        public SongClock(SongBlocks song, MixSettings mix)
        {
            heardBefore = mix.Start + mix.End;
            frames = song.Blocks.Where(Heard).Select(b => b.Frame).Distinct().OrderBy(f => f).ToArray();
            Delay = song.Blocks.Count > 0 ? song.Blocks.Min(b => b.Lead) : 0;
        }

        /// <summary>Samples from a frame to its sound, at the least.</summary>
        public long Delay { get; }

        /// <summary>Triggers that came after their frame's place had been asked for (should stay 0).</summary>
        public int Late { get; private set; }

        /// <summary>A block was triggered at <paramref name="frame"/> (counted from the plan's frame 0). In order.</summary>
        public void Trigger(Block b, long frame)
        {
            if (!Heard(b))
                return;
            if (frame * FrameSamples <= asked)
                Late++;
            pending.Enqueue((frame, b.Frame));
        }

        /// <summary>The place in the song of the sound at the performance's sample <paramref name="p"/>, as the
        /// performance's sample that has it when the song is played as planned; <paramref name="held"/>: how long her
        /// place has stood waiting for her next block by then (0 while it moves). Asked in order.</summary>
        public long At(long p, out long held)
        {
            long t = p - Delay;
            asked = Math.Max(asked, t);
            while (pending.TryPeek(out var x) && x.Frame * FrameSamples <= t)
            {
                pending.Dequeue();
                long tf = x.Frame * FrameSamples, u = x.Plan * FrameSamples;
                long before = placed ? Line(tf) : tf;   // until the first block, the plan's own clock
                int next = Array.BinarySearch(frames, x.Plan + 1);
                if (next < 0)
                    next = ~next;
                (t0, u0, jump, placed) = (tf, u, before - u, true);
                cap = next < frames.Length ? frames[next] * FrameSamples : long.MaxValue;
            }
            if (!placed)
            {
                held = 0;
                return p;
            }
            held = cap == long.MaxValue ? 0 : Math.Max(0, t - (t0 + cap - u0));
            return Line(t) + Delay;
        }

        public long At(long p) => At(p, out _);

        // her place at game time t since the last trigger: on with the clock up to the cap, the jump made up as it glides
        private long Line(long t)
        {
            long d = t - t0;
            long u = Math.Min(u0 + d, cap);
            return d < Glide ? u + jump * (Glide - d) / Glide : u;
        }

        private bool Heard(Block b) => b.Frame * FrameSamples + b.Lead < heardBefore;
    }
}
