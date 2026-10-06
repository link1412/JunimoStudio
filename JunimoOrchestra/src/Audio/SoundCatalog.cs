using System;
using System.Collections.Generic;
using System.Linq;

namespace JunimoOrchestra.Audio
{
    /// <summary>A preset found in the loaded SoundFont.</summary>
    internal readonly record struct PresetInfo(int Bank, int Program, string Name);

    /// <summary>
    /// What a SoundFont holds, sorted the way the panel lists it. Works for any SF2, not just the bundled GeneralUser GS:
    /// players can bring their own, and fonts lay their banks out differently (FluidR3 has 31 drum kits, Timbres of
    /// Heaven XG kits in bank 126, SGM placeholder presets named "-----", some fonts are a single piano).
    /// <list type="bullet">
    /// <item>Bank 0 is the 128 General MIDI programs. A font with most of them is a GM font and its rows are named by GM
    /// (the slot's meaning is the standard); otherwise by the font's own preset names.</item>
    /// <item>Variations of a program are its presets in banks 1–119. Banks 120–127 are left out of the lists: fonts use
    /// them for copies of the drum kits (GeneralUser GS, FatBoy), XG kits (Timbres of Heaven) or MT-32 sets, never
    /// for variations. They still play when picked by number.</item>
    /// <item>Drum kits are bank 128, as SF2 has it.</item>
    /// <item>Placeholder presets (no letter or digit in the name) and dividers ("--SGM drum kits--") are left out.</item>
    /// <item>A missing preset plays what MeltySynth falls back to (<see cref="Plays"/>).</item>
    /// </list>
    /// </summary>
    internal sealed class SoundCatalog
    {
        public const int DrumBank = 128;
        public const int LastVariantBank = 119;

        /// <summary>A GM font has at least this many of the 128 programs in bank 0.</summary>
        private const int GmPrograms = 96;

        public static readonly SoundCatalog Empty = new(Array.Empty<PresetInfo>(), "");

        private readonly Dictionary<(int Bank, int Program), string> names;
        private readonly Dictionary<int, List<int>> variantBanks;
        private readonly (int Bank, int Program)? lowest;

        public SoundCatalog(IReadOnlyList<PresetInfo> presets, string fontName)
        {
            this.FontName = fontName.Trim();
            this.names = new Dictionary<(int, int), string>();
            foreach (PresetInfo p in presets)
                this.names.TryAdd((p.Bank, p.Program), p.Name.Trim());   // the synth keeps the first of a pair too
            this.Count = this.names.Count;
            // MeltySynth's default preset: the lowest bank, then program
            if (this.names.Count > 0)
                this.lowest = this.names.Keys.OrderBy(k => k.Bank).ThenBy(k => k.Program).First();

            this.variantBanks = this.names.Keys
                .Where(k => k.Bank is > 0 and <= LastVariantBank && IsReal(this.names[k]))
                .GroupBy(k => k.Program)
                .ToDictionary(g => g.Key, g => g.Select(k => k.Bank).OrderBy(b => b).ToList());
            this.DrumKits = this.names.Keys
                .Where(k => k.Bank == DrumBank && IsReal(this.names[k]))
                .Select(k => k.Program).OrderBy(p => p).ToList();
            this.IsGm = Enumerable.Range(0, 128).Count(p => this.names.ContainsKey((0, p))) >= GmPrograms;
            this.IsGeneralUser = this.FontName.StartsWith("GeneralUser GS 2.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The font's own name (its INFO bank name).</summary>
        public string FontName { get; }

        /// <summary>How many presets it has (0 = nothing loaded).</summary>
        public int Count { get; }

        /// <summary>Bank 0 holds (nearly) all of General MIDI: rows are named by GM.</summary>
        public bool IsGm { get; }

        /// <summary>The bundled font's family (GeneralUser GS 2.x): the mod's translated variation and kit names, which
        /// go by its bank and program numbers, apply.</summary>
        public bool IsGeneralUser { get; }

        /// <summary>Drum kit program numbers (bank 128).</summary>
        public IReadOnlyList<int> DrumKits { get; }

        /// <summary>Banks (1–119) holding variations of a GM program.</summary>
        public IReadOnlyList<int> VariantBanks(int program)
            => this.variantBanks.TryGetValue(program, out List<int>? banks) ? banks : Array.Empty<int>();

        public bool Exists(int bank, int program) => this.names.ContainsKey((bank, program));

        /// <summary>The font's name for a preset, or null if it doesn't have it.</summary>
        public string? Name(int bank, int program) => this.names.TryGetValue((bank, program), out string? name) ? name : null;

        /// <summary>What actually sounds for a bank and program, like the synth picks it: the preset itself, else the
        /// same program in bank 0 (or for drums the standard kit, 128:0), else the font's lowest preset. Null when
        /// nothing is loaded.</summary>
        public (int Bank, int Program)? Plays(int bank, int program)
        {
            if (this.Exists(bank, program))
                return (bank, program);
            (int, int) gm = bank < DrumBank ? (0, program) : (DrumBank, 0);
            if (this.names.ContainsKey(gm))
                return gm;
            return this.lowest;
        }

        /// <summary>Everything worth landing on at random: bank 0, the variations and the kits.</summary>
        public IEnumerable<(int Bank, int Program)> Pickable
            => this.names.Keys.Where(k => (k.Bank <= LastVariantBank || k.Bank == DrumBank) && IsReal(this.names[k]));

        /// <summary>A name with a letter or digit in it, not a placeholder ("-----") or a divider ("--SGM drum kits--").</summary>
        private static bool IsReal(string name) => name.Any(char.IsLetterOrDigit) && !name.StartsWith("--", StringComparison.Ordinal);
    }
}
