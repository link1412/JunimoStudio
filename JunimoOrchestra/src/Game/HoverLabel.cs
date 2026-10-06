using System;
using System.Linq;
using JunimoOrchestra.Data;
using JunimoOrchestra.UI;
using JunimoOrchestra.UI.Pix;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using SObject = StardewValley.Object;

namespace JunimoOrchestra.Game
{
    /// <summary>A little name tag over the block under the cursor ("八音盒 · C4 E4 G4"), and the tiles it hears someone
    /// from, tinted on the ground.</summary>
    internal sealed class HoverLabel
    {
        private const int Scale = 3;
        private readonly SoundNames names;

        public HoverLabel(SoundNames names)
        {
            this.names = names;
        }

        /// <summary>The hovered block's reach: the tiles straight across from it where stepping plays it, in its colour,
        /// fainter further out (drawn with the world, under the HUD).</summary>
        public void DrawReach(SpriteBatch b, int worldReach)
        {
            if (Game1.activeClickableMenu != null || Game1.eventUp || Game1.currentLocation == null)
                return;
            Vector2 tile = Game1.currentCursorTile;
            if (!Game1.currentLocation.objects.TryGetValue(tile, out SObject? obj) || Patches.BlockFamily(obj) is not Family family)
                return;
            int reach = BlockSettings.ReachOf(obj, worldReach);
            foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                for (int k = 1; k <= reach; k++)
                {
                    float alpha = 0.34f - 0.16f * (k - 1) / Math.Max(1, BlockSettings.MaxReach - 1);
                    Vector2 at = Game1.GlobalToLocal(Game1.viewport, new Vector2((tile.X + dx * k) * 64, (tile.Y + dy * k) * 64));
                    var r = new Rectangle((int)at.X + 2, (int)at.Y + 2, 60, 60);
                    b.Draw(Game1.staminaRect, r, family.Accent * alpha);
                    Color edge = family.Accent * Math.Min(1, alpha * 2.2f);   // a crisp rim, so it reads on any ground
                    b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, r.Width, 4), edge);
                    b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - 4, r.Width, 4), edge);
                    b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y + 4, 4, r.Height - 8), edge);
                    b.Draw(Game1.staminaRect, new Rectangle(r.Right - 4, r.Y + 4, 4, r.Height - 8), edge);
                }
            }
        }

        public void Draw(SpriteBatch b)
        {
            if (Game1.activeClickableMenu != null || Game1.eventUp || Game1.currentLocation == null)
                return;
            Vector2 tile = Game1.currentCursorTile;
            if (!Game1.currentLocation.objects.TryGetValue(tile, out SObject? obj) || Patches.BlockFamily(obj) is not Family family)
                return;

            BlockSettings s = BlockSettings.Read(obj, family);
            SpriteFont font = Game1.smallFont;
            string gap = font.MeasureString("A A").X - font.MeasureString("AA").X < 8 ? "  " : " "; // the zh font's space is very thin
            string notes = s.Notes.Count is > 0 and <= 4
                ? string.Join(gap, s.Notes.OrderBy(n => n.Delay).ThenBy(n => n.Pitch).Select(n => SoundNames.Note(n.Pitch)))
                : this.names.T("ui.label.notes").Replace("{{count}}", s.Notes.Count.ToString());
            string text = $"{this.names.Sounding(s.Bank, s.Program)}{gap}·{gap}{notes}";

            Vector2 size = font.MeasureString(text);
            int w = (int)Math.Ceiling(size.X / Scale) + 8;
            const int h = 11;
            Ramp acc = Ramp.Of(family.Accent);
            Texture2D box = SpriteCache.Get($"label:{w}:{acc.Hex}", () => Px.Box(w, h, 3, acc.O, Px.C("milk")));
            Texture2D tail = SpriteCache.Get($"labeltail:{acc.Hex}", () => PixImage.Grid(new[] { "OWWWO", ".OWO.", "..O.." },
                new System.Collections.Generic.Dictionary<char, Color> { ['O'] = acc.O, ['W'] = Px.C("milk") }));

            Vector2 anchor = Game1.GlobalToLocal(Game1.viewport, new Vector2(tile.X * 64 + 32, tile.Y * 64 - 8));
            anchor = Utility.ModifyCoordinatesForUIScale(anchor);
            var pos = new Vector2((int)(anchor.X - w * Scale / 2f), (int)(anchor.Y - (h + 2) * Scale));
            b.Draw(box, pos, null, Color.White, 0, Vector2.Zero, Scale, SpriteEffects.None, 1);
            b.Draw(tail, pos + new Vector2((w / 2 - 2) * Scale, (h - 1) * Scale), null, Color.White, 0, Vector2.Zero, Scale, SpriteEffects.None, 1);
            b.DrawString(font, text, pos + new Vector2((w * Scale - size.X) / 2, (h * Scale - size.Y) / 2 + 3), Px.C("ink"));
        }
    }
}
