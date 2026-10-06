using System.Collections.Generic;
using System.Linq;

namespace JunimoOrchestra.Data
{
    /// <summary>
    /// One of the three Junimo items: crafted for nothing, 100 at a time, once the Community Center is restored, and placed
    /// as a block (<see cref="Families.JunimoStart"/>) with a look of its own. A classical Junimo plays its family's
    /// classical instrument (<see cref="Game.ClassicalArt"/>); a Junimo musician plays a real one, the nearest to what it
    /// plays (<see cref="Game.OrchestraArt.Musician"/>); a band Junimo is the game's own Junimo with its family's
    /// instrument (<see cref="Game.ModernArt"/>). Each follows its block when the instrument is changed.
    /// </summary>
    internal sealed class JunimoKind
    {
        /// <summary>Stable key used in the item id and i18n keys.</summary>
        public string Key { get; }

        /// <summary>Its icon in assets/textures/items.png (art/junimo_items.py).</summary>
        public int Index { get; }

        /// <summary>The look its block gets (<see cref="BlockSettings.Look"/>).</summary>
        public string Look { get; }

        public string ItemId => Family.ItemPrefix + "junimo_" + this.Key;
        public string QualifiedItemId => "(O)" + this.ItemId;

        public JunimoKind(string key, int index, string look)
        {
            this.Key = key;
            this.Index = index;
            this.Look = look;
        }
    }

    internal static class JunimoKinds
    {
        public static readonly JunimoKind Classical = new("classical", 0, "classical");
        public static readonly JunimoKind Musician = new("musician", 1, Game.OrchestraArt.MusicianLook);
        public static readonly JunimoKind Band = new("band", 2, Game.ModernArt.Look);

        public static readonly IReadOnlyList<JunimoKind> All = new[] { Classical, Musician, Band };

        private static readonly Dictionary<string, JunimoKind> ByItemId = All.ToDictionary(k => k.ItemId);

        public static JunimoKind? FromItemId(string? itemId)
            => itemId != null && ByItemId.TryGetValue(itemId, out JunimoKind? kind) ? kind : null;
    }
}
