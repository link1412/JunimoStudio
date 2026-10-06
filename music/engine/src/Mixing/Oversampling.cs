using System;

namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// 4x oversampling as scipy's resample_poly does it: the low-pass is firwin(81, 1/4, window=('kaiser', 5.0)), the
    /// output samples centred on the input's, zeros before the start. Sums run oldest sample first, as scipy's upfirdn
    /// runs them.
    /// </summary>
    public static class Oversampling
    {
        public const int Factor = 4;
        public const int HalfTaps = 10 * Factor;      // resample_poly's half_len
        public const int Taps = 2 * HalfTaps + 1;     // 81
        /// <summary>Input samples an upsampled value looks ahead.</summary>
        public const int UpLookahead = HalfTaps / Factor;

        /// <summary>The low-pass at the 4x rate, DC gain 1 (downsampling).</summary>
        public static readonly double[] Down = Kernel();

        /// <summary>The same, times 4 (upsampling, where 3 of every 4 input samples are zeros).</summary>
        public static readonly double[] Up = Array.ConvertAll(Down, h => h * Factor);

        /// <summary>
        /// The upsampled value at 4q + r from the input around q: <paramref name="x"/> holds x[q - 10 .. q + 10],
        /// oldest first.
        /// </summary>
        public static double Upsample(double[] x, int r)
        {
            double acc = 0;
            for (int k = r == 0 ? 0 : 1; k <= 2 * UpLookahead; k++)
                acc += x[k] * Up[Taps - 1 - Factor * k + r];
            return acc;
        }

        /// <summary>firwin(81, 0.25, window=('kaiser', 5.0)): a windowed sinc, scaled to a DC gain of exactly 1.</summary>
        private static double[] Kernel()
        {
            const double cutoff = 1.0 / Factor, beta = 5.0;
            double alpha = 0.5 * (Taps - 1);
            var h = new double[Taps];
            double i0Beta = BesselI0(beta);
            for (int n = 0; n < Taps; n++)
            {
                double m = n - alpha;
                double x = cutoff * m;
                double y = Math.PI * (x == 0 ? 1.0e-20 : x);   // numpy's sinc
                double r = (n - alpha) / alpha;
                h[n] = cutoff * (Math.Sin(y) / y) * (BesselI0(beta * Math.Sqrt(1 - r * r)) / i0Beta);
            }
            double s = PairwiseSum(h, 0, h.Length);
            for (int n = 0; n < Taps; n++)
                h[n] /= s;
            return h;
        }

        /// <summary>The modified Bessel function of the first kind, order 0 (its power series).</summary>
        public static double BesselI0(double x)
        {
            double q = x * x / 4, term = 1, sum = 1;
            for (int k = 1; k < 500; k++)
            {
                term *= q / ((double)k * k);
                sum += term;
                if (term < sum * 1e-17)
                    break;
            }
            return sum;
        }

        /// <summary>numpy's pairwise sum (what np.sum and mean do along a contiguous axis).</summary>
        public static double PairwiseSum(double[] a, int start, int n)
        {
            if (n < 8)
            {
                double res = 0;
                for (int i = 0; i < n; i++)
                    res += a[start + i];
                return res;
            }
            if (n <= 128)
            {
                double r0 = a[start], r1 = a[start + 1], r2 = a[start + 2], r3 = a[start + 3],
                       r4 = a[start + 4], r5 = a[start + 5], r6 = a[start + 6], r7 = a[start + 7];
                int i;
                for (i = 8; i < n - n % 8; i += 8)
                {
                    r0 += a[start + i]; r1 += a[start + i + 1]; r2 += a[start + i + 2]; r3 += a[start + i + 3];
                    r4 += a[start + i + 4]; r5 += a[start + i + 5]; r6 += a[start + i + 6]; r7 += a[start + i + 7];
                }
                double res = ((r0 + r1) + (r2 + r3)) + ((r4 + r5) + (r6 + r7));
                for (; i < n; i++)
                    res += a[start + i];
                return res;
            }
            int n2 = n / 2;
            n2 -= n2 % 8;
            return PairwiseSum(a, start, n2) + PairwiseSum(a, start + n2, n - n2);
        }
    }
}
