using System;
using System.Collections.Generic;
using JunimoOrchestra.Data;
using JunimoOrchestra.UI.Pix;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace JunimoOrchestra.UI
{
    internal enum TunerTab { Instrument, Notes, Effects, Global }

    internal enum Pop { None, More, Tempo, Velocity, Help }

    /// <summary>
    /// The block tuner: a pixel-art panel drawn at 4x (see docs/UX-DESIGN.md and mockups/).
    /// Immediate-mode: each frame draws the UI and records the clickable regions it drew.
    /// </summary>
    internal sealed partial class TunerMenu : IClickableMenu
    {
        private const int PW = 232, PH = 160, Z = PixCanvas.Z;
        private const int OX = 74, OY = 23;          // content card origin
        private const int TabW = 36, TabStep = 38;
        private const string ClipboardPrefix = "JunimoOrchestra:";

        private static TunerTab lastTab = TunerTab.Instrument;
        private static string? clipboard;

        private readonly ModEntry mod;
        private readonly SoundNames names;
        private readonly GameLocation location;
        private readonly Vector2 tile;
        private SObject block;
        private Family family;
        private readonly BlockSettings settings;

        private TunerTab tab;
        private Pop pop;
        private NumberEntry? entry;

        private readonly PixCanvas cv = new();
        private readonly List<Hit> hits = new();
        private Hit? dragging;
        private int layer;
        private float mouseX, mouseY;   // art pixels
        private string? plaqueTip;      // the plaque's name when it was cut: the whole of it, over everything
        private float screenScale = 1f;
        private string? toast;
        private double toastUntil;

        // the SoundFonts as of opening the panel, and the font switched to (play the block once it's loaded)
        private List<string>? fontChoices;
        private int previewAfterFont = -1;

        // preview animation
        private double previewStart = -1;
        private double[] previewNoteStarts = Array.Empty<double>();
        private double previewSecondsPerTick = 0.001;
        private double previewLength;

        private sealed class Hit
        {
            public Rectangle R;
            public int Layer;
            public Action? Click;
            public Action? Right;
            public Action<float, float, bool>? Drag;
            public Action? Release;
            public Action<int>? Scroll;
        }

        public TunerMenu(ModEntry mod, GameLocation location, SObject block, Family family)
        {
            this.mod = mod;
            this.names = mod.Names;
            this.location = location;
            this.tile = block.TileLocation;
            this.block = block;
            this.family = family;
            this.settings = BlockSettings.Read(block, family);
            this.tab = lastTab;
            this.InitNotes();
            this.Relayout();
            Game1.playSound("bigSelect");
        }

        private Ramp Acc => Ramp.Of(this.family.Accent);
        private static Color Ink => Px.C("ink");
        private static Color Ink2 => Px.C("ink2");
        private static Color Ink3 => Px.C("ink3");
        private static Color White => Px.C("white");
        private string T(string key) => this.names.T(key);
        private string T(string key, object tokens) => this.mod.Helper.Translation.Get(key, tokens);
        private int ActiveLayer => this.pop != Pop.None ? 1 : 0;
        private static double Now => Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0;

        /****
        ** Settings
        ****/
        /// <summary>Save the working settings onto the block (live: walking past it plays the new tune immediately).</summary>
        private void Commit()
        {
            this.settings.Normalize();
            Family wanted = Families.ForProgram(this.settings.Bank, this.settings.Program);
            if (wanted != this.family)
                this.SwapFamily(wanted);
            this.settings.Write(this.block);
        }

        /// <summary>A block of another family is a different item: replace it in place, keeping its tune.</summary>
        private void SwapFamily(Family wanted)
        {
            var replacement = ItemRegistry.Create<SObject>(wanted.QualifiedItemId);
            foreach (var pair in this.block.modData.Pairs)
                replacement.modData[pair.Key] = pair.Value;
            replacement.TileLocation = this.tile;
            if (this.location.objects.TryGetValue(this.tile, out SObject? current) && current == this.block)
                this.location.objects[this.tile] = replacement;
            this.block = replacement;
            this.family = wanted;
            if (this.drumView != wanted.IsDrumKit)
                this.InitNotes();
        }

        private void Preview()
        {
            var starts = this.mod.Player.Preview(this.settings);
            if (starts == null)
                return;
            int tempo = this.settings.EffectiveTempo(this.mod.World.DefaultTempo);
            this.previewSecondsPerTick = 60.0 / tempo / BlockSettings.Ppq;
            this.previewStart = Now + Audio.SynthEngine.LatencyMs / 1000.0;
            this.previewNoteStarts = new double[starts.Count];
            for (int i = 0; i < starts.Count; i++)
                this.previewNoteStarts[i] = starts[i];
            this.previewLength = this.settings.LengthTicks * this.previewSecondsPerTick;
        }

        private void StopPreview()
        {
            this.mod.Player.StopPreview();
            this.previewStart = -1;
        }

        private bool PreviewPlaying => this.previewStart >= 0 && Now - this.previewStart < this.previewLength + 0.1;

        /// <summary>Play a single note with this block's sound (clicking keys), a beat long unless told.</summary>
        private void PlayNote(int pitch, int velocity = 100, int duration = BlockSettings.Ppq)
        {
            var one = this.settings.Clone();
            one.Notes.Clear();
            one.Notes.Add(new BlockNote(pitch, 0, duration, velocity));
            this.mod.Player.Preview(one);
        }

        private void Toast(string text)
        {
            this.toast = text;
            this.toastUntil = Now + 1.6;
        }

        /****
        ** Layout & input plumbing
        ****/
        private void Relayout()
        {
            int vw = Game1.uiViewport.Width, vh = Game1.uiViewport.Height;
            this.screenScale = Math.Min(1f, Math.Min((vw - 16) / (float)(PW * Z), (vh - 16) / (float)((PH + 10) * Z)));
            float virtW = vw / this.screenScale, virtH = vh / this.screenScale;
            this.cv.OriginX = (int)((virtW - PW * Z) / 2);
            this.cv.OriginY = (int)((virtH - PH * Z) / 2) + 16;
            this.xPositionOnScreen = (int)(this.cv.OriginX * this.screenScale);
            this.yPositionOnScreen = (int)((this.cv.OriginY - 8 * Z) * this.screenScale);
            this.width = (int)(PW * Z * this.screenScale);
            this.height = (int)((PH + 8) * Z * this.screenScale);
        }

        internal void DebugSetTab(int index) => this.SwitchTab((TunerTab)index);

        /// <summary>UI-space screen position of an art pixel (debug bridge / tests).</summary>
        internal Point ScreenOf(float ax, float ay)
            => new((int)((this.cv.OriginX + ax * Z) * this.screenScale), (int)((this.cv.OriginY + ay * Z) * this.screenScale));

        private (float x, float y) ToArt(int sx, int sy)
            => ((sx / this.screenScale - this.cv.OriginX) / Z, (sy / this.screenScale - this.cv.OriginY) / Z);

        /// <summary>Register a clickable area (art px) for this frame; returns whether the mouse is over it.</summary>
        private bool Region(int x, int y, int w, int h, Action? click = null, Action? right = null,
            Action<float, float, bool>? drag = null, Action? release = null, Action<int>? scroll = null)
        {
            var r = new Rectangle(x, y, w, h);
            this.hits.Add(new Hit { R = r, Layer = this.layer, Click = click, Right = right, Drag = drag, Release = release, Scroll = scroll });
            return this.Hover(r);
        }

        private bool Hover(Rectangle r)
            => this.layer >= this.ActiveLayer && (this.dragging == null || this.dragging.R == r)
               && r.Contains((int)Math.Floor(this.mouseX), (int)Math.Floor(this.mouseY));

        private bool Hover(int x, int y, int w, int h) => this.Hover(new Rectangle(x, y, w, h));

        private Hit? HitAt(int sx, int sy, Func<Hit, bool> wants)
        {
            (float ax, float ay) = this.ToArt(sx, sy);
            var p = new Point((int)Math.Floor(ax), (int)Math.Floor(ay));
            for (int i = this.hits.Count - 1; i >= 0; i--)
            {
                Hit h = this.hits[i];
                if (h.Layer > 0 && this.pop == Pop.None)
                    continue; // drawn last frame, but the popover has closed since
                if (h.Layer >= this.ActiveLayer && h.R.Contains(p) && wants(h))
                    return h;
            }
            return null;
        }

        private void FinishEntry(bool keepIfSameTarget = false)
        {
            if (this.entry != null && !keepIfSameTarget)
            {
                this.entry.Confirm();
                this.entry = null;
            }
        }

        private void BeginEntry(string id, int current, int min, int max, Action<int> commit)
        {
            this.FinishEntry();
            this.entry = new NumberEntry(id, current, min, max, v =>
            {
                commit(v);
                this.Commit();
            });
            this.entry.Begin();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            this.FinishEntry();
            Hit? hit = this.HitAt(x, y, h => h.Click != null || h.Drag != null || h.Layer > 0);
            if (hit == null)
            {
                if (this.pop != Pop.None)
                    this.pop = Pop.None;
                return;
            }
            hit.Click?.Invoke();
            if (hit.Drag != null)
            {
                this.dragging = hit;
                (float ax, float ay) = this.ToArt(x, y);
                hit.Drag(ax, ay, true);
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            if (this.dragging?.Drag != null)
            {
                (float ax, float ay) = this.ToArt(x, y);
                this.dragging.Drag(ax, ay, false);
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            Hit? d = this.dragging;
            this.dragging = null;
            d?.Release?.Invoke();
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            this.FinishEntry();
            Hit? hit = this.HitAt(x, y, h => h.Right != null || h.Layer > 0);
            if (hit == null)
            {
                this.pop = Pop.None;
                return;
            }
            hit.Right?.Invoke();
        }

        public override void receiveScrollWheelAction(int direction)
        {
            Hit? hit = this.HitAt(Game1.getMouseX(), Game1.getMouseY(), h => h.Scroll != null);
            hit?.Scroll?.Invoke(direction > 0 ? 1 : -1);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (this.entry != null)
            {
                if (key == Keys.Escape)
                {
                    this.entry.Cancel();
                    this.entry = null;
                }
                return; // typing: never close the menu on E etc.
            }
            if (key == Keys.Escape && this.pop != Pop.None)
            {
                this.pop = Pop.None;
                return;
            }
            if (this.tab == TunerTab.Notes && this.NotesKey(key))
                return;
            if (key == Keys.Space)
            {
                this.Preview();
                return;
            }
            base.receiveKeyPress(key);
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => this.Relayout();

        protected override void cleanupBeforeExit()
        {
            this.FinishEntry();
            this.Commit();
            this.StopPreview();
            lastTab = this.tab;
            base.cleanupBeforeExit();
        }

        public override void update(GameTime time)
        {
            base.update(time);
            if (this.entry is { Done: true })
                this.entry = null;
            if (this.previewAfterFont >= 0 && !this.mod.Engine.IsLoading && this.mod.Engine.Generation != this.previewAfterFont)
            {
                this.previewAfterFont = -1;
                this.Preview();
            }
        }

        /****
        ** Drawing
        ****/
        public override void draw(SpriteBatch b)
        {
            this.Relayout();
            this.hits.Clear();
            this.layer = 0;
            (this.mouseX, this.mouseY) = this.ToArt(Game1.getMouseX(), Game1.getMouseY());

            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), new Color(20, 8, 24) * 0.45f);
            bool scaled = this.screenScale < 0.999f;
            if (scaled)
            {
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateScale(this.screenScale));
            }
            this.cv.B = b;

            // the whole panel swallows clicks so nothing falls through to the world
            this.Region(0, -8, PW, PH + 8);
            this.plaqueTip = null;
            this.DrawShell();
            switch (this.tab)
            {
                case TunerTab.Instrument: this.DrawInstrumentTab(); break;
                case TunerTab.Notes: this.DrawNotesTab(); break;
                case TunerTab.Effects: this.DrawEffectsTab(); break;
                case TunerTab.Global: this.DrawGlobalTab(); break;
            }
            if (this.plaqueTip is string tip)
            {
                int w = PixCanvas.Measure(tip) / Z + 10;
                this.cv.Spr($"bubble:{w}:12", () => Px.Bubble(w, 12), 4, 59);
                this.cv.Text(tip, 4, 59, Ink, align: Align.Center, w: w, dy: 2);
            }
            this.layer = 1;
            this.DrawPopover();
            this.DrawToast();

            if (scaled)
            {
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            }
            this.drawMouse(b);
        }

        private void DrawShell()
        {
            Ramp acc = this.Acc;
            Texture2D atlas = this.mod.UiAtlas;
            this.cv.Spr(atlas, PixData.AtlasPanel, 0, 0);

            // title ribbon + close button
            this.cv.Spr(atlas, PixData.AtlasRibbon, (PW - 104) / 2, -8);
            this.cv.Text(this.mod.ContentHooks.ItemName(this.family, this.settings.Look), (PW - 104) / 2, -8, White, PixCanvas.Big,
                (0, 3, Px.C("berry")), Align.Center, 104, dy: 4);
            bool closeHover = this.Region(PW - 16, -5, 13, 13, () => this.exitThisMenu());
            this.cv.Spr($"round:13:{closeHover}", () => Px.RoundButton(13, "pink", closeHover ? "hover" : "normal"), PW - 16, -5);
            this.cv.Icon("x", White, PW - 13, -1);

            this.DrawStage(acc);

            // plaque: what this block plays
            this.cv.Spr("plaque", () => Px.Card(60, 23, 3, "milk", "gold2", false), 8, 71);
            this.cv.Rect(10, 72, 56, 1, Px.C("gold3"));
            string sounding = this.names.Sounding(this.settings.Bank, this.settings.Program);
            string shown = PixCanvas.Fit(sounding, 58 * Z);
            if (shown != sounding && this.Hover(8, 71, 60, 11))
                this.plaqueTip = sounding;
            this.cv.Text(shown, 8, 72, Ink, align: Align.Center, w: 60, dy: 2);
            int tempo = this.settings.EffectiveTempo(this.mod.World.DefaultTempo);
            this.cv.Text(this.T(this.settings.Notes.Count == 1 ? "ui.summary.one" : "ui.summary", new { count = this.settings.Notes.Count, tempo }), 8, 82, acc.D, align: Align.Center, w: 60);

            // play / stop
            bool playing = this.PreviewPlaying;
            bool playHover = this.Region(8, 97, 60, 20, () =>
            {
                if (this.PreviewPlaying)
                    this.StopPreview();
                else
                    this.Preview();
            });
            this.cv.Spr($"btn:60:20:pink:{playHover}", () => Px.Button(60, 20, "pink", playHover ? "hover" : "normal"), 8, 97);
            this.cv.Icon(playing ? "stop" : "play", White, 24, 103);
            this.cv.Text(this.T(playing ? "ui.stop" : "ui.preview"), 32, 97, White, PixCanvas.Big, (0, 3, Px.C("berry")), dy: 10);

            // copy / paste: icon and word, or just the words when they don't fit beside the icons (English)
            string copy = this.T("ui.copy"), paste = this.T("ui.paste");
            bool icons = Math.Max(PixCanvas.Measure(copy), PixCanvas.Measure(paste)) <= 15 * Z;
            bool copyHover = this.Region(8, 121, 29, 15, this.CopySettings);
            this.cv.Spr($"btn:29:15:milk:{copyHover}", () => Px.Button(29, 15, "milk", copyHover ? "hover" : "normal"), 8, 121);
            bool pasteHover = this.Region(39, 121, 29, 15, this.PasteSettings);
            this.cv.Spr($"btn:29:15:milk:{pasteHover}", () => Px.Button(29, 15, "milk", pasteHover ? "hover" : "normal"), 39, 121);
            if (icons)
            {
                this.cv.Icon("copy", Ink, 12, 124, Px.C("milk"));
                this.cv.Text(copy, 19, 121, Ink, align: Align.Center, w: 18, dy: 12);
                this.cv.Icon("paste", Ink, 43, 124, Px.C("milk"));
                this.cv.Text(paste, 50, 121, Ink, align: Align.Center, w: 18, dy: 12);
            }
            else
            {
                this.cv.Text(PixCanvas.Fit(copy, 25 * Z), 8, 121, Ink, align: Align.Center, w: 29, dy: 12);
                this.cv.Text(PixCanvas.Fit(paste, 25 * Z), 39, 121, Ink, align: Align.Center, w: 29, dy: 12);
            }

            // tabs
            (string key, string icon)[] tabs = { ("ui.tab.instrument", "notes"), ("ui.tab.notes", "keys"), ("ui.tab.effects", "wave") };
            for (int i = 0; i < tabs.Length; i++)
            {
                int x = 76 + i * TabStep;
                bool sel = (int)this.tab == i;
                TunerTab target = (TunerTab)i;
                bool hover = this.Region(x, 11, TabW, 13, () => this.SwitchTab(target)) && !sel;
                this.cv.Spr($"tab:{TabW}:{sel}", () => Px.Tab(TabW, 13, sel), x, hover ? 10 : 11);
                int pad = (TabW - 23) / 2;
                string label = this.T(tabs[i].key);
                int ty = (sel ? 14 : 18) - (hover ? 4 : 0);
                if (PixCanvas.Measure(label) <= (TabW - 13 - pad - 2) * Z)
                {
                    this.cv.Icon(tabs[i].icon, sel ? Ink : Ink2, x + 3 + pad, (sel ? 15 : 16) - (hover ? 1 : 0), sel ? White : Px.C("blush"));
                    this.cv.Text(label, x + 13 + pad, 11, sel ? Ink : Ink2, dy: ty);
                }
                else
                    this.cv.Text(PixCanvas.Fit(label, (TabW - 4) * Z), x, 11, sel ? Ink : Ink2, align: Align.Center, w: TabW, dy: ty);
            }
            bool gsel = this.tab == TunerTab.Global;
            const int gx = 196;
            bool ghover = this.Region(gx, 11, 27, 13, () => this.SwitchTab(TunerTab.Global)) && !gsel;
            this.cv.Spr($"goldtab:{gsel}", () => Px.GoldTab(gsel), gx, ghover ? 10 : 11);
            string glabel = this.T("ui.tab.global");
            int gty = (gsel ? 14 : 18) - (ghover ? 4 : 0);
            if (PixCanvas.Measure(glabel) <= 12 * Z)
            {
                this.cv.Icon("globe", Px.C("goldD"), gx + 3, (gsel ? 15 : 16) - (ghover ? 1 : 0), gsel ? White : Px.C("gold3"));
                this.cv.Text(glabel, gx + 13, 11, Px.C("goldD"), dy: gty);
            }
            else
                this.cv.Text(PixCanvas.Fit(glabel, 24 * Z), gx, 11, Px.C("goldD"), align: Align.Center, w: 27, dy: gty);

            if (gsel)
            {
                this.cv.Spr("card:gold", () => Px.Card(150, 114, 4, "milk", "gold2", false), OX, OY);
                this.cv.Rect(gx + 1, 23, 25, 2, Px.C("gold3"));
            }
            else
            {
                this.cv.Spr("card:lace", () => Px.Card(150, 114, 4), OX, OY);
                this.cv.Rect(77 + (int)this.tab * TabStep, 23, TabW - 2, 2, Px.C("milk"));
            }

            // bottom bar: this block's volume
            this.cv.Spr("bottombar", () => Px.Card(216, 14, 4, "milk2", "rose2", false), 8, 141);
            this.cv.Icon("speaker", Ink, 13, 144);
            this.cv.Text(this.T("ui.volume"), 23, 141, Ink, dy: 12);
            this.HeartSlider(54, 142, 104, this.settings.Volume, v => this.settings.Volume = v, acc);
            this.cv.Text(this.settings.Volume.ToString(), 161, 141, Ink, dy: 12);
            bool doneHover = this.Region(184, 142, 38, 12, () => this.exitThisMenu());
            this.cv.Spr($"btn:38:12:mint:{doneHover}", () => Px.Button(38, 12, "mint", doneHover ? "hover" : "normal"), 184, 142);
            this.cv.Icon("check", Px.C("mintD"), 192, 145);
            this.cv.Text(this.T("ui.done"), 200, 142, Px.C("mintD"), dy: 9);
        }

        private void SwitchTab(TunerTab target)
        {
            if (this.tab == target)
                return;
            this.tab = target;
            this.pop = Pop.None;
            Game1.playSound("smallSelect");
        }

        /// <summary>The stage: the block with a tinted Junimo on top that hops along with the preview.</summary>
        private void DrawStage(Ramp acc)
        {
            this.cv.Spr(this.mod.UiAtlas, PixData.AtlasStage, 8, 14);
            float hop = 0, blockHop = 0;
            if (this.previewStart >= 0)
            {
                double t = Now - this.previewStart;
                foreach (double s in this.previewNoteStarts)
                {
                    double dt = t - s;
                    if (dt >= 0 && dt < 0.3)
                    {
                        hop = Math.Min(hop, (float)(-Math.Sin(Math.PI * dt / 0.3) * 4));
                        blockHop = Math.Min(blockHop, (float)(-Math.Sin(Math.PI * dt / 0.2) * 1.5));
                    }
                }
            }
            Vector2 pos = this.cv.ToScreen(30, 44) + new Vector2(0, blockHop * Z);
            this.cv.B.Draw(this.mod.BlocksTexture, pos, new Rectangle(this.family.Index * 16, 0, 16, 16), Color.White, 0, Vector2.Zero, Z, SpriteEffects.None, 1);
            this.cv.B.Draw(Game1.staminaRect, new Rectangle((int)pos.X + 3 * Z, (int)pos.Y + 1 * Z, 10 * Z, 2 * Z), new Color(40, 10, 40) * (110 / 255f));
            Color tint = Color.Lerp(this.family.Accent, Color.White, 0.35f);
            int frame = 44;
            Texture2D junimo = Game1.content.Load<Texture2D>("Characters\\Junimo");
            this.cv.B.Draw(junimo, this.cv.ToScreen(30, 30) + new Vector2(0, (hop + blockHop) * Z),
                new Rectangle(frame % 8 * 16, frame / 8 * 16, 16, 16), tint, 0, Vector2.Zero, Z, SpriteEffects.None, 1);
            this.cv.Icon("note", Px.C("gold3"), 21, 31);
            this.cv.Icon("notes", Px.C("blush"), 49, 27);
            this.cv.Icon("sparkle", Px.C("gold3"), 52, 40);
        }

        /// <summary>Pink slider with the heart knob (block volume).</summary>
        private void HeartSlider(int x, int y, int w, int value, Action<int> set, Ramp acc)
        {
            int fw = (int)(w * value / 127f);
            this.Region(x - 3, y - 1, w + 6, 11, drag: (ax, _, _) =>
            {
                set(Math.Clamp((int)Math.Round((ax - x) / w * 127), 0, 127));
            }, release: () =>
            {
                this.Commit();
                this.Preview();
            });
            this.cv.Spr($"track:{w}", () => Px.SliderTrack(w), x, y + 2);
            if (fw > 0)
                this.cv.Spr($"fill:{fw}:{acc.Hex}", () => Px.SliderFill(fw, acc), x, y + 2);
            this.cv.Spr("heartknob", Px.HeartKnob, x + fw - 6, y);
        }

        private void DrawToast()
        {
            if (this.toast == null || Now > this.toastUntil)
                return;
            int w = PixCanvas.Measure(this.toast) / Z + 12;
            int x = (PW - w) / 2, y = PH - 30;
            this.cv.Spr($"bubble:{w}:14", () => Px.Bubble(w, 14), x, y);
            this.cv.Text(this.toast, x, y, Ink, align: Align.Center, w: w, dy: 3);
        }

        /****
        ** Copy / paste
        ****/
        private void CopySettings()
        {
            this.Commit();
            clipboard = this.settings.Serialize();
            DesktopClipboard.SetText(ClipboardPrefix + clipboard);
            this.Toast(this.T("ui.copied"));
            Game1.playSound("coin");
        }

        private void PasteSettings()
        {
            string? raw = clipboard;
            string system = "";
            if (DesktopClipboard.GetText(ref system) && system.StartsWith(ClipboardPrefix))
                raw = system[ClipboardPrefix.Length..].Trim();
            if (raw == null || !BlockSettings.TryParse(raw, out BlockSettings? pasted))
            {
                this.Toast(this.T("ui.paste-empty"));
                return;
            }
            BlockSettings p = pasted!;
            this.settings.Bank = p.Bank;
            this.settings.Program = p.Program;
            this.settings.Tempo = p.Tempo;
            this.settings.Volume = p.Volume;
            this.settings.Reverb = p.Reverb;
            this.settings.Chorus = p.Chorus;
            this.settings.Vibrato = p.Vibrato;
            this.settings.Pan = p.Pan;
            this.settings.TriggerByPlayers = p.TriggerByPlayers;
            this.settings.TriggerByCreatures = p.TriggerByCreatures;
            this.settings.Notes = new List<BlockNote>(p.Notes);
            this.Commit();
            this.InitNotes();
            this.Toast(this.T("ui.pasted"));
            this.Preview();
        }

        /****
        ** Shared widgets
        ****/
        private void SmallButton(int x, int y, string icon, int w, int h, Action click, Action? right = null)
        {
            bool hover = this.Region(x, y, w, h, click, right);
            this.cv.Spr($"btn:{w}:{h}:milk:{hover}", () => Px.Button(w, h, "milk", hover ? "hover" : "normal"), x, y);
            (int iw, int ih) = Px.IconSize(icon);
            this.cv.Icon(icon, Ink, x + (w - iw) / 2, y + (h - 3 - ih) / 2 + 1);
        }

        /// <summary>Rounded pink ± button (steppers).</summary>
        private void StepButton(int x, int y, string icon, Action click)
        {
            bool hover = this.Region(x, y, 9, 9, click);
            this.cv.Spr($"step:{hover}", () => Px.Box(9, 9, 3, Px.C("rose2"), hover ? Px.C("pink") : Px.C("blush")), x, y);
            (int iw, int ih) = Px.IconSize(icon);
            this.cv.Icon(icon, Px.C("berry"), x + (9 - iw) / 2, y + (9 - ih) / 2);
        }

        /// <summary>A number in a white box; click it to type an exact value.</summary>
        private void ValueBox(string id, int x, int y, int w, string text, int value, int min, int max, Action<int> set)
        {
            bool editing = this.entry?.Id == id;
            bool hover = this.Region(x, y, w, 11, () => this.BeginEntry(id, value, min, max, set));
            Ramp acc = this.Acc;
            if (editing)
                this.cv.Spr($"vbox:{w}:edit:{acc.Hex}", () => Px.Box(w, 11, 3, acc.O, acc.H, White), x, y);
            else
                this.cv.Spr($"vbox:{w}:{hover}", () => Px.Box(w, 11, 3, hover ? Px.C("rose2") : Px.C("milk4"), White), x, y);
            string shown = editing ? this.entry!.Text : text;
            int tw = this.cv.Text(shown, x, y, Ink, align: Align.Center, w: w, dy: 6);
            if (editing && (int)(Now * 2) % 2 == 0)
                this.cv.Rect(x + (w * Z + tw) / 8 + 1, y + 2, 1, 7, acc.O);
        }

        /// <summary>Plain slider with a round knob (effects, master volume).</summary>
        private void KnobSlider(int x, int y, int w, int value, int max, Action<int> set, Ramp acc, bool centred = false, Action? released = null)
        {
            this.Region(x - 3, y - 1, w + 6, 9, drag: (ax, _, _) =>
            {
                set(Math.Clamp((int)Math.Round((ax - x) / w * max), 0, max));
            }, release: () =>
            {
                this.Commit();
                released?.Invoke();
            });
            this.cv.Spr($"track:{w}", () => Px.SliderTrack(w), x, y);
            int pos = x + (int)Math.Round(w * value / (float)max);
            if (centred)
            {
                int mid = x + w / 2;
                this.cv.Rect(mid, y - 1, 1, 9, Ink3);
                int lo = Math.Min(mid, pos), hi = Math.Max(mid, pos);
                if (hi - lo > 2)
                {
                    int fw = hi - lo + 3;
                    this.cv.Spr($"fill:{fw}:{acc.Hex}", () => Px.SliderFill(fw, acc), lo - 1, y);
                }
            }
            else
            {
                int fw = Math.Max(6, pos - x);
                this.cv.Spr($"fill:{fw}:{acc.Hex}", () => Px.SliderFill(fw, acc), x, y);
            }
            this.cv.Spr($"knob:{acc.Hex}", () => Px.Knob(acc), pos - 3, y);
        }

        private void ToggleSwitch(int x, int y, bool on, Action flip)
        {
            this.Region(x, y, 19, 10, flip);
            this.cv.Spr($"toggle:{on}", () => Px.Toggle(on), x, y);
        }

        /// <summary>Popover bubble with an optional tail pointing at its anchor. Clicks inside are swallowed.</summary>
        private void Bubble(int x, int y, int w, int h, int? tailX = null, bool tailUp = false)
        {
            this.Region(x, y, w, h);
            this.cv.Spr($"bubble:{w}:{h}", () => Px.Bubble(w, h), x, y);
            if (tailX is int tx)
                this.cv.Spr($"tail:{tailUp}", () => Px.BubbleTail(tailUp), tx, tailUp ? y - 3 : y + h - 1);
        }

        private void MorePill(int x, int y, bool active, Action click)
        {
            Ramp acc = this.Acc;
            bool hover = this.Region(x, y, 20, 11, click);
            string state = active ? "selected" : hover ? "hover" : "normal";
            this.cv.Spr($"pill:20:11:{state}:{acc.Hex}", () => Px.Pill(20, 11, state, acc), x, y);
            this.cv.Text(this.T("ui.more"), x, y, active ? White : Ink2, align: Align.Center, w: 20, dy: 6,
                shadow: active ? (0, 2, acc.O) : null);
        }

        private void DrawPopover()
        {
            switch (this.pop)
            {
                case Pop.More: this.DrawMorePopover(); break;
                case Pop.Tempo: this.DrawTempoPopover(); break;
                case Pop.Velocity: this.DrawVelocityPopover(); break;
                case Pop.Help: this.DrawHelpPopover(); break;
            }
        }

        private void TogglePop(Pop which)
        {
            this.pop = this.pop == which ? Pop.None : which;
            Game1.playSound("shwip");
        }
    }
}
