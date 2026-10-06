using System;
using System.Collections.Generic;
using System.Linq;
using JunimoOrchestra.Audio;
using JunimoOrchestra.Data;
using StardewModdingAPI;

namespace JunimoOrchestra.UI
{
    /// <summary>Display names for instruments, variants, drum kits, notes and drums, for whichever SoundFont is loaded
    /// (see <see cref="SoundCatalog"/>).</summary>
    internal sealed class SoundNames
    {
        private static readonly string[] NoteLetters = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private readonly ITranslationHelper tr;
        private readonly SynthEngine engine;
        private SoundCatalog catalog = SoundCatalog.Empty;
        private int generation = -1;

        public SoundNames(ITranslationHelper tr, SynthEngine engine)
        {
            this.tr = tr;
            this.engine = engine;
        }

        /// <summary>The loaded font's catalog (rebuilt when another font is loaded).</summary>
        public SoundCatalog Catalog
        {
            get
            {
                if (this.generation != this.engine.Generation)
                {
                    this.generation = this.engine.Generation;
                    this.catalog = new SoundCatalog(this.engine.Presets, this.engine.FontName);
                }
                return this.catalog;
            }
        }

        public string T(string key) => this.tr.Get(key);

        public string T(string key, object tokens) => this.tr.Get(key, tokens);

        private string? Optional(string key)
        {
            Translation t = this.tr.Get(key);
            return t.HasValue() ? t.ToString() : null;
        }

        /// <summary>Whether the font has this preset (true while nothing is loaded yet).</summary>
        public bool Exists(int bank, int program) => this.Catalog.Count == 0 || this.Catalog.Exists(bank, program);

        /// <summary>Banks (1–119) holding variations of a GM program.</summary>
        public IReadOnlyList<int> VariantBanks(int program) => this.Catalog.VariantBanks(program);

        /// <summary>Drum kit program numbers in bank 128 (the standard kit's number when the font has none).</summary>
        public IReadOnlyList<int> DrumKits => this.Catalog.DrumKits.Count > 0 ? this.Catalog.DrumKits : new[] { 0 };

        public IEnumerable<(int bank, int program)> AllPresets => this.Catalog.Pickable;

        /// <summary>The name of a bank and program as listed. Bank 0 goes by General MIDI in a GM font (the slot's
        /// meaning), else by the font; the mod's translated variation and kit names only for GeneralUser GS 2, whose
        /// numbers they were written for; anything else by the font's own name.</summary>
        public string Program(int bank, int program)
        {
            SoundCatalog c = this.Catalog;
            string? own = c.Name(bank, program);
            if (bank >= Families.DrumBank)
            {
                string? translated = c.IsGeneralUser || c.Count == 0 ? this.Optional($"drumkit.{program}") : null;
                return translated ?? own ?? $"Drums {program + 1}";
            }
            if (bank > 0)
            {
                string? translated = c.IsGeneralUser ? this.Optional($"variant.{bank}.{program}") : null;
                return translated ?? own ?? $"{this.T($"program.{program}")} ({bank})";
            }
            return c.IsGm || own == null ? this.T($"program.{program}") : own;
        }

        /// <summary>The name of what really sounds for a bank and program: the preset, or what the synth falls back to
        /// when the font doesn't have it.</summary>
        public string Sounding(int bank, int program)
            => this.Catalog.Plays(bank, program) is (int b, int p) ? this.Program(b, p) : this.Program(bank, program);

        public string FamilyName(Family f) => this.T($"family.{f.Key}.name");
        public string FamilyCategory(Family f) => this.T($"family.{f.Key}.category");
        public string FamilyDesc(Family f) => this.T($"family.{f.Key}.desc");

        public static string Note(int pitch) => NoteLetters[((pitch % 12) + 12) % 12] + (pitch / 12 - 1);

        public string? Drum(int key) => this.Optional($"drum.{key}");
    }
}
