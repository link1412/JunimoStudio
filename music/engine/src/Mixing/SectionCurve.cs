using System;
using System.Collections.Generic;
using System.Linq;

namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// A by-section setting as a value per sample (mix.py's curve()): each section's value ramps in from 0 across its
    /// start and back out across its end, over the mix's ramp time; overlapping sections add, in their listed order;
    /// 0 outside every section. Each ramp is numpy's interp on the same four points.
    /// </summary>
    public sealed class SectionCurve
    {
        private readonly Trapezoid[] parts;
        private readonly double rate;
        private double lastValue = 0, lastGain = 1;

        public SectionCurve(IEnumerable<SectionValue> values, MixSettings mix)
        {
            rate = mix.Rate;
            parts = values.Select(v =>
            {
                var (a, b) = mix.Sections[v.Section];
                return new Trapezoid(a - mix.Ramp / 2, a + mix.Ramp / 2, b - mix.Ramp / 2, b + mix.Ramp / 2, v.Start, v.End);
            }).ToArray();
        }

        public bool IsEmpty => parts.Length == 0;

        /// <summary>The value at sample i of the mix.</summary>
        public double At(long i)
        {
            double t = i / rate;
            double v = 0;
            foreach (var p in parts)
                v += p.At(t);
            return v;
        }

        /// <summary>The value at sample i as a gain: 10^(dB / 20).</summary>
        public double Gain(long i)
        {
            double v = At(i);
            if (v != lastValue)
            {
                lastValue = v;
                lastGain = Math.Pow(10, v / 20);
            }
            return lastGain;
        }

        private readonly struct Trapezoid
        {
            private readonly double x0, x1, x2, x3, v0, v1, s0, s1, s2;

            public Trapezoid(double x0, double x1, double x2, double x3, double v0, double v1)
            {
                (this.x0, this.x1, this.x2, this.x3, this.v0, this.v1) = (x0, x1, x2, x3, v0, v1);
                s0 = (v0 - 0.0) / (x1 - x0);
                s1 = (v1 - v0) / (x2 - x1);
                s2 = (0.0 - v1) / (x3 - x2);
            }

            // np.interp(t, [x0, x1, x2, x3], [0, v0, v1, 0], left=0, right=0)
            public double At(double t)
            {
                if (t < x0 || t > x3)
                    return 0;
                if (t < x1)
                    return t == x0 ? 0.0 : s0 * (t - x0) + 0.0;
                if (t < x2)
                    return t == x1 ? v0 : s1 * (t - x1) + v0;
                if (t < x3)
                    return t == x2 ? v1 : s2 * (t - x2) + v1;
                return 0.0;   // t == x3
            }
        }
    }
}
