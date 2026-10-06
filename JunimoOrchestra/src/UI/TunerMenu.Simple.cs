using System;
using System.Linq;
using JunimoOrchestra.Data;
using JunimoOrchestra.UI.Pix;
using Microsoft.Xna.Framework;

namespace JunimoOrchestra.UI
{
    /// <summary>The simplified 音符 tab (Global > 我的偏好, on by default): the block's notes as chips, and rows that set the
    /// picked one: octave -1 to 9 and name C to B (so all 128 pitches), then its length and delay as the score draws them
    /// (whole to sixteenth, and a dot). The same view for every instrument; velocity and other lengths are in the full roll,
    /// and a note made there keeps them here.</summary>
    internal sealed partial class TunerMenu
    {
        private static readonly string[] PitchNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        /// <summary>The sharps above the naturals: semitone, and the natural (0-6) they sit after.</summary>
        private static readonly (int Semi, int After)[] SharpSlots = { (1, 0), (3, 1), (6, 3), (8, 4), (10, 5) };

        /// <summary>The note values of 时值 and 延迟: the glyph, the ticks, and 1/2/4/8/16 for the names.</summary>
        private static readonly (string Glyph, int Ticks, int Den)[] NoteValues =
            { ("n1", Ppq * 4, 1), ("n2", Ppq * 2, 2), ("n4", Ppq, 4), ("n8", Ppq / 2, 8), ("n16", Ppq / 4, 16) };

        private int simpleOctave = 4;   // the octave a note picked in an empty block goes to
        private int chipStart;          // the first chip shown (in sounding order) when they don't all fit
        private (string Text, int X, int Y, bool Below)? simpleTip;   // the hover bubble, drawn over everything

        private void DrawSimpleNotes()
        {
            Ramp acc = this.Acc;
            if (this.selected < 0 && this.settings.Notes.Count > 0)
                this.selected = 0;
            this.simpleTip = null;

            // toolbar: tempo, the way to the full roll (it's in Global: gold, like every other Global thing), help
            this.DrawTempoChip(OX + 6, OY + 3, acc);
            Ramp gold = Ramp.Of("#f2b640");
            string full = this.T("ui.simple.full");
            int fw = Math.Max(32, PixCanvas.Measure(full) / Z + 17);
            int fx = OX + 134 - fw;
            bool fullHover = this.Region(fx, OY + 3, fw, 11, () => this.SwitchTab(TunerTab.Global));
            string fst = fullHover ? "selected" : "normal";
            this.cv.Spr($"pill:{fw}:11:{fst}:{gold.Hex}", () => Px.Pill(fw, 11, fst, gold), fx, OY + 3);
            this.cv.Icon("globe", Px.C("goldD"), fx + 3, OY + 5, Px.C("gold3"));
            this.cv.Text(full, fx + 13, OY + 3, Px.C("goldD"), dy: 6);
            if (fullHover)
                this.simpleTip = (this.T("ui.simple.full.tip"), fx + fw / 2, OY + 14, true);
            bool helpHover = this.Region(OX + 137, OY + 3, 10, 10, () => this.TogglePop(Pop.Help));
            this.cv.Spr($"help:{helpHover}", () => Px.Box(10, 10, 4, Px.C("rose2"), helpHover ? Px.C("pink") : Px.C("blush")), OX + 137, OY + 3);
            this.cv.Text("?", OX + 137, OY + 3, Px.C("berry"), align: Align.Center, w: 10, dy: 3);

            this.DrawNoteChips(acc);
            this.DrawRadioGroups(acc);
            BlockNote? sel = this.Sel;
            this.DrawValueRow(this.T("ui.duration"), OY + 84, sel?.Duration, false, acc, this.SetSimpleLength,
                v => this.T("ui.simple.length", new { name = this.T($"ui.simple.v{v.Den}"), beats = this.T($"ui.simple.beats.{v.Den}") }));
            this.DrawValueRow(this.T("ui.delay"), OY + 98, sel?.Delay, true, acc, this.SetSimpleDelay,
                v => this.T("ui.simple.later", new { name = this.T($"ui.simple.v{v.Den}"), beats = this.T($"ui.simple.beats.{v.Den}") }));

            this.DrawSimpleTip();
        }

        /// <summary>"这个方块 [C4] [E4] › [G4] (+) … (bin)": the notes in the order they sound, an arrow where the moment
        /// changes. Click a chip to pick it (and hear it), + adds a note at the picked one's moment, the bin takes the picked
        /// one away. Chips that don't fit scroll, keeping the picked one in view.</summary>
        private void DrawNoteChips(Ramp acc)
        {
            int y = OY + 17;
            int x0 = OX + 6 + this.cv.Text(this.T("ui.simple.block"), OX + 6, y, Ink2, dy: 6) / Z + 4;
            const int limit = OX + 120;   // + sits after the chips, the bin at the far right
            const int arrow = 6;          // the "later" arrow between moments, with its gap
            var notes = this.settings.Notes;

            if (notes.Count == 0)
            {
                this.cv.Text(this.T("ui.simple.empty"), x0, y, Ink3, dy: 6);
                this.SmallButton(OX + 121, y, "plus", 11, 11, this.AddSimpleNote);
                return;
            }

            int[] order = Enumerable.Range(0, notes.Count).OrderBy(i => notes[i].Delay).ThenBy(i => notes[i].Pitch).ToArray();
            int[] widths = order.Select(i => PixCanvas.Measure(SoundNames.Note(notes[i].Pitch)) / Z + 7).ToArray();
            bool[] later = order.Select((i, k) => k > 0 && notes[i].Delay != notes[order[k - 1]].Delay).ToArray();
            int pos = Math.Max(0, Array.IndexOf(order, this.selected));
            int Span(int from, int to) => Enumerable.Range(from, to - from + 1).Sum(k => widths[k] + (k > from ? 2 + (later[k] ? arrow : 0) : 0));
            void Pick(int k) => this.PickChip(order[Math.Clamp(k, 0, order.Length - 1)]);

            bool overflow = x0 + Span(0, order.Length - 1) > limit;
            int left = overflow ? x0 + 11 : x0, right = overflow ? limit - 11 : limit;
            if (!overflow)
                this.chipStart = 0;
            else
            {
                // keep the picked chip in view, moving the window as little as possible
                this.chipStart = Math.Clamp(this.chipStart, 0, order.Length - 1);
                if (pos < this.chipStart)
                    this.chipStart = pos;
                while (this.chipStart < pos && left + Span(this.chipStart, pos) > right)
                    this.chipStart++;
            }

            this.Region(x0, y, limit - x0, 11, scroll: d => Pick(pos - d));
            int x = left, last = this.chipStart - 1;
            for (int k = this.chipStart; k < order.Length; k++)
            {
                int gap = k > this.chipStart && later[k] ? arrow : 0;
                if (x + gap + widths[k] > right)
                    break;
                if (gap > 0)
                {
                    this.cv.Icon("right", Ink3, x, y + 2);
                    x += gap;
                }
                int index = order[k], w = widths[k];
                bool on = index == this.selected;
                bool hover = this.Region(x, y, w, 11, () => this.PickChip(index));
                string st = on ? "selected" : hover ? "hover" : "normal";
                this.cv.Spr($"pill:{w}:11:{st}:{acc.Hex}", () => Px.Pill(w, 11, st, acc), x, y);
                this.cv.Text(SoundNames.Note(notes[index].Pitch), x, y, on ? White : Ink, align: Align.Center, w: w, dy: 6,
                    shadow: on ? (0, 2, acc.O) : null);
                x += w + 2;
                last = k;
            }
            if (overflow)
            {
                if (this.chipStart > 0)
                    this.StepButton(x0, y + 1, "left", () => Pick(pos - 1));
                if (last < order.Length - 1)
                    this.StepButton(right + 2, y + 1, "right", () => Pick(pos + 1));
            }
            this.SmallButton(overflow ? limit + 1 : Math.Min(x + 1, limit + 1), y, "plus", 11, 11, this.AddSimpleNote);

            // the last note stays: a block always plays something here
            bool canDelete = notes.Count > 1;
            this.SmallButton(OX + 134, y, "trash", 11, 11, () =>
            {
                if (notes.Count > 1)
                    this.DeleteNote(this.selected);
                else
                    this.Toast(this.T("ui.simple.last"));
            });
            if (!canDelete)
                this.cv.Rect(OX + 134, y, 11, 11, Px.C("milk") * 0.6f);
        }

        /// <summary>八度 -1 … 9 and the names C … B under it (the sharps above and between the naturals, like black
        /// keys). Picking either moves the picked note there and plays it; a pitch past G9 can't be picked.</summary>
        private void DrawRadioGroups(Ramp acc)
        {
            BlockNote? sel = this.Sel;
            int octave = sel is BlockNote n ? n.Pitch / 12 - 1 : this.simpleOctave;
            int? semi = sel is BlockNote s ? s.Pitch % 12 : null;

            // 八度: low … 中 … high, the 中 over 4 (middle C's octave)
            string octaveLabel = this.T("ui.simple.octave");
            this.cv.Text(octaveLabel, OX + 6, OY + 30, Ink2, dy: 2);
            this.cv.Text(this.T("ui.simple.low"), OX + 6 + PixCanvas.Measure(octaveLabel) / Z + 6, OY + 30, Ink3, dy: 2);
            this.cv.Text(this.T("ui.simple.middle"), OX + 5 + 5 * 13, OY + 30, Ink3, align: Align.Center, w: 12, dy: 2);
            this.cv.Text(this.T("ui.simple.high"), OX + 100, OY + 30, Ink3, align: Align.Right, w: 47, dy: 2);
            for (int i = 0; i < 11; i++)
            {
                int o = i - 1;
                bool possible = semi is not int sm || IsPitch((o + 1) * 12 + sm);
                this.RadioPill(OX + 5 + i * 13, OY + 38, 12, 12, o.ToString(), o == octave, possible, acc, () =>
                {
                    if (semi is int sm2)
                        this.SetSimplePitch((o + 1) * 12 + sm2);
                    else
                        this.simpleOctave = o;
                });
            }

            // the names: C D E say what they are, no label
            void Name(int semitone, int x, int y, int w, int h)
            {
                int pitch = (octave + 1) * 12 + semitone;
                this.RadioPill(x, y, w, h, PitchNames[semitone], semitone == semi, IsPitch(pitch), acc, () =>
                {
                    if (this.Sel is null)
                        this.AddNote(pitch, 0, Ppq);
                    else
                        this.SetSimplePitch(pitch);
                });
            }
            foreach (var (sharp, after) in SharpSlots)
                Name(sharp, OX + 5 + (after + 1) * 20 - 8, OY + 53, 16, 12);
            for (int i = 0; i < 7; i++)
                Name(WhiteSemis[i], OX + 5 + i * 20, OY + 66, 19, 14);
        }

        /// <summary>时值 or 延迟: [0] (延迟 only) [whole] [half] [quarter] [eighth] [sixteenth] [dot], in the same columns
        /// for both rows. A length that isn't one of these (made in the full roll) shows none picked and the dot off.</summary>
        private void DrawValueRow(string label, int y, int? ticks, bool zero, Ramp acc, Action<int> set, Func<(string Glyph, int Ticks, int Den), string> tip)
        {
            const int h = 12;
            this.cv.Text(label, OX + 6, y, Ink2, dy: (h - 7) * 2 - 2);
            bool has = ticks.HasValue;
            int t = ticks ?? -1;
            bool dotted = t > 0 && t % 3 == 0 && NoteValues.Any(v => v.Ticks == t / 3 * 2);
            int plain = dotted ? t / 3 * 2 : t;
            bool known = NoteValues.Any(v => v.Ticks == plain);

            if (zero && this.RadioPill(OX + 28, y, 13, h, "0", t == 0, has, acc, () => set(0)))
                this.simpleTip = (this.T("ui.simple.now"), OX + 34, y, false);
            for (int i = 0; i < NoteValues.Length; i++)
            {
                var v = NoteValues[i];
                int x = OX + 43 + i * 17;
                if (this.RadioPill(x, y, 15, h, null, v.Ticks == plain, has, acc, () => set(v.Ticks), v.Glyph))
                    this.simpleTip = (tip(v), x + 7, y, false);
            }
            // the dot: one and a half times the value picked; again, back to the plain one
            if (this.RadioPill(OX + 130, y, 13, h, null, dotted, has && known, acc, () => set(dotted ? plain : plain / 2 * 3), "dot"))
                this.simpleTip = (this.T("ui.simple.dot"), OX + 136, y, false);
        }

        /// <summary>One radio button, a label or a glyph; clicking the picked one again picks it again (and plays it).
        /// Returns whether the mouse is over it.</summary>
        private bool RadioPill(int x, int y, int w, int h, string? label, bool on, bool possible, Ramp acc, Action pick, string? glyph = null)
        {
            bool hover = this.Region(x, y, w, h, possible ? pick : null) && possible;
            string st = on ? "selected" : hover ? "hover" : "normal";
            this.cv.Spr($"pill:{w}:{h}:{st}:{acc.Hex}", () => Px.Pill(w, h, st, acc), x, y, possible ? 1f : 0.45f);
            Color col = on ? White : possible ? Ink : Ink3;
            if (glyph != null)
            {
                (int gw, int gh) = Px.IconSize(glyph);
                this.cv.Icon(glyph, col, x + (w - gw) / 2, y + (h - 3 - gh) / 2 + 1);
            }
            else
            {
                this.cv.Text(label!, x, y, col, align: Align.Center, w: w, dy: (h - 7) * 2 - 2, shadow: on ? (0, 2, acc.O) : null);
            }
            return hover;
        }

        /// <summary>The hover bubble: over the value under the mouse, or under 完整.</summary>
        private void DrawSimpleTip()
        {
            if (this.simpleTip is not var (text, ax, ay, below))
                return;
            const int h = 14;
            int w = Math.Min(142, PixCanvas.Measure(text) / Z + 12);
            int bx = Math.Clamp(ax - w / 2, OX + 4, OX + 146 - w);
            int by = below ? ay + 3 : ay - h - 3;
            this.cv.Spr($"bubble:{w}:{h}", () => Px.Bubble(w, h), bx, by);
            this.cv.Spr($"tail:{below}", () => Px.BubbleTail(below), ax - 3, below ? by - 3 : by + h - 1);
            this.cv.Text(PixCanvas.Fit(text, (w - 10) * Z), bx + 6, by + 3, Ink);
        }

        private void PickChip(int index)
        {
            if (this.settings.Notes.Count == 0)
                return;
            index = Math.Clamp(index, 0, this.settings.Notes.Count - 1);
            if (index == this.selected)
                return;
            this.selected = index;
            this.RevealSelected();
            BlockNote n = this.settings.Notes[index];
            this.PlayNote(n.Pitch, n.Velocity);
        }

        /// <summary>Move the picked note to a pitch and play it. If another note already sounds that pitch at the same
        /// moment, pick that one instead (like clicking a key in the full roll).</summary>
        private void SetSimplePitch(int pitch)
        {
            if (this.Sel is not BlockNote n || !IsPitch(pitch))
                return;
            int clash = this.settings.Notes.FindIndex(o => o.Pitch == pitch && o.Delay == n.Delay);
            if (clash >= 0 && clash != this.selected)
                this.selected = clash;
            else if (pitch != n.Pitch)
                this.EditSelected(e => e.Pitch = pitch);
            this.RevealSelected();
            this.PlayNote(pitch, n.Velocity);
        }

        /// <summary>时值: the picked note's length, played at that length.</summary>
        private void SetSimpleLength(int ticks)
        {
            if (this.Sel is not BlockNote n)
                return;
            if (ticks != n.Duration)
                this.EditSelected(e => e.Duration = ticks);
            this.PlayNote(n.Pitch, n.Velocity, ticks);
        }

        /// <summary>延迟: when the picked note sounds, then the whole block plays so the rhythm can be heard. If the same
        /// pitch already sounds at that moment, pick that one instead.</summary>
        private void SetSimpleDelay(int ticks)
        {
            if (this.Sel is not BlockNote n)
                return;
            int clash = this.settings.Notes.FindIndex(o => o.Pitch == n.Pitch && o.Delay == ticks);
            if (clash >= 0 && clash != this.selected)
                this.selected = clash;
            else if (ticks != n.Delay)
                this.EditSelected(e => e.Delay = ticks);
            this.Preview();
        }

        /// <summary>+: another note at the picked one's moment (same length and velocity), on the next natural above that
        /// isn't sounding yet, so it shows up as its own chip ready to be moved.</summary>
        private void AddSimpleNote()
        {
            if (this.Sel is not BlockNote n)
            {
                this.AddNote(Math.Min((this.simpleOctave + 1) * 12, TopPitch), 0, Ppq);
                return;
            }
            bool Free(int p) => IsPitch(p) && !this.settings.Notes.Any(o => o.Pitch == p && o.Delay == n.Delay);
            int[] candidates = Enumerable.Range(n.Pitch + 1, TopPitch - n.Pitch).Where(p => WhiteSemis.Contains(p % 12))
                .Concat(Enumerable.Range(n.Pitch + 1, TopPitch - n.Pitch))
                .Concat(Enumerable.Range(0, n.Pitch).Reverse())
                .ToArray();
            int pitch = candidates.FirstOrDefault(Free, -1);
            if (pitch < 0)
                return;
            this.AddNote(pitch, n.Delay, n.Duration);   // takes the picked note's velocity
        }
    }
}
