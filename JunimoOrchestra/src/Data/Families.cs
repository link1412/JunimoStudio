using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace JunimoOrchestra.Data
{
    /// <summary>One placeable instrument block: a General MIDI instrument family (or the drum kit).</summary>
    internal sealed class Family
    {
        public const string ItemPrefix = "link1412.JunimoOrchestra_";

        /// <summary>Stable key used in item ids, i18n keys and sprite lookup.</summary>
        public string Key { get; }

        /// <summary>Index into the block sprite sheet and GM family order (0-15), 16 = drum kit.</summary>
        public int Index { get; }

        /// <summary>Program chosen when a fresh block of this family is crafted.</summary>
        public int DefaultProgram { get; }

        /// <summary>UI accent colour (panel highlights, junimo tint, particles).</summary>
        public Color Accent { get; }

        public bool IsDrumKit => this.Index == Families.DrumKitIndex;
        public string ItemId => ItemPrefix + this.Key;
        public string QualifiedItemId => "(O)" + this.ItemId;

        /// <summary>First GM program (0-based) of this family; drum kits use bank 128 instead.</summary>
        public int FirstProgram => this.IsDrumKit ? 0 : this.Index * 8;

        public Family(string key, int index, int defaultProgram, string accent)
        {
            this.Key = key;
            this.Index = index;
            this.DefaultProgram = defaultProgram;
            this.Accent = Colors.Hex(accent);
        }
    }

    internal static class Families
    {
        public const int DrumKitIndex = 16;
        public const int DrumBank = 128;

        /// <summary>Every block's recipe: 5 wood and 5 fiber ("id count id count").</summary>
        public const string BlockRecipe = "388 5 771 5";

        /// <summary>How many a recipe makes, blocks and Junimos alike.</summary>
        public const int CraftYield = 100;

        /// <summary>Order matches the sprite sheet and the General MIDI family order.</summary>
        public static readonly IReadOnlyList<Family> All = new[]
        {
            new Family("piano", 0, 0, "#8a7ce0"),
            new Family("bells", 1, 10, "#ff82ad"),
            new Family("organ", 2, 19, "#ff8a66"),
            new Family("guitar", 3, 24, "#ffa83d"),
            new Family("bass", 4, 33, "#5b8def"),
            new Family("violin", 5, 40, "#e0506f"),
            new Family("choir", 6, 52, "#b88cf0"),
            new Family("trumpet", 7, 56, "#43b6ec"),
            new Family("sax", 8, 65, "#6272e0"),
            new Family("ocarina", 9, 79, "#4fcb86"),
            new Family("synth", 10, 80, "#c65cf0"),
            new Family("cloud", 11, 88, "#8fa3ff"),
            new Family("stardust", 12, 98, "#f2b640"),
            new Family("kalimba", 13, 108, "#98c943"),
            new Family("steeldrum", 14, 114, "#2fbfb2"),
            new Family("songbird", 15, 123, "#46a6f5"),
            new Family("drumkit", 16, 0, "#ff5a6a"),
        };

        private static readonly Dictionary<string, Family> ByItemId = All.ToDictionary(f => f.ItemId);

        public static Family? FromItemId(string? itemId)
            => itemId != null && ByItemId.TryGetValue(itemId, out Family? family) ? family : null;

        public static Family ForProgram(int bank, int program)
            => bank >= DrumBank ? All[DrumKitIndex] : All[program / 8];

        public static Family DrumKit => All[DrumKitIndex];

        /// <summary>What a Junimo item is placed as: a violin, until its instrument is changed.</summary>
        public static Family JunimoStart => All[5];
    }

    internal static class Colors
    {
        public static Color Hex(string hex)
        {
            hex = hex.TrimStart('#');
            return new Color(
                System.Convert.ToInt32(hex.Substring(0, 2), 16),
                System.Convert.ToInt32(hex.Substring(2, 2), 16),
                System.Convert.ToInt32(hex.Substring(4, 2), 16));
        }
    }
}
