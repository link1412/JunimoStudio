using System;
using System.Collections.Generic;
using System.Linq;

namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// The mixer, as mix.py mixes and masters: each strip (EQ, width and balance, side rides, gain, rides, compressor,
    /// clipper), the strips summed, the bus (rides, section EQs, width above the low end, the high-pass), then the
    /// master (bus compressor, fader, soft clipper, limiter, fade at the end). It runs sample by sample, so the output
    /// is the same however the input is cut into chunks, a fixed <see cref="Latency"/> behind: what goes in as sample m
    /// of the mix comes out as sample m - Latency. The compressors and the limiter work in blocks counted from the mix's
    /// first sample, so each stage starts on the first real sample of the one before it, not on its warm-up silence.
    /// From <see cref="MixSettings.End"/> on the master hears silence (mix.py cuts the bus there) and the output is
    /// silent. The rides, section EQs, side rides and the fade go by each input sample's place in the mix, which the
    /// caller may give (her place in the song, <see cref="Performance.SongClock"/>): by default input sample m is the
    /// mix's sample start + m. A mixer can also start later in the mix (a performance picked up halfway): its first
    /// input is then the mix's sample <c>start</c>, and the compressors and the limiter count their blocks from there.
    /// </summary>
    public sealed class Mixer
    {
        private readonly MixSettings mix;
        private readonly Strip[] strips;
        private readonly int stripLatency;
        private readonly SectionCurve rides;
        private readonly (Cascade Eq, SectionCurve Amount)[] sectionEqs;
        private readonly Cascade? sideHigh;
        private readonly double sideBoost;
        private readonly SectionCurve sideRides;
        private readonly Cascade? highpass;
        private readonly Compressor? comp;
        private readonly double fader;
        private readonly SoftClip? clip;
        private readonly Limiter? limiter;
        private readonly long fadeStart;
        private readonly double fadeStep;
        private readonly long start;
        private readonly long[] places;   // the place in the mix of the last Latency + 1 samples taken
        private long taken;

        /// <param name="start">The mix's sample that comes in first (0: the mixer starts with the mix).</param>
        public Mixer(MixSettings mix, long start = 0)
        {
            this.mix = mix;
            this.start = start;
            double rate = mix.Rate;
            stripLatency = mix.Strips.Select(s => Strip.Lookahead(s, rate)).DefaultIfEmpty(0).Max();
            strips = mix.Strips.Select(s => new Strip(s, mix, stripLatency)).ToArray();
            var bus = mix.Bus;
            rides = new SectionCurve(bus.Rides, mix);
            sectionEqs = bus.SectionEq.Select(e => (Cascade.Eq(e.Eq, rate, 2), new SectionCurve(e.Sections, mix))).ToArray();
            if (bus.Side != null)
            {
                sideHigh = Cascade.Highpass(2, bus.Side.Hz, rate, 1);
                sideBoost = Math.Pow(10, bus.Side.Db / 20) - 1;
            }
            sideRides = new SectionCurve(bus.Side?.Rides ?? new List<SectionValue>(), mix);
            if (bus.Highpass != null)
                highpass = Cascade.Highpass(bus.Highpass.Order, bus.Highpass.Hz, rate, 2);
            fader = 1;
            fadeStart = mix.End;
            var master = mix.Master;
            if (master != null)
            {
                if (master.Comp != null)
                    comp = new Compressor(master.Comp, rate);
                fader = Math.Pow(10, master.GainDb / 20);
                if (master.Clip != null)
                    clip = new SoftClip(master.Clip);
                if (master.Limiter != null)
                    limiter = new Limiter(master.Limiter, rate);
                int fade = (int)(master.Fade * rate);
                fadeStart = mix.End - fade;
                fadeStep = fade > 1 ? -1.0 / (fade - 1) : 0;
            }
            MasterLatency = (comp?.Latency ?? 0) + (clip != null ? SoftClip.Latency : 0) + (limiter?.Latency ?? 0);
            places = new long[Latency + 1];
        }

        /// <summary>Samples from a strip's input to the output.</summary>
        public int Latency => stripLatency + MasterLatency;

        public int MasterLatency { get; }

        public IReadOnlyList<StripSettings> Strips => mix.Strips;

        /// <summary>Samples taken when the output first reached the mix's end (null until it has): from that output
        /// sample on, the master is over.</summary>
        public long? EndedAt { get; private set; }

        /// <summary>Mix one chunk: <paramref name="left"/>[s] and <paramref name="right"/>[s] are strip s's input,
        /// <paramref name="frames"/> samples of each; as many samples come out, <see cref="Latency"/> behind.</summary>
        public void Process(IReadOnlyList<double[]> left, IReadOnlyList<double[]> right, int frames,
                            double[] outLeft, double[] outRight) =>
            Process(left, right, 0, frames, outLeft, outRight);

        /// <summary>The same, from <paramref name="offset"/> in the inputs and the outputs; <paramref name="at"/>[f],
        /// if given, is input f's place in the mix (the mix's sample whose rides, section EQs and fade it gets).</summary>
        public void Process(IReadOnlyList<double[]> left, IReadOnlyList<double[]> right, int offset, int frames,
                            double[] outLeft, double[] outRight, long[]? at = null)
        {
            for (int f = offset; f < offset + frames; f++)
            {
                long m = taken++;
                long here = at?[f] ?? start + m;
                places[m % places.Length] = here;
                double busL = 0, busR = 0;
                for (int s = 0; s < strips.Length; s++)
                {
                    strips[s].Process(left[s][f], right[s][f], here, out double l, out double r);
                    busL += l;
                    busR += r;
                }
                long n = m - stripLatency;   // samples the bus has had
                if (n < 0)
                {
                    outLeft[f] = outRight[f] = 0;
                    continue;
                }
                Bus(ref busL, ref busR, places[n % places.Length]);
                Master(busL, busR, n, m, out outLeft[f], out outRight[f]);
            }
        }

        private void Bus(ref double l, ref double r, long j)
        {
            if (j >= mix.End)
            {
                l = r = 0;
                return;
            }
            if (!rides.IsEmpty)
            {
                double g = rides.Gain(j);
                l *= g;
                r *= g;
            }
            foreach (var (eq, amount) in sectionEqs)
            {
                double el = eq.Process(0, l), er = eq.Process(1, r);
                double a = amount.At(j);
                l += (el - l) * a;
                r += (er - r) * a;
            }
            if (sideHigh != null)
            {
                double m = (l + r) / 2, sd = (l - r) / 2;
                sd = (sd + sideHigh.Process(0, sd) * sideBoost) * sideRides.Gain(j);
                l = m + sd;
                r = m - sd;
            }
            if (highpass != null)
            {
                l = highpass.Process(0, l);
                r = highpass.Process(1, r);
            }
        }

        // k counts down each stage's latency from the samples the master has had: once it is negative, the stage is
        // still warming up and the next one isn't fed yet; m: samples taken before this one
        private void Master(double l, double r, long k, long m, out double outL, out double outR)
        {
            outL = outR = 0;
            if (comp != null)
            {
                comp.Process(l, r, out l, out r);
                if ((k -= comp.Latency) < 0)
                    return;
            }
            l *= fader;
            r *= fader;
            if (clip != null)
            {
                clip.Process(l, r, out l, out r);
                if ((k -= SoftClip.Latency) < 0)
                    return;
            }
            if (limiter != null)
            {
                limiter.Process(l, r, out l, out r);
                if ((k -= limiter.Latency) < 0)
                    return;
            }
            long j = places[k % places.Length];   // the place of the sample coming out
            if (j >= mix.End)
            {
                EndedAt ??= m;
                return;
            }
            if (j >= fadeStart)
            {
                long n = j - fadeStart;
                double v = n == mix.End - fadeStart - 1 ? 0.0 : n * fadeStep + 1.0;   // np.linspace(1, 0, fade)
                l *= v * v;
                r *= v * v;
            }
            outL = l;
            outR = r;
        }

        /// <summary>One strip, delayed to the strips' common latency.</summary>
        private sealed class Strip
        {
            private readonly Cascade eq;
            private readonly double sideGain, balanceL, balanceR, gain;
            private readonly bool balance;
            private readonly SectionCurve sideRides, rides;
            private readonly Compressor? comp;
            private readonly Delay? delay;
            private readonly SoftClip? clip;
            private readonly int latency;
            private long taken;

            public Strip(StripSettings s, MixSettings mix, int latency)
            {
                this.latency = latency;
                eq = Cascade.Eq(s.Eq, mix.Rate, 2);
                sideGain = Math.Pow(10, s.Side / 20);
                balance = s.Balance != 0;
                balanceL = Math.Pow(10, s.Balance / 40);
                balanceR = Math.Pow(10, -s.Balance / 40);
                sideRides = new SectionCurve(s.SideRides, mix);
                gain = Math.Pow(10, s.Gain / 20);
                rides = new SectionCurve(s.Rides, mix);
                int clipLatency = s.Clip != null ? SoftClip.Latency : 0;
                if (s.Comp != null)
                    comp = new Compressor(s.Comp, mix.Rate, latency - clipLatency);
                else
                    delay = new Delay(latency - clipLatency);
                if (s.Clip != null)
                    clip = new SoftClip(s.Clip);
            }

            /// <summary>The samples this strip's own processing looks ahead.</summary>
            public static int Lookahead(StripSettings s, double rate) =>
                (s.Comp != null ? Compressor.LookaheadFor(s.Comp, rate) : 0) + (s.Clip != null ? SoftClip.Latency : 0);

            /// <param name="j">The sample's place in the mix.</param>
            public void Process(double l, double r, long j, out double outL, out double outR)
            {
                long n = taken++;
                if (!eq.IsEmpty)
                {
                    l = eq.Process(0, l);
                    r = eq.Process(1, r);
                }
                double m = (l + r) / 2, sd = (l - r) / 2 * sideGain;
                l = m + sd;
                r = m - sd;
                if (balance)
                {
                    l *= balanceL;
                    r *= balanceR;
                }
                if (!sideRides.IsEmpty)
                {
                    m = (l + r) / 2;
                    sd = (l - r) / 2 * sideRides.Gain(j);
                    l = m + sd;
                    r = m - sd;
                }
                l *= gain;
                r *= gain;
                if (!rides.IsEmpty)
                {
                    double g = rides.Gain(j);
                    l *= g;
                    r *= g;
                }
                if (comp != null)
                    comp.Process(l, r, out l, out r);
                else
                    delay!.Process(l, r, out l, out r);
                outL = outR = 0;
                if (clip != null)
                {
                    if (n < latency - SoftClip.Latency)   // the compressor still warming up
                        return;
                    clip.Process(l, r, out l, out r);
                }
                outL = l;
                outR = r;
            }
        }

        private sealed class Delay
        {
            private readonly double[] bufL, bufR;
            private long taken;

            public Delay(int samples)
            {
                bufL = new double[samples + 1];
                bufR = new double[samples + 1];
            }

            public void Process(double l, double r, out double outL, out double outR)
            {
                long m = taken++;
                int n = bufL.Length;
                bufL[m % n] = l;
                bufR[m % n] = r;
                long i = m - (n - 1);
                outL = i >= 0 ? bufL[i % n] : 0;
                outR = i >= 0 ? bufR[i % n] : 0;
            }
        }
    }
}
