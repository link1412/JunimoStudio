using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Junimo.Engine.Performance
{
    public enum ParamKind { Cc, Rpn, Nrpn, Bend, Program, XgPart }

    /// <summary>A part setting or performance control of the model (music/tools/perfmodel.py PARAMS): its name, how it
    /// travels as MIDI and the device's value for it after a system reset (null where it differs by part).</summary>
    public sealed record Param(string Name, ParamKind Kind, int A, int B, int? Default);

    /// <summary>The model's settings and world parameters. The table is perfmodel's; block data carries perfmodel's
    /// copy and <see cref="Check"/> refuses data written against a different one.</summary>
    public static class Model
    {
        /// <summary>In the order a block's starting state is sent: the bank before the program it applies to, the
        /// RPN/NRPN pointer last so a block leaves it where the song had it.</summary>
        public static readonly Param[] Params =
        {
            new("bank_msb", ParamKind.Cc, 0, 0, 0),
            new("bank_lsb", ParamKind.Cc, 32, 0, 0),
            new("program", ParamKind.Program, 0, 0, 0),
            new("expression", ParamKind.Cc, 11, 0, 127),
            new("modulation", ParamKind.Cc, 1, 0, 0),
            new("pitch_bend", ParamKind.Bend, 0, 0, 0),
            new("sustain", ParamKind.Cc, 64, 0, 0),
            new("soft", ParamKind.Cc, 67, 0, 0),
            new("volume", ParamKind.Cc, 7, 0, 100),
            new("pan", ParamKind.Cc, 10, 0, 64),
            new("reverb", ParamKind.Cc, 91, 0, 40),
            new("chorus", ParamKind.Cc, 93, 0, 0),
            new("variation", ParamKind.Cc, 94, 0, 0),
            new("resonance", ParamKind.Cc, 71, 0, 64),
            new("release", ParamKind.Cc, 72, 0, 64),
            new("attack", ParamKind.Cc, 73, 0, 64),
            new("brightness", ParamKind.Cc, 74, 0, 64),
            new("bend_range", ParamKind.Rpn, 0, 0, 2),
            new("vibrato_rate", ParamKind.Nrpn, 1, 8, 64),
            new("vibrato_depth", ParamKind.Nrpn, 1, 9, 64),
            new("vibrato_delay", ParamKind.Nrpn, 1, 10, 64),
            new("filter_cutoff", ParamKind.Nrpn, 1, 32, 64),
            new("filter_resonance", ParamKind.Nrpn, 1, 33, 64),
            new("eg_attack", ParamKind.Nrpn, 1, 99, 64),
            new("eg_decay", ParamKind.Nrpn, 1, 100, 64),
            new("eg_release", ParamKind.Nrpn, 1, 102, 64),
            new("element_reserve", ParamKind.XgPart, 0x00, 0, null),
            new("rpn_msb", ParamKind.Cc, 101, 0, 127),
            new("rpn_lsb", ParamKind.Cc, 100, 0, 127),
            new("nrpn_msb", ParamKind.Cc, 99, 0, 127),
            new("nrpn_lsb", ParamKind.Cc, 98, 0, 127),
        };

        public static readonly Dictionary<string, Param> ByName = Params.ToDictionary(p => p.Name);
        public static readonly Dictionary<string, int> Order = Params.Select((p, i) => (p.Name, i)).ToDictionary(x => x.Name, x => x.i);

        /// <summary>World parameters set by XG parameter change (F0 43 10 4C a b c data F7): name, address, data bytes.</summary>
        public static readonly (string Name, byte A, byte B, byte C, int Size)[] WorldParams =
        {
            ("reverb_type", 0x02, 0x01, 0x00, 2),
            ("chorus_type", 0x02, 0x01, 0x20, 2),
            ("variation_type", 0x02, 0x01, 0x40, 2),
            ("variation_connection", 0x02, 0x01, 0x5A, 1),
        };

        public static readonly Dictionary<string, byte[]> Resets = new()
        {
            ["GM"] = new byte[] { 0xF0, 0x7E, 0x7F, 0x09, 0x01, 0xF7 },
            ["XG"] = new byte[] { 0xF0, 0x43, 0x10, 0x4C, 0x00, 0x00, 0x7E, 0x00, 0xF7 },
        };

        /// <summary>The device's value after a reset, where a part's state no longer holds a setting the synth has
        /// (a block played out of order). Drums (channel 10) start on the drum bank.</summary>
        public static int? DefaultFor(Param p, int channel) => p.Name == "bank_msb" && channel == 9 ? 127 : p.Default;

        /// <summary>A world value as the system message that sets it: "GM" / "XG" for a reset, else the data bytes
        /// written as decimal numbers ("1 0").</summary>
        public static byte[] EncodeWorld(string name, string value)
        {
            if (name == "system")
                return Resets[value];
            var (_, a, b, c, size) = WorldParams.First(w => w.Name == name);
            byte[] data = value.Split(' ').Select(byte.Parse).ToArray();
            if (data.Length != size)
                throw new InvalidDataException($"{name} 要 {size} 个字节，给的是 \"{value}\"");
            return new byte[] { 0xF0, 0x43, 0x10, 0x4C, a, b, c }.Concat(data).Append((byte)0xF7).ToArray();
        }

        /// <summary>Refuse block data whose parameter tables differ from this one (it was written by another
        /// version of the model).</summary>
        public static void Check(JsonElement paramsJson, JsonElement worldJson)
        {
            var theirs = paramsJson.EnumerateArray().Select(e => e.EnumerateArray().Select(Text).ToArray()).ToList();
            var ours = Params.Select(p => p.Kind switch
            {
                ParamKind.Cc => new[] { p.Name, "cc", p.A.ToString() },
                ParamKind.Rpn => new[] { p.Name, "rpn", p.A.ToString(), p.B.ToString() },
                ParamKind.Nrpn => new[] { p.Name, "nrpn", p.A.ToString(), p.B.ToString() },
                ParamKind.Bend => new[] { p.Name, "bend" },
                ParamKind.Program => new[] { p.Name, "program" },
                _ => new[] { p.Name, "xgpart", p.A.ToString() },
            }).ToList();
            for (int i = 0; i < Math.Max(theirs.Count, ours.Count); i++)
            {
                string a = i < theirs.Count ? string.Join(" ", theirs[i]) : "（没有）";
                string b = i < ours.Count ? string.Join(" ", ours[i]) : "（没有）";
                if (a != b)
                    throw new InvalidDataException($"参数表和模型不一致：第 {i + 1} 项，数据里是 {a}，引擎里是 {b}");
            }
            var theirWorld = worldJson.EnumerateArray().Select(e => string.Join(" ", e.EnumerateArray().Select(Text))).ToList();
            var ourWorld = WorldParams.Select(w => $"{w.Name} {w.A} {w.B} {w.C} {w.Size}").ToList();
            if (!theirWorld.SequenceEqual(ourWorld))
                throw new InvalidDataException($"全局参数表和模型不一致：数据里是 {string.Join("; ", theirWorld)}");
        }

        private static string Text(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText();
    }
}
