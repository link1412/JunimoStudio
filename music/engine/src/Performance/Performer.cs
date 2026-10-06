using System;
using System.Collections.Generic;
using System.Linq;

namespace Junimo.Engine.Performance
{
    /// <summary>A MIDI message for the synth at an exact output sample.</summary>
    public readonly record struct TimedMessage(long Sample, byte[] Bytes);

    /// <summary>
    /// Turns triggered blocks into the messages a synth receives: blocks in, MIDI out, the way a DAW plays its
    /// clips. The mod and the headless tools share it, so a performance sends the very same bytes on the very same
    /// samples whichever synth listens.
    ///
    /// A block triggered at game frame f starts at f x 735 samples plus its lead; an event dt ticks after its home
    /// falls on the first sample at or after that start + its phase + the exact time of dt at the block's own tempo.
    /// Events of all blocks go out in time order, within a sample in song order. Just before a part's first event in a
    /// block, the settings of its starting state that differ from what the synth has are sent, and settings the synth
    /// has that the state doesn't go back to the device's value; a block that follows the one before it needs none.
    /// The world's state (system reset, effect types) is checked the same way before a block's first event. The first
    /// block of a performance lines the song's tick 0 up with the synth's grid (MeltySynth takes messages every 64
    /// samples), so the song falls on the grid where a whole-song render puts it.
    /// </summary>
    public sealed class Performer
    {
        public const int FrameSamples = ExactClock.Rate / 60;

        private readonly int ppq;
        private readonly int grid;
        private readonly ExactClock clock;
        private readonly PriorityQueue<Item, Key> queue = new();
        private readonly Dictionary<string, int>[] have = Enumerable.Range(0, 16).Select(_ => new Dictionary<string, int>()).ToArray();
        private readonly Pointer[] pointer = new Pointer[16];
        private Dictionary<string, string> world = new();
        private long order;
        private long? shift;

        /// <param name="grid">Samples between the moments the synth takes messages: 64 for MeltySynth, 1 for MU2000.</param>
        public Performer(SongBlocks song, int grid)
        {
            this.ppq = song.Ppq;
            this.grid = grid;
            this.clock = new ExactClock(song.Ppq, song.Tempo);
        }

        /// <summary>Settings sent because the synth didn't have a block's starting state. Following the plan: none.</summary>
        public List<string> Added { get; } = new();

        /// <summary>Starting states the synth still didn't match after they were sent (a setting with no known device
        /// value to go back to). Should stay empty.</summary>
        public List<string> Mismatches { get; } = new();

        public bool Idle => this.queue.Count == 0;

        /// <summary>Schedule everything a block sends, triggered at the given game frame.</summary>
        public void Trigger(Block b, long frame)
        {
            long tau = frame * FrameSamples;
            this.shift ??= ExactClock.Mod(
                -(tau + b.Lead - ExactClock.FloorDiv(this.clock.Numerator(b.HomeTick), this.clock.Denominator)), this.grid);
            long start = tau + this.shift.Value + b.Lead;
            var local = new ExactClock(this.ppq, b.Tempo);
            long phase = checked(b.PhaseNum * (local.Denominator / b.PhaseDen));

            var items = new List<(long Dt, long Seq, int Sub, Item Item)>();
            foreach (var (ch, part) in b.Parts)
            {
                var first = (Dt: long.MaxValue, Seq: long.MaxValue);
                foreach (var n in part.Notes)
                {
                    items.Add((n.Dt, n.OnSeq, 0, new Item(What.NoteOn, ch) { Key = n.Key, Value = n.Velocity }));
                    items.Add((n.Dt + n.Length, n.OffSeq, 0, new Item(What.NoteOff, ch) { Key = n.Key }));
                    first = Min(first, (n.Dt, n.OnSeq));
                }
                foreach (var (name, points) in part.Lanes)
                {
                    foreach (var p in points)
                    {
                        items.Add((p.Dt, p.Seq, 0, new Item(What.Lane, ch) { Name = name, Value = p.Value }));
                        first = Min(first, (p.Dt, p.Seq));
                    }
                }
                items.Add((first.Dt, first.Seq, -1, new Item(What.PartState, ch) { State = part.State }));
            }
            foreach (var w in b.WorldPoints)
                items.Add((w.Dt, w.Seq, 0, new Item(What.World, -1) { Name = w.Name, Text = w.Value }));
            var head = items.Select(x => (x.Dt, x.Seq)).Min();
            items.Add((head.Dt, head.Seq, -2, new Item(What.WorldState, -1) { World = b.WorldState }));

            foreach (var (dt, seq, sub, item) in items)
            {
                long sample = start + ExactClock.CeilDiv(phase + local.Numerator(dt), local.Denominator);
                item.Tick = b.HomeTick + dt;
                this.queue.Enqueue(item, new Key(sample, item.Tick, seq, sub, this.order++));
            }
        }

        /// <summary>Send everything due before the given sample, in order.</summary>
        public void Dispatch(long before, ICollection<TimedMessage> output)
        {
            while (this.queue.TryPeek(out var item, out var key) && key.Sample < before)
            {
                this.queue.Dequeue();
                this.Handle(item, key.Sample, output);
            }
        }

        private void Handle(Item item, long sample, ICollection<TimedMessage> output)
        {
            void Put(byte[] bytes)
            {
                if (bytes[0] >> 4 == 0xB && bytes[1] is 99 or 98 or 101 or 100)
                    this.pointer[bytes[0] & 0x0F] = this.pointer[bytes[0] & 0x0F].To(bytes[1], bytes[2]);
                output.Add(new TimedMessage(sample, bytes));
            }

            int ch = item.Channel;
            switch (item.What)
            {
                case What.NoteOn:
                    Put(new[] { (byte)(0x90 | ch), (byte)item.Key, (byte)item.Value });
                    break;
                case What.NoteOff:
                    Put(new[] { (byte)(0x90 | ch), (byte)item.Key, (byte)0 });
                    break;
                case What.Lane:
                    this.Set(ch, Model.ByName[item.Name!], item.Value, Put);
                    break;
                case What.World:
                    this.SetWorld(item.Name!, item.Text!, Put);
                    break;
                case What.PartState:
                    this.Settle(ch, item.State!, item.Tick, Put);
                    break;
                case What.WorldState:
                    this.SettleWorld(item.World!, item.Tick, Put);
                    break;
            }
        }

        private void Set(int ch, Param p, int value, Action<byte[]> put)
        {
            switch (p.Kind)
            {
                case ParamKind.Cc:
                    put(Cc(ch, p.A, value));
                    break;
                case ParamKind.Program:
                    put(new[] { (byte)(0xC0 | ch), (byte)value });
                    break;
                case ParamKind.Bend:
                    int u = value + 8192;
                    put(new[] { (byte)(0xE0 | ch), (byte)(u & 0x7F), (byte)(u >> 7) });
                    break;
                case ParamKind.XgPart:
                    put(new byte[] { 0xF0, 0x43, 0x10, 0x4C, 0x08, (byte)ch, (byte)p.A, (byte)value, 0xF7 });
                    break;
                default:   // RPN / NRPN: a data entry, after selecting the parameter if the pointer isn't on it
                    bool rpn = p.Kind == ParamKind.Rpn;
                    if (this.pointer[ch] == new Pointer(rpn ? 1 : 2, p.A, p.B))
                    {
                        put(Cc(ch, 6, value));
                        break;
                    }
                    put(Cc(ch, rpn ? 101 : 99, p.A));
                    put(Cc(ch, rpn ? 100 : 98, p.B));
                    put(Cc(ch, 6, value));
                    if (rpn)   // deselect so a stray data entry can't change it
                    {
                        put(Cc(ch, 101, 127));
                        put(Cc(ch, 100, 127));
                    }
                    break;
            }
            this.have[ch][p.Name] = value;
        }

        private void SetWorld(string name, string value, Action<byte[]> put)
        {
            put(Model.EncodeWorld(name, value));
            if (name == "system")
            {
                foreach (var h in this.have)
                    h.Clear();
                this.world = new Dictionary<string, string> { ["system"] = value };
            }
            else
            {
                this.world[name] = value;
            }
        }

        private void Settle(int ch, Dictionary<string, int> state, long tick, Action<byte[]> put)
        {
            var mine = this.have[ch];
            var diff = state.Where(kv => !mine.TryGetValue(kv.Key, out int v) || v != kv.Value)
                .OrderBy(kv => Model.Order[kv.Key]).ToList();
            var stale = mine.Keys.Where(k => !state.ContainsKey(k)).OrderBy(k => Model.Order[k]).ToList();
            if (diff.Count == 0 && stale.Count == 0)
                return;
            this.Added.Add($"tick {tick} ch{ch + 1}：补 {string.Join(" ", diff.Select(kv => $"{kv.Key}={kv.Value}"))}"
                           + (stale.Count > 0 ? $"，回到默认 {string.Join(" ", stale)}" : ""));
            foreach (var (name, value) in diff)
                this.Set(ch, Model.ByName[name], value, put);
            foreach (string name in stale)
            {
                var p = Model.ByName[name];
                if (Model.DefaultFor(p, ch) is int value)
                {
                    this.Set(ch, p, value, put);
                    mine.Remove(name);
                }
            }
            var want = Normal(state, ch);
            var got = Normal(mine, ch);
            if (!want.OrderBy(kv => kv.Key).SequenceEqual(got.OrderBy(kv => kv.Key)))
                this.Mismatches.Add($"tick {tick} ch{ch + 1}");
        }

        private void SettleWorld(Dictionary<string, string> state, long tick, Action<byte[]> put)
        {
            var sent = new List<string>();
            if (state.TryGetValue("system", out string? system)
                && (!this.world.TryGetValue("system", out string? current) || current != system))
            {
                this.SetWorld("system", system, put);
                sent.Add(system);
            }
            foreach (var (name, value) in state.Where(kv => kv.Key != "system"))
            {
                if (!this.world.TryGetValue(name, out string? now) || now != value)
                {
                    this.SetWorld(name, value, put);
                    sent.Add($"{name}={value}");
                }
            }
            if (sent.Count > 0)
                this.Added.Add($"tick {tick} 全局：补 {string.Join(" ", sent)}");
        }

        /// <summary>A state with every setting the device has a known value for filled in.</summary>
        private static Dictionary<string, int> Normal(Dictionary<string, int> state, int ch)
        {
            var full = new Dictionary<string, int>(state);
            foreach (var p in Model.Params)
            {
                if (!full.ContainsKey(p.Name) && Model.DefaultFor(p, ch) is int value)
                    full[p.Name] = value;
            }
            return full;
        }

        private static byte[] Cc(int ch, int control, int value) => new[] { (byte)(0xB0 | ch), (byte)control, (byte)value };

        private static (long Dt, long Seq) Min((long Dt, long Seq) a, (long Dt, long Seq) b) => a.CompareTo(b) <= 0 ? a : b;

        private enum What { WorldState, PartState, NoteOn, NoteOff, Lane, World }

        private sealed class Item
        {
            public Item(What what, int channel)
            {
                this.What = what;
                this.Channel = channel;
            }

            public What What { get; }
            public int Channel { get; }
            public int Key { get; init; }
            public int Value { get; init; }
            public string? Name { get; init; }
            public string? Text { get; init; }
            public Dictionary<string, int>? State { get; init; }
            public Dictionary<string, string>? World { get; init; }
            public long Tick { get; set; }
        }

        private readonly record struct Key(long Sample, long Tick, long Seq, int Sub, long Order) : IComparable<Key>
        {
            public int CompareTo(Key o)
            {
                int c = this.Sample.CompareTo(o.Sample);
                if (c == 0) c = this.Tick.CompareTo(o.Tick);
                if (c == 0) c = this.Seq.CompareTo(o.Seq);
                if (c == 0) c = this.Sub.CompareTo(o.Sub);
                return c != 0 ? c : this.Order.CompareTo(o.Order);
            }
        }

        /// <summary>Which RPN (1) or NRPN (2) a channel's data entry goes to; -1 where a half isn't selected.</summary>
        private readonly record struct Pointer(int Kind, int Msb, int Lsb)
        {
            public Pointer To(int control, int value)
            {
                int kind = control is 99 or 98 ? 2 : 1;
                var (msb, lsb) = this.Kind == kind ? (this.Msb, this.Lsb) : (-1, -1);
                return control is 99 or 101 ? new Pointer(kind, value, lsb) : new Pointer(kind, msb, value);
            }
        }
    }
}
