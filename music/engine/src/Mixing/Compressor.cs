using System;

namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// mix.py's compressor as a stream. The detector hears the signal through a 2nd-order Butterworth high-pass, takes
    /// its power in blocks of int(rate / 1000) samples (mix.py's "1 ms": 44 at 44.1 kHz), averages
    /// <see cref="CompSettings.WindowMs"/>
    /// blocks centred on each one (np.convolve 'same'), and turns that into gain reduction through a soft knee; the
    /// reduction is smoothed block by block with the attack and release, and the gain is interpolated between block
    /// centres. Looking ahead that far makes it a fixed <see cref="Latency"/>: sample i comes out when sample
    /// i + Latency goes in.
    /// </summary>
    public sealed class Compressor
    {
        private readonly double threshold, knee, slope, attack, release;
        private readonly int window, before;      // blocks averaged; how many of them lie before the centre
        private readonly double weight;           // np.ones(window) / window
        private readonly Cascade sidechain;
        private readonly int block;               // samples per level block
        private readonly double[] squares;
        private int inBlock;
        private readonly double[] powers;         // the last `window` block powers
        private long blocks;                      // blocks whose power is known
        private double reduction;                 // smoothed gain reduction, dB
        private readonly double[] gains;          // 10^(-reduction / 20) of the latest blocks
        private double firstGain = 1;
        private long levels;                      // blocks whose gain is known
        private readonly double[] delayL, delayR;
        private long taken;

        public Compressor(CompSettings s, double rate, int latency = -1)
        {
            block = (int)(rate / 1000);
            squares = new double[block];
            threshold = s.Threshold;
            knee = s.Knee;
            slope = 1 - 1 / s.Ratio;
            attack = Math.Exp(-1e-3 / s.Attack);
            release = Math.Exp(-1e-3 / s.Release);
            window = s.WindowMs;
            before = window / 2;
            weight = 1.0 / window;
            sidechain = Cascade.Highpass(2, s.SidechainHz, rate, 2);
            powers = new double[window];
            MinimumLatency = LookaheadFor(s, rate);
            Latency = latency < 0 ? MinimumLatency : latency;
            if (Latency < MinimumLatency)
                throw new ArgumentException($"compressor: needs at least {MinimumLatency} samples of latency");
            delayL = new double[Latency + 1];
            delayR = new double[Latency + 1];
            gains = new double[Latency / block + 4];
        }

        public int MinimumLatency { get; }

        /// <summary>
        /// The samples the gain at sample i depends on past i: it is interpolated toward the next block's centre
        /// (at most half a block on), whose level averages blocks up to window - 1 - window / 2 further on.
        /// </summary>
        public static int LookaheadFor(CompSettings s, double rate)
        {
            int block = (int)(rate / 1000);
            int after = s.WindowMs - 1 - s.WindowMs / 2;
            return block * (after + 1) + block / 2 - 1;
        }
        public int Latency { get; }

        /// <summary>Takes the next sample; gives the one <see cref="Latency"/> samples back (silence at first).</summary>
        public void Process(double l, double r, out double outL, out double outR)
        {
            long m = taken++;
            delayL[m % delayL.Length] = l;
            delayR[m % delayR.Length] = r;

            double dl = sidechain.Process(0, l), dr = sidechain.Process(1, r);
            squares[inBlock++] = (dl * dl + dr * dr) / 2;
            if (inBlock == block)
            {
                inBlock = 0;
                powers[blocks % window] = Oversampling.PairwiseSum(squares, 0, block) / block;
                blocks++;
                long k = blocks - 1 - (window - 1 - before);   // the block whose average is now complete
                if (k >= 0)
                    Level(k);
            }

            long i = m - Latency;
            if (i < 0)
            {
                outL = outR = 0;
                return;
            }
            double g = GainAt(i);
            outL = delayL[i % delayL.Length] * g;
            outR = delayR[i % delayR.Length] * g;
        }

        private void Level(long k)
        {
            double acc = 0;
            for (int j = 0; j < window; j++)
            {
                long b = k - before + j;
                acc += (b >= 0 ? powers[b % window] : 0.0) * weight;
            }
            double level = 10 * Math.Log10(acc + 1e-20);
            double over = level - threshold;
            double gr = over <= -knee / 2 ? 0.0
                : over >= knee / 2 ? over * slope
                : slope * ((over + knee / 2) * (over + knee / 2)) / (2 * knee);
            reduction = gr > reduction ? attack * reduction + (1 - attack) * gr
                                       : release * reduction + (1 - release) * gr;
            gains[k % gains.Length] = Math.Pow(10, -reduction / 20);
            if (k == 0)
                firstGain = gains[0];
            levels = k + 1;
        }

        // np.interp(i / 44, block centres (k + 0.5), gains)
        private double GainAt(long i)
        {
            double t = i / (double)block;
            if (t < 0.5)
                return firstGain;
            long j = (long)Math.Floor(t - 0.5);
            if (j + 1 >= levels)
                throw new InvalidOperationException("compressor: gain not known yet");
            double g0 = gains[j % gains.Length];
            if (t == j + 0.5)
                return g0;
            double g1 = gains[(j + 1) % gains.Length];
            return (g1 - g0) / ((j + 1.5) - (j + 0.5)) * (t - (j + 0.5)) + g0;
        }
    }
}
