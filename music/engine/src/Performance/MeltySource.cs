using System;
using System.Collections.Generic;
using MeltySynth;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// The open sound source: timed messages into MeltySynth. The synth renders in blocks of 64 samples and takes
    /// messages between them, so a message is applied just before the first block that starts at or after its sample.
    /// System exclusive messages are dropped, as MeltySynth's own MIDI file reader drops them.
    /// </summary>
    public sealed class MeltySource
    {
        private readonly Synthesizer synth;
        private readonly Queue<TimedMessage> pending = new();
        private long position;
        private long last = long.MinValue;

        public MeltySource(Synthesizer synth)
        {
            this.synth = synth;
        }

        /// <summary>Samples rendered so far.</summary>
        public long Position => this.position;

        public void Send(TimedMessage m)
        {
            if (m.Sample < this.last)
                throw new ArgumentException($"消息必须按时间先后送来：第 {m.Sample} 个采样在第 {this.last} 个之后才到");
            this.last = m.Sample;
            this.pending.Enqueue(m);
        }

        /// <summary>Render the next stretch; its length must be a whole number of the synth's blocks.</summary>
        public void Render(Span<float> left, Span<float> right)
        {
            int size = this.synth.BlockSize;
            if (left.Length % size != 0)
                throw new ArgumentException($"一次要渲染 {size} 的整数倍个采样");
            for (int offset = 0; offset < left.Length; offset += size)
            {
                while (this.pending.Count > 0 && this.pending.Peek().Sample <= this.position)
                    this.Apply(this.pending.Dequeue().Bytes);
                this.synth.Render(left.Slice(offset, size), right.Slice(offset, size));
                this.position += size;
            }
        }

        private void Apply(byte[] b)
        {
            if (b[0] is 0xF0 or 0xF7)
                return;
            this.synth.ProcessMidiMessage(b[0] & 0x0F, b[0] & 0xF0, b.Length > 1 ? b[1] : 0, b.Length > 2 ? b[2] : 0);
        }
    }
}
