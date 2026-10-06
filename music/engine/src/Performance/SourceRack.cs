using System;
using System.Collections.Generic;
using System.Linq;
using MeltySynth;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// The sound sources, one per mixer strip, all running in step: each takes timed messages and renders the same
    /// stretch of the performance.
    /// </summary>
    public interface ISourceRack : IDisposable
    {
        int Count { get; }

        /// <summary>The synth's processing grid in samples: the performance lines tick 0 up with it.</summary>
        int Grid { get; }

        /// <summary>A message for source <paramref name="unit"/>, at or after the samples rendered so far.</summary>
        void Send(int unit, TimedMessage m);

        /// <summary>The next <paramref name="frames"/> samples of every source, into left[unit] and right[unit].</summary>
        void Render(int frames, double[][] left, double[][] right);
    }

    /// <summary>
    /// The open sources: one MeltySynth per strip (the same sound font), each with its own reverb and chorus, as each
    /// MU2000 of the retro rack has its own effects. Any number of samples at a time: the synths' 64-sample blocks are
    /// kept in step behind it.
    /// </summary>
    public sealed class MeltyRack : ISourceRack
    {
        private readonly MeltySource[] sources;
        private readonly float[][] blockL, blockR;
        private int used;            // samples of the current block already handed out
        private readonly int block;

        public MeltyRack(SoundFont font, int count, int rate = ExactClock.Rate)
        {
            var settings = new SynthesizerSettings(rate) { BlockSize = 64, MaximumPolyphony = 128, EnableReverbAndChorus = true };
            sources = Enumerable.Range(0, count).Select(_ =>
            {
                var synth = new Synthesizer(font, settings);
                synth.SetReverbRoom(0.7f, 0.4f, 0.33f);   // JunimoOrchestra's "Hall"
                return new MeltySource(synth);
            }).ToArray();
            block = settings.BlockSize;
            blockL = sources.Select(_ => new float[block]).ToArray();
            blockR = sources.Select(_ => new float[block]).ToArray();
            used = block;
        }

        public int Count => sources.Length;
        public int Grid => block;

        public void Send(int unit, TimedMessage m) => sources[unit].Send(m);

        public void Render(int frames, double[][] left, double[][] right)
        {
            for (int done = 0; done < frames;)
            {
                if (used == block)
                {
                    for (int k = 0; k < sources.Length; k++)
                        sources[k].Render(blockL[k], blockR[k]);
                    used = 0;
                }
                int n = Math.Min(block - used, frames - done);
                for (int k = 0; k < sources.Length; k++)
                {
                    for (int f = 0; f < n; f++)
                    {
                        left[k][done + f] = blockL[k][used + f];
                        right[k][done + f] = blockR[k][used + f];
                    }
                }
                used += n;
                done += n;
            }
        }

        public void Dispose()
        {
        }
    }
}
