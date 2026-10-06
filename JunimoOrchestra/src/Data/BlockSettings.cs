using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StardewValley;

namespace JunimoOrchestra.Data
{
    /// <summary>One MIDI note inside a block. Times are MIDI ticks at <see cref="BlockSettings.Ppq"/> per quarter note.</summary>
    internal struct BlockNote : IEquatable<BlockNote>
    {
        public int Pitch;      // 0-127
        public int Delay;      // ticks after the trigger
        public int Duration;   // ticks, >= 1
        public int Velocity;   // 1-127

        public BlockNote(int pitch, int delay, int duration, int velocity)
        {
            this.Pitch = pitch;
            this.Delay = delay;
            this.Duration = duration;
            this.Velocity = velocity;
        }

        public int End => this.Delay + this.Duration;

        public bool Equals(BlockNote o) => this.Pitch == o.Pitch && this.Delay == o.Delay && this.Duration == o.Duration && this.Velocity == o.Velocity;
        public override bool Equals(object? obj) => obj is BlockNote n && this.Equals(n);
        public override int GetHashCode() => HashCode.Combine(this.Pitch, this.Delay, this.Duration, this.Velocity);
    }

    /// <summary>Everything a single block remembers. Stored compactly in the object's modData.</summary>
    internal sealed class BlockSettings
    {
        public const string ModDataKey = "link1412.JunimoOrchestra/block";
        public const int Ppq = 480;
        public const int MaxNotes = 64;
        public const int MaxTicks = Ppq * 128;
        public const int MaxReach = 8;
        public const int MinTempo = 20;
        public const int MaxTempo = 300;

        public int Bank;              // 0-127 melodic variants, 128 = drum kits
        public int Program;           // 0-127 (GM number - 1)
        public int Tempo;             // 0 = follow the save-wide default tempo
        public int Volume = 100;      // CC7
        public int Reverb = 40;       // CC91
        public int Chorus;            // CC93
        public int Vibrato;           // CC1
        public int Pan = 64;          // CC10, 64 = centre
        public bool TriggerByPlayers = true;
        public bool TriggerByCreatures;

        /// <summary>How far away it hears someone, in tiles straight across (its row and column): 1 = right next to it,
        /// more lets a row of blocks lie a few tiles off the path, or several rows side by side. 0 = not set: the save's
        /// <see cref="WorldSettings.DefaultReach"/>, as the tempo follows the save's when it isn't set.</summary>
        public int Reach;

        public List<BlockNote> Notes = new();

        /// <summary>A qualified item ID to draw instead of the block (set dressing: a flower that's secretly a piano), or null;
        /// for film sets also "crop:&lt;seed id&gt;", "junimo:..." (<see cref="Game.OrchestraArt"/>) and "classical[:family[:dx:dy]]"
        /// (<see cref="Game.ClassicalArt"/>).</summary>
        public string? Look;

        /// <summary>Staging for film sets (optional, comma separated): "hide" = invisible until first played, "ghost" = never
        /// seen at all (only heard),
        /// "bye:T" = vanish T game ticks after being played, "relay:dx:dy:T" = play the block at (x+dx, y+dy) T ticks later,
        /// "rest:T" = a Junimo musician puts its instrument away T ticks after being played, "dance:P[:O]" = the beat (P ticks,
        /// the first one O ticks after being played) it sways to while playing and dances to once resting, "still:A:B" =
        /// it holds still from A to B ticks after being played, "conduct:A:B" = a classical conductor beats time from A to B,
        /// "bow:T" = it lifts its top hat to us T ticks after being played, "star:T" = a Junimo star pops up over it then and
        /// it jumps for joy.
        /// Song blocks relay too.</summary>
        public string? Stage;

        public bool IsDrumKit => this.Bank >= Families.DrumBank;

        public static BlockSettings CreateDefault(Family family)
        {
            var s = new BlockSettings
            {
                Bank = family.IsDrumKit ? Families.DrumBank : 0,
                Program = family.DefaultProgram
            };
            s.Notes.Add(new BlockNote(family.IsDrumKit ? 38 : 60, 0, Ppq, 100));
            return s;
        }

        public int EffectiveTempo(int worldTempo) => this.Tempo > 0 ? this.Tempo : worldTempo;

        public int EffectiveReach(int worldReach) => this.Reach > 0 ? this.Reach : worldReach;

        /// <summary>Tick at which the last note ends.</summary>
        public int LengthTicks => this.Notes.Count == 0 ? 0 : this.Notes.Max(n => n.End);

        public BlockSettings Clone()
        {
            var c = (BlockSettings)this.MemberwiseClone();
            c.Notes = new List<BlockNote>(this.Notes);
            return c;
        }

        public void Normalize()
        {
            this.Bank = Math.Clamp(this.Bank, 0, 128);
            this.Program = Math.Clamp(this.Program, 0, 127);
            this.Tempo = this.Tempo <= 0 ? 0 : Math.Clamp(this.Tempo, MinTempo, MaxTempo);
            this.Volume = Math.Clamp(this.Volume, 0, 127);
            this.Reverb = Math.Clamp(this.Reverb, 0, 127);
            this.Chorus = Math.Clamp(this.Chorus, 0, 127);
            this.Vibrato = Math.Clamp(this.Vibrato, 0, 127);
            this.Pan = Math.Clamp(this.Pan, 0, 127);
            this.Reach = this.Reach <= 0 ? 0 : Math.Clamp(this.Reach, 1, MaxReach);
            for (int i = 0; i < this.Notes.Count; i++)
            {
                BlockNote n = this.Notes[i];
                n.Pitch = Math.Clamp(n.Pitch, 0, 127);
                n.Delay = Math.Clamp(n.Delay, 0, MaxTicks);
                n.Duration = Math.Clamp(n.Duration, 1, MaxTicks);
                n.Velocity = Math.Clamp(n.Velocity, 1, 127);
                this.Notes[i] = n;
            }
            if (this.Notes.Count > MaxNotes)
                this.Notes.RemoveRange(MaxNotes, this.Notes.Count - MaxNotes);
        }

        /****
        ** Storage
        ****/
        public static BlockSettings Read(StardewValley.Object obj, Family family)
        {
            if (obj.modData.TryGetValue(ModDataKey, out string? raw) && TryParse(raw, out BlockSettings? parsed))
                return parsed!;
            return CreateDefault(family);
        }

        public void Write(StardewValley.Object obj)
        {
            this.Normalize();
            obj.modData[ModDataKey] = this.Serialize();
        }

        public static bool HasSettings(Item item) => item.modData.ContainsKey(ModDataKey);

        /// <summary>The reach of a placed block (its own <see cref="Reach"/>, or the save's if it has none), read straight
        /// from its data (cheap enough to call every tick).</summary>
        public static int ReachOf(StardewValley.Object obj, int worldReach)
            => RawValue(obj, ";r=") is string r && int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0
                ? Math.Clamp(v, 1, MaxReach) : worldReach;

        /// <summary>The bank, program and first note's pitch of a placed block, read straight from its data (cheap enough to
        /// call while drawing).</summary>
        public static (int Bank, int Program, int Pitch) SoundOf(StardewValley.Object obj, Family family)
        {
            static int Int(string? v, int fallback)
                => v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : fallback;
            if (!obj.modData.ContainsKey(ModDataKey))
                return (family.IsDrumKit ? Families.DrumBank : 0, family.DefaultProgram, family.IsDrumKit ? 38 : 60);
            string? notes = RawValue(obj, ";n=");
            int comma = notes?.IndexOf(',') ?? -1;
            return (Int(RawValue(obj, ";b="), 0), Int(RawValue(obj, ";p="), family.DefaultProgram),
                Int(comma > 0 ? notes![..comma] : null, -1));
        }

        /// <summary>The <see cref="Look"/> of a placed block, read straight from its data (cheap enough to call while drawing).</summary>
        public static string? LookOf(StardewValley.Object obj) => RawValue(obj, ";look=");

        /// <summary>The <see cref="Stage"/> directions of a placed block, read straight from its data.</summary>
        public static string? StageOf(StardewValley.Object obj) => RawValue(obj, ";stage=");

        private static string? RawValue(StardewValley.Object obj, string marker)
        {
            if (!obj.modData.TryGetValue(ModDataKey, out string? raw))
                return null;
            int start = raw.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return null;
            start += marker.Length;
            int end = raw.IndexOf(';', start);
            return end < 0 ? raw[start..] : raw[start..end];
        }

        public string Serialize()
        {
            var sb = new StringBuilder("v1");
            void Kv(string k, int v) => sb.Append(';').Append(k).Append('=').Append(v.ToString(CultureInfo.InvariantCulture));
            Kv("b", this.Bank);
            Kv("p", this.Program);
            Kv("t", this.Tempo);
            Kv("vol", this.Volume);
            Kv("rev", this.Reverb);
            Kv("cho", this.Chorus);
            Kv("mod", this.Vibrato);
            Kv("pan", this.Pan);
            Kv("tp", this.TriggerByPlayers ? 1 : 0);
            Kv("tc", this.TriggerByCreatures ? 1 : 0);
            if (this.Reach > 0)
                Kv("r", this.Reach);
            if (!string.IsNullOrEmpty(this.Look))
                sb.Append(";look=").Append(this.Look);
            if (!string.IsNullOrEmpty(this.Stage))
                sb.Append(";stage=").Append(this.Stage);
            sb.Append(";n=");
            sb.Append(string.Join("|", this.Notes.Select(n => $"{n.Pitch},{n.Delay},{n.Duration},{n.Velocity}")));
            return sb.ToString();
        }

        public static bool TryParse(string raw, out BlockSettings? settings)
        {
            settings = null;
            try
            {
                string[] parts = raw.Split(';');
                if (parts.Length == 0 || parts[0] != "v1")
                    return false;
                var s = new BlockSettings();
                foreach (string part in parts.Skip(1))
                {
                    int eq = part.IndexOf('=');
                    if (eq < 0)
                        continue;
                    string key = part[..eq];
                    string value = part[(eq + 1)..];
                    if (key == "look")
                    {
                        s.Look = value.Length > 0 ? value : null;
                        continue;
                    }
                    if (key == "stage")
                    {
                        s.Stage = value.Length > 0 ? value : null;
                        continue;
                    }
                    if (key == "n")
                    {
                        foreach (string note in value.Split('|', StringSplitOptions.RemoveEmptyEntries))
                        {
                            int[] f = note.Split(',').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                            if (f.Length == 4)
                                s.Notes.Add(new BlockNote(f[0], f[1], f[2], f[3]));
                        }
                        continue;
                    }
                    int v = int.Parse(value, CultureInfo.InvariantCulture);
                    switch (key)
                    {
                        case "b": s.Bank = v; break;
                        case "p": s.Program = v; break;
                        case "t": s.Tempo = v; break;
                        case "vol": s.Volume = v; break;
                        case "rev": s.Reverb = v; break;
                        case "cho": s.Chorus = v; break;
                        case "mod": s.Vibrato = v; break;
                        case "pan": s.Pan = v; break;
                        case "tp": s.TriggerByPlayers = v != 0; break;
                        case "tc": s.TriggerByCreatures = v != 0; break;
                        case "r": s.Reach = v; break;
                    }
                }
                s.Normalize();
                settings = s;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
