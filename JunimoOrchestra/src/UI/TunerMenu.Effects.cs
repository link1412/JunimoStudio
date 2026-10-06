using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunimoOrchestra.Audio;
using JunimoOrchestra.Data;
using JunimoOrchestra.UI.Pix;

namespace JunimoOrchestra.UI
{
    /// <summary>效果 tab (MIDI CC91/93/1/10, all 0–127) and 全局 tab (save-wide settings + my preferences).</summary>
    internal sealed partial class TunerMenu
    {
        private static readonly string[] RoomKeys = { "dry", "cabin", "room", "hall", "church", "cave" };

        private void DrawEffectsTab()
        {
            Ramp acc = this.Acc;
            this.cv.Text(this.T("ui.fx.title"), OX + 6, OY + 3, Ink2, dy: 2);
            int y = OY + 14;
            this.FxRow("reverb", y, this.T("ui.fx.reverb"), this.settings.Reverb, v => this.settings.Reverb = v, acc);
            this.FxRow("chorus", y + 14, this.T("ui.fx.chorus"), this.settings.Chorus, v => this.settings.Chorus = v, acc);
            this.FxRow("vibrato", y + 28, this.T("ui.fx.vibrato"), this.settings.Vibrato, v => this.settings.Vibrato = v, acc);
            this.FxRow("pan", y + 42, this.T("ui.fx.pan"), this.settings.Pan, v => this.settings.Pan = v, acc, centred: true, text: this.PanText(this.settings.Pan));
            int lw = this.FxLabelW;
            this.cv.Text(this.T("ui.fx.left"), OX + 6 + lw, y + 51, Ink3);
            this.cv.Text(this.T("ui.fx.right"), OX + 6 + lw + 84 - (lw - 22) - 2, y + 51, Ink3);

            // the reverb "space" lives in the global tab
            bool tipHover = this.Region(OX + 6, OY + 73, 138, 11, () => this.SwitchTab(TunerTab.Global));
            this.cv.Spr($"tip:{tipHover}", () => Px.Box(138, 11, 3, Px.C("gold2"), tipHover ? Px.C("gold") : Px.C("gold3")), OX + 6, OY + 73);
            this.cv.Icon("globe", Px.C("goldD"), OX + 9, OY + 75);
            this.cv.Text(this.T("ui.fx.room-tip", new { room = this.T("ui.room." + RoomKeys[(int)this.mod.World.Room]) }), OX + 20, OY + 73, Px.C("goldD"), dy: 5);

            this.cv.Rect(OX + 6, OY + 88, 138, 1, Px.C("milk3"));
            this.cv.Text(this.T("ui.trigger"), OX + 6, OY + 90, Ink2, dy: 2);

            // reach: how many tiles away, straight across, someone sets it off (shown on the ground when hovered). Not set,
            // it's the save's (the globe, the number faded); − and + give the block its own, the round arrow takes it away
            int worldReach = this.mod.World.DefaultReach;
            bool ownReach = this.settings.Reach > 0;
            int reach = this.settings.EffectiveReach(worldReach);
            void SetReach(int r)
            {
                if (r == this.settings.Reach)
                    return;
                this.settings.Reach = r;
                this.Commit();
                StardewValley.Game1.playSound("drumkit6");
            }
            string reachLabel = this.T("ui.trigger.reach");
            int rx = OX + 144 - 10 - 18 - 10;
            int ix = rx - 2 - PixCanvas.Measure(reachLabel) / Z - 11;
            this.cv.Text(reachLabel, rx - 2, OY + 90, Ink2, align: Align.Right, w: 0, dy: 2);
            string? reachTip = null;
            if (ownReach)
            {
                bool back = this.Region(ix, OY + 89, 9, 10, () => SetReach(0));
                this.cv.Icon("reset", back ? acc.O : Ink3, ix + 1, OY + 90);
                if (back)
                    reachTip = this.T("ui.trigger.reach.back", new { n = worldReach });
            }
            else
            {
                bool global = this.Region(ix, OY + 89, 10, 10, () => this.SwitchTab(TunerTab.Global));
                this.cv.Icon("globe", global ? Px.C("goldD") : Px.C("gold2"), ix, OY + 90);
                if (global)
                    reachTip = this.T("ui.trigger.reach.global", new { n = worldReach });
            }
            this.SmallButton(rx, OY + 89, "minus", 10, 10, () => SetReach(Math.Max(1, reach - 1)));
            if (this.Hover(rx + 10, OY + 89, 18, 10))
                reachTip = this.T(ownReach ? "ui.trigger.reach.own" : "ui.trigger.reach.inherited", new { n = reach });
            this.cv.Text(this.T("ui.trigger.reach.n", new { n = reach }), rx + 10, OY + 90, ownReach ? Ink : Ink3, align: Align.Center, w: 18, dy: 2);
            this.SmallButton(rx + 28, OY + 89, "plus", 10, 10, () => SetReach(Math.Min(BlockSettings.MaxReach, reach + 1)));
            if (reachTip != null)
            {
                int w = PixCanvas.Measure(reachTip) / Z + 10;
                this.cv.Spr($"bubble:{w}:12", () => Px.Bubble(w, 12), OX + 146 - w, OY + 76);
                this.cv.Text(reachTip, OX + 146 - w, OY + 76, Ink, align: Align.Center, w: w, dy: 2);
            }
            (bool on, string icon, string label, Action flip)[] triggers =
            {
                (this.settings.TriggerByPlayers, "person", this.T("ui.trigger.players"), () => this.settings.TriggerByPlayers = !this.settings.TriggerByPlayers),
                (this.settings.TriggerByCreatures, "chicken", this.T("ui.trigger.creatures"), () => this.settings.TriggerByCreatures = !this.settings.TriggerByCreatures),
            };
            int x = OX + 6;
            for (int i = 0; i < triggers.Length; i++)
            {
                var t = triggers[i];
                int yy = OY + 101;
                this.ToggleSwitch(x, yy, t.on, () =>
                {
                    t.flip();
                    this.Commit();
                    StardewValley.Game1.playSound("drumkit6");
                });
                this.cv.Icon(t.icon, Ink2, x + 22, yy + 1);
                int tw = this.cv.Text(PixCanvas.Fit(t.label, (OX + 146 - x - 31) * Z), x + 31, yy - 2, Ink, dy: 4);
                x = Math.Max(x + 50, x + 31 + tw / Z + 6);
            }
        }

        private string PanText(int pan)
            => pan == 64 ? this.T("ui.fx.center")
                : pan < 64 ? this.T("ui.fx.pan-left", new { n = 64 - pan })
                : this.T("ui.fx.pan-right", new { n = pan - 64 });

        private int FxLabelW => Math.Max(22, new[] { "ui.fx.reverb", "ui.fx.chorus", "ui.fx.vibrato", "ui.fx.pan" }
            .Max(k => PixCanvas.Measure(this.T(k))) / Z + 4);

        private void FxRow(string id, int y, string label, int value, Action<int> set, Ramp acc, bool centred = false, string? text = null)
        {
            int x = OX + 6;
            this.cv.Text(label, x, y, Ink2, dy: 6);
            int lw = this.FxLabelW;
            int tx = x + lw, tw = 84 - (lw - 22);
            this.KnobSlider(tx, y + 2, tw, value, 127, set, acc, centred, released: this.Preview);
            this.ValueBox(id, tx + tw + 4, y, 26, text ?? value.ToString(), value, 0, 127, set);
        }

        /****
        ** 全局
        ****/
        private void DrawGlobalTab()
        {
            WorldSettings world = this.mod.World;
            ModConfig config = this.mod.Config;
            Ramp gold = Ramp.Of("#f2b640");
            Ramp mint = Ramp.Of("#62bf96");

            this.cv.Spr("hdr:save", () => Px.Box(70, 12, 3, Px.C("gold2"), Px.C("gold3")), OX + 4, OY + 4);
            this.cv.Icon("globe", Px.C("goldD"), OX + 7, OY + 6);
            this.cv.Text(PixCanvas.Fit(this.T("ui.global.save"), 55 * Z), OX + 17, OY + 4, Px.C("goldD"), dy: 6);
            this.cv.Spr("hdr:prefs", () => Px.Box(68, 12, 3, Px.C("mint2"), Px.C("mint3")), OX + 78, OY + 4);
            this.cv.Icon("person", Px.C("mintD"), OX + 82, OY + 6);
            this.cv.Text(PixCanvas.Fit(this.T("ui.global.prefs"), 54 * Z), OX + 90, OY + 4, Px.C("mintD"), dy: 6);
            this.cv.Rect(OX + 75, OY + 18, 1, 92, Px.C("milk3"));

            // --- this save
            int L = OX + 6;
            this.cv.Text(this.T("ui.global.tempo"), L, OY + 21, Ink2, dy: 2);
            void SetTempo(int v)
            {
                world.DefaultTempo = Math.Clamp(v, BlockSettings.MinTempo, BlockSettings.MaxTempo);
            }
            this.SmallButton(L + 30, OY + 22, "minus", 9, 10, () => SetTempo(world.DefaultTempo - (Shift ? 10 : 1)));
            this.cv.Icon("sn4", Ink, L + 41, OY + 23);
            this.ValueBox("worldtempo", L + 45, OY + 22, 14, world.DefaultTempo.ToString(), world.DefaultTempo, BlockSettings.MinTempo, BlockSettings.MaxTempo, SetTempo);
            this.SmallButton(L + 60, OY + 22, "plus", 9, 10, () => SetTempo(world.DefaultTempo + (Shift ? 10 : 1)));

            this.cv.Text(this.T("ui.global.room"), L, OY + 37, Ink2, dy: 2);
            for (int i = 0; i < RoomKeys.Length; i++)
            {
                int c = i % 3, r = i / 3;
                int x = L + c * 22, y = OY + 48 + r * 13;
                Room room = (Room)i;
                bool sel = world.Room == room;
                bool hover = this.Region(x, y, 20, 11, () =>
                {
                    world.Room = room;
                    this.Preview();
                });
                string st = sel ? "selected" : hover ? "hover" : "normal";
                this.cv.Spr($"pill:20:11:{st}:{gold.Hex}", () => Px.Pill(20, 11, st, gold), x, y);
                this.cv.Text(this.T("ui.room." + RoomKeys[i]), x, y, sel ? White : Ink, align: Align.Center, w: 20, dy: 8,
                    shadow: sel ? (0, 2, gold.O) : null);
            }

            string trLabel = this.T("ui.global.transpose");
            this.cv.Text(trLabel, L, OY + 79, Ink2, dy: 2);
            int tl = Math.Max(18, PixCanvas.Measure(trLabel) / Z + 2);
            this.SmallButton(L + tl, OY + 80, "minus", 10, 10, () => world.Transpose = Math.Max(-12, world.Transpose - 1));
            string tr = world.Transpose > 0 ? "+" + world.Transpose : world.Transpose.ToString();
            this.cv.Text(this.T("ui.global.semitones", new { n = tr }), L + tl + 11, OY + 79, Ink, align: Align.Center, w: 55 - tl - 11, dy: 2);
            this.SmallButton(L + 55, OY + 80, "plus", 10, 10, () => world.Transpose = Math.Min(12, world.Transpose + 1));

            // the reach of every block that doesn't set its own
            string reachLabel = this.T("ui.global.reach");
            this.cv.Text(reachLabel, L, OY + 93, Ink2, dy: 2);
            int rl = Math.Max(18, PixCanvas.Measure(reachLabel) / Z + 2);
            this.SmallButton(L + rl, OY + 94, "minus", 10, 10, () => world.DefaultReach = Math.Max(1, world.DefaultReach - 1));
            this.cv.Text(this.T("ui.trigger.reach.n", new { n = world.DefaultReach }), L + rl + 11, OY + 93, Ink, align: Align.Center, w: 55 - rl - 11, dy: 2);
            this.SmallButton(L + 55, OY + 94, "plus", 10, 10, () => world.DefaultReach = Math.Min(BlockSettings.MaxReach, world.DefaultReach + 1));

            // --- my preferences
            int R = OX + 80;
            this.cv.Text(this.T("ui.global.master"), R, OY + 21, Ink2, dy: 2);
            this.KnobSlider(R + 26, OY + 23, 34, config.MasterVolume, 127, v => config.MasterVolume = v, mint,
                released: () =>
                {
                    this.mod.SaveConfig();
                    this.Preview();
                });
            (bool on, string label, Action flip)[] prefs =
            {
                (config.SpatialAudio, this.T("ui.global.spatial"), () => config.SpatialAudio = !config.SpatialAudio),
                (config.ShowLabels, this.T("ui.global.labels"), () => config.ShowLabels = !config.ShowLabels),
                (config.NoteParticles, this.T("ui.global.particles"), () => config.NoteParticles = !config.NoteParticles),
                (config.SimpleNotes, this.T("ui.global.simple"), () => config.SimpleNotes = !config.SimpleNotes),
            };
            for (int i = 0; i < prefs.Length; i++)
            {
                var p = prefs[i];
                int y = OY + 36 + i * 12;
                this.ToggleSwitch(R, y, p.on, () =>
                {
                    p.flip();
                    this.mod.SaveConfig();
                });
                this.cv.Text(p.label, R + 22, y - 2, Ink, dy: 4);
            }
            this.cv.Text(this.T("ui.global.font"), R, OY + 84, Ink2, dy: 2);
            this.FontPicker(R, OY + 93, mint);
            this.cv.Text(this.T("ui.global.gmcm"), R - 1, OY + 103, Ink3, dy: 4);
        }

        /// <summary>‹ SoundFont ›: the built-in one and the content packs' (<see cref="ModEntry.SoundFontChoices"/>), in turn. A new one plays as soon
        /// as it's loaded (a big font takes a few seconds; the one before plays until then), and the block plays to show
        /// it off.</summary>
        private void FontPicker(int x, int y, Ramp mint)
        {
            ModConfig config = this.mod.Config;
            List<string> choices = this.fontChoices ??= this.mod.SoundFontChoices();
            int at = Math.Max(0, choices.IndexOf(config.SoundFont));
            void Pick(int step)
            {
                config.SoundFont = choices[(at + step + choices.Count) % choices.Count];
                this.mod.SaveConfig();
                this.mod.ApplySoundFont();
                this.previewAfterFont = this.mod.Engine.Generation;
            }
            this.StepButton(x, y + 1, "left", () => Pick(-1));
            this.StepButton(x + 57, y + 1, "right", () => Pick(1));

            string choice = config.SoundFont;
            bool loading = this.mod.Engine.IsLoading;
            bool failed = !loading && this.mod.Engine.FontPath != this.mod.SoundFontPath(choice);
            string label = loading ? this.T("ui.global.font.loading")
                : choice.Length == 0 ? this.T("ui.global.font.builtin")
                : Path.GetFileNameWithoutExtension(choice);
            bool hover = this.Region(x + 10, y, 46, 11, () => Pick(1));
            this.cv.Spr($"fontbox:{hover}", () => Px.Box(46, 11, 3, hover ? mint.O : Px.C("milk4"), White), x + 10, y);
            this.cv.Text(PixCanvas.Fit(label, 43 * Z), x + 10, y, failed ? Px.C("rose2") : Ink, align: Align.Center, w: 46, dy: 6);

            // the font's own name, or why it couldn't be loaded
            string? tip = failed && this.mod.Engine.FontProblem is FontProblem problem ? this.mod.Describe(problem)
                : hover && !loading ? this.mod.Engine.FontName : null;
            if (hover && !string.IsNullOrEmpty(tip))
            {
                int w = Math.Min(PixCanvas.Measure(tip) / Z + 10, 144);
                int tx = Math.Clamp(x + 33 - w / 2, OX + 3, OX + 147 - w);
                this.cv.Spr($"bubble:{w}:12", () => Px.Bubble(w, 12), tx, y - 13);
                this.cv.Text(PixCanvas.Fit(tip, (w - 8) * Z), tx, y - 13, failed ? Px.C("rose2") : Ink, align: Align.Center, w: w, dy: 2);
            }
        }
    }
}
