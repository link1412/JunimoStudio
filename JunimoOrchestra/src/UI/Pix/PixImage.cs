using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JunimoOrchestra.UI.Pix
{
    /// <summary>A small RGBA pixel buffer (straight alpha), the C# twin of a PIL image in art/ui.py.</summary>
    internal sealed class PixImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color[] Pixels;

        public PixImage(int width, int height, Color? fill = null)
        {
            this.Width = Math.Max(0, width);
            this.Height = Math.Max(0, height);
            this.Pixels = new Color[this.Width * this.Height];
            if (fill is Color c)
                Array.Fill(this.Pixels, c);
        }

        public Color this[int x, int y]
        {
            get => this.Pixels[y * this.Width + x];
            set => this.Pixels[y * this.Width + x] = value;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < this.Width && y < this.Height;

        public void Put(int x, int y, Color c)
        {
            if (this.InBounds(x, y))
                this[x, y] = c;
        }

        public void HLine(int x, int y, int w, Color c)
        {
            for (int i = 0; i < w; i++)
                this.Put(x + i, y, c);
        }

        public void VLine(int x, int y, int h, Color c)
        {
            for (int i = 0; i < h; i++)
                this.Put(x, y + i, c);
        }

        /// <summary>Alpha-composite another image on top (PIL's alpha_composite for our 0/255-alpha art).</summary>
        public void Blit(PixImage src, int x, int y)
        {
            for (int sy = 0; sy < src.Height; sy++)
            {
                for (int sx = 0; sx < src.Width; sx++)
                {
                    Color s = src[sx, sy];
                    if (s.A == 0 || !this.InBounds(x + sx, y + sy))
                        continue;
                    if (s.A == 255)
                    {
                        this[x + sx, y + sy] = s;
                        continue;
                    }
                    Color d = this[x + sx, y + sy];
                    float a = s.A / 255f;
                    float da = d.A / 255f * (1 - a);
                    float oa = a + da;
                    this[x + sx, y + sy] = new Color(
                        (int)Math.Round((s.R * a + d.R * da) / oa),
                        (int)Math.Round((s.G * a + d.G * da) / oa),
                        (int)Math.Round((s.B * a + d.B * da) / oa),
                        (int)Math.Round(oa * 255));
                }
            }
        }

        /// <summary>Upload as a premultiplied texture, as SpriteBatch expects.</summary>
        public Texture2D ToTexture(GraphicsDevice device)
        {
            var data = new Color[this.Pixels.Length];
            for (int i = 0; i < data.Length; i++)
            {
                Color c = this.Pixels[i];
                data[i] = c.A == 255 ? c : new Color(c.R * c.A / 255, c.G * c.A / 255, c.B * c.A / 255, c.A);
            }
            var tex = new Texture2D(device, Math.Max(1, this.Width), Math.Max(1, this.Height));
            if (this.Width > 0 && this.Height > 0)
                tex.SetData(data);
            return tex;
        }

        /// <summary>Build an image from an ASCII grid; '.' and ' ' are transparent.</summary>
        public static PixImage Grid(string[] rows, IReadOnlyDictionary<char, Color> palette)
        {
            int w = 0;
            foreach (string r in rows)
                w = Math.Max(w, r.Length);
            var im = new PixImage(w, rows.Length);
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char ch = rows[y][x];
                    if (ch == '.' || ch == ' ')
                        continue;
                    if (palette.TryGetValue(ch, out Color c))
                        im[x, y] = c;
                }
            }
            return im;
        }
    }

    /// <summary>A hue-shifted colour ramp, ported from px.ramp: shadows lean cool, highlights lean warm.</summary>
    internal sealed class Ramp
    {
        public readonly Color O;   // outline
        public readonly Color D;   // dark
        public readonly Color M;   // mid
        public readonly Color L;   // base
        public readonly Color H;   // light
        public readonly Color HH;  // highlight
        public readonly string Hex;

        private static readonly Dictionary<string, Ramp> Cache = new();

        private Ramp(string hex)
        {
            this.Hex = hex;
            Color c = Hex2(hex);
            double Cool(double amt) => Toward(c, 250, amt);
            double Warm(double amt) => Toward(c, 55, amt);
            this.O = Shift(c, -0.42, -0.05, Cool(0.22));
            this.D = Shift(c, -0.20, -0.04, Cool(0.10));
            this.M = Shift(c, -0.08, 0.00, Cool(0.05));
            this.L = c;
            this.H = Shift(c, +0.12, -0.05, Warm(0.10));
            this.HH = Shift(c, +0.24, -0.10, Warm(0.16));
        }

        public static Ramp Of(string hex)
        {
            if (!Cache.TryGetValue(hex, out Ramp? r))
                Cache[hex] = r = new Ramp(hex);
            return r;
        }

        public static Ramp Of(Color c) => Of($"#{c.R:x2}{c.G:x2}{c.B:x2}");

        public static Color Hex2(string hex)
        {
            hex = hex.TrimStart('#');
            return new Color(Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex.Substring(2, 2), 16), Convert.ToInt32(hex.Substring(4, 2), 16));
        }

        private static double Mod1(double v) => v - Math.Floor(v);

        private static double Toward(Color c, double targetHueDeg, double amount)
        {
            (double h, _, _) = RgbToHls(c.R / 255.0, c.G / 255.0, c.B / 255.0);
            double t = targetHueDeg / 360;
            double d = Mod1(t - h + 0.5) - 0.5;
            return d * amount;
        }

        private static Color Shift(Color c, double dl, double ds, double dh)
        {
            (double h, double l, double s) = RgbToHls(c.R / 255.0, c.G / 255.0, c.B / 255.0);
            h = Mod1(h + dh);
            l = Math.Clamp(l + dl, 0, 1);
            s = Math.Clamp(s + ds, 0, 1);
            (double r, double g, double b) = HlsToRgb(h, l, s);
            return new Color((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255), c.A);
        }

        // Python's colorsys, verbatim
        private static (double h, double l, double s) RgbToHls(double r, double g, double b)
        {
            double maxc = Math.Max(r, Math.Max(g, b)), minc = Math.Min(r, Math.Min(g, b));
            double sumc = maxc + minc, rangec = maxc - minc;
            double l = sumc / 2.0;
            if (minc == maxc)
                return (0, l, 0);
            double s = l <= 0.5 ? rangec / sumc : rangec / (2.0 - maxc - minc);
            double rc = (maxc - r) / rangec, gc = (maxc - g) / rangec, bc = (maxc - b) / rangec;
            double h = r == maxc ? bc - gc : g == maxc ? 2.0 + rc - bc : 4.0 + gc - rc;
            return (Mod1(h / 6.0), l, s);
        }

        private static (double r, double g, double b) HlsToRgb(double h, double l, double s)
        {
            if (s == 0.0)
                return (l, l, l);
            double m2 = l <= 0.5 ? l * (1.0 + s) : l + s - l * s;
            double m1 = 2.0 * l - m2;
            return (V(m1, m2, h + 1 / 3.0), V(m1, m2, h), V(m1, m2, h - 1 / 3.0));
        }

        private static double V(double m1, double m2, double hue)
        {
            hue = Mod1(hue);
            if (hue < 1 / 6.0)
                return m1 + (m2 - m1) * hue * 6.0;
            if (hue < 0.5)
                return m2;
            if (hue < 2 / 3.0)
                return m1 + (m2 - m1) * (2 / 3.0 - hue) * 6.0;
            return m1;
        }
    }
}
