namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// mix.py's soft_clip as a stream: up 4x, each value over Knee x ceiling bent toward the ceiling with a tanh, down
    /// 4x (both as scipy's resample_poly). The two filters look 10 + 10 samples ahead: sample i comes out when sample
    /// i + 20 goes in.
    /// </summary>
    public sealed class SoftClip
    {
        public const int Latency = 2 * Oversampling.UpLookahead;

        private const int Window = 2 * Oversampling.UpLookahead + 1;   // input samples an upsampled value needs
        private const int Ring = 128;                                   // oversampled values kept (needs 84)

        private readonly double ceiling, knee, span;
        private readonly double[][] inputs = { new double[Window], new double[Window] };
        private readonly double[][] clipped = { new double[Ring], new double[Ring] };
        private long taken;

        public SoftClip(ClipSettings s)
        {
            ceiling = s.Ceiling;
            knee = s.Knee * s.Ceiling;
            span = ceiling - knee;
        }

        /// <summary>Takes the next sample; gives the one 20 samples back (silence at first).</summary>
        public void Process(double l, double r, out double outL, out double outR)
        {
            long m = taken++;
            outL = Channel(0, l, m);
            outR = Channel(1, r, m);
        }

        private double Channel(int ch, double x, long m)
        {
            // the newest input: the window now holds x[q - 10 .. q + 10] for q = m - 10
            double[] w = inputs[ch];
            System.Array.Copy(w, 1, w, 0, Window - 1);
            w[Window - 1] = x;
            long q = m - Oversampling.UpLookahead;
            double[] u = clipped[ch];
            if (q >= 0)
            {
                for (int r = 0; r < Oversampling.Factor; r++)
                    u[(4 * q + r) % Ring] = Bend(Oversampling.Upsample(w, r));
            }

            // down: out[i] = sum of u[4i - 40 .. 4i + 40] (oldest first) through the low-pass
            long i = m - Latency;
            if (i < 0)
                return 0;
            double acc = 0;
            for (int k = 0; k < Oversampling.Taps; k++)
            {
                long at = 4 * i - Oversampling.HalfTaps + k;
                if (at >= 0)
                    acc += u[at % Ring] * Oversampling.Down[Oversampling.Taps - 1 - k];
            }
            return acc;
        }

        private double Bend(double v)
        {
            double a = System.Math.Abs(v);
            if (!(a > knee))
                return v;
            return System.Math.Sign(v) * (knee + span * System.Math.Tanh((a - knee) / span));
        }
    }
}
