using System;
using System.Collections.Generic;
using System.Linq;
using JunimoOrchestra.Data;
using JunimoOrchestra.UI.Pix;
using Microsoft.Xna.Framework;
using StardewValley;

namespace JunimoOrchestra.UI
{
    /// <summary>音色 tab: family grid on the left, the family's GM programs (and sound bank variants) on the right.</summary>
    internal sealed partial class TunerMenu
    {
        private const int ListX = OX + 68, ListW = 78, ListTop = OY + 15, ListRows = 9, RowStep = 11;
        private int listScroll;
        private int lastListSelection = -1;
        private string? rowTip;   // a hovered row or chip: what plays instead when the font hasn't got it, else its name if it was cut

        /// <summary>One line in the program list: a program/kit row, or a row of variant chips.</summary>
        private readonly record struct ListItem(int Program, bool IsChips, List<int>? Banks);

        private void DrawInstrumentTab()
        {
            Ramp acc = this.Acc;
            Family? hovered = this.DrawFamilyGrid(acc);
            this.rowTip = null;
            this.DrawProgramList(acc);
            if (hovered != null)
                this.FamilyTooltip(hovered);
            else if (this.rowTip is string tip && this.pop == Pop.None)
            {
                int w = PixCanvas.Measure(tip) / Z + 10;
                int y = Math.Clamp((int)this.mouseY + 6, OY + 2, OY + 100);
                this.cv.Spr($"bubble:{w}:12", () => Px.Bubble(w, 12), OX + 146 - w, y);
                this.cv.Text(tip, OX + 146 - w, y, Ink, align: Align.Center, w: w, dy: 2);
            }
        }

        private Family? DrawFamilyGrid(Ramp acc)
        {
            Family? hovered = null;
            for (int i = 0; i < Families.All.Count; i++)
            {
                Family f = Families.All[i];
                int x = OX + 7 + i % 3 * 20, y = OY + 7 + i / 3 * 17;
                bool sel = f == this.family;
                bool hover = this.Region(x - 2, y - 2, 20, 17, () => this.PickFamily(f));
                if (hover)
                    hovered = f;
                if (sel)
                {
                    Ramp a = Ramp.Of(f.Accent);
                    this.cv.Spr($"plate:sel:{a.Hex}", () =>
                    {
                        var p = new PixImage(20, 19);
                        Px.RRLayers(p, 0, 0, 20, 19, 5, new[] { (0, a.O), (1, a.H), (2, White) });
                        return p;
                    }, x - 2, y - 2);
                }
                else if (hover)
                {
                    this.cv.Spr("plate:hover", () =>
                    {
                        var p = new PixImage(20, 19);
                        Px.RRLayers(p, 0, 0, 20, 19, 5, new[] { (0, Px.C("rose2")), (1, Px.C("blush")) });
                        return p;
                    }, x - 2, y - 2);
                }
                int lift = sel || hover ? 1 : 0;
                this.cv.Spr(this.mod.BlocksTexture, new Rectangle(f.Index * 16, 0, 16, 16), x, y - lift);
            }
            // selected heart on top of everything in the grid
            int si = this.family.Index;
            this.cv.Spr("heartbadge", Px.HeartBadge, OX + 7 + si % 3 * 20 + 12, OY + 7 + si / 3 * 17 - 4);

            // dice: a random sound
            int dx = OX + 7 + 2 * 20, dy = OY + 7 + 5 * 17;
            bool diceHover = this.Region(dx, dy, 16, 15, this.RandomSound);
            this.cv.Spr($"btn:16:15:milk:{diceHover}", () => Px.Button(16, 15, "milk", diceHover ? "hover" : "normal"), dx, dy);
            this.cv.Icon("dice", Ink2, dx + 4, dy + 3);
            return hovered;
        }

        private void FamilyTooltip(Family f)
        {
            int i = f.Index;
            int hx = OX + 7 + i % 3 * 20, hy = OY + 7 + i / 3 * 17;
            string line1 = $"{this.T("ui.family-header", new { name = this.names.FamilyName(f) })} · {this.names.FamilyCategory(f)}";
            string line2 = this.names.FamilyDesc(f);
            int w = Math.Max(62, Math.Max(PixCanvas.Measure(line1), PixCanvas.Measure(line2)) / Z + 12);
            int x = OX + 4, y = hy - 25;
            bool below = y < OY;
            if (below)
                y = hy + 18;
            this.cv.Spr($"bubble:{w}:22", () => Px.Bubble(w, 22), x, y);
            this.cv.Spr($"tail:{below}", () => Px.BubbleTail(below), hx + 5, below ? y - 3 : y + 21);
            this.cv.Text(line1, x + 6, y + 3, Ink);
            this.cv.Text(line2, x + 6, y + 11, Ink3);
        }

        private List<ListItem> BuildList()
        {
            var items = new List<ListItem>();
            if (this.family.IsDrumKit)
            {
                foreach (int kit in this.names.DrumKits)
                    items.Add(new ListItem(kit, false, null));
                return items;
            }
            for (int p = this.family.FirstProgram; p < this.family.FirstProgram + 8; p++)
            {
                items.Add(new ListItem(p, false, null));
                IReadOnlyList<int> banks = this.names.VariantBanks(p);
                if (p == this.settings.Program && banks.Count > 0)
                {
                    // wrap the chips (bank 0 + its variants) into as many rows as they need
                    var all = new List<int> { 0 };
                    all.AddRange(banks);
                    var row = new List<int>();
                    int used = 0;
                    foreach (int b in all)
                    {
                        int w = this.ChipWidth(b, p);
                        if (row.Count > 0 && used + w > ListW - 4)
                        {
                            items.Add(new ListItem(p, true, row));
                            row = new List<int>();
                            used = 0;
                        }
                        row.Add(b);
                        used += w + 1;
                    }
                    if (row.Count > 0)
                        items.Add(new ListItem(p, true, row));
                }
            }
            return items;
        }

        private int ChipWidth(int bank, int program)
            => Math.Max(14, PixCanvas.Measure(this.names.Program(bank, program)) / Z + 5);

        private void DrawProgramList(Ramp acc)
        {
            // header: family name · GM category (unless they're the same word), and 更多
            string family = this.names.FamilyName(this.family), category = this.names.FamilyCategory(this.family);
            string name = this.T("ui.family-header", new { name = family });
            int tw = this.cv.Text(name, ListX, OY + 3, Ink, dy: 2);
            string cat = "· " + category;
            if (!string.Equals(family, category, StringComparison.OrdinalIgnoreCase)
                && ListX + (tw + 8) / Z + PixCanvas.Measure(cat) / Z < OX + 124)
                this.cv.Text(cat, ListX + (tw + 8) / Z, OY + 3, Ink3, dy: 2);
            this.MorePill(OX + 126, OY + 3, this.pop == Pop.More, () => this.TogglePop(Pop.More));

            List<ListItem> items = this.BuildList();
            int selIndex = items.FindIndex(it => !it.IsChips && it.Program == this.settings.Program);
            if (selIndex != this.lastListSelection)
            {
                // keep the selection (and its chips) in view
                this.lastListSelection = selIndex;
                if (selIndex >= 0)
                {
                    int chipRows = items.Skip(selIndex + 1).TakeWhile(it => it.IsChips).Count();
                    if (selIndex < this.listScroll)
                        this.listScroll = selIndex;
                    else if (selIndex + chipRows >= this.listScroll + ListRows)
                        this.listScroll = selIndex + chipRows - ListRows + 1;
                }
            }
            int maxScroll = Math.Max(0, items.Count - ListRows);
            this.listScroll = Math.Clamp(this.listScroll, 0, maxScroll);
            this.Region(ListX, ListTop, ListW, ListRows * RowStep, scroll: d => this.listScroll = Math.Clamp(this.listScroll - d, 0, maxScroll));

            int y = ListTop;
            for (int i = this.listScroll; i < items.Count && i < this.listScroll + ListRows; i++, y += RowStep)
            {
                ListItem it = items[i];
                if (it.IsChips)
                    this.DrawChips(it, y, acc);
                else
                    this.DrawProgramRow(it.Program, y, acc);
            }
            if (this.listScroll > 0)
                this.cv.Icon("up", Ink3, ListX + ListW / 2 - 2, ListTop - 4);
            if (this.listScroll < maxScroll)
                this.cv.Icon("down", Ink3, ListX + ListW / 2 - 2, ListTop + ListRows * RowStep - 3);
        }

        private void DrawProgramRow(int program, int y, Ramp acc)
        {
            bool drums = this.family.IsDrumKit;
            bool sel = program == this.settings.Program;
            int bank = drums ? Families.DrumBank : 0;
            bool hover = this.Region(ListX, y, ListW, 10, () => this.PickProgram(bank, program));
            bool missing = !this.names.Exists(bank, program);   // this font hasn't got it: something else plays
            string state = sel ? "selected" : hover ? "hover" : "normal";
            this.cv.Spr($"pill:{ListW}:10:{state}:{acc.Hex}", () => Px.Pill(ListW, 10, state, acc), ListX, y);
            this.cv.Icon("sn8", sel ? White : acc.M, ListX + 3, y + 1);
            string label = drums ? this.names.Program(Families.DrumBank, program) : this.names.Program(0, program);
            int variants = drums ? 0 : this.names.VariantBanks(program).Count;
            int room = (variants > 0 ? ListW - 29 - 10 : ListW - 9 - 12) * Z;
            string shown = PixCanvas.Fit(label, room);
            if (hover)
                this.rowTip = missing ? this.T("ui.more.missing", new { name = this.names.Sounding(bank, program) }) : shown != label ? label : null;
            this.cv.Text(shown, ListX + 9, y, sel ? White : missing ? Ink3 : Ink, dy: 5, shadow: sel ? (0, 2, acc.O) : null);
            if (variants > 0)
            {
                const int bw = 13;
                this.cv.Spr($"badge:{sel}:{acc.Hex}", () => Px.Box(bw, 7, 2, sel ? White : acc.O, sel ? acc.O : acc.H), ListX + ListW - 29, y + 1);
                this.cv.Text($"+{variants}", ListX + ListW - 29, y + 1, sel ? White : acc.O, align: Align.Center, w: bw, dy: -2);
            }
            this.cv.Text((program + 1).ToString(), ListX, y, sel ? acc.H : Ink3, align: Align.Right, w: ListW - 3, dy: 5);
        }

        private void DrawChips(ListItem it, int y, Ramp acc)
        {
            this.cv.Rect(OX + 70, y, 1, 9, acc.L);
            int x = OX + 72;
            foreach (int bank in it.Banks!)
            {
                string name = this.names.Program(bank, it.Program);
                string label = PixCanvas.Fit(name, (ListW - 8) * Z);
                int w = Math.Min(this.ChipWidth(bank, it.Program), ListW - 4);
                bool sel = bank == this.settings.Bank;
                bool hover = this.Region(x, y, w, 9, () => this.PickProgram(bank, it.Program));
                if (hover && label != name)
                    this.rowTip = name;
                string state = sel ? "selected" : hover ? "hover" : "normal";
                this.cv.Spr($"pill:{w}:9:{state}:{acc.Hex}", () => Px.Pill(w, 9, state, acc), x, y);
                this.cv.Text(label, x, y, sel ? White : Ink, align: Align.Center, w: w, dy: 4, shadow: sel ? (0, 2, acc.O) : null);
                x += w + 1;
            }
        }

        private void PickFamily(Family f)
        {
            if (f == this.family)
            {
                this.Preview();
                return;
            }
            this.settings.Bank = f.IsDrumKit ? Families.DrumBank : 0;
            this.settings.Program = f.DefaultProgram;
            if (f.IsDrumKit != this.family.IsDrumKit)
                this.ConvertNotesForDrums(f.IsDrumKit);
            this.Commit();
            this.listScroll = 0;
            this.lastListSelection = -1;
            Game1.playSound("drumkit6");
            this.Preview();
        }

        private void PickProgram(int bank, int program)
        {
            this.settings.Bank = bank;
            this.settings.Program = program;
            this.Commit();
            this.Preview();
        }

        private void RandomSound()
        {
            var all = this.names.AllPresets.ToList();
            if (all.Count == 0)
                return;
            (int bank, int program) = all[Game1.random.Next(all.Count)];
            bool drums = bank >= Families.DrumBank;
            if (drums != this.family.IsDrumKit)
                this.ConvertNotesForDrums(drums);
            this.settings.Bank = bank;
            this.settings.Program = program;
            this.Commit();
            this.lastListSelection = -1;
            Game1.playSound("dwoop");
            this.Preview();
        }

        /// <summary>
        /// A lone default note means "the player hasn't written anything yet": swap C4 and the snare so a
        /// fresh block still sounds sensible after switching between melodic and drums.
        /// </summary>
        private void ConvertNotesForDrums(bool toDrums)
        {
            if (this.settings.Notes.Count == 1)
            {
                BlockNote n = this.settings.Notes[0];
                if (toDrums && n.Pitch == 60)
                    n.Pitch = 38;
                else if (!toDrums && n.Pitch == 38)
                    n.Pitch = 60;
                this.settings.Notes[0] = n;
            }
        }

        private void DrawMorePopover()
        {
            int bx = OX + 70, by = OY + 16, bw = 76;
            bool missing = !this.names.Exists(this.settings.Bank, this.settings.Program);
            int bh = missing ? 56 : 46;
            this.Bubble(bx, by, bw, bh, OX + 132, tailUp: true);
            this.cv.Text(this.T("ui.more.title"), bx + 5, by + 3, Ink2);
            (string label, string id, int value, int shown, int min, int max, Action<int> set)[] rows =
            {
                (this.T("ui.more.bank"), "bank", this.settings.Bank, this.settings.Bank, 0, 128, v => this.settings.Bank = v),
                (this.T("ui.more.program"), "program", this.settings.Program, this.settings.Program + 1, 1, 128, v => this.settings.Program = v - 1),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                int yy = by + 12 + i * 12;
                this.cv.Text(r.label, bx + 5, yy, Ink, dy: 4);
                this.StepButton(bx + 26, yy + 1, "minus", () => { r.set(Math.Max(r.min, r.shown - 1)); this.AfterNumberPick(); });
                this.ValueBox(r.id, bx + 36, yy, 20, r.shown.ToString(), r.shown, r.min, r.max, v => { r.set(v); this.AfterNumberPick(); });
                this.StepButton(bx + 57, yy + 1, "plus", () => { r.set(Math.Min(r.max, r.shown + 1)); this.AfterNumberPick(); });
            }
            this.cv.Text(this.T("ui.more.hint"), bx + 5, by + 36, Ink3);
            if (missing)
                this.cv.Text(this.T("ui.more.missing", new { name = this.names.Sounding(this.settings.Bank, this.settings.Program) }), bx + 5, by + 45, Px.C("rose2"));
        }

        private void AfterNumberPick()
        {
            this.Commit();
            this.lastListSelection = -1;
            this.Preview();
        }
    }
}
