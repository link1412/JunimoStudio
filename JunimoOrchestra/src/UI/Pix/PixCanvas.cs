using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace JunimoOrchestra.UI.Pix
{
    internal enum Align { Left, Center, Right }

    /// <summary>Cache of generated sprites, keyed by a description of how they were drawn.</summary>
    internal static class SpriteCache
    {
        private static readonly Dictionary<string, Texture2D> Textures = new();

        public static Texture2D Get(string key, Func<PixImage> build)
        {
            if (!Textures.TryGetValue(key, out Texture2D? tex) || tex.IsDisposed)
                Textures[key] = tex = build().ToTexture(Game1.graphics.GraphicsDevice);
            return tex;
        }

        public static void Clear()
        {
            foreach (Texture2D tex in Textures.Values)
                tex.Dispose();
            Textures.Clear();
        }
    }

    /// <summary>
    /// Draws in panel "art pixel" coordinates, 4 screen pixels per art pixel, like Canvas in art/mockup.py.
    /// Text uses the game's own SpriteFonts at native size; <c>dx/dy</c> are in screen pixels.
    /// </summary>
    internal sealed class PixCanvas
    {
        public const int Z = 4;

        public SpriteBatch B = null!;
        public int OriginX;
        public int OriginY;

        public static SpriteFont Small => Game1.smallFont;
        public static SpriteFont Big => Game1.dialogueFont;
        public static SpriteFont Tiny => Game1.tinyFont;

        public Vector2 ToScreen(int x, int y) => new(this.OriginX + x * Z, this.OriginY + y * Z);

        public void Spr(Texture2D tex, int x, int y, float alpha = 1f)
            => this.B.Draw(tex, this.ToScreen(x, y), null, Color.White * alpha, 0f, Vector2.Zero, Z, SpriteEffects.None, 1f);

        public void Spr(Texture2D tex, Rectangle src, int x, int y, Color? tint = null, float scale = Z)
            => this.B.Draw(tex, this.ToScreen(x, y), src, tint ?? Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);

        /// <summary>Draw a cached generated sprite.</summary>
        public void Spr(string key, Func<PixImage> build, int x, int y, float alpha = 1f)
            => this.Spr(SpriteCache.Get(key, build), x, y, alpha);

        /// <summary>Solid rectangle in art pixels.</summary>
        public void Rect(int x, int y, int w, int h, Color c)
        {
            if (w > 0 && h > 0)
                this.B.Draw(Game1.staminaRect, new Rectangle(this.OriginX + x * Z, this.OriginY + y * Z, w * Z, h * Z), c);
        }

        public void Icon(string name, Color col, int x, int y, Color? hi = null, Color? acc = null)
        {
            Color h = hi ?? Px.C("white");
            this.Spr($"icon:{name}:{col.PackedValue}:{h.PackedValue}:{acc?.PackedValue}", () => Px.Icon(name, col, h, acc), x, y);
        }

        /// <summary>Shorten text with ".." until it fits in a width (screen px).</summary>
        public static string Fit(string text, int maxPx, SpriteFont? font = null)
        {
            if (Measure(text, font) <= maxPx)
                return text;
            for (int n = text.Length - 1; n > 0; n--)
            {
                string t = text[..n].TrimEnd() + "..";
                if (Measure(t, font) <= maxPx)
                    return t;
            }
            return "";
        }

        public static int Measure(string text, SpriteFont? font = null)
            => text.Length == 0 ? 0 : (int)Math.Round((font ?? Small).MeasureString(text).X);

        /// <summary>Draw text; returns its width in screen pixels (like Canvas.text in the mockups).</summary>
        public int Text(string text, int x, int y, Color col, SpriteFont? font = null, (int dx, int dy, Color c)? shadow = null,
            Align align = Align.Left, int w = 0, int dx = 0, int dy = 0)
        {
            font ??= Small;
            int tw = Measure(text, font);
            int sx = this.OriginX + x * Z + dx;
            if (align == Align.Center)
                sx = this.OriginX + x * Z + (w * Z - tw) / 2 + dx;
            else if (align == Align.Right)
                sx = this.OriginX + x * Z + w * Z - tw + dx;
            int sy = this.OriginY + y * Z + dy;
            if (shadow is { } s)
                this.B.DrawString(font, text, new Vector2(sx + s.dx, sy + s.dy), s.c);
            this.B.DrawString(font, text, new Vector2(sx, sy), col);
            return tw;
        }

        /// <summary>Draw a string at raw screen coordinates (tiny ruler labels etc.).</summary>
        public void TextPx(string text, int sx, int sy, Color col, SpriteFont? font = null)
            => this.B.DrawString(font ?? Small, text, new Vector2(sx, sy), col);
    }
}
