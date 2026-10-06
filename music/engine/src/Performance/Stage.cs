using System;
using System.Collections.Generic;
using System.Linq;
using Junimo.Engine.Mixing;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// Everything between the blocks and the speakers, as the game's audio thread runs it: the performer turns triggered
    /// blocks into timed messages, each mixer strip's source hears the performance with only its own parts' notes
    /// sounding (<see cref="Stems.Keep"/>), and the mixer mixes the sources from the performance's sample
    /// <see cref="MixSettings.Start"/> on. The output is the mix, <see cref="Mixer.Latency"/> samples behind the
    /// performance; silent before the mix starts and after it ends. The mix follows her place in the song
    /// (<see cref="SongClock"/>): played as planned that's the performance's own clock; late, it waits for her. The run
    /// ends when the master is over, which is when her place has passed the mix's end, or once she has stood still
    /// for <see cref="GiveUpAfter"/> (<see cref="End"/>). A stage can start later in the performance (she picked the
    /// song up halfway): the sources' sample 0 is then the performance's sample <see cref="Start"/>, and the mixer
    /// starts at the mix's sample there.
    /// </summary>
    public sealed class Stage
    {
        /// <summary>How long her place in the song may stand still before the run is given up (30 s).</summary>
        public const long GiveUpAfter = 30L * ExactClock.Rate;

        private readonly Performer? performer;
        private readonly SongClock? clock;
        private readonly IReadOnlyList<TimedMessage>? fixedMessages;
        private int nextFixed;
        private readonly ISourceRack rack;
        private readonly Mixer mixer;
        private readonly MixSettings mix;
        private readonly IReadOnlySet<int>[] keep;
        private readonly List<TimedMessage> due = new();
        private readonly double[][] left, right;
        private readonly long start;
        private readonly long fed;   // the performance's sample the mixer takes first
        private long[] places = Array.Empty<long>();
        private long position;
        private int late;

        /// <summary>The blocks, as she triggers them.</summary>
        /// <param name="start">The performance's sample the sources start on (0 for a performance from the top).</param>
        public Stage(SongBlocks song, MixSettings mix, ISourceRack rack, long start = 0)
            : this(mix, rack, start)
        {
            performer = new Performer(song, rack.Grid);
            clock = new SongClock(song, mix);
        }

        /// <summary>Messages timed already (a MIDI file played straight through): the reference for the blocks.</summary>
        public Stage(IReadOnlyList<TimedMessage> messages, MixSettings mix, ISourceRack rack)
            : this(mix, rack, 0)
        {
            fixedMessages = messages;
        }

        private Stage(MixSettings mix, ISourceRack rack, long start)
        {
            if (rack.Count != mix.Strips.Count)
                throw new ArgumentException($"{mix.Strips.Count} strips but {rack.Count} sources");
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start));
            this.mix = mix;
            this.rack = rack;
            this.start = start;
            position = start;
            fed = Math.Max(start, mix.Start);
            mixer = new Mixer(mix, fed - mix.Start);
            keep = mix.Strips.Select(s => (IReadOnlySet<int>)s.Channels.ToHashSet()).ToArray();
            left = mix.Strips.Select(_ => Array.Empty<double>()).ToArray();
            right = mix.Strips.Select(_ => Array.Empty<double>()).ToArray();
        }

        public Performer? Performer => performer;
        public Mixer Mixer => mixer;

        /// <summary>The performance's sample the sources started on.</summary>
        public long Start => start;

        /// <summary>The performance's next sample to render.</summary>
        public long Position => position;

        /// <summary>Messages that came after their sample had been rendered (they were played at once instead), and
        /// triggers that came after the mix had gone by their frame.</summary>
        public int Late => late + (clock?.Late ?? 0);

        /// <summary>The performance's sample the run ends on (its output silent from there), once it's known.</summary>
        public long? End { get; private set; }

        /// <summary>The performance's sample whose sound is the output's sample at performance sample p.</summary>
        public long Heard(long p) => p - mixer.Latency;

        public void Trigger(Block b, long frame)
        {
            (performer ?? throw new InvalidOperationException("a stage playing fixed messages takes no blocks")).Trigger(b, frame);
            clock!.Trigger(b, frame);
        }

        /// <summary>Render the next <paramref name="frames"/> samples of the performance and mix them.</summary>
        public void Render(int frames, double[] outL, double[] outR)
        {
            due.Clear();
            long end = position + frames;
            if (performer != null)
            {
                performer.Dispatch(end, due);
            }
            else
            {
                while (nextFixed < fixedMessages!.Count && fixedMessages[nextFixed].Sample < end)
                    due.Add(fixedMessages[nextFixed++]);
            }
            foreach (var m in due)
            {
                var at = m;
                long sample = Math.Max(at.Sample, position);
                if (sample != at.Sample)
                    late++;
                at = new TimedMessage(sample - start, at.Bytes);   // on the sources' own clock
                for (int s = 0; s < keep.Length; s++)
                    rack.Send(s, Stems.Keep(at, keep[s]));
            }
            for (int s = 0; s < keep.Length; s++)
            {
                if (left[s].Length < frames)
                {
                    left[s] = new double[frames];
                    right[s] = new double[frames];
                }
            }
            rack.Render(frames, left, right);
            // each sample's place in the mix, and where she has stood still too long
            if (places.Length < frames)
                places = new long[frames];
            long? givenUp = null;
            for (int f = 0; f < frames; f++)
            {
                long p = position + f, held = 0;
                places[f] = (clock?.At(p, out held) ?? p) - mix.Start;
                if (held >= GiveUpAfter)
                    givenUp ??= p;
            }
            // the mixer starts at the mix's first sample
            int skip = (int)Math.Clamp(mix.Start - position, 0, frames);
            Array.Clear(outL, 0, skip);
            Array.Clear(outR, 0, skip);
            mixer.Process(left, right, skip, frames - skip, outL, outR, places);
            if (End == null)
            {
                long? over = fed + mixer.EndedAt;   // the master is over
                End = over == null ? givenUp : givenUp == null ? over : Math.Min(over.Value, givenUp.Value);
            }
            if (End is long e && e < end)
            {
                int from = (int)Math.Max(0, e - position);
                Array.Clear(outL, from, frames - from);
                Array.Clear(outR, from, frames - from);
            }
            position = end;
        }
    }
}
