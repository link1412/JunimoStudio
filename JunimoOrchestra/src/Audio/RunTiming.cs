using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Junimo.Engine.Performance;

namespace JunimoOrchestra.Audio
{
    /// <summary>
    /// Dev: where a captured run's time went, for finding out why the output ran dry or skipped. Every game frame
    /// (&lt;stem&gt;.frames.csv: when it came, what the output had queued and did, how far the audio thread had rendered,
    /// the garbage collections since the frame before) and every render of the audio thread (&lt;stem&gt;.renders.csv: when,
    /// for how long, how much of that in the sources). A stall of the game shows as a frame that came late with the audio
    /// rendered as far as it was allowed; a stall of the audio as renders that fell behind.
    /// </summary>
    internal sealed class RunTiming
    {
        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;
        private readonly long t0 = Stopwatch.GetTimestamp();
        private readonly List<Frame> frames = new(16384);    // game thread
        private readonly List<Render> renders = new(16384);  // audio thread
        private int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        private long gcIndex = GC.GetGCMemoryInfo(GCKind.Any).Index;

        /// <summary>Milliseconds since the run began.</summary>
        public double Now => (Stopwatch.GetTimestamp() - this.t0) * MsPerTick;

        /// <summary>Time the sources took, in the render going on (audio thread).</summary>
        public double SourcesMs { get; set; }

        /// <summary>A game frame was over and the output was fed (game thread).</summary>
        /// <param name="lag">How far the audio thread was behind what it was allowed to render (samples).</param>
        /// <param name="ev">"dry" (the output had run dry), "skip" (it was behind the game and skipped), or "".</param>
        public void FrameDone(long tick, long now, long heard, int pending, long queued, long lag, int waiting, string ev, long skipped)
        {
            int c0 = GC.CollectionCount(0), c1 = GC.CollectionCount(1), c2 = GC.CollectionCount(2);
            double pause = 0;
            int gen = -1;
            GCMemoryInfo gc = GC.GetGCMemoryInfo(GCKind.Any);
            if (gc.Index != this.gcIndex)
            {
                this.gcIndex = gc.Index;
                gen = gc.Generation;
                foreach (TimeSpan p in gc.PauseDurations)
                    pause += p.TotalMilliseconds;
            }
            this.frames.Add(new Frame(tick, this.Now, now, heard, pending, queued, lag, waiting, ev, skipped,
                                      c0 - this.gc0, c1 - this.gc1, c2 - this.gc2, gen, pause));
            (this.gc0, this.gc1, this.gc2) = (c0, c1, c2);
        }

        /// <summary>The audio thread rendered <paramref name="samples"/> from the performance's sample
        /// <paramref name="at"/>, starting at <paramref name="began"/> (ms).</summary>
        public void Rendered(double began, int samples, long at) =>
            this.renders.Add(new Render(began, this.Now - began, this.SourcesMs, samples, at));

        /// <summary>Write both tables (after the audio thread is done).</summary>
        public void Write(string stem)
        {
            var f = new StringBuilder("tick,ms,now,heard,pending,queued,lag,waiting,event,skipped,gc0,gc1,gc2,gc_gen,gc_pause_ms\n");
            foreach (Frame x in this.frames)
            {
                f.Append(CultureInfo.InvariantCulture, $"{x.Tick},{x.Ms:0.###},{x.Now},{x.Heard},{x.Pending},{x.Queued},{x.Lag},");
                f.Append(CultureInfo.InvariantCulture, $"{x.Waiting},{x.Event},{x.Skipped},{x.Gc0},{x.Gc1},{x.Gc2},{x.GcGen},{x.GcPause:0.###}\n");
            }
            File.WriteAllText(stem + ".frames.csv", f.ToString());
            var r = new StringBuilder("ms,render_ms,sources_ms,samples,at\n");
            foreach (Render x in this.renders)
                r.Append(CultureInfo.InvariantCulture, $"{x.Ms:0.###},{x.RenderMs:0.###},{x.SourcesMs:0.###},{x.Samples},{x.At}\n");
            File.WriteAllText(stem + ".renders.csv", r.ToString());
        }

        private readonly record struct Frame(long Tick, double Ms, long Now, long Heard, int Pending, long Queued, long Lag,
                                             int Waiting, string Event, long Skipped, int Gc0, int Gc1, int Gc2, int GcGen,
                                             double GcPause);

        private readonly record struct Render(double Ms, double RenderMs, double SourcesMs, int Samples, long At);
    }

    /// <summary>Dev: a rack that tells <see cref="RunTiming"/> how long its sources took to render.</summary>
    internal sealed class TimedRack : ISourceRack
    {
        private readonly ISourceRack inner;
        private readonly RunTiming timing;

        public TimedRack(ISourceRack inner, RunTiming timing)
        {
            this.inner = inner;
            this.timing = timing;
        }

        public int Count => this.inner.Count;
        public int Grid => this.inner.Grid;

        public void Send(int unit, TimedMessage m) => this.inner.Send(unit, m);

        public void Render(int frames, double[][] left, double[][] right)
        {
            double began = this.timing.Now;
            this.inner.Render(frames, left, right);
            this.timing.SourcesMs += this.timing.Now - began;
        }

        public void Dispose() => this.inner.Dispose();
    }
}
