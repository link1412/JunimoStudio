using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JunimoOrchestra.UI.Pix
{
    /// <summary>C# port of the sprite functions in art/ui.py (same names, same pixels).</summary>
    internal static class Px
    {
        public static Color C(string name) => PixData.P[name];

        public static readonly Color Clear = new(0, 0, 0, 0);

        /****
        ** Primitives
        ****/
        public static void FillRR(PixImage im, int x0, int y0, int w, int h, int r, Color col, bool onlyTop = false, bool onlyBottom = false)
        {
            int[] prof = PixData.Corners[Math.Clamp(r, 0, PixData.Corners.Length - 1)];
            for (int y = 0; y < h; y++)
            {
                int ry = y < r ? y : (y >= h - r ? h - 1 - y : -1);
                int inset = 0;
                if (ry >= 0 && ry < prof.Length)
                {
                    bool top = y < r;
                    inset = (onlyTop && !top) || (onlyBottom && top) ? 0 : prof[ry];
                }
                for (int x = inset; x < w - inset; x++)
                    im.Put(x0 + x, y0 + y, col);
            }
        }

        public static void RRLayers(PixImage im, int x, int y, int w, int h, int r, (int inset, Color col)[] layers, bool onlyTop = false, bool onlyBottom = false)
        {
            foreach ((int inset, Color col) in layers)
                FillRR(im, x + inset, y + inset, w - 2 * inset, h - 2 * inset, Math.Max(0, r - inset), col, onlyTop, onlyBottom);
        }

        /// <summary>Dashed "sewn" line along a rounded rect's perimeter.</summary>
        public static void StitchRR(PixImage im, int x, int y, int w, int h, int r, Color col, int dash = 2, int gap = 2)
        {
            int[] prof = PixData.Corners[r];
            int Inset(int ry) => ry < prof.Length ? prof[ry] : 0;
            var pts = new List<(int, int)>();
            for (int xx = Inset(0); xx < w - Inset(0); xx++)
                pts.Add((xx, 0));
            for (int yy = 1; yy < h - 1; yy++)
            {
                int ry = Math.Min(yy, h - 1 - yy);
                pts.Add((ry < r ? w - 1 - Inset(ry) : w - 1, yy));
            }
            for (int xx = w - 1 - Inset(0); xx > Inset(0) - 1; xx--)
                pts.Add((xx, h - 1));
            for (int yy = h - 2; yy > 0; yy--)
            {
                int ry = Math.Min(yy, h - 1 - yy);
                pts.Add((ry < r ? Inset(ry) : 0, yy));
            }
            var seen = new HashSet<(int, int)>();
            int k = 0;
            foreach (var p in pts)
            {
                if (!seen.Add(p))
                    continue;
                if (k % (dash + gap) < dash)
                    im.Put(x + p.Item1, y + p.Item2, col);
                k++;
            }
        }

        public static PixImage Gingham(int w, int h, int cell = 4)
        {
            var im = new PixImage(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool sx = (x / cell) % 2 == 0, sy = (y / cell) % 2 == 0;
                    im[x, y] = sx && sy ? C("ging3") : (sx || sy ? C("ging2") : C("ging1"));
                }
            }
            return im;
        }

        public static PixImage Solid(int w, int h, Color c) => new(w, h, c);

        /****
        ** Containers
        ****/
        public static PixImage Card(int w, int h, int r = 4, string fill = "milk", string edge = "rose2", bool lace = true)
        {
            var im = new PixImage(w, h + 1);
            FillRR(im, 0, 1, w, h, r, C("ging3"));
            RRLayers(im, 0, 0, w, h, r, new[] { (0, C(edge)), (1, C(fill)) });
            im.HLine(r, h - 2, w - 2 * r, C("milk2"));
            if (lace)
                StitchRR(im, 2, 2, w - 4, h - 4, Math.Max(0, r - 2), C("blush"), 1, 2);
            return im;
        }

        public static PixImage Tab(int w, int h, bool selected)
        {
            var im = new PixImage(w, h);
            if (selected)
            {
                RRLayers(im, 0, 0, w, h + 3, 3, new[] { (0, C("rose2")), (1, C("milk")) }, onlyTop: true);
                im.HLine(3, 1, w - 6, C("white"));
            }
            else
            {
                RRLayers(im, 0, 2, w, h, 3, new[] { (0, C("rose2")), (1, C("pink")) }, onlyTop: true);
                im.HLine(3, 3, w - 6, C("blush"));
            }
            return im;
        }

        public static PixImage GoldTab(bool selected)
        {
            var im = new PixImage(27, 13);
            if (selected)
                RRLayers(im, 0, 0, 27, 16, 3, new[] { (0, C("goldD")), (1, C("gold3")) }, onlyTop: true);
            else
            {
                RRLayers(im, 0, 2, 27, 13, 3, new[] { (0, C("goldD")), (1, C("gold")) }, onlyTop: true);
                im.HLine(3, 3, 21, C("gold3"));
            }
            return im;
        }

        /// <summary>Speech-bubble popover body (tail drawn separately).</summary>
        public static PixImage Bubble(int w, int h)
        {
            var b = new PixImage(w, h);
            RRLayers(b, 0, 0, w, h, 4, new[] { (0, C("berry")), (1, C("white")) });
            RRLayers(b, 2, 2, w - 4, h - 4, 3, new[] { (0, C("blush")) });
            RRLayers(b, 3, 3, w - 6, h - 6, 2, new[] { (0, C("milk")) });
            return b;
        }

        public static PixImage BubbleTail(bool up)
        {
            string[] rows = up
                ? new[] { "...O...", "..OWO..", ".OWWWO.", "OWWWWWO" }
                : new[] { "OWWWWWO", ".OWWWO.", "..OWO..", "...O..." };
            return PixImage.Grid(rows, new Dictionary<char, Color> { ['O'] = C("berry"), ['W'] = C("white") });
        }

        /// <summary>Rounded box with an outline and a fill (chips, value boxes, headers).</summary>
        public static PixImage Box(int w, int h, int r, Color edge, Color fill, Color? inner = null)
        {
            var im = new PixImage(w, h);
            if (inner is Color c)
                RRLayers(im, 0, 0, w, h, r, new[] { (0, edge), (1, fill), (2, c) });
            else
                RRLayers(im, 0, 0, w, h, r, new[] { (0, edge), (1, fill) });
            return im;
        }

        /****
        ** Widgets
        ****/
        private static readonly Dictionary<string, string[]> Btn = new()
        {
            ["pink"] = new[] { "berry", "rose", "rose2", "pink", "white" },
            ["mint"] = new[] { "mintD", "mint", "mint2", "mint3", "mintD" },
            ["milk"] = new[] { "berry2", "milk", "milk3", "white", "ink" },
            ["gold"] = new[] { "goldD", "gold", "gold2", "gold3", "goldD" },
        };

        public static PixImage Button(int w, int h, string style = "pink", string state = "normal", int r = 3)
        {
            string[] s = Btn[style];
            Color o = C(s[0]), f = C(s[1]), lip = C(s[2]), hi = C(s[3]);
            var im = new PixImage(w, h);
            int dy = state == "down" ? 1 : 0;
            int bodyH = h;
            RRLayers(im, 0, dy, w, bodyH - dy, r, new[] { (0, o), (1, lip) });
            int topH = bodyH - dy - (state == "down" ? 1 : 3);
            FillRR(im, 1, 1 + dy, w - 2, topH - 1, Math.Max(0, r - 1), f);
            im.HLine(r, 2 + dy, w - 2 * r - 2, hi);
            im.Put(2, 3 + dy, hi);
            if (state == "hover")
            {
                FillRR(im, 1, 1, w - 2, topH - 1, Math.Max(0, r - 1), hi);
                FillRR(im, 2, 3, w - 4, topH - 4, Math.Max(0, r - 2), f);
                im.HLine(r, 2, w - 2 * r - 2, C("white"));
            }
            return im;
        }

        public static PixImage RoundButton(int d, string style = "pink", string state = "normal")
            => Button(d, d, style, state, Math.Min(6, d / 2 - 1));

        public static PixImage Pill(int w, int h, string state, Ramp? accent)
        {
            var im = new PixImage(w, h);
            if (state == "selected" && accent != null)
            {
                RRLayers(im, 0, 0, w, h, 3, new[] { (0, accent.O), (1, accent.L) });
                im.HLine(3, 1, w - 6, accent.H);
                im.HLine(3, h - 2, w - 6, accent.D);
            }
            else if (state == "hover")
            {
                RRLayers(im, 0, 0, w, h, 3, new[] { (0, C("rose2")), (1, C("blush")) });
                im.HLine(3, 1, w - 6, C("white"));
            }
            else
            {
                RRLayers(im, 0, 0, w, h, 3, new[] { (0, C("milk4")), (1, C("white")) });
                im.HLine(3, h - 2, w - 6, C("milk2"));
            }
            return im;
        }

        public static PixImage SliderTrack(int w)
        {
            var im = new PixImage(w, 7);
            RRLayers(im, 0, 0, w, 7, 3, new[] { (0, C("berry2")), (1, C("milk2")) });
            im.HLine(3, 5, w - 6, C("milk"));
            return im;
        }

        public static PixImage SliderFill(int w, Ramp accent)
        {
            var im = new PixImage(w, 7);
            RRLayers(im, 0, 0, w, 7, 3, new[] { (0, C("berry2")), (1, accent.L) });
            im.HLine(3, 2, w - 5, accent.H);
            im.HLine(3, 5, w - 5, accent.M);
            return im;
        }

        public static PixImage HeartBadge() => PixImage.Grid(new[]
        {
            ".OO.OO.",
            "ORWORRO",
            "ORRRRRO",
            ".ORRRO.",
            "..ORO..",
            "...O...",
        }, new Dictionary<char, Color> { ['O'] = C("berry"), ['R'] = Ramp.Hex2("#ff6f96"), ['W'] = C("white") });

        public static PixImage HeartKnob() => PixImage.Grid(PixData.HeartKnob, new Dictionary<char, Color>
        {
            ['O'] = C("berry"), ['R'] = Ramp.Hex2("#ff6f96"), ['W'] = C("white"), ['d'] = Ramp.Hex2("#d9406e")
        });

        public static PixImage Knob(Ramp accent) => PixImage.Grid(new[]
        {
            "..OOO..",
            ".OHLLO.",
            "OHLLLLO",
            "OLLLLdO",
            "OLLLLdO",
            ".OddO..",
            "..OOO..",
        }, new Dictionary<char, Color> { ['O'] = accent.O, ['H'] = C("white"), ['L'] = accent.L, ['d'] = accent.M });

        public static PixImage Toggle(bool on)
        {
            var im = new PixImage(19, 10);
            Color o = C(on ? "mintD" : "milk4"), f = C(on ? "mint" : "milk2");
            RRLayers(im, 0, 0, 19, 10, 4, new[] { (0, o), (1, f) });
            var k = new PixImage(8, 8);
            RRLayers(k, 0, 0, 8, 8, 3, new[] { (0, o), (1, C("white")) });
            im.Blit(k, on ? 10 : 1, 1);
            return im;
        }

        public static PixImage WhiteKey(int w, int h, string state = "normal", Ramp? accent = null)
        {
            var im = new PixImage(w, h);
            if (state == "missing")
            {
                // past G9: MIDI has no such key, so only its faint shape
                RRLayers(im, 0, -3, w, h + 3, 3, new[] { (0, C("milk3")), (1, C("milk2")) }, onlyBottom: true);
                return im;
            }
            Color fill = C("keyW"), shade = C("keyW2");
            if (state == "hover") { fill = C("blush"); shade = C("pink"); }
            else if (state == "selected" && accent != null) { fill = accent.H; shade = accent.L; }
            else if (state == "down") { fill = C("pink"); shade = C("rose"); }
            RRLayers(im, 0, -3, w, h + 3, 3, new[] { (0, C("keyO")), (1, fill) }, onlyBottom: true);
            for (int yy = h - 5; yy < h - 1; yy++)
                im.HLine(1, yy, w - 2, shade);
            FillRR(im, 1, h - 5, w - 2, 4, 2, shade, onlyBottom: true);
            im.HLine(1, h - 6, w - 2, state == "normal" ? C("white") : fill);
            im.HLine(1, 0, w - 2, C("keyW2"));
            return im;
        }

        public static PixImage BlackKey(int w, int h, string state = "normal", Ramp? accent = null)
        {
            var im = new PixImage(w, h);
            Color body = C("keyB"), top = C("keyB2"), lip = C("keyB3");
            if (state == "hover") { body = C("plum3"); top = Ramp.Hex2("#8a5a8c"); }
            else if (state == "selected" && accent != null) { body = accent.M; top = accent.H; lip = accent.O; }
            RRLayers(im, 0, -2, w, h + 2, 2, new[] { (0, C("keyB3")), (1, body) }, onlyBottom: true);
            FillRR(im, 1, h - 4, w - 2, 3, 1, lip, onlyBottom: true);
            im.VLine(1, 1, h - 6, top);
            im.HLine(1, h - 5, w - 2, top);
            return im;
        }

        /// <summary>A note in the waterfall.</summary>
        public static PixImage Candy(int w, int h, Ramp accent, bool selected)
        {
            h = Math.Max(h, 3);
            if (selected)
            {
                var s = new PixImage(w + 2, h + 2);
                RRLayers(s, 0, 0, w + 2, h + 2, 2, new[] { (0, C("white")) });
                RRLayers(s, 1, 1, w, h, 1, new[] { (0, C("berry")), (1, accent.L) });
                s.VLine(2, 2, Math.Max(1, h - 3), C("white"));
                return s;
            }
            var im = new PixImage(w, h);
            RRLayers(im, 0, 0, w, h, 1, new[] { (0, accent.O), (1, accent.L) });
            if (w > 3 && h > 3)
                im.VLine(1, 1, Math.Max(1, h - 3), accent.H);
            return im;
        }

        /// <summary>An icon from the exported grids. Colours default to ink on white.</summary>
        public static PixImage Icon(string name, Color col, Color? hi = null, Color? acc = null)
        {
            var pal = new Dictionary<char, Color>
            {
                ['O'] = col,
                ['W'] = hi ?? C("white"),
                ['A'] = acc ?? C("rose")
            };
            return PixImage.Grid(PixData.Icons[name], pal);
        }

        public static (int w, int h) IconSize(string name)
        {
            string[] rows = PixData.Icons[name];
            int w = 0;
            foreach (string r in rows)
                w = Math.Max(w, r.Length);
            return (w, rows.Length);
        }
    }
}
