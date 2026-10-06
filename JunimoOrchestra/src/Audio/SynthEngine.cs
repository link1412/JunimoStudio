using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JunimoOrchestra.Data;
using MeltySynth;
using Microsoft.Xna.Framework.Audio;

namespace JunimoOrchestra.Audio
{
    /// <summary>Per-note channel parameters (MIDI controller values, 0-127).</summary>
    internal readonly record struct ChannelParams(int Bank, int Program, int Volume, int Expression, int Reverb, int Chorus, int Vibrato, int Pan);

    /// <summary>A note ready for the synth: key/velocity plus start and length in audio frames relative to "now".</summary>
    internal readonly record struct ScheduledNote(int Key, int Velocity, long StartFrame, long LengthFrames);

    /// <summary>Why a SoundFont couldn't be loaded: the file, what went wrong ("missing", "sf3": compressed, which
    /// MeltySynth can't read, "invalid") and the details.</summary>
    internal sealed record FontProblem(string File, string Kind, string Detail)
    {
        public override string ToString() => $"{this.File}: {this.Kind} ({this.Detail})";
    }

    /// <summary>
    /// Real-time SoundFont synthesizer. MeltySynth renders on a dedicated thread into a streaming
    /// <see cref="DynamicSoundEffectInstance"/>; notes are scheduled with sample accuracy so a phrase
    /// keeps its rhythm even if the game stutters.
    /// </summary>
    internal sealed class SynthEngine : IDisposable
    {
        public const int SampleRate = 44100;
        private const int ChunkFrames = 512;          // ~11.6 ms per submitted buffer
        private const int MaxPendingBuffers = 8;       // ~93 ms of queued audio: MonoGame only recycles buffers once per game frame
        private const int SynthBlock = 64;             // MeltySynth block size (1.45 ms scheduling grain)
        private const int ChannelCount = 16;

        /// <summary>The game updates at a fixed 60 Hz, so one game tick is exactly this many output frames.</summary>
        private const int FramesPerTick = SampleRate / 60;

        /// <summary>Headroom added when (re)anchoring game ticks to the output, so later ticks never land in the past.</summary>
        private const int AnchorMargin = 2 * ChunkFrames;

        private static double latencyMs = MaxPendingBuffers * ChunkFrames * 1000.0 / SampleRate;

        /// <summary>Extra scheduling headroom (ms), e.g. while filming: a game hitch shorter than the headroom never shifts
        /// the music, at the cost of the blocks sounding this much later after they're triggered (their hops follow).</summary>
        public static int ExtraLatencyMs;

        /// <summary>How long after a block is triggered its first note is heard, used to line visuals up with the sound.</summary>
        public static double LatencyMs => latencyMs;

        private readonly Action<string> log;
        private readonly object gate = new();
        private readonly PriorityQueue<Event, (long, long)> queue = new();
        private readonly Dictionary<long, int> groupGeneration = new();
        private readonly List<ActiveNote> active = new();
        private readonly ChannelState[] channels = Enumerable.Range(0, ChannelCount).Select(_ => new ChannelState()).ToArray();

        private Synthesizer? synth;
        private volatile Synthesizer? nextSynth;     // a newly loaded font, for the render thread to switch to
        private DynamicSoundEffectInstance? output;
        private Thread? thread;
        private volatile bool running;
        private long renderedFrames;
        private long starvedCount; // times the output ran dry before we refilled it (diagnostics)
        private long anchorTick = long.MinValue;
        private long anchorFrame;
        private long reanchorCount; // diagnostics
        private readonly List<string> reanchorLog = new(); // diagnostics: "tick:shift ms" for each re-anchor
        private long sequence;
        private long noteToken;
        private volatile float gain = 1f;

        /// <summary>Make-up gain: MeltySynth's master volume (0.5) plus GeneralUser GS's levels sit ~10 dB under vanilla sound effects.</summary>
        private const float OutputBoost = 3f;
        private Room pendingRoom = Room.Hall;
        private volatile bool roomDirty = true;
        private Task? loadTask;

        // optional capture of the output for diagnostics (render thread writes, game thread starts/stops)
        private volatile BinaryWriter? capture;
        private long captureFramesLeft;
        private string? capturePath;

        public bool IsReady => this.synth != null && this.running;

        /// <summary>Why no SoundFont could be loaded at all (the blocks are silent).</summary>
        public string? LoadError { get; private set; }

        /// <summary>Why the chosen SoundFont couldn't be loaded, when the built-in one plays instead (null when fine).</summary>
        public FontProblem? FontProblem { get; private set; }

        /// <summary>A SoundFont is being loaded (the one before keeps playing until it's ready).</summary>
        public bool IsLoading => this.loadTask is { IsCompleted: false };

        public IReadOnlyList<PresetInfo> Presets { get; private set; } = Array.Empty<PresetInfo>();

        /// <summary>The loaded SoundFont's own name (its INFO bank name).</summary>
        public string FontName { get; private set; } = "";

        /// <summary>The file it was loaded from.</summary>
        public string FontPath { get; private set; } = "";

        /// <summary>Counts the SoundFonts loaded, so whoever lists their presets knows when to look again.</summary>
        public int Generation => this.generation;
        private volatile int generation;

        /// <summary>The SoundFont, once loaded (song blocks' open source plays it too).</summary>
        public SoundFont? Font { get; private set; }

        /// <summary>The next audio frame that will be rendered.</summary>
        public long Now => Interlocked.Read(ref this.renderedFrames);

        public int ActiveVoices => this.synth?.ActiveVoiceCount ?? 0;

        public SynthEngine(Action<string> log)
        {
            this.log = log;
        }

        /****
        ** Lifecycle
        ****/
        /// <summary>Parse a SoundFont on a worker thread; call <see cref="TryStart"/> from the game thread afterwards. Also
        /// switches to another font while playing: the one before plays on until the new one is ready.</summary>
        /// <param name="soundFontPath">The font to play.</param>
        /// <param name="fallbackPath">The font to play instead if that one can't be loaded (the built-in one).</param>
        public void BeginLoad(string soundFontPath, string? fallbackPath = null)
        {
            Task? before = this.loadTask;
            this.loadTask = Task.Run(() =>
            {
                before?.Wait();   // one at a time, in order
                FontProblem? problem = null;
                foreach (string path in fallbackPath == null || fallbackPath == soundFontPath
                    ? new[] { soundFontPath } : new[] { soundFontPath, fallbackPath })
                {
                    try
                    {
                        this.Load(path);
                        this.FontProblem = problem;
                        this.LoadError = null;
                        return;
                    }
                    catch (Exception ex)
                    {
                        problem ??= Describe(path, ex);
                        this.log($"Couldn't load the SoundFont {path}: {ex}");
                    }
                }
                this.FontProblem = problem;
                if (this.synth == null)
                    this.LoadError = problem?.ToString();
            });
        }

        private void Load(string path)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SoundFont soundFont;
            using (FileStream stream = File.OpenRead(path))
                soundFont = new SoundFont(stream);
            var settings = new SynthesizerSettings(SampleRate)
            {
                BlockSize = SynthBlock,
                MaximumPolyphony = 128,
                EnableReverbAndChorus = true
            };
            var synth = new Synthesizer(soundFont, settings);
            this.Presets = soundFont.Presets
                .Select(p => new PresetInfo(p.BankNumber, p.PatchNumber, p.Name))
                .OrderBy(p => p.Program).ThenBy(p => p.Bank)
                .ToArray();
            this.Font = soundFont;
            this.FontName = soundFont.Info.BankName ?? "";
            this.FontPath = path;
            if (this.synth == null)
                this.synth = synth;
            else
                this.nextSynth = synth;
            this.generation++;
            this.log($"SoundFont '{this.FontName}' loaded in {watch.ElapsedMilliseconds} ms ({soundFont.Presets.Count} presets).");
        }

        /// <summary>Why a SoundFont couldn't be loaded, for the player.</summary>
        private static FontProblem Describe(string path, Exception ex) => new(Path.GetFileName(path), ex switch
        {
            FileNotFoundException or DirectoryNotFoundException => "missing",
            NotSupportedException when ex.Message.Contains("SoundFont3") => "sf3",
            _ => "invalid"
        }, ex.Message);

        /// <summary>Create the audio output once the SoundFont is parsed. Must run on the game thread.</summary>
        public bool TryStart()
        {
            if (this.running || this.synth == null)
                return this.running;
            try
            {
                this.output = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Stereo);
                this.running = true;
                this.thread = new Thread(this.RenderLoop)
                {
                    IsBackground = true,
                    Name = "Junimo Orchestra synth",
                    Priority = ThreadPriority.AboveNormal
                };
                this.thread.Start();
                return true;
            }
            catch (Exception ex)
            {
                this.LoadError = ex.Message;
                this.log("Couldn't open the audio output: " + ex);
                this.running = false;
                return false;
            }
        }

        public void Dispose()
        {
            this.running = false;
            this.thread?.Join(500);
            try
            {
                this.output?.Stop();
                this.output?.Dispose();
            }
            catch
            {
                // shutting down anyway
            }
            this.StopCapture();
        }

        /****
        ** Game-thread API
        ****/
        /// <summary>Overall output gain (already includes the player's volume settings).</summary>
        public void SetGain(float value) => this.gain = Math.Max(0f, value);

        public void SetRoom(Room room)
        {
            this.pendingRoom = room;
            this.roomDirty = true;
        }

        /// <summary>
        /// Play a phrase for a group (usually one block). Anything the group was still playing or
        /// waiting to play is cut off first, so re-triggering a block restarts it from the top.
        /// </summary>
        /// <param name="gameTick">The game tick the phrase was triggered on. Phrases triggered on game ticks are placed
        /// exactly <see cref="FramesPerTick"/> apart per tick, so rhythm comes from the game clock (what you see)
        /// instead of from when the render thread happened to run. Null plays as soon as possible (menu previews).</param>
        public void PlayPhrase(long group, ChannelParams parameters, IReadOnlyList<ScheduledNote> notes, long? gameTick = null)
        {
            if (!this.IsReady)
                return;
            lock (this.gate)
            {
                int gen = this.RestartGroupLocked(group);
                long start = this.StartFrameLocked(gameTick);
                foreach (ScheduledNote note in notes)
                {
                    long token = ++this.noteToken;
                    long on = start + Math.Max(0, note.StartFrame);
                    long off = on + Math.Max(SynthBlock, note.LengthFrames);
                    this.Enqueue(new Event(EventType.NoteOn, group, gen, token, note.Key, note.Velocity, parameters), on);
                    this.Enqueue(new Event(EventType.NoteOff, group, gen, token, note.Key, 0, parameters), off);
                }
            }
        }

        /// <summary>The output frame a phrase triggered on a game tick should start at.</summary>
        private long StartFrameLocked(long? gameTick)
        {
            long earliest = this.Now + SynthBlock; // first block that hasn't been rendered yet
            if (gameTick is not long tick)
                return earliest;

            long ideal = this.anchorFrame + (tick - this.anchorTick) * FramesPerTick;
            bool lost = this.anchorTick == long.MinValue
                || ideal < earliest                                               // game hitched or was paused: we'd be late
                || ideal > earliest + 2 * (MaxPendingBuffers * ChunkFrames + AnchorMargin)
                    + (long)ExtraLatencyMs * SampleRate / 1000;                    // clocks drifted apart
            if (lost)
            {
                long before = ideal;
                bool again = this.anchorTick != long.MinValue;
                // anchor on what's being heard right now plus a full output queue, not on how far the render thread
                // happens to be ahead: that lead swings by several buffers, and anchoring on a short queue would make a
                // later full one look "late" and shift everything after it
                int queued = (this.output?.PendingBufferCount ?? MaxPendingBuffers) * ChunkFrames;
                long heard = this.Now - queued;
                this.anchorTick = tick;
                this.anchorFrame = ideal = Math.Max(earliest, heard + MaxPendingBuffers * ChunkFrames) + AnchorMargin
                    + (long)ExtraLatencyMs * SampleRate / 1000;
                latencyMs = (ideal - heard) * 1000.0 / SampleRate;
                if (again)
                {
                    this.reanchorCount++;
                    if (this.reanchorLog.Count < 50)
                        this.reanchorLog.Add($"{tick}:{(ideal - before) * 1000.0 / SampleRate:+0.0;-0.0}");
                }
            }
            return ideal;
        }

        /// <summary>Release everything a group is playing and drop its pending notes.</summary>
        public void StopGroup(long group)
        {
            lock (this.gate)
                this.RestartGroupLocked(group);
        }

        /// <summary>Release every note and clear every pending event.</summary>
        public void StopAll()
        {
            lock (this.gate)
            {
                this.queue.Clear();
                this.groupGeneration.Clear();
                this.Enqueue(new Event(EventType.ReleaseAll, 0, 0, 0, 0, 0, default), 0);
            }
        }

        /// <summary>Write the next few seconds of output to a WAV file (diagnostics).</summary>
        public void StartCapture(string path, double seconds)
        {
            this.StopCapture();
            var writer = new BinaryWriter(File.Create(path));
            WavHeader(writer, 0);
            this.capturePath = path;
            this.captureFramesLeft = (long)(seconds * SampleRate);
            this.capture = writer;
        }

        /****
        ** Render thread
        ****/
        private void RenderLoop()
        {
            var left = new float[ChunkFrames];
            var right = new float[ChunkFrames];
            var bytes = new byte[ChunkFrames * 4];
            bool started = false;

            while (this.running)
            {
                try
                {
                    DynamicSoundEffectInstance output = this.output!;
                    if (output.PendingBufferCount >= MaxPendingBuffers)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (this.nextSynth is Synthesizer next)
                    {
                        // another font: what was sounding stops, every channel is set up again on its next note
                        lock (this.gate)
                        {
                            this.synth = next;
                            this.nextSynth = null;
                            this.active.Clear();
                            foreach (ChannelState ch in this.channels)
                            {
                                ch.Held.Clear();
                                ch.Initialised = false;
                            }
                        }
                        this.roomDirty = true;
                    }

                    if (this.roomDirty)
                    {
                        this.roomDirty = false;
                        this.ApplyRoom(this.pendingRoom);
                    }

                    if (started && output.PendingBufferCount == 0)
                        Interlocked.Increment(ref this.starvedCount);
                    this.RenderChunk(left, right);
                    this.Convert(left, right, bytes);
                    output.SubmitBuffer(bytes);
                    if (!started)
                    {
                        output.Play();
                        started = true;
                    }
                }
                catch (Exception ex)
                {
                    this.log("Synth thread error: " + ex);
                    Thread.Sleep(50);
                }
            }
        }

        private void RenderChunk(float[] left, float[] right)
        {
            Synthesizer synth = this.synth!;
            long frame = this.Now;
            for (int offset = 0; offset < ChunkFrames; offset += SynthBlock)
            {
                lock (this.gate)
                {
                    long due = frame + offset + SynthBlock - 1;
                    while (this.queue.TryPeek(out Event ev, out (long time, long seq) key) && key.time <= due)
                    {
                        this.queue.Dequeue();
                        this.Process(synth, ev);
                    }
                }
                synth.Render(left.AsSpan(offset, SynthBlock), right.AsSpan(offset, SynthBlock));
            }
            Interlocked.Add(ref this.renderedFrames, ChunkFrames);
        }

        private void Process(Synthesizer synth, in Event ev)
        {
            switch (ev.Type)
            {
                case EventType.ReleaseAll:
                    synth.NoteOffAll(false);
                    this.active.Clear();
                    foreach (ChannelState ch in this.channels)
                        ch.Held.Clear();
                    return;

                case EventType.ReleaseGroup:
                    for (int i = this.active.Count - 1; i >= 0; i--)
                    {
                        if (this.active[i].Group == ev.Group)
                            this.ReleaseAt(synth, i);
                    }
                    return;
            }

            // events from a restarted group are stale
            if (!this.groupGeneration.TryGetValue(ev.Group, out int gen) || gen != ev.Generation)
                return;

            if (ev.Type == EventType.NoteOn)
            {
                int channel = this.AllocateChannel(synth, ev.Params, ev.Key);
                synth.NoteOn(channel, ev.Key, ev.Velocity);
                this.channels[channel].Held.Add(ev.Key);
                this.active.Add(new ActiveNote(ev.Group, ev.Token, channel, ev.Key));
            }
            else if (ev.Type == EventType.NoteOff)
            {
                long token = ev.Token;
                int index = this.active.FindIndex(a => a.Token == token);
                if (index >= 0)
                    this.ReleaseAt(synth, index);
            }
        }

        private void ReleaseAt(Synthesizer synth, int index)
        {
            ActiveNote note = this.active[index];
            this.active.RemoveAt(index);
            List<int> held = this.channels[note.Channel].Held;
            held.Remove(note.Key);
            // another block may still hold the same key on this channel; its own note-off will release both
            if (!held.Contains(note.Key))
                synth.NoteOff(note.Channel, note.Key);
        }

        /// <summary>Find a channel already configured for these parameters, or repurpose the least recently used one.</summary>
        private int AllocateChannel(Synthesizer synth, in ChannelParams p, int key)
        {
            long now = this.Now;
            int best = -1;
            for (int i = 0; i < ChannelCount; i++)
            {
                if (i == 9)
                    continue; // MeltySynth offsets the bank on channel 10; every other channel takes bank 128 for drums directly
                ChannelState ch = this.channels[i];
                if (ch.Initialised && ch.Params == p && !ch.Held.Contains(key))
                {
                    best = i;
                    break;
                }
            }
            if (best < 0)
            {
                long oldest = long.MaxValue;
                for (int pass = 0; pass < 2 && best < 0; pass++)
                {
                    for (int i = 0; i < ChannelCount; i++)
                    {
                        if (i == 9)
                            continue;
                        ChannelState ch = this.channels[i];
                        if (pass == 0 && ch.Held.Count > 0)
                            continue; // prefer silent channels so changing controllers can't touch other notes
                        if (ch.LastUsed < oldest)
                        {
                            oldest = ch.LastUsed;
                            best = i;
                        }
                    }
                }
                this.Configure(synth, best, p);
            }
            this.channels[best].LastUsed = now;
            return best;
        }

        private void Configure(Synthesizer synth, int channel, in ChannelParams p)
        {
            ChannelState ch = this.channels[channel];
            ChannelParams old = ch.Params;
            const int cc = 0xB0;
            if (old.Bank != p.Bank || old.Program != p.Program || !ch.Initialised)
            {
                synth.ProcessMidiMessage(channel, cc, 0, p.Bank);
                synth.ProcessMidiMessage(channel, 0xC0, p.Program, 0);
            }
            if (old.Volume != p.Volume || !ch.Initialised)
                synth.ProcessMidiMessage(channel, cc, 7, p.Volume);
            if (old.Expression != p.Expression || !ch.Initialised)
                synth.ProcessMidiMessage(channel, cc, 11, p.Expression);
            if (old.Reverb != p.Reverb || !ch.Initialised)
                synth.ProcessMidiMessage(channel, cc, 91, p.Reverb);
            if (old.Chorus != p.Chorus || !ch.Initialised)
                synth.ProcessMidiMessage(channel, cc, 93, p.Chorus);
            if (old.Vibrato != p.Vibrato || !ch.Initialised)
                synth.ProcessMidiMessage(channel, cc, 1, p.Vibrato);
            if (old.Pan != p.Pan || !ch.Initialised)
                synth.ProcessMidiMessage(channel, cc, 10, p.Pan);
            ch.Params = p;
            ch.Initialised = true;
        }

        private void Convert(float[] left, float[] right, byte[] bytes)
        {
            float g = this.gain * OutputBoost;
            BinaryWriter? cap = this.capture;
            for (int i = 0; i < ChunkFrames; i++)
            {
                short l = ToPcm(left[i] * g);
                short r = ToPcm(right[i] * g);
                int o = i * 4;
                bytes[o] = (byte)l;
                bytes[o + 1] = (byte)(l >> 8);
                bytes[o + 2] = (byte)r;
                bytes[o + 3] = (byte)(r >> 8);
            }
            if (cap != null)
            {
                cap.Write(bytes);
                this.captureFramesLeft -= ChunkFrames;
                if (this.captureFramesLeft <= 0)
                    this.StopCapture();
            }
        }

        /// <summary>Soft-knee limiter so stacked chords saturate gently instead of clipping.</summary>
        private static short ToPcm(float x)
        {
            float a = Math.Abs(x);
            if (a > 0.75f)
            {
                float over = (a - 0.75f) / 0.25f;
                a = 0.75f + 0.25f * over / (1f + over);
            }
            float y = Math.Sign(x) * Math.Min(a, 1f);
            return (short)(y * 32767f);
        }

        /****
        ** Helpers
        ****/
        private int RestartGroupLocked(long group)
        {
            int gen = this.groupGeneration.TryGetValue(group, out int g) ? g + 1 : 1;
            this.groupGeneration[group] = gen;
            this.Enqueue(new Event(EventType.ReleaseGroup, group, gen, 0, 0, 0, default), 0);
            return gen;
        }

        private void Enqueue(in Event ev, long time)
        {
            this.queue.Enqueue(ev, (time, ++this.sequence));
        }

        private void ApplyRoom(Room room)
        {
            try
            {
                (float size, float damp, float wet) = room switch
                {
                    Room.Dry => (0.3f, 0.6f, 0f),
                    Room.Cabin => (0.25f, 0.7f, 0.2f),
                    Room.Room => (0.45f, 0.55f, 0.27f),
                    Room.Hall => (0.7f, 0.4f, 0.33f),
                    Room.Church => (0.88f, 0.25f, 0.4f),
                    Room.Cave => (0.97f, 0.1f, 0.45f),
                    _ => (0.5f, 0.5f, 1f / 3f)
                };
                this.synth!.SetReverbRoom(size, damp, wet);
            }
            catch (Exception ex)
            {
                this.log("Couldn't set the reverb room: " + ex.Message);
            }
        }

        private void StopCapture()
        {
            BinaryWriter? writer = this.capture;
            this.capture = null;
            if (writer == null)
                return;
            long dataBytes = writer.BaseStream.Length - 44;
            writer.Seek(0, SeekOrigin.Begin);
            WavHeader(writer, (int)dataBytes);
            writer.Dispose();
            this.log($"Captured audio to {this.capturePath}.");
        }

        private static void WavHeader(BinaryWriter w, int dataBytes)
        {
            w.Write("RIFF"u8);
            w.Write(36 + dataBytes);
            w.Write("WAVE"u8);
            w.Write("fmt "u8);
            w.Write(16);
            w.Write((short)1);
            w.Write((short)2);
            w.Write(SampleRate);
            w.Write(SampleRate * 4);
            w.Write((short)4);
            w.Write((short)16);
            w.Write("data"u8);
            w.Write(dataBytes);
        }

        private enum EventType
        {
            NoteOn,
            NoteOff,
            ReleaseGroup,
            ReleaseAll
        }

        private readonly record struct Event(EventType Type, long Group, int Generation, long Token, int Key, int Velocity, ChannelParams Params);

        private readonly record struct ActiveNote(long Group, long Token, int Channel, int Key);

        private sealed class ChannelState
        {
            public ChannelParams Params;
            public bool Initialised;
            public long LastUsed;
            public readonly List<int> Held = new();
        }
    }
}
