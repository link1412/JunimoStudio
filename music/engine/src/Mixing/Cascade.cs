using System;
using System.Collections.Generic;
using System.Linq;

namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// Second-order sections in series, run as scipy's sosfilt runs them (transposed direct form II, a0 = 1), with a
    /// state per channel. All the mixer's filters are these.
    /// </summary>
    public sealed class Cascade
    {
        private readonly double[] c;    // b0 b1 b2 a1 a2 per section
        private readonly double[] z;    // z0 z1 per section, per channel
        private readonly int sections;

        public Cascade(IReadOnlyList<double[]> sos, int channels)
        {
            sections = sos.Count;
            c = sos.SelectMany(s => new[] { s[0], s[1], s[2], s[4], s[5] }).ToArray();
            z = new double[sections * 2 * channels];
        }

        public bool IsEmpty => sections == 0;

        public double Process(int channel, double x)
        {
            for (int s = 0, ci = 0, zi = channel * sections * 2; s < sections; s++, ci += 5, zi += 2)
            {
                double y = c[ci] * x + z[zi];
                z[zi] = c[ci + 1] * x - c[ci + 3] * y + z[zi + 1];
                z[zi + 1] = c[ci + 2] * x - c[ci + 4] * y;
                x = y;
            }
            return x;
        }

        /// <summary>An EQ: its bands in order, each an RBJ biquad.</summary>
        public static Cascade Eq(IEnumerable<Band> bands, double rate, int channels) =>
            new(bands.Select(b => Rbj(b.Kind, b.Hz, b.Db, b.Q, rate)).ToList(), channels);

        /// <summary>A Butterworth high-pass (even order) as RBJ high-pass sections, the flattest first as scipy's
        /// butter(..., output='sos') orders them.</summary>
        public static Cascade Highpass(int order, double hz, double rate, int channels)
        {
            if (order < 2 || order % 2 != 0)
                throw new ArgumentException("Butterworth high-pass: even orders only", nameof(order));
            var sos = new List<double[]>();
            for (int k = order / 2; k >= 1; k--)
                sos.Add(Rbj("hp", hz, 0, 1 / (2 * Math.Sin((2 * k - 1) * Math.PI / (2 * order))), rate));
            return new Cascade(sos, channels);
        }

        /// <summary>RBJ cookbook biquad [b0 b1 b2 1 a1 a2], computed in the same order as mix.py's biquad().</summary>
        public static double[] Rbj(string kind, double f0, double gainDb, double q, double rate)
        {
            double a = Math.Pow(10, gainDb / 40);
            double w = 2 * Math.PI * f0 / rate;
            double cw = Math.Cos(w), sw = Math.Sin(w);
            double alpha = sw / (2 * q);
            double[] b, aa;
            switch (kind)
            {
                case "hp":
                    b = new[] { (1 + cw) / 2, -(1 + cw), (1 + cw) / 2 };
                    aa = new[] { 1 + alpha, -2 * cw, 1 - alpha };
                    break;
                case "lp":
                    b = new[] { (1 - cw) / 2, 1 - cw, (1 - cw) / 2 };
                    aa = new[] { 1 + alpha, -2 * cw, 1 - alpha };
                    break;
                case "peak":
                    b = new[] { 1 + alpha * a, -2 * cw, 1 - alpha * a };
                    aa = new[] { 1 + alpha / a, -2 * cw, 1 - alpha / a };
                    break;
                case "lowshelf":
                case "highshelf":
                {
                    double s = q;   // shelf slope
                    alpha = sw / 2 * Math.Sqrt((a + 1 / a) * (1 / s - 1) + 2);
                    double sa = 2 * Math.Sqrt(a) * alpha;
                    if (kind == "lowshelf")
                    {
                        b = new[] { a * ((a + 1) - (a - 1) * cw + sa), 2 * a * ((a - 1) - (a + 1) * cw),
                                    a * ((a + 1) - (a - 1) * cw - sa) };
                        aa = new[] { (a + 1) + (a - 1) * cw + sa, -2 * ((a - 1) + (a + 1) * cw),
                                     (a + 1) + (a - 1) * cw - sa };
                    }
                    else
                    {
                        b = new[] { a * ((a + 1) + (a - 1) * cw + sa), -2 * a * ((a - 1) + (a + 1) * cw),
                                    a * ((a + 1) + (a - 1) * cw - sa) };
                        aa = new[] { (a + 1) - (a - 1) * cw + sa, 2 * ((a - 1) - (a + 1) * cw),
                                     (a + 1) - (a - 1) * cw - sa };
                    }
                    break;
                }
                default:
                    throw new ArgumentException($"unknown EQ band \"{kind}\"", nameof(kind));
            }
            return new[] { b[0] / aa[0], b[1] / aa[0], b[2] / aa[0], aa[0] / aa[0], aa[1] / aa[0], aa[2] / aa[0] };
        }
    }
}
