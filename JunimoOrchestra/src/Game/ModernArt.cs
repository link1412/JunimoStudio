using System;
using JunimoOrchestra.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace JunimoOrchestra.Game
{
    /// <summary>
    /// The band look for blocks (the modern set: assets/textures/junimo_blocks.png, made by art/junimo_blocks.py): the game's own
    /// Junimo, as it comes out of a Junimo Hut, playing its family's instrument (a keyboard, a keytar, an electric guitar,
    /// a mic, a drum kit...), a few in something small for character (shades, a hat, a beret, a bow). A block whose look is
    /// "band" is drawn as its own family's, so it takes up the instrument it's tuned to. The sheet has a 16x24 cell per
    /// family (column = Family.Index) and frame (row): idle, blink, A, B, long note, and what it stands on (row 5:
    /// the trumpeter's and the keytarist's speaker cabinet, the pan piper's hay bale); a cell stands on its tile like a big
    /// craftable (the top 8 rows rise above it) and is never wider than it, so a row of them side by side never overlaps.
    /// It moves only as it plays its own notes, as the classical set does (<see cref="ClassicalArt.FrameOf"/>): A and B in
    /// turn on each new note, held while it sounds, its eyes shut on a long note; one that holds its instrument (a
    /// guitar, a keytar, a sax...) keeps still while it plays and breathes while it doesn't (<see cref="Motion"/>); one at an instrument on a stand or the floor (a keyboard, a glockenspiel, the upright
    /// violin, a mic, a steel pan, the drum kit) only plays. It never squashes, and no notes float up: the instrument
    /// being played is the effect.
    /// </summary>
    internal static class ModernArt
    {
        private const int Width = 16, Height = 24;

        /// <summary>The Band Junimo item's look.</summary>
        public const string Look = "band";

        public static bool IsLook(string? look) => look == Look;

        /// <summary>The sheet's row of what the trumpeter and the keytarist (a speaker cabinet) and the pan piper (a hay
        /// bale) stand on: holding their instruments up at their faces with nothing below, they hovered. It's drawn under
        /// them, still as they breathe.</summary>
        private const int Stand = 5;

        private static bool Stands(Family family) => family.Key is "trumpet" or "synth" or "ocarina";

        /// <summary>Whether it holds its instrument, so it can breathe with it.</summary>
        private static bool Holds(Family family) => family.Key is "organ" or "guitar" or "bass" or "trumpet" or "sax"
            or "ocarina" or "synth" or "cloud" or "stardust" or "kalimba" or "songbird";

        /// <summary>How one holding its instrument moves (as the classical set's do, <see cref="ClassicalArt.Motion"/>):
        /// still while it plays, and its breath while it doesn't. Its lean (radians, about its feet) and lift (world pixels, negative = up).</summary>
        public static (float Lean, float Lift) Motion(Family family, Staging stage, long age, NoteState note, int x, int y)
        {
            if (!Holds(family) || stage.IsStill(age))
                return (0f, 0f);
            var breath = NoteState.Breath(x, y);
            float waiting = 1f - note.Busy;
            return (NoteState.BreathSway * breath.Sway * waiting, -breath.Rise * 2f * waiting);
        }

        /// <param name="lean">Its lean (radians, about its feet) and lift (world pixels, negative = up), see <see cref="Motion"/>.</param>
        public static void Draw(SpriteBatch b, Texture2D sheet, Family family, int x, int y, float alpha, float layer, int frame,
            float lean = 0f, float lift = 0f)
        {
            Vector2 floor = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 64));
            b.Draw(Game1.shadowTexture, floor + new Vector2(0, -6), Game1.shadowTexture.Bounds, Color.White * alpha, 0f,
                new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y),
                3f * (1 + Math.Max(-0.3f, lift / 60f)), SpriteEffects.None, layer - 1e-5f);
            if (Stands(family))
                b.Draw(sheet, floor, new Rectangle(family.Index * Width, Stand * Height, Width, Height), Color.White * alpha, 0f,
                    new Vector2(Width / 2f, Height), 4f, SpriteEffects.None, layer - 2e-6f);
            b.Draw(sheet, floor + new Vector2(0, lift), new Rectangle(family.Index * Width, frame * Height, Width, Height),
                Color.White * alpha, lean, new Vector2(Width / 2f, Height), 4f, SpriteEffects.None, layer);
        }
    }
}
