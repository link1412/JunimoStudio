using System;
using System.Collections.Generic;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// Where a tick falls in output samples, exactly. A tempo map makes every tick a rational number of samples
    /// (ticks x microseconds per quarter x 44100 / (ppq x 1e6)), so a time is kept as a numerator over
    /// <see cref="Denominator"/> and nothing ever drifts. A message goes out on the first sample at or after its exact
    /// time, the way both synths take one: S-MU2000's render feeds it before the first sample whose time has reached
    /// it, MeltySynth applies it before the first block that starts at or after it.
    /// </summary>
    public sealed class ExactClock
    {
        public const int Rate = 44100;

        private readonly long[] starts;
        private readonly long[] micros;
        private readonly long[] numerators;

        /// <param name="tempo">(tick, microseconds per quarter note); MIDI's 120 BPM holds until the first point.</param>
        public ExactClock(int ppq, IReadOnlyList<(long Tick, long Micros)> tempo)
        {
            var points = new List<(long Tick, long Micros)>(tempo);
            if (points.Count == 0 || points[0].Tick > 0)
                points.Insert(0, (0, 500000));
            this.Denominator = (long)ppq * 1_000_000;
            this.starts = new long[points.Count];
            this.micros = new long[points.Count];
            this.numerators = new long[points.Count];
            long at = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                    at = checked(at + (points[i].Tick - points[i - 1].Tick) * points[i - 1].Micros * Rate);
                this.starts[i] = points[i].Tick;
                this.micros[i] = points[i].Micros;
                this.numerators[i] = at;
            }
        }

        /// <summary>The unit of <see cref="Numerator"/>: one sample is ppq x 1e6 of them.</summary>
        public long Denominator { get; }

        /// <summary>The exact time of a tick in samples, times <see cref="Denominator"/>. Before tick 0 the first
        /// tempo runs back.</summary>
        public long Numerator(long tick)
        {
            int i = 0;
            while (i + 1 < this.starts.Length && this.starts[i + 1] <= tick)
                i++;
            return checked(this.numerators[i] + (tick - this.starts[i]) * this.micros[i] * Rate);
        }

        /// <summary>The first sample at or after the tick's exact time.</summary>
        public long FirstSample(long tick) => CeilDiv(this.Numerator(tick), this.Denominator);

        public static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return a % b != 0 && (a < 0) != (b < 0) ? q - 1 : q;
        }

        public static long CeilDiv(long a, long b) => -FloorDiv(-a, b);

        public static long Mod(long a, long b) => a - FloorDiv(a, b) * b;
    }
}
