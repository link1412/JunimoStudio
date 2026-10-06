using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Junimo.Engine.Mixing
{
    /// <summary>An EQ band as an RBJ cookbook biquad: "hp", "lp", "peak", "lowshelf" or "highshelf" (for a shelf, Q is
    /// its slope).</summary>
    public readonly record struct Band(string Kind, double Hz, double Db, double Q);

    /// <summary>A setting that changes by section: its value at the section's start and at its end, ramped in across
    /// the start and out across the end. In dB, or for a section EQ, how much of the EQ is heard (0-1).</summary>
    public readonly record struct SectionValue(string Section, double Start, double End);

    /// <summary>An RMS compressor, stereo linked, its detector hearing the signal through a high-pass. Window: how many
    /// 44-sample blocks ("1 ms") its level is averaged over.</summary>
    public sealed record CompSettings(double Threshold, double Ratio, double Attack, double Release, double Knee,
                                      int WindowMs, double SidechainHz);

    /// <summary>A clipper at 4x oversampling: linear ceiling, soft from Knee x ceiling up.</summary>
    public sealed record ClipSettings(double Ceiling, double Knee);

    /// <summary>A true-peak lookahead limiter.</summary>
    public sealed record LimiterSettings(double CeilingDb, double Lookahead, double Release);

    /// <summary>Width on the bus: the side above <see cref="Hz"/> raised by <see cref="Db"/>, and the whole side
    /// ridden by section.</summary>
    public sealed record SideSettings(double Hz, double Db, List<SectionValue> Rides);

    public sealed record HighpassSettings(double Hz, int Order);

    /// <summary>An EQ on the bus, faded in over its sections.</summary>
    public sealed record SectionEq(List<SectionValue> Sections, List<Band> Eq);

    /// <summary>One mixer channel: the parts (MIDI channels) whose notes it carries, and its strip.</summary>
    public sealed class StripSettings
    {
        public string Name { get; init; } = "";
        public List<int> Channels { get; init; } = new();
        public List<Band> Eq { get; init; } = new();
        public double Side { get; init; }                 // dB on the side (M/S width)
        public double Balance { get; init; }              // dB toward the left
        public List<SectionValue> SideRides { get; init; } = new();
        public double Gain { get; init; }                 // dB
        public List<SectionValue> Rides { get; init; } = new();
        public CompSettings? Comp { get; init; }
        public ClipSettings? Clip { get; init; }
    }

    public sealed class BusSettings
    {
        public List<SectionValue> Rides { get; init; } = new();
        public List<SectionEq> SectionEq { get; init; } = new();
        public SideSettings? Side { get; init; }
        public HighpassSettings? Highpass { get; init; }
    }

    public sealed class MasterSettings
    {
        public CompSettings? Comp { get; init; }
        public double GainDb { get; init; }
        public ClipSettings? Clip { get; init; }
        public LimiterSettings? Limiter { get; init; }
        public double Fade { get; init; }                 // s, at the end
    }

    /// <summary>
    /// A song's mix: the strips, the bus and the master. Sample 0 of the mix is the performance's sample
    /// <see cref="Start"/>; section times are seconds from it; the mix is <see cref="End"/> samples long. Written for
    /// the short Voyage by its mix.py (scratchpad export_mix.py), with the constants mix.py works out from the stems
    /// (thresholds, the drum clip's ceiling, the fader) frozen in.
    /// </summary>
    public sealed class MixSettings
    {
        public int Rate { get; init; } = 44100;
        public long Start { get; init; }
        public long End { get; init; }
        public double Ramp { get; init; }                  // s: a section's value ramps in and out across its edges
        public Dictionary<string, (double Start, double End)> Sections { get; init; } = new();
        public List<StripSettings> Strips { get; init; } = new();
        public BusSettings Bus { get; init; } = new();
        public MasterSettings? Master { get; init; }

        public static MixSettings Load(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            return Parse(doc.RootElement);
        }

        public static MixSettings Parse(JsonElement root)
        {
            if (root.GetProperty("format").GetInt32() != 1)
                throw new InvalidDataException("mix: unknown format");
            var bus = root.GetProperty("bus");
            var mix = new MixSettings
            {
                Rate = root.GetProperty("rate").GetInt32(),
                Start = root.GetProperty("start").GetInt64(),
                End = root.GetProperty("end").GetInt64(),
                Ramp = root.GetProperty("ramp").GetDouble(),
                Sections = root.GetProperty("sections").EnumerateObject().ToDictionary(
                    p => p.Name, p => (p.Value[0].GetDouble(), p.Value[1].GetDouble())),
                Strips = root.GetProperty("strips").EnumerateArray().Select(s => new StripSettings
                {
                    Name = s.GetProperty("name").GetString()!,
                    Channels = s.GetProperty("channels").EnumerateArray().Select(c => c.GetInt32()).ToList(),
                    Eq = Bands(s.GetProperty("eq")),
                    Side = s.GetProperty("side").GetDouble(),
                    Balance = s.GetProperty("balance").GetDouble(),
                    SideRides = Values(s.GetProperty("side_rides")),
                    Gain = s.GetProperty("gain").GetDouble(),
                    Rides = Values(s.GetProperty("rides")),
                    Comp = Comp(s.GetProperty("comp")),
                    Clip = Clip(s.GetProperty("clip")),
                }).ToList(),
                Bus = new BusSettings
                {
                    Rides = Values(bus.GetProperty("rides")),
                    SectionEq = bus.GetProperty("section_eq").EnumerateArray()
                        .Select(e => new SectionEq(Values(e.GetProperty("sections")), Bands(e.GetProperty("eq")))).ToList(),
                    Side = Null(bus.GetProperty("side"), s => new SideSettings(
                        s.GetProperty("hz").GetDouble(), s.GetProperty("db").GetDouble(), Values(s.GetProperty("rides")))),
                    Highpass = Null(bus.GetProperty("highpass"), h => new HighpassSettings(
                        h.GetProperty("hz").GetDouble(), h.GetProperty("order").GetInt32())),
                },
                Master = Null(root.GetProperty("master"), m => new MasterSettings
                {
                    Comp = Comp(m.GetProperty("comp")),
                    GainDb = m.GetProperty("gain_db").GetDouble(),
                    Clip = Clip(m.GetProperty("clip")),
                    Limiter = Null(m.GetProperty("limiter"), l => new LimiterSettings(
                        l.GetProperty("ceiling_db").GetDouble(), l.GetProperty("lookahead").GetDouble(),
                        l.GetProperty("release").GetDouble())),
                    Fade = m.GetProperty("fade").GetDouble(),
                }),
            };
            foreach (var v in mix.Strips.SelectMany(s => s.Rides.Concat(s.SideRides))
                         .Concat(mix.Bus.Rides).Concat(mix.Bus.SectionEq.SelectMany(e => e.Sections))
                         .Concat(mix.Bus.Side?.Rides ?? new()))
            {
                if (!mix.Sections.ContainsKey(v.Section))
                    throw new InvalidDataException($"mix: no section \"{v.Section}\"");
            }
            return mix;
        }

        private static T? Null<T>(JsonElement e, Func<JsonElement, T> parse) where T : class =>
            e.ValueKind == JsonValueKind.Null ? null : parse(e);

        private static List<Band> Bands(JsonElement e) => e.EnumerateArray()
            .Select(b => new Band(b[0].GetString()!, b[1].GetDouble(), b[2].GetDouble(), b[3].GetDouble())).ToList();

        private static List<SectionValue> Values(JsonElement e) => e.EnumerateArray()
            .Select(v => new SectionValue(v[0].GetString()!, v[1].GetDouble(), v[2].GetDouble())).ToList();

        private static CompSettings? Comp(JsonElement e) => Null(e, c => new CompSettings(
            c.GetProperty("threshold").GetDouble(), c.GetProperty("ratio").GetDouble(),
            c.GetProperty("attack").GetDouble(), c.GetProperty("release").GetDouble(),
            c.GetProperty("knee").GetDouble(), c.GetProperty("window_ms").GetInt32(),
            c.GetProperty("sidechain_hz").GetDouble()));

        private static ClipSettings? Clip(JsonElement e) => Null(e, c => new ClipSettings(
            c.GetProperty("ceiling").GetDouble(), c.GetProperty("knee").GetDouble()));
    }
}
