using System;

namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// mix.py's limiter as a stream. For each sample, the gain that brings its 4x-oversampled peak (both channels) to
    /// the ceiling; a hard stage takes the lowest of those over +-lookahead x 2 and smooths it over +-lookahead / 2,
    /// never above the lowest over +-lookahead / 2; a slow stage follows the hard one block by block (32 samples,
    /// instant down, exponential release) and is interpolated between blocks. The gain is the lower of the two. The
    /// windows are scipy.ndimage's (centred, the start mirrored), so the limiter looks 204 samples ahead.
    /// </summary>
    public sealed class Limiter
    {
        public const int BlockSize = 32;

        private readonly double ceiling, release;
        private readonly int wide, narrow;           // half widths: the hard minimum (la), the smoothing (la / 2)
        private readonly int ring;
        private readonly double[] need, hardest, hard;   // per sample, rings
        private readonly double[] envelope;              // per block, ring
        private readonly double[][] inputs = { new double[2 * Oversampling.UpLookahead + 1],
                                               new double[2 * Oversampling.UpLookahead + 1] };
        private readonly double[] delayL, delayR;
        private double mean;                         // the smoothing's running mean
        private double slow = 1;                     // the slow stage's state
        private double blockMin = 1;
        private long taken;

        public Limiter(LimiterSettings s, double rate)
        {
            ceiling = Math.Pow(10, s.CeilingDb / 20);
            int la = Math.Max(1, (int)(s.Lookahead * rate));
            // ndimage windows of size 2la + 1 and la + 1, origin 0: [i - size / 2, i + size - size / 2 - 1]
            wide = (2 * la + 1) / 2;
            narrow = (la + 1) / 2;
            if ((la + 1) - narrow - 1 != narrow)
                throw new ArgumentException("limiter: the lookahead must make an odd window");
            release = Math.Exp(-BlockSize / (s.Release * rate));
            // hard[i] is known once x[i + 10 + wide + narrow] is in; the slow stage needs it up to the end of the next
            // block
            HardDelay = Oversampling.UpLookahead + wide + narrow;
            Latency = HardDelay + 2 * BlockSize - 2;
            ring = 1;
            while (ring < Latency + 4 * wide + 4 * BlockSize)
                ring *= 2;
            need = new double[ring];
            hardest = new double[ring];
            hard = new double[ring];
            envelope = new double[ring / BlockSize + 4];
            delayL = new double[ring];
            delayR = new double[ring];
        }

        private int HardDelay { get; }
        public int Latency { get; }

        /// <summary>Takes the next sample; gives the one <see cref="Latency"/> samples back (silence at first).</summary>
        public void Process(double l, double r, out double outL, out double outR)
        {
            long m = taken++;
            delayL[m % ring] = l;
            delayR[m % ring] = r;
            Shift(inputs[0], l);
            Shift(inputs[1], r);

            // the gain each sample needs: its true peak to the ceiling
            long q = m - Oversampling.UpLookahead;
            if (q >= 0)
            {
                double peak = 0;
                for (int k = 0; k < Oversampling.Factor; k++)
                {
                    peak = Math.Max(peak, Math.Abs(Oversampling.Upsample(inputs[0], k)));
                    peak = Math.Max(peak, Math.Abs(Oversampling.Upsample(inputs[1], k)));
                }
                need[q % ring] = Math.Min(1.0, ceiling / Math.Max(peak, 1e-12));
            }

            // the lowest need over +-wide (before the start: none, as the mirror only repeats what's inside)
            long c = q - wide;
            if (c >= 0)
            {
                double lo = 1.0;
                for (long k = Math.Max(0, c - wide); k <= c + wide; k++)
                    lo = Math.Min(lo, need[k % ring]);
                hardest[c % ring] = lo;
            }

            // smoothed over +-narrow (ndimage's running mean, mirrored at the start), never above the lowest need
            // over +-narrow
            long h = c - narrow;
            if (h >= 0)
            {
                if (h == 0)
                {
                    mean = 0;
                    for (long k = -narrow; k <= narrow; k++)
                        mean += hardest[Mirror(k) % ring];
                    mean /= 2 * narrow + 1;
                }
                else
                {
                    mean += (hardest[(h + narrow) % ring] - hardest[Mirror(h - narrow - 1) % ring]) / (2 * narrow + 1);
                }
                double lo = 1.0;
                for (long k = Math.Max(0, h - narrow); k <= h + narrow; k++)
                    lo = Math.Min(lo, need[k % ring]);
                hard[h % ring] = Math.Min(mean, lo);

                // the slow stage, one value per block of 32
                blockMin = h % BlockSize == 0 ? hard[h % ring] : Math.Min(blockMin, hard[h % ring]);
                if (h % BlockSize == BlockSize - 1)
                {
                    slow = blockMin < slow ? blockMin : release * slow + (1 - release) * blockMin;
                    envelope[(h / BlockSize) % envelope.Length] = slow;
                }
            }

            long i = m - Latency;
            if (i < 0)
            {
                outL = outR = 0;
                return;
            }
            // np.interp(i / 32, block numbers, envelope)
            long b = i / BlockSize;
            double e0 = envelope[b % envelope.Length];
            double s = e0;
            if (i % BlockSize != 0)
            {
                double t = i / (double)BlockSize;
                double e1 = envelope[(b + 1) % envelope.Length];
                s = (e1 - e0) / ((double)(b + 1) - b) * (t - b) + e0;
            }
            double g = Math.Min(s, hard[i % ring]);
            outL = delayL[i % ring] * g;
            outR = delayR[i % ring] * g;
        }

        private static long Mirror(long k) => k < 0 ? -k - 1 : k;

        private static void Shift(double[] w, double x)
        {
            Array.Copy(w, 1, w, 0, w.Length - 1);
            w[^1] = x;
        }
    }
}
