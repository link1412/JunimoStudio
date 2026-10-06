using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JunimoOrchestra.Data;
using JunimoOrchestra.UI.Pix;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace JunimoOrchestra.UI
{
    /// <summary>音符 tab: range strip, note waterfall, two-octave keyboard and the selected note's inspector. Every MIDI
    /// pitch can be written, C-1 (0) to G9 (127).</summary>
    internal sealed partial class TunerMenu
    {
        private const int Ppq = BlockSettings.Ppq;
        private const int PxPerQ = 10;                 // waterfall: a quarter note is 10 art px tall
        private const int KbX = OX + 5, KeyW = 10;     // keyboard origin and white key pitch
        private const int FallY = OY + 17, FallH = 46; // waterfall area
        private const int KeysY = OY + 64, KeysH = 24, BlackH = 15;
        private const int SnapTicks = 120;
        private static readonly int[] WhiteSemis = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] BlackSemis = { 1, 3, 6, 8, 10 };

        /// <summary>The highest MIDI pitch (G9).</summary>
        private const int TopPitch = 127;

        /// <summary>The highest view, C8–B9: the keys above G9 are drawn missing.</summary>
        private const int TopLo = 108;

        /// <summary>The range strip: an 8-pixel cell per octave (7 white keys and a line), C-1 to B9.</summary>
        private const int StripCell = 8, StripW = 11 * StripCell - 1 + 2;

        private int lo = 60;            // lowest visible pitch (always a C)
        private string? stripTip;       // the hovered range strip's bubble, drawn over the waterfall
        private int scrollTicks;        // tick at the bottom of the waterfall
        private int selected = -1;      // index into settings.Notes
        private bool drumView;
        private int tempoChipX = OX + 110;   // where the tempo chip was drawn: its popover points there

        // drag state
        private BlockNote dragOrigin;
        private float dragStartX, dragStartY;
        private bool dragResize;

        private int FallTicks => FallH * Ppq / PxPerQ;
        private BlockNote? Sel => this.selected >= 0 && this.selected < this.settings.Notes.Count ? this.settings.Notes[this.selected] : null;
        private static bool Shift => Game1.input.GetKeyboardState().IsKeyDown(Keys.LeftShift) || Game1.input.GetKeyboardState().IsKeyDown(Keys.RightShift);

        private void InitNotes()
        {
            this.drumView = this.family.IsDrumKit;
            this.selected = this.settings.Notes.Count > 0 ? 0 : -1;
            this.scrollTicks = 0;
            this.lo = this.drumView ? 36 : 60;
            if (this.Sel is BlockNote n && (n.Pitch < this.lo || n.Pitch >= this.lo + 24))
                this.lo = ClampLo(n.Pitch / 12 * 12);
        }

        private static int ClampLo(int lo) => Math.Clamp(lo / 12 * 12, 0, TopLo);

        private static bool IsPitch(int m) => m >= 0 && m <= TopPitch;

        /// <summary>The range strip's column for a pitch (inside its frame): its white key, or for a black key the white
        /// key below it.</summary>
        private static int StripX(int m)
        {
            int semi = m % 12;
            int i = Array.IndexOf(WhiteSemis, semi);
            return 1 + m / 12 * StripCell + (i >= 0 ? i : Array.IndexOf(WhiteSemis, semi - 1));
        }

        /// <summary>Scroll the keyboard so the selected note is visible.</summary>
        private void RevealSelected()
        {
            if (this.Sel is BlockNote n && (n.Pitch < this.lo || n.Pitch >= this.lo + 24))
                this.lo = ClampLo(n.Pitch / 12 * 12);
        }

        /// <summary>Screen column of each visible pitch: (x, width, isBlack).</summary>
        private Dictionary<int, (int x, int w, bool black)> Columns()
        {
            var cols = new Dictionary<int, (int, int, bool)>();
            for (int oc = 0; oc < 2; oc++)
                for (int i = 0; i < 7; i++)
                    cols[this.lo + oc * 12 + WhiteSemis[i]] = (KbX + (oc * 7 + i) * KeyW + 1, KeyW - 1, false);
            for (int oc = 0; oc < 2; oc++)
            {
                foreach (int semi in BlackSemis)
                {
                    int m = this.lo + oc * 12 + semi;
                    cols[m] = (cols[m - 1].Item1 + KeyW - 3, 5, true);
                }
            }
            return cols;
        }

        /// <summary>Pitch under an art-px x position inside the waterfall/keyboard (black lanes win).</summary>
        private int? PitchAt(float ax, bool preferBlack = true)
        {
            var cols = this.Columns();
            if (preferBlack)
                foreach (var (m, c) in cols)
                    if (c.black && ax >= c.x && ax < c.x + c.w)
                        return IsPitch(m) ? m : null;
            foreach (var (m, c) in cols)
                if (!c.black && ax >= c.x - 1 && ax < c.x + c.w)
                    return IsPitch(m) ? m : null;
            return null;
        }

        private int TickAtY(float ay) => this.scrollTicks + (int)Math.Round((FallY + FallH - 1 - ay) * Ppq / PxPerQ);

        private void DrawNotesTab()
        {
            Ramp acc = this.Acc;
            if (this.selected >= this.settings.Notes.Count)
                this.selected = this.settings.Notes.Count - 1;
            if (this.mod.Config.SimpleNotes)
            {
                this.DrawSimpleNotes();
                return;
            }
            this.DrawRangeStrip(OX + 6, OY + 3, acc);
            this.DrawTempoChip(OX + 110, OY + 3, acc);
            bool helpHover = this.Region(OX + 137, OY + 3, 10, 10, () => this.TogglePop(Pop.Help));
            this.cv.Spr($"help:{helpHover}", () => Px.Box(10, 10, 4, Px.C("rose2"), helpHover ? Px.C("pink") : Px.C("blush")), OX + 137, OY + 3);
            this.cv.Text("?", OX + 137, OY + 3, Px.C("berry"), align: Align.Center, w: 10, dy: 3);

            double playTick = this.PreviewPlaying ? (Now - this.previewStart) / this.previewSecondsPerTick : -1;
            this.DrawWaterfall(acc, playTick);
            this.DrawKeyboard(acc, playTick);
            this.DrawInspector(acc);
            if (this.stripTip is string tip)
            {
                int w = PixCanvas.Measure(tip) / Z + 10;
                this.cv.Spr($"bubble:{w}:12", () => Px.Bubble(w, 12), OX + 6, FallY + 1);
                this.cv.Text(tip, OX + 6, FallY + 1, Ink, align: Align.Center, w: w, dy: 2);
            }
        }

        /****
        ** Toolbar
        ****/
        /// <summary>A minimap of all 128 pitches: a cell per octave, a pixel per white key (a black key darkens the top of
        /// the white key below it, so the groups of two and three read as a keyboard), past G9 missing like on the
        /// keyboard. A frame marks the two visible octaves, a tick above it each pitch the block uses. Click or drag to
        /// look elsewhere.</summary>
        private void DrawRangeStrip(int sx, int y, Ramp acc)
        {
            this.cv.Spr("rangestrip128", () =>
            {
                var fr = new PixImage(StripW, 8);
                Px.RRLayers(fr, 0, 0, StripW, 8, 1, new[] { (0, Px.C("keyO")) });
                for (int oct = 0; oct < 11; oct++)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        int m = oct * 12 + WhiteSemis[i];
                        bool black = WhiteSemis[i] is not (4 or 11) && IsPitch(m + 1);
                        for (int yy = 0; yy < 6; yy++)
                            fr[StripX(m), 1 + yy] = !IsPitch(m) ? Px.C(yy % 2 == 0 ? "milk3" : "milk2")
                                : black && yy < 3 ? Px.C("keyB") : Px.C("keyW");
                    }
                }
                return fr;
            }, sx, y + 1);
            // the clicked spot lands mid-view
            int LoAt(float ax) => ClampLo((int)Math.Round((ax - (sx + 1)) / StripCell - 1) * 12);
            bool hover = this.Region(sx, y - 2, StripW, 11, drag: (ax, _, _) => this.lo = LoAt(ax));
            this.stripTip = null;
            if (hover && this.pop == Pop.None)
            {
                int at = this.dragging == null ? LoAt(this.mouseX) : this.lo;
                this.stripTip = this.T("ui.range.tip", new { from = SoundNames.Note(at), to = SoundNames.Note(Math.Min(at + 23, TopPitch)) });
            }
            int wx = sx + StripX(this.lo) - 1;
            this.cv.Spr($"bracket:{acc.Hex}", () =>
            {
                var br = new PixImage(2 * StripCell + 1, 10);
                Px.RRLayers(br, 0, 0, 2 * StripCell + 1, 10, 1, new[] { (0, acc.O) });
                Px.RRLayers(br, 1, 1, 2 * StripCell - 1, 8, 0, new[] { (0, Px.Clear) });
                return br;
            }, wx, y);
            foreach (int m in this.settings.Notes.Select(n => StripX(n.Pitch)).Distinct())
                this.cv.Rect(sx + m, y - 2, 1, 2, acc.O);
        }

        private void DrawTempoChip(int x, int y, Ramp acc)
        {
            this.tempoChipX = x;
            bool follows = this.settings.Tempo <= 0;
            int bpm = this.settings.EffectiveTempo(this.mod.World.DefaultTempo);
            string label = bpm.ToString();
            int w = Math.Max(22, PixCanvas.Measure(label) / Z + 11);
            bool hover = this.Region(x, y, w, 11, () => this.TogglePop(Pop.Tempo));
            Color edge = follows ? Px.C("gold2") : acc.O;
            Color fill = follows ? Px.C("gold3") : acc.H;
            if (hover)
                fill = follows ? Px.C("gold") : acc.L;
            this.cv.Spr($"box:{w}:11:3:{edge.PackedValue}:{fill.PackedValue}", () => Px.Box(w, 11, 3, edge, fill), x, y);
            Color col = follows ? Px.C("goldD") : acc.O;
            this.cv.Icon("sn4", col, x + 3, y + 2);
            this.cv.Text(label, x + 8, y, col, align: Align.Center, w: w - 10, dy: 6);
        }

        /****
        ** Waterfall
        ****/
        private void DrawWaterfall(Ramp acc, double playTick)
        {
            var cols = this.Columns();
            int x = KbX + 1, w = 14 * KeyW - 1, h = FallH;
            int bottom = playTick >= 0 ? (int)playTick : this.scrollTicks;
            var used = this.settings.Notes.Select(n => n.Pitch).ToHashSet();

            // background: lanes, beat lines, frame (cached per view)
            int phase = bottom % Ppq;
            string key = $"fall:{this.lo}:{phase / 48}:{string.Join(",", used.Where(cols.ContainsKey).OrderBy(m => m))}";
            this.cv.Spr(key, () => this.BuildWaterfallBg(cols, used, phase), x - 1, FallY - 1);

            // ruler labels every quarter note
            for (int t = (bottom / Ppq + 1) * Ppq; t < bottom + this.FallTicks; t += Ppq)
            {
                int yy = FallY + h - 1 - (t - bottom) * PxPerQ / Ppq;
                string lab = t.ToString(CultureInfo.InvariantCulture);
                this.cv.TextPx(lab, this.cv.OriginX + (x + w - 2 - lab.Length * 3) * Z, this.cv.OriginY + yy * Z + 2, Ink3, PixCanvas.Tiny);
            }
            this.cv.Text("tick", x + w - 10, FallY - 1, Ink3, dy: -2);

            // empty space: click to add a note there, wheel to scroll through time
            this.Region(x, FallY, w, h, click: () =>
            {
                int? pitch = this.PitchAt(this.mouseX);
                if (pitch is int p)
                    this.AddNote(p, this.SnapTick(Math.Max(0, this.TickAtY(this.mouseY)), -1), this.Sel?.Duration ?? Ppq);
            }, scroll: d => this.scrollTicks = Math.Clamp(this.scrollTicks + d * Ppq / 2, 0, BlockSettings.MaxTicks));

            // candies
            for (int i = 0; i < this.settings.Notes.Count; i++)
            {
                BlockNote n = this.settings.Notes[i];
                if (!cols.TryGetValue(n.Pitch, out var c))
                    continue;
                int top = FallY + h - 1 - (int)Math.Round((n.End - bottom) / (double)Ppq * PxPerQ);
                int bot = FallY + h - 1 - (int)Math.Round((n.Delay - bottom) / (double)Ppq * PxPerQ);
                int clipTop = Math.Max(top, FallY), clipBot = Math.Min(bot, FallY + h);
                if (clipBot - clipTop < 1 && !(top < FallY + h && bot > FallY))
                    continue;
                int hgt = Math.Max(3, clipBot - clipTop - 1);
                bool sel = i == this.selected;
                int cw = c.w - (c.black ? 0 : 2);
                int cx = c.x + (c.black ? 0 : 1);
                int index = i;
                this.Region(cx, clipTop, cw, hgt,
                    click: () => this.PressCandy(index),
                    right: () => this.DeleteNote(index),
                    drag: (ax, ay, first) => this.DragCandy(index, ax, ay, first),
                    release: this.Commit);
                this.cv.Spr($"candy:{cw}:{hgt}:{sel}:{acc.Hex}", () => Px.Candy(cw, hgt, acc, sel), cx - (sel ? 1 : 0), clipTop - (sel ? 1 : 0));
            }

            // notes outside the two visible octaves
            int left = this.settings.Notes.Count(n => n.Pitch < this.lo);
            int right = this.settings.Notes.Count(n => n.Pitch >= this.lo + 24);
            if (left > 0)
                this.OffscreenBadge(KbX + 2, left, "left", acc, () => this.lo = ClampLo(this.settings.Notes.Where(n => n.Pitch < this.lo).Max(n => n.Pitch) / 12 * 12 - 12));
            if (right > 0)
                this.OffscreenBadge(KbX + 14 * KeyW - 28, right, "right", acc, () => this.lo = ClampLo(this.settings.Notes.Where(n => n.Pitch >= this.lo + 24).Min(n => n.Pitch) / 12 * 12));
            if (playTick < 0 && this.settings.Notes.Any(n => n.End > bottom + this.FallTicks))
                this.cv.Icon("up", acc.O, x + w / 2 - 2, FallY + 1);
            if (playTick < 0 && this.scrollTicks > 0)
                this.cv.Icon("down", acc.O, x + w / 2 - 2, FallY + h - 5);
        }

        private PixImage BuildWaterfallBg(Dictionary<int, (int x, int w, bool black)> cols, HashSet<int> used, int phase)
        {
            int x = KbX + 1, w = 14 * KeyW - 1, h = FallH;
            var bg = new PixImage(w, h, Px.C("white"));
            foreach (var (m, c) in cols.OrderBy(kv => kv.Value.x))
            {
                if (!IsPitch(m))
                {
                    // no such key: hatched, nothing can be written there
                    for (int yy = 0; yy < h; yy++)
                        for (int xx = c.x - x - (c.black ? 0 : 1); xx < c.x - x + c.w; xx++)
                            bg.Put(xx, yy, (xx + yy) % 4 == 0 ? Px.C("milk3") : Px.C("milk2"));
                    continue;
                }
                Color? tint = c.black ? Ramp.Hex2("#f6ecf2") : null;
                if (used.Contains(m))
                    tint = c.black ? Ramp.Hex2("#f7e3ec") : Ramp.Hex2("#fff0f5");
                if (tint is Color t)
                    for (int yy = 0; yy < h; yy++)
                        for (int xx = c.x - x; xx < c.x - x + c.w; xx++)
                            bg.Put(xx, yy, t);
                if (!c.black)
                    for (int yy = 0; yy < h; yy += 2)
                        bg.Put(c.x - x - 1, yy, Px.C("milk2"));
            }
            // beat lines: solid every quarter, dotted every eighth, shifted by the scroll phase
            int half = PxPerQ / 2;
            int offset = (int)Math.Round(phase / (double)Ppq * PxPerQ);
            for (int k = 0; k <= h / half + 2; k++)
            {
                int yy = h - 1 - (k * half - offset);
                if (yy < 0 || yy >= h)
                    continue;
                bool solid = k % 2 == 0;
                for (int xx = 0; xx < w; xx += solid ? 1 : 2)
                    bg.Put(xx, yy, solid ? Px.C("milk3") : Px.C("ging2"));
            }
            var frame = new PixImage(w + 2, h + 2);
            Px.RRLayers(frame, 0, 0, w + 2, h + 2, 2, new[] { (0, Px.C("milk4")) });
            frame.Blit(bg, 1, 1);
            return frame;
        }

        private void OffscreenBadge(int x, int count, string dir, Ramp acc, Action jump)
        {
            this.Region(x, FallY + 2, 10, 9, jump);
            this.cv.Spr($"offbadge:{acc.Hex}", () => Px.Box(10, 9, 3, acc.O, acc.H), x, FallY + 2);
            this.cv.Icon(dir, acc.O, x + 2, FallY + 3);
            this.cv.TextPx(count.ToString(), this.cv.OriginX + (x + 6) * Z, this.cv.OriginY + (FallY + 2) * Z - 2, acc.O, PixCanvas.Tiny);
        }

        /****
        ** Keyboard
        ****/
        private void DrawKeyboard(Ramp acc, double playTick)
        {
            var cols = this.Columns();
            var has = this.settings.Notes.Select(n => n.Pitch).ToHashSet();
            var sounding = playTick >= 0
                ? this.settings.Notes.Where(n => n.Delay <= playTick && playTick < n.End).Select(n => n.Pitch).ToHashSet()
                : new HashSet<int>();
            int? selPitch = this.Sel?.Pitch;
            int? hoverPitch = null;

            for (int oc = 0; oc < 2; oc++)
            {
                for (int i = 0; i < 7; i++)
                {
                    int m = this.lo + oc * 12 + WhiteSemis[i];
                    int x = KbX + (oc * 7 + i) * KeyW;
                    if (!IsPitch(m))
                    {
                        this.cv.Spr("wkey:missing", () => Px.WhiteKey(KeyW + 1, KeysH, "missing"), x, KeysY);
                        continue;
                    }
                    bool hover = this.Region(x, KeysY, KeyW, KeysH, () => this.LeftKey(m), () => this.RightKey(m));
                    if (hover)
                        hoverPitch = m;
                    string st = sounding.Contains(m) ? "down" : m == selPitch ? "selected" : (has.Contains(m) || hover) ? "hover" : "normal";
                    this.cv.Spr($"wkey:{st}:{acc.Hex}", () => Px.WhiteKey(KeyW + 1, KeysH, st, acc), x, KeysY);
                    if (WhiteSemis[i] == 0 && m != selPitch)
                        this.cv.Text(SoundNames.Note(m), x, KeysY + KeysH - 12, Ink3, align: Align.Center, w: KeyW + 1);
                }
            }
            for (int oc = 0; oc < 2; oc++)
            {
                foreach (int semi in BlackSemis)
                {
                    int m = this.lo + oc * 12 + semi;
                    if (!IsPitch(m))
                        continue;
                    int bx = cols[m - 1].x + KeyW - 4;
                    bool hover = this.Region(bx, KeysY, 7, BlackH, () => this.LeftKey(m), () => this.RightKey(m));
                    if (hover)
                        hoverPitch = m;
                    string st = sounding.Contains(m) || m == selPitch ? "selected" : hover ? "hover" : "normal";
                    this.cv.Spr($"bkey:{st}:{acc.Hex}", () => Px.BlackKey(7, BlackH, st, acc), bx, KeysY);
                }
            }
            if (selPitch is int sp && cols.TryGetValue(sp, out var sc))
                this.cv.Spr("heartbadge", Px.HeartBadge, sc.x + (sc.black ? 0 : 1), KeysY + (sc.black ? BlackH - 8 : KeysH - 9));

            // drums: name the drum under the mouse
            if (this.drumView && hoverPitch is int hp && this.pop == Pop.None && this.dragging == null)
            {
                string tip = SoundNames.Note(hp) + (this.names.Drum(hp) is string d ? " · " + d : "");
                int w = PixCanvas.Measure(tip) / Z + 10;
                int tx = Math.Clamp(cols[hp].x + cols[hp].w / 2 - w / 2, OX + 2, OX + 148 - w);
                this.cv.Spr($"bubble:{w}:12", () => Px.Bubble(w, 12), tx, KeysY - 14);
                this.cv.Text(tip, tx, KeysY - 14, Ink, align: Align.Center, w: w, dy: 2);
            }
        }

        /****
        ** Inspector
        ****/
        private void DrawInspector(Ramp acc)
        {
            int x = OX + 5, y1 = OY + 90, y2 = OY + 102;
            if (this.Sel is not BlockNote n)
            {
                this.cv.Text(this.T(this.settings.Notes.Count == 0 ? "ui.notes.empty" : "ui.notes.none"), x, y1 + 4, Ink3, dy: 6);
                return;
            }
            string chipText = SoundNames.Note(n.Pitch);
            if (this.drumView && this.names.Drum(n.Pitch) is string drum)
                chipText += " " + drum;
            int chipW = Math.Clamp(PixCanvas.Measure(chipText) / Z + 6, 20, 46);
            this.cv.Spr($"chip:{chipW}:{acc.Hex}", () => Px.Box(chipW, 11, 3, acc.O, acc.L), x, y1);
            this.cv.Text(chipText, x, y1, White, align: Align.Center, w: chipW, dy: 6, shadow: (0, 2, acc.O));

            int bpm = this.settings.EffectiveTempo(this.mod.World.DefaultTempo);
            int labelW = Math.Max(12, Math.Max(PixCanvas.Measure(this.T("ui.delay")), PixCanvas.Measure(this.T("ui.duration"))) / Z + 2);
            int fx = x + chipW + 3;
            this.TickField("delay", fx, y1, labelW, this.T("ui.delay"), n.Delay, 0, bpm, v => this.EditSelected(s => s.Delay = v));
            this.TickField("duration", fx, y2, labelW, this.T("ui.duration"), n.Duration, 1, bpm, v => this.EditSelected(s => s.Duration = v));
            this.MorePill(OX + 113, y2, this.pop == Pop.Velocity, () => this.TogglePop(Pop.Velocity));
            this.SmallButton(OX + 135, y2, "trash", 11, 11, () => this.DeleteNote(this.selected));
        }

        /// <summary>[− ticks +] followed by a friendly translation: the note value (when exact) and seconds.</summary>
        private void TickField(string id, int x, int y, int labelW, string label, int ticks, int min, int bpm, Action<int> set)
        {
            const int w = 36;
            this.cv.Text(label, x, y, Ink2, dy: 6);
            int fx = x + labelW;
            bool editing = this.entry?.Id == id;
            if (editing)
            {
                this.ValueBox(id, fx, y, w, ticks.ToString(), ticks, min, BlockSettings.MaxTicks, set);
            }
            else
            {
                this.cv.Spr("tickbox", () => Px.Box(w, 11, 3, Px.C("milk4"), White), fx, y);
                this.StepButton(fx + 1, y + 1, "minus", () => set(Math.Max(min, ticks - (Shift ? 1 : SnapTicks))));
                this.StepButton(fx + w - 10, y + 1, "plus", () => set(Math.Min(BlockSettings.MaxTicks, ticks + (Shift ? 1 : SnapTicks))));
                bool hover = this.Region(fx + 10, y, w - 20, 11, () => this.BeginEntry(id, ticks, min, BlockSettings.MaxTicks, set));
                this.cv.Text(ticks.ToString(), fx + 10, y, hover ? Px.C("rose2") : Ink, align: Align.Center, w: w - 20, dy: 6);
            }

            int hx = fx + w + 3;
            if (NoteGlyph(ticks) is (string glyph, bool dotted, bool triplet))
            {
                (int gw, _) = Px.IconSize(glyph);
                this.cv.Icon(glyph, Ink3, hx, y + 2);
                hx += gw + 1;
                if (dotted)
                {
                    this.cv.Icon("dot", Ink3, hx, y + 6);
                    hx += 3;
                }
                if (triplet)
                {
                    this.cv.Icon("three", Ink3, hx, y + 1);
                    hx += Px.IconSize("three").w + 1;
                }
                hx += 2;
            }
            double secs = ticks / (double)Ppq * 60 / bpm;
            string s = secs.ToString("0.##", CultureInfo.InvariantCulture);
            this.cv.Text(this.T("ui.seconds", new { s }), hx, y, Ink3, dy: 6);
        }

        private static (string glyph, bool dotted, bool triplet)? NoteGlyph(int ticks) => ticks switch
        {
            1920 => ("sn1", false, false),
            960 => ("sn2", false, false),
            480 => ("sn4", false, false),
            240 => ("sn8", false, false),
            120 => ("sn16", false, false),
            2880 => ("sn1", true, false),
            1440 => ("sn2", true, false),
            720 => ("sn4", true, false),
            360 => ("sn8", true, false),
            180 => ("sn16", true, false),
            640 => ("sn2", false, true),
            320 => ("sn4", false, true),
            160 => ("sn8", false, true),
            80 => ("sn16", false, true),
            _ => null
        };

        /****
        ** Popovers
        ****/
        private void DrawTempoPopover()
        {
            Ramp acc = this.Acc;
            // not set, it's the save's tempo (the globe); − + or typing give the block its own, the round arrow takes it away
            bool own = this.settings.Tempo > 0;
            int world = this.mod.World.DefaultTempo;
            int bpm = this.settings.EffectiveTempo(world);
            string follow = this.T("ui.tempo.follow", new { tempo = world }), back = this.T("ui.tempo.back", new { tempo = world });
            // as wide as the longer hint (so it doesn't jump when − + or the arrow switch them), growing to the left from
            // the chip; the − + on its right
            int hints = Math.Max(PixCanvas.Measure(follow), PixCanvas.Measure(back)) / Z;
            int tail = this.tempoChipX + 8;
            int bw = Math.Max(86, hints + 10), bx = Math.Clamp(tail - (bw - 28), OX + 4, OX + 146 - bw), by = OY + 16, bh = 34;
            int cx = bx + bw - 36;
            this.Bubble(bx, by, bw, bh, tail, tailUp: true);
            this.cv.Text(this.T("ui.tempo.title"), bx + 5, by + 3, Ink2);
            void Set(int v)
            {
                this.settings.Tempo = v <= 0 ? 0 : Math.Clamp(v, BlockSettings.MinTempo, BlockSettings.MaxTempo);
                this.Commit();
            }
            if (own)
            {
                bool hover = this.Region(bx + 5, by + 11, 9, 10, () => Set(0));
                this.cv.Icon("reset", hover ? acc.O : Ink3, bx + 6, by + 13);
            }
            else
            {
                this.cv.Icon("globe", Px.C("gold2"), bx + 5, by + 13);
            }
            this.StepButton(cx, by + 12, "minus", () => Set(bpm - (Shift ? 10 : 1)));
            this.cv.Icon("sn4", own ? Ink : Ink3, cx + 10, by + 13);
            this.ValueBox("tempo", cx + 14, by + 11, 12, bpm.ToString(), bpm, BlockSettings.MinTempo, BlockSettings.MaxTempo, Set);
            this.StepButton(cx + 26, by + 12, "plus", () => Set(bpm + (Shift ? 10 : 1)));
            this.cv.Text(this.T(own ? "ui.tempo.own" : "ui.tempo.global"), bx + 17, by + 10, own ? Ink : Ink3, dy: 4);
            this.cv.Text(own ? back : follow, bx + 5, by + 23, Ink3);
        }

        private void DrawVelocityPopover()
        {
            if (this.Sel is not BlockNote n)
            {
                this.pop = Pop.None;
                return;
            }
            Ramp acc = this.Acc;
            int bx = OX + 50, by = OY + 72, bw = 96, bh = 28;
            this.Bubble(bx, by, bw, bh, OX + 119, tailUp: false);
            this.cv.Text(this.T("ui.velocity"), bx + 5, by + 3, Ink2, dy: 4);
            int vx = bx + 5 + Math.Max(16, PixCanvas.Measure(this.T("ui.velocity")) / Z + 3);
            this.KnobSlider(vx, by + 6, bx + bw - 34 - vx, n.Velocity, 127, v => this.EditSelected(s => s.Velocity = Math.Max(1, v), commit: false), acc,
                released: () => this.PlayNote(this.Sel?.Pitch ?? 60, this.Sel?.Velocity ?? 100));
            this.ValueBox("velocity", bx + bw - 29, by + 4, 24, n.Velocity.ToString(), n.Velocity, 1, 127, v => this.EditSelected(s => s.Velocity = v));
            this.cv.Text(this.T("ui.velocity.hint"), bx + 5, by + 16, Ink3, dy: 2);
        }

        private void DrawHelpPopover()
        {
            bool simple = this.mod.Config.SimpleNotes;
            string prefix = simple ? "ui.simple.help" : "ui.help";
            int bx = OX + 4, by = OY + 16, bw = 142, lines = 7, bh = 8 + lines * 9;
            this.Bubble(bx, by, bw, bh, OX + 139, tailUp: true);
            for (int i = 0; i < lines; i++)
                this.cv.Text(PixCanvas.Fit(this.T($"{prefix}.{i + 1}"), (bw - 10) * Z), bx + 6, by + 3 + i * 9, i % 2 == 0 ? Ink : Ink2);
        }

        /****
        ** Editing
        ****/
        /// <summary>Change the selected note (<c>s => s.Delay = v</c>), keeping every field in MIDI range.</summary>
        private void EditSelected(Action<BlockNoteBox> edit, bool commit = true)
        {
            if (this.Sel is not BlockNote n)
                return;
            var box = new BlockNoteBox(n);
            edit(box);
            n = box.Note;
            n.Delay = Math.Clamp(n.Delay, 0, BlockSettings.MaxTicks);
            n.Duration = Math.Clamp(n.Duration, 1, BlockSettings.MaxTicks);
            n.Pitch = Math.Clamp(n.Pitch, 0, 127);
            n.Velocity = Math.Clamp(n.Velocity, 1, 127);
            this.settings.Notes[this.selected] = n;
            if (commit)
                this.Commit();
        }

        /// <summary>Lets lambdas assign note fields (<c>s => s.Delay = v</c>).</summary>
        private sealed class BlockNoteBox
        {
            public BlockNote Note;
            public BlockNoteBox(BlockNote n) => this.Note = n;
            public int Pitch { get => this.Note.Pitch; set => this.Note.Pitch = value; }
            public int Delay { get => this.Note.Delay; set => this.Note.Delay = value; }
            public int Duration { get => this.Note.Duration; set => this.Note.Duration = value; }
            public int Velocity { get => this.Note.Velocity; set => this.Note.Velocity = value; }
        }

        private void AddNote(int pitch, int delay, int duration, bool select = true)
        {
            if (this.settings.Notes.Count >= BlockSettings.MaxNotes)
            {
                this.Toast(this.T("ui.notes.full", new { max = BlockSettings.MaxNotes }));
                return;
            }
            int existing = this.settings.Notes.FindIndex(n => n.Pitch == pitch && n.Delay == delay);
            if (existing >= 0)
            {
                if (select)
                    this.selected = existing;
                this.PlayNote(pitch);
                return;
            }
            this.settings.Notes.Add(new BlockNote(pitch, delay, Math.Max(1, duration), this.Sel?.Velocity ?? 100));
            if (select)
                this.selected = this.settings.Notes.Count - 1;
            this.Commit();
            this.PlayNote(pitch);
        }

        private void DeleteNote(int index)
        {
            if (index < 0 || index >= this.settings.Notes.Count)
                return;
            this.settings.Notes.RemoveAt(index);
            if (this.selected == index)
                this.selected = Math.Min(index, this.settings.Notes.Count - 1);
            else if (this.selected > index)
                this.selected--;
            this.RevealSelected();
            this.Commit();
            Game1.playSound("trashcan");
        }

        private void LeftKey(int pitch)
        {
            if (this.Sel is BlockNote n)
            {
                int clash = this.settings.Notes.FindIndex(o => o.Pitch == pitch && o.Delay == n.Delay);
                if (clash >= 0 && clash != this.selected)
                    this.selected = clash;
                else
                    this.EditSelected(s => s.Pitch = pitch);
                this.PlayNote(pitch, n.Velocity);
            }
            else
            {
                this.AddNote(pitch, 0, Ppq);
            }
        }

        private void RightKey(int pitch)
        {
            BlockNote? anchor = this.Sel;
            int delay = anchor?.Delay ?? 0;
            int existing = this.settings.Notes.FindIndex(n => n.Pitch == pitch && n.Delay == delay);
            if (existing >= 0)
            {
                this.DeleteNote(existing);
                return;
            }
            int keep = this.selected;
            this.AddNote(pitch, delay, anchor?.Duration ?? Ppq, select: keep < 0);
        }

        private void PressCandy(int index)
        {
            this.selected = index;
            this.RevealSelected();
            BlockNote n = this.settings.Notes[index];
            this.PlayNote(n.Pitch, n.Velocity);
        }

        private void DragCandy(int index, float ax, float ay, bool first)
        {
            if (index >= this.settings.Notes.Count)
                return;
            if (first)
            {
                this.dragOrigin = this.settings.Notes[index];
                this.dragStartX = ax;
                this.dragStartY = ay;
                // grabbing the top edge (the end of the note) stretches it instead of moving it
                int top = FallY + FallH - 1 - (int)Math.Round((this.dragOrigin.End - this.scrollTicks) / (double)Ppq * PxPerQ);
                this.dragResize = ay < top + 2 && this.dragOrigin.Duration * PxPerQ / Ppq >= 3;
                return;
            }
            int dt = (int)Math.Round((this.dragStartY - ay) * Ppq / PxPerQ);
            BlockNote n = this.dragOrigin;
            if (this.dragResize)
            {
                int end = this.SnapTick(this.dragOrigin.End + dt, index);
                n.Duration = Math.Clamp(end - n.Delay, 1, BlockSettings.MaxTicks);
            }
            else
            {
                n.Delay = Math.Clamp(this.SnapTick(this.dragOrigin.Delay + dt, index), 0, BlockSettings.MaxTicks);
                if (Math.Abs(ax - this.dragStartX) >= 1 && this.PitchAt(ax) is int p)
                    n.Pitch = p;
                if (n.Pitch != this.settings.Notes[index].Pitch)
                    this.PlayNote(n.Pitch, n.Velocity);
            }
            this.settings.Notes[index] = n;
        }

        /// <summary>Magnet: snap to other notes' edges or the 120-tick grid, whichever is closer. Shift = exact.</summary>
        private int SnapTick(int tick, int ignoreIndex)
        {
            tick = Math.Max(0, tick);
            if (Shift)
                return tick;
            int best = (int)Math.Round(tick / (double)SnapTicks) * SnapTicks;
            int bestDist = Math.Abs(best - tick);
            for (int i = 0; i < this.settings.Notes.Count; i++)
            {
                if (i == ignoreIndex)
                    continue;
                BlockNote o = this.settings.Notes[i];
                foreach (int edge in new[] { o.Delay, o.End })
                {
                    int d = Math.Abs(edge - tick);
                    if (d < bestDist)
                    {
                        best = edge;
                        bestDist = d;
                    }
                }
            }
            return best;
        }

        /// <summary>Arrow keys move the selected note; Delete removes it.</summary>
        private bool NotesKey(Keys key)
        {
            if (this.Sel is not BlockNote n)
                return false;
            int delta = key switch
            {
                Keys.Left => -1,
                Keys.Right => 1,
                Keys.Down => -12,
                Keys.Up => 12,
                _ => 0
            };
            if (delta != 0)
            {
                int p = n.Pitch + delta;
                if (!IsPitch(p))
                {
                    Game1.playSound("cancel");
                    return true;
                }
                this.EditSelected(s => s.Pitch = p);
                if (p < this.lo || p >= this.lo + 24)
                    this.lo = ClampLo(p / 12 * 12);
                this.PlayNote(p, n.Velocity);
                return true;
            }
            if (key == Keys.Delete || key == Keys.Back)
            {
                if (this.mod.Config.SimpleNotes && this.settings.Notes.Count <= 1)
                    this.Toast(this.T("ui.simple.last"));
                else
                    this.DeleteNote(this.selected);
                return true;
            }
            return false;
        }
    }
}
