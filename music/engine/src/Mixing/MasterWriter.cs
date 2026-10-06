using System;
using System.IO;

namespace Junimo.Engine.Mixing
{
    /// <summary>
    /// Writes the mixer's output as the master: a 24-bit stereo WAV, each sample converted the way soundfile
    /// (libsndfile 1.2) converts float64 (rounded to float, scaled to 32 bits, the top three bytes), so it holds the
    /// same bytes mix.py would write; optionally also the float64 samples themselves (interleaved, little-endian).
    /// The master is as long as what's written: samples come in order, a chunk at a time, and any before them the mix
    /// never reached (before a performance picked up halfway) are silent. The WAV's sizes are filled in at the end.
    /// </summary>
    public sealed class MasterWriter : IDisposable
    {
        private readonly BinaryWriter wav;
        private readonly BinaryWriter? raw;
        private long written;
        private bool closed;

        public MasterWriter(string path, int rate, string? rawPath = null)
        {
            wav = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16));
            wav.Write("RIFF"u8); wav.Write(36); wav.Write("WAVE"u8);
            wav.Write("fmt "u8); wav.Write(16); wav.Write((short)1); wav.Write((short)2);
            wav.Write(rate); wav.Write(rate * 6); wav.Write((short)6); wav.Write((short)24);
            wav.Write("data"u8); wav.Write(0);
            if (rawPath != null)
                raw = new BinaryWriter(new FileStream(rawPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16));
        }

        /// <summary>Samples written so far (silence included).</summary>
        public long Written => written;

        /// <summary>The master's samples from <paramref name="at"/> on: <paramref name="count"/> of them, from
        /// <paramref name="offset"/> in the buffers. Ones before what's written already are dropped.</summary>
        public void Write(long at, double[] left, double[] right, int offset, int count)
        {
            while (written < at)
                Put(0, 0);
            for (int f = 0; f < count; f++)
            {
                if (at + f >= written)
                    Put(left[offset + f], right[offset + f]);
            }
        }

        public void Dispose()
        {
            if (closed)
                return;
            closed = true;
            int data = checked((int)(written * 6));
            wav.Seek(4, SeekOrigin.Begin);
            wav.Write(36 + data);
            wav.Seek(40, SeekOrigin.Begin);
            wav.Write(data);
            wav.Dispose();
            raw?.Dispose();
        }

        private void Put(double l, double r)
        {
            Write24(wav, l);
            Write24(wav, r);
            if (raw != null)
            {
                raw.Write(l);
                raw.Write(r);
            }
            written++;
        }

        private static void Write24(BinaryWriter w, double x)
        {
            float v = (float)((float)x * 2147483648f);
            int n = (int)Math.Clamp(Math.Round((double)v), int.MinValue, int.MaxValue);   // lrintf
            w.Write((byte)(n >> 8));
            w.Write((byte)(n >> 16));
            w.Write((byte)(n >> 24));
        }
    }
}
