using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Junimo.Engine.Performance;
using JunimoOrchestra.Songs;
using MeltySynth;
using Microsoft.Xna.Framework.Audio;

namespace JunimoOrchestra.Audio
{
    /// <summary>
    /// Plays song blocks. Each run through a song is the whole chain of music/engine (<see cref="LiveStage"/>: the
    /// performer, one source per mixer strip, the mixer) on an audio thread of its own, into an output of its own. The
    /// audio thread renders only what the game has made safe, so the music is exactly what a headless replay of the
    /// same triggers renders, whatever the game's frame rate does. The mix follows her place in the song (late, it
    /// waits for her; stopped, it stops with her) and the run lasts until the master is over, or until she has left
    /// the song standing for 30 s. The output keeps it in step with the game: it plays
    /// the sample of the frame on screen, skipping ahead when the game caught up after a hitch, and waiting a moment
    /// (silence) when it ran dry. The sources are the open one (the mod's SoundFont, a MeltySynth per strip) or, where
    /// configured, the retro one: MU2000s in a local S-MU2000 build with the player's own ROMs (music/engine/retro),
    /// which never ship with the mod.
    /// </summary>
    internal sealed class SongPlayer
    {
        /// <summary>How far past the game's frame the audio renders, so what the output keeps queued (and how long a
        /// stall it rides out): 0.3 s, the most every block's lead allows (songblocks.py gives each at least 0.3 s; a
        /// song with a shorter one gets that, LiveStage sees to it).</summary>
        public const int Ahead = SynthEngine.SampleRate * 3 / 10;

        private readonly Func<ModConfig> config;
        private readonly Func<SoundFont?> font;
        private readonly Func<string?> fontError;
        private readonly Action<string> log;
        private readonly List<Session> draining = new();
        private Session? armed;
        private Session? playing;
        private int runs;
        private string? refused;

        public SongPlayer(Func<ModConfig> config, Func<SoundFont?> font, Func<string?> fontError, Action<string> log)
        {
            this.config = config;
            this.font = font;
            this.fontError = fontError;
            this.log = log;
        }

        /// <summary>Dev: when set, every run writes its master (24-bit WAV and float64 samples), its triggers (what
        /// music/engine/cli replay needs) and where its time went (<see cref="RunTiming"/>) to this folder.</summary>
        public string? CaptureDir { get; set; }

        /// <summary>How the last run went (dev).</summary>
        public string LastRun { get; private set; } = "(none)";

        public string Status =>
            $"armed={(this.armed == null ? "-" : $"{this.armed.Song.Id}{(this.armed.Ready ? " ready" : this.armed.Failed ? " failed" : " booting")}")} "
            + $"playing={(this.playing == null ? "-" : this.playing.Describe())} last={this.LastRun}";

        /// <summary>A run has ended since the last check (so a fresh one can be armed).</summary>
        public bool RunEnded { get; private set; }

        /// <summary>Get a fresh run of a song ready: its sources started (the retro MU2000s take a few seconds to power
        /// on), waiting for her first block.</summary>
        public void Arm(Song song)
        {
            if (this.armed != null && this.armed.Song == song && !this.armed.Failed)
                return;
            this.armed?.Dispose();
            this.armed = new Session(song, this.CreateRack, this.log);
        }

        public bool IsReady(string songId) => this.armed?.Song.Id == songId && this.armed.Ready;

        /// <summary>A song block was triggered on game frame <paramref name="tick"/>. Game thread.</summary>
        public void Trigger(Song song, Block block, long tick)
        {
            if (this.playing != null && !this.playing.Finished && this.playing.Song != song)
            {
                if (this.refused != song.Id)
                    this.log($"Song {song.Id}: ignored while {this.playing.Song.Id} is playing.");
                this.refused = song.Id;
                return;
            }
            if (this.playing == null || this.playing.Finished)
            {
                if (block.Frame * Performer.FrameSamples + block.Lead >= song.HeardUntil)
                    return;   // it starts after the master: a new run would have nothing to play
                if (this.playing != null)
                    this.draining.Add(this.playing);
                Session run;
                if (this.armed != null && this.armed.Song == song && !this.armed.Failed)
                {
                    run = this.armed;
                    this.armed = null;
                }
                else
                {
                    run = new Session(song, this.CreateRack, this.log);
                }
                this.runs++;
                if (this.CaptureDir != null)
                {
                    Directory.CreateDirectory(this.CaptureDir);
                    run.CaptureTo(Path.Combine(this.CaptureDir, $"{song.Id}-{DateTime.Now:HHmmss}-{this.runs}"));
                }
                this.playing = run;
            }
            this.playing.Trigger(block, tick);
        }

        /// <summary>Game frame <paramref name="tick"/> is over (its blocks triggered): let the audio go on, and hand
        /// what's rendered to the output. Game thread, every frame.</summary>
        public void Update(long tick, float gain)
        {
            this.RunEnded = false;
            if (this.playing != null)
            {
                this.playing.FrameDone(tick);
                this.playing.Pump(tick, gain);
                if (this.playing.Over)
                {
                    this.End(this.playing);
                    this.playing = null;
                }
            }
            for (int i = this.draining.Count - 1; i >= 0; i--)
            {
                Session s = this.draining[i];
                s.Pump(tick, gain);
                if (s.Over)
                {
                    this.End(s);
                    this.draining.RemoveAt(i);
                }
            }
        }

        public void StopAll()
        {
            foreach (Session s in this.draining)
                this.End(s);
            this.draining.Clear();
            if (this.playing != null)
                this.End(this.playing);
            this.playing = null;
            this.armed?.Dispose();
            this.armed = null;
        }

        private void End(Session s)
        {
            this.LastRun = $"{s.Song.Id} {s.Describe()}";
            s.Dispose();
            this.log($"Song run over: {this.LastRun}");
            this.RunEnded = true;
        }

        private ISourceRack CreateRack(Song song)
        {
            ModConfig cfg = this.config();
            int strips = song.Mix.Strips.Count;
            if (string.Equals(cfg.SongSource, "retro", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(cfg.RetroHelper) && Directory.Exists(cfg.RetroRoms))
                    return new RetroRack(cfg.RetroHelper, cfg.RetroRoms, strips);
                this.log("The retro source isn't set up (RetroHelper, RetroRoms in config.json): playing the open one.");
            }
            for (int waited = 0; this.font() == null; waited += 50)
            {
                if (this.fontError() is string error)
                    throw new InvalidOperationException("the SoundFont didn't load: " + error);
                if (waited > 60_000)
                    throw new TimeoutException("the SoundFont is still loading");
                Thread.Sleep(50);
            }
            return new MeltyRack(this.font()!, strips);
        }

        /// <summary>A stretch of the performance, rendered.</summary>
        private sealed class Chunk
        {
            public const int Size = 1024;
            public readonly double[] L = new double[Size];
            public readonly double[] R = new double[Size];
            public long At;     // the performance's sample of L[0]
            public int Count;
            public int Used;    // already handed over (or skipped)
            public long Next => this.At + this.Used;
        }

        /// <summary>One run through a song.</summary>
        private sealed class Session : IDisposable
        {
            private const int Slack = 2 * Performer.FrameSamples;   // how far behind the output may drift before it skips
            private const int MaxBuffer = 2048;                     // samples per buffer handed to the output

            private readonly Task<ISourceRack> rack;
            private readonly Action<string> log;
            private readonly List<(Block Block, long Tick)> early = new();
            private readonly ConcurrentQueue<Chunk> rendered = new();
            private readonly ConcurrentBag<Chunk> spare = new();
            private readonly List<Chunk> waiting = new();
            private readonly Queue<int> submitted = new();
            private LiveStage? live;
            private Thread? audio;
            private volatile bool stop;
            private volatile Exception? error;
            private bool reported;
            private DynamicSoundEffectInstance? output;
            private bool started;
            private long submittedEnd;
            private byte[] bytes = new byte[MaxBuffer * 4];
            private string? capture;
            private RunTiming? timing;

            public Session(Song song, Func<Song, ISourceRack> createRack, Action<string> log)
            {
                this.Song = song;
                this.log = log;
                this.rack = Task.Run(() => createRack(song));
            }

            public Song Song { get; }
            public bool Ready => this.rack.IsCompletedSuccessfully;
            public bool Failed => this.rack.IsFaulted || this.error != null;

            /// <summary>Samples skipped to stay in step with the game.</summary>
            public long Dropped { get; private set; }

            /// <summary>Times the output ran dry.</summary>
            public int Starved { get; private set; }

            /// <summary>Nothing more will be rendered.</summary>
            public bool Finished => (this.live?.Done ?? false) || this.Failed;

            /// <summary>Everything rendered has been played (or the run failed).</summary>
            public bool Over => this.Finished && this.rendered.IsEmpty && this.waiting.Count == 0
                                && (this.output == null || this.output.PendingBufferCount == 0);

            public string Describe()
            {
                if (this.Failed)
                    return $"failed: {(this.error ?? this.rack.Exception?.GetBaseException())?.Message}";
                if (this.live == null)
                    return this.early.Count > 0 ? $"waiting for its sources ({this.early.Count} blocks so far)" : "not started";
                double seconds = (this.live.Position - this.live.Start) / (double)SynthEngine.SampleRate;
                return $"{seconds:0.0} s rendered, {this.live.Triggers.Count} blocks, late {this.live.Late}, "
                       + $"skipped {this.Dropped / (double)SynthEngine.SampleRate:0.000} s, ran dry {this.Starved}x"
                       + (this.capture != null ? $", captured to {this.capture}.*" : "");
            }

            public void CaptureTo(string stem)
            {
                this.capture = stem;
                this.timing = new RunTiming();
            }

            public void Trigger(Block b, long tick)
            {
                if (this.live != null)
                    this.live.Trigger(b, tick);
                else
                    this.early.Add((b, tick));
            }

            public void FrameDone(long tick)
            {
                if (this.live == null && this.early.Count > 0 && this.Ready)
                    this.Begin();
                this.live?.FrameDone(tick);
                if (this.Failed && !this.reported)
                {
                    this.reported = true;
                    this.log($"Song {this.Song.Id} can't play: {this.Describe()}");
                }
            }

            private void Begin()
            {
                ISourceRack sources = this.timing != null ? new TimedRack(this.rack.Result, this.timing) : this.rack.Result;
                var stage = new LiveStage(this.Song.Blocks, this.Song.Mix, sources, Ahead);
                if (this.capture != null)
                    stage.Capture(this.capture + ".wav", this.capture + ".f64");
                foreach ((Block b, long tick) in this.early)
                    stage.Trigger(b, tick);
                this.early.Clear();
                this.live = stage;
                this.audio = new Thread(this.Render)
                {
                    IsBackground = true,
                    Name = "Junimo Orchestra song",
                };
                this.audio.Start();
            }

            /// <summary>Audio thread: render whatever the game has made safe.</summary>
            private void Render()
            {
                string raised = AudioThread.Raise();
                if (raised.StartsWith("couldn't"))
                    this.log($"Song {this.Song.Id}: the audio thread {raised}.");
                try
                {
                    LiveStage stage = this.live!;
                    while (!this.stop && !stage.Done)
                    {
                        if (this.rendered.Count > 10 * SynthEngine.SampleRate / Chunk.Size)
                        {
                            Thread.Sleep(5);   // nobody is taking it: wait rather than pile it up
                            continue;
                        }
                        Chunk c = this.spare.TryTake(out Chunk? old) ? old : new Chunk();
                        double began = this.timing?.Now ?? 0;
                        if (this.timing != null)
                            this.timing.SourcesMs = 0;
                        c.Count = stage.Render(Chunk.Size, c.L, c.R, out c.At);
                        c.Used = 0;
                        if (c.Count > 0)
                            this.timing?.Rendered(began, c.Count, c.At);
                        if (c.Count == 0)
                        {
                            this.spare.Add(c);
                            Thread.Sleep(1);
                            continue;
                        }
                        this.rendered.Enqueue(c);
                    }
                }
                catch (Exception ex)
                {
                    this.error = ex;
                }
            }

            /// <summary>Game thread: hand what's rendered to the output, in step with frame <paramref name="tick"/>.</summary>
            public void Pump(long tick, float gain)
            {
                int starved = this.Starved;
                long dropped = this.Dropped;
                this.Feed(tick, gain);
                if (this.timing == null || this.live?.Origin is not long origin)
                    return;
                int pending = this.output?.PendingBufferCount ?? 0;
                while (this.submitted.Count > pending)
                    this.submitted.Dequeue();
                long queued = this.submitted.Sum();
                long frame = (tick - origin) * Performer.FrameSamples;
                long lag = frame + Ahead - this.live.Position;   // what it could render before this frame, less what it has
                string ev = this.Starved != starved ? "dry" : this.Dropped != dropped ? "skip" : "";
                this.timing.FrameDone(tick, frame, this.submittedEnd - queued, pending, queued, lag, this.waiting.Count, ev,
                                      this.Dropped - dropped);
            }

            private void Feed(long tick, float gain)
            {
                while (this.rendered.TryDequeue(out Chunk? c))
                    this.waiting.Add(c);
                if (this.waiting.Count == 0 || this.live?.Origin is not long origin)
                    return;
                long now = (tick - origin) * Performer.FrameSamples;   // the performance's sample of this frame
                this.output ??= new DynamicSoundEffectInstance(SynthEngine.SampleRate, AudioChannels.Stereo);
                int pending = this.output.PendingBufferCount;
                while (this.submitted.Count > pending)
                    this.submitted.Dequeue();
                if (!this.started || pending == 0)
                {
                    // starting, or the output ran dry: from this frame's sample on, silence where nothing's rendered yet
                    if (this.started)
                        this.Starved++;
                    this.Skip(now);
                    if (this.waiting.Count == 0)
                        return;
                    long first = this.waiting[0].Next;
                    this.submittedEnd = now;
                    if (first > now)
                        this.Submit(null, (int)Math.Min(first - now, SynthEngine.SampleRate), gain);
                }
                else
                {
                    long heard = this.submittedEnd - this.submitted.Sum();   // about what the output is playing
                    if (now - heard > Slack)
                        this.Skip(this.waiting[0].Next + (now - heard));
                }
                foreach (Chunk c in this.waiting)
                {
                    this.Submit(c, c.Count - c.Used, gain);
                    this.spare.Add(c);
                }
                this.waiting.Clear();
                if (!this.started)
                {
                    this.output.Play();
                    this.started = true;
                }
            }

            /// <summary>Drop what's waiting before the performance's sample <paramref name="until"/>.</summary>
            private void Skip(long until)
            {
                while (this.waiting.Count > 0)
                {
                    Chunk c = this.waiting[0];
                    int drop = (int)Math.Clamp(until - c.Next, 0, c.Count - c.Used);
                    c.Used += drop;
                    this.Dropped += drop;
                    if (c.Used < c.Count)
                        return;
                    this.waiting.RemoveAt(0);
                    this.spare.Add(c);
                }
            }

            /// <summary>Hand <paramref name="count"/> samples of a chunk (or silence) to the output, as 16-bit PCM.</summary>
            private void Submit(Chunk? c, int count, float gain)
            {
                for (int done = 0; done < count;)
                {
                    int n = Math.Min(MaxBuffer, count - done);
                    for (int i = 0; i < n; i++)
                    {
                        short l = 0, r = 0;
                        if (c != null)
                        {
                            l = Pcm(c.L[c.Used + i] * gain);
                            r = Pcm(c.R[c.Used + i] * gain);
                        }
                        int o = i * 4;
                        this.bytes[o] = (byte)l;
                        this.bytes[o + 1] = (byte)(l >> 8);
                        this.bytes[o + 2] = (byte)r;
                        this.bytes[o + 3] = (byte)(r >> 8);
                    }
                    this.output!.SubmitBuffer(this.bytes, 0, n * 4);
                    this.submitted.Enqueue(n);
                    if (c != null)
                    {
                        c.Used += n;
                        this.submittedEnd = c.Next;
                    }
                    else
                    {
                        this.submittedEnd += n;
                    }
                    done += n;
                }
            }

            private static short Pcm(double x) => (short)Math.Round(Math.Clamp(x, -1.0, 32767.0 / 32768.0) * 32768.0);

            public void Dispose()
            {
                this.stop = true;
                try
                {
                    this.output?.Stop();
                    this.output?.Dispose();
                }
                catch
                {
                    // shutting down anyway
                }
                // the rest off the game thread: the audio thread finishing its chunk, the retro helper powering off
                LiveStage? stage = this.live;
                Thread? thread = this.audio;
                string? capture = this.capture;
                RunTiming? timing = this.timing;
                Task<ISourceRack> rack = this.rack;
                Task.Run(() =>
                {
                    thread?.Join(5000);
                    if (stage != null)
                    {
                        if (capture != null)
                        {
                            stage.WriteTriggers(capture + ".triggers.txt");
                            timing?.Write(capture);
                        }
                        stage.Dispose();
                    }
                    else
                    {
                        rack.ContinueWith(t =>
                        {
                            if (t.IsCompletedSuccessfully)
                                t.Result.Dispose();
                        });
                    }
                });
            }
        }
    }
}
