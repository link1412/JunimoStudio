using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// Everything a standard MIDI file sends to a synth, each message on the first sample at or after its exact time:
    /// the whole song played straight from the file, with no model and no blocks, to hold block performances against.
    /// Tracks merge by tick, a tick's messages in track order then file order (as S-MU2000's reader does).
    /// </summary>
    public sealed class MidiFileStream
    {
        public int Ppq { get; private init; }
        public List<(long Tick, long Micros)> Tempo { get; } = new();
        public List<(long Tick, byte[] Bytes)> Messages { get; } = new();
        public long End { get; private set; }

        public List<TimedMessage> Timed()
        {
            var clock = new ExactClock(this.Ppq, this.Tempo);
            return this.Messages.Select(m => new TimedMessage(clock.FirstSample(m.Tick), m.Bytes)).ToList();
        }

        public long EndSample => new ExactClock(this.Ppq, this.Tempo).FirstSample(this.End);

        public static MidiFileStream Read(string path)
        {
            byte[] d = File.ReadAllBytes(path);
            int p = 0;
            uint U32() { uint v = (uint)(d[p] << 24 | d[p + 1] << 16 | d[p + 2] << 8 | d[p + 3]); p += 4; return v; }
            int U16() { int v = d[p] << 8 | d[p + 1]; p += 2; return v; }
            long Vlq()
            {
                long v = 0;
                while (true)
                {
                    byte b = d[p++];
                    v = v << 7 | (uint)(b & 0x7F);
                    if ((b & 0x80) == 0)
                        return v;
                }
            }
            string Tag() { string t = System.Text.Encoding.ASCII.GetString(d, p, 4); p += 4; return t; }

            if (Tag() != "MThd")
                throw new InvalidDataException($"{path} 不是 MIDI 文件");
            int headerEnd = (int)U32() + p;
            U16();                                // format: tracks merge the same way either way
            int tracks = U16();
            int division = U16();
            if ((division & 0x8000) != 0)
                throw new InvalidDataException("SMPTE 时间单位的 MIDI 文件不支持");
            p = headerEnd;
            var song = new MidiFileStream { Ppq = division };
            var all = new List<(long Tick, int Track, int Index, byte[] Bytes, long Tempo)>();
            for (int t = 0; t < tracks && p < d.Length; t++)
            {
                while (Tag() != "MTrk")
                    p += (int)U32();
                int end = (int)U32() + p;
                long tick = 0;
                int running = 0, index = 0;
                while (p < end)
                {
                    tick += Vlq();
                    int status = d[p] >= 0x80 ? d[p++] : running;
                    if (status == 0xFF)
                    {
                        int type = d[p++];
                        int len = (int)Vlq();
                        if (type == 0x51)
                            all.Add((tick, t, index++, Array.Empty<byte>(), d[p] << 16 | d[p + 1] << 8 | d[p + 2]));
                        else if (type == 0x2F)
                            song.End = Math.Max(song.End, tick);
                        p += len;
                    }
                    else if (status is 0xF0 or 0xF7)
                    {
                        int len = (int)Vlq();
                        byte[] bytes = status == 0xF0 ? new[] { (byte)0xF0 }.Concat(d.Skip(p).Take(len)).ToArray()
                                                      : d.Skip(p).Take(len).ToArray();
                        all.Add((tick, t, index++, bytes, -1));
                        p += len;
                    }
                    else
                    {
                        running = status;
                        int n = (status & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;
                        all.Add((tick, t, index++, new[] { (byte)status }.Concat(d.Skip(p).Take(n)).ToArray(), -1));
                        p += n;
                    }
                }
                p = end;
            }
            foreach (var e in all.OrderBy(e => e.Tick).ThenBy(e => e.Track).ThenBy(e => e.Index))
            {
                if (e.Tempo >= 0)
                    song.Tempo.Add((e.Tick, e.Tempo));
                else
                    song.Messages.Add((e.Tick, e.Bytes));
            }
            return song;
        }
    }
}
