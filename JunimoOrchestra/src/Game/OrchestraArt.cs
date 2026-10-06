using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using JunimoOrchestra.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using SObject = StardewValley.Object;

namespace JunimoOrchestra.Game
{
    /// <summary>A block's notes as heard (seconds of game time), sorted by start. Notes starting together (a chord) are
    /// one stroke of the bow.</summary>
    internal sealed class PlayedNotes
    {
        public readonly double[] Starts, Ends;
        public readonly int[] Pitches, Strokes;

        /// <summary>How long a beat of its tempo is (seconds; 0 if it isn't known).</summary>
        public readonly double Beat;

        /// <summary>When its last note ends.</summary>
        public readonly double LastEnd;

        public PlayedNotes(double[] starts, double[] ends, int[] pitches, double beat = 0)
        {
            this.Starts = starts;
            this.Ends = ends;
            this.Pitches = pitches;
            this.Beat = beat;
            this.LastEnd = ends.Length > 0 ? ends.Max() : double.NegativeInfinity;
            this.Strokes = new int[starts.Length];
            for (int i = 1; i < starts.Length; i++)
                this.Strokes[i] = this.Strokes[i - 1] + (starts[i] - starts[i - 1] > 0.001 ? 1 : 0);
        }
    }

    /// <summary>What a block's notes are doing right now: the most recent note that has started (-1 before the first).</summary>
    internal readonly struct NoteState
    {
        public static readonly NoteState None = new(null, 0, -1);
        public readonly PlayedNotes? Notes;
        public readonly double Now;
        public readonly int Index;

        public NoteState(PlayedNotes? notes, double now, int index)
        {
            this.Notes = notes;
            this.Now = now;
            this.Index = index;
        }

        /// <summary>Seconds since the latest note started.</summary>
        public double Since => this.Index >= 0 ? this.Now - this.Notes!.Starts[this.Index] : 0;

        /// <summary>Whether a recent note is still ringing.</summary>
        public bool Sounding
        {
            get
            {
                for (int j = this.Index; j >= 0 && j > this.Index - 8; j--)
                    if (this.Notes!.Ends[j] > this.Now)
                        return true;
                return false;
            }
        }

        /// <summary>Seconds until the next note starts.</summary>
        public double UntilNext => this.Index >= 0 && this.Index + 1 < this.Notes!.Starts.Length
            ? this.Notes.Starts[this.Index + 1] - this.Now : double.MaxValue;

        /// <summary>A new note (or chord) is an accent (see <see cref="Accents"/>) if none came this long before (seconds).</summary>
        public const double AccentSeconds = 0.2;

        /// <summary>How long it takes to settle back into its breath after its last note (seconds), see <see cref="Busy"/>.</summary>
        public const double Settle = 0.6;

        /// <summary>How taken up it is with its playing right now (0-1): from its first note (coming in over a moment) until
        /// its last ends, settling over <see cref="Settle"/> after; 0 before its first note. It breathes as much as it
        /// isn't: while it plays it keeps still, as the playing is what's to be watched.</summary>
        public float Busy
        {
            get
            {
                if (this.Notes is not { } notes || this.Index < 0)
                    return 0f;
                double into = (this.Now - notes.Starts[0]) / 0.15, after = (this.Now - notes.LastEnd) / Settle;
                return (float)Math.Clamp(Math.Min(into, 1 - after), 0, 1);
            }
        }

        /// <summary>How far one waiting for its cue leans with its breath at most (radians): the least bit.</summary>
        public const float BreathSway = 0.01f;

        /// <summary>How far one waiting for its cue has risen with its breath (0-1) and which way it's swaying (-1 to 1)
        /// right now: slowly, each on a breath of its own (by its tile), so a row of them isn't a drill team.</summary>
        public static (float Rise, float Sway) Breath(int x, int y)
        {
            double now = Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0;
            int seed = ((x * 37 + y * 91) % 100 + 100) % 100;
            double t = now / (2.2 + seed % 5 * 0.12) + seed / 100.0;
            return ((float)(1 - Math.Cos(2 * Math.PI * t)) / 2f, (float)Math.Sin(2 * Math.PI * t));
        }

        /// <summary>When each block's accents come (seconds, as heard), worked out once per block.</summary>
        private static readonly ConditionalWeakTable<PlayedNotes, double[]> AccentTimes = new();

        /// <summary>When its accents come (seconds, as heard): its new notes or chords, but none within
        /// <see cref="AccentSeconds"/> of the last, so a quick run has them at its own pace (on every other eighth of the
        /// finale) instead of on every note. A wind player rocks its instrument with them.</summary>
        public double[] Accents => this.Notes != null ? AccentTimes.GetValue(this.Notes, AccentsOf) : Array.Empty<double>();

        private static double[] AccentsOf(PlayedNotes notes)
        {
            var accents = new List<double>();
            foreach (double start in notes.Starts)
                if (accents.Count == 0 || start - accents[^1] >= AccentSeconds - 1e-6)
                    accents.Add(start);
            return accents.ToArray();
        }

        /// <summary>Which stroke of the bow this is (0, 1, 2...: down-bow, up-bow, down-bow...).</summary>
        public int Stroke => this.Index >= 0 ? this.Notes!.Strokes[this.Index] : -1;

        /// <summary>How long the latest stroke's notes last (seconds), and the highest of their pitches.</summary>
        public (double Length, int Pitch) StrokeNotes
        {
            get
            {
                double length = 0;
                int pitch = 0;
                for (int j = this.Index; j >= 0 && this.Notes!.Strokes[j] == this.Notes.Strokes[this.Index]; j--)
                {
                    length = Math.Max(length, this.Notes.Ends[j] - this.Notes.Starts[j]);
                    pitch = Math.Max(pitch, this.Notes.Pitches[j]);
                }
                return (length, pitch);
            }
        }

        /// <summary>The latest note (index) matching a test, or -1.</summary>
        public int LatestWhere(Func<int, bool> pitchTest)
        {
            for (int j = this.Index; j >= 0; j--)
                if (pitchTest(this.Notes!.Pitches[j]))
                    return j;
            return -1;
        }
    }

    /// <summary>
    /// Junimo musicians for film sets: a block whose look is "junimo:RRGGBB[:instrument[:dx:dy]]" is drawn as a Junimo of that
    /// colour playing a real instrument (assets/textures/orchestra.png, made by tools/orchestra_sprites.py), in concert dress (a top
    /// hat and a red bow tie). It really plays: a string player draws the bow the whole length of each note (down-bow, up-bow)
    /// with its hand on the frog and its other hand moving along the neck with the pitch; a pianist's hands go to the keys it
    /// plays and press them; a harpist plucks the string for each note; drummers strike. The section sways
    /// together to the beat (its staging's "dance:P:O"); not staged, it keeps still while it plays, so the playing is what's
    /// watched, and breathes while it doesn't (<see cref="NoteState.Breath"/>). On long notes they close their eyes. After its "rest" the
    /// instrument blows away as leaves and it dances on the beat.
    /// </summary>
    internal static class OrchestraArt
    {
        private const int Cell = 32;

        /// <summary>Top of the concert dress row (16x20 cells: front, side, back; then two closed-eye Junimo frames).</summary>
        private const int DressY = 320;

        private enum Kind { Bowed, Low, Plucked, Keys, Drum, Held }

        /// <summary>Sheet rows (must match tools/orchestra_sprites.py), where the Junimo's feet go relative to the cell's
        /// (16, 31), how it's played, and whether the Junimo holds it (so it moves with the player).</summary>
        private static readonly Dictionary<string, (int Row, int Dx, int Dy, Kind Kind, bool Carried)> Instruments = new()
        {
            ["violin"] = (0, -3, 0, Kind.Bowed, true),
            ["viola"] = (1, -3, 0, Kind.Bowed, true),
            ["cello"] = (2, 4, 0, Kind.Low, true),
            ["bass"] = (3, -5, 0, Kind.Low, true),
            ["harp"] = (4, 6, 0, Kind.Plucked, false),
            ["piano"] = (5, -2, -9, Kind.Keys, false),
            ["timpani"] = (6, 0, -5, Kind.Drum, false),
            ["cymbals"] = (7, 0, 0, Kind.Held, true),
            ["shaker"] = (8, -2, 0, Kind.Held, true),
            ["congas"] = (9, 0, -6, Kind.Drum, false),
        };

        private static readonly Dictionary<string, (Color Tint, string? Instrument, Vector2 Offset)?> Looks = new();

        /// <summary>A string player leans into a new bow stroke this quickly, and back over this long (seconds).</summary>
        private const double StrokeAttack = 0.04, StrokeLean = 0.3;

        /// <summary>The Junimo musician item's look: coloured as the classical Junimo playing the same would be, playing the
        /// instrument nearest to the one the block plays (see <see cref="Musician"/>).</summary>
        public const string MusicianLook = "musician";

        /// <summary>A Junimo look as drawn: a "junimo:..." look as written, or <see cref="MusicianLook"/> worked out from the
        /// block. Null if the look is neither.</summary>
        public static (Color Tint, string? Instrument, Vector2 Offset)? Resolve(string? look, SObject obj, Family family)
        {
            if (look != MusicianLook)
                return Parse(look);
            var (bank, program, pitch) = BlockSettings.SoundOf(obj, family);
            return (ClassicalArt.Tint(ClassicalArt.CellFamily(family, bank, program)), Musician(bank, program, pitch), Vector2.Zero);
        }

        /// <summary>The instrument on the sheet nearest to a General MIDI one (a drum kit's by its first note), or null for a
        /// wind, a voice or a sound effect: that Junimo dances to its notes instead.</summary>
        public static string? Musician(int bank, int program, int pitch)
        {
            if (bank >= Families.DrumBank)
                return pitch switch { 49 or 52 or 55 or 57 => "cymbals", 54 or 69 or 70 or 82 => "shaker", _ => "congas" };
            return program switch
            {
                <= 8 or (>= 16 and <= 23) or (>= 80 and <= 103) => "piano",    // pianos, celesta, organs, synths
                (>= 11 and <= 13) or 47 or 116 => "timpani",                    // mallets, timpani, taiko
                (>= 24 and <= 31) or 46 or (>= 104 and <= 108) => "harp",       // guitars, harp, plucked strings
                (>= 32 and <= 39) or 43 => "bass",
                40 or 44 or 45 or (>= 48 and <= 51) or 110 => "violin",         // violin, string sections, fiddle
                41 => "viola",
                42 => "cello",
                55 or 119 => "cymbals",                                         // orchestra hit, reverse cymbal
                112 or 113 or 115 => "shaker",                                  // bell, agogo, woodblock
                114 or 117 or 118 => "congas",                                  // steel drums, toms
                _ => null,
            };
        }

        /// <summary>Parse a Junimo look, or null if the look isn't one. The offset (world pixels) lets a seat sit between tiles.</summary>
        public static (Color Tint, string? Instrument, Vector2 Offset)? Parse(string? look)
        {
            if (look == null || !look.StartsWith("junimo", StringComparison.Ordinal))
                return null;
            if (Looks.TryGetValue(look, out var cached))
                return cached;
            string[] f = look.Split(':');
            Color tint = new(120, 220, 120);
            if (f.Length > 1 && f[1].Length == 6 && int.TryParse(f[1], NumberStyles.HexNumber, null, out int rgb))
                tint = new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
            string? instrument = f.Length > 2 && Instruments.ContainsKey(f[2]) ? f[2] : null;
            Vector2 offset = Vector2.Zero;
            if (f.Length > 4 && int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dx)
                && int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dy))
                offset = new Vector2(Math.Clamp(dx, -48, 48), Math.Clamp(dy, -48, 48));
            var parsed = ((Color, string?, Vector2)?)(tint, instrument, offset);
            Looks[look] = parsed;
            return parsed;
        }

        /// <param name="hop">The block's note hop (pixels, negative = up), see <see cref="BlockVisuals.Sample"/>.</param>
        /// <param name="age">Game ticks since it was first played.</param>
        /// <param name="grow">Size while it pops in (0-1).</param>
        public static void Draw(SpriteBatch b, Texture2D sheet, int x, int y, Vector2 seat, float alpha, float layer, Color tint,
            string? instrument, NoteState note, float hop, long age, Staging stage, float grow)
        {
            Vector2 floor = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 60) + seat);
            float z = 4f * grow;
            bool still = stage.IsStill(age);
            long moveAge = stage.MoveAge(age);
            // its beat, staged (its dance); not staged, it breathes while it isn't playing
            bool staged = stage.Dance > 0;
            double beats = staged ? (moveAge - stage.DanceOffset) / stage.Dance : moveAge / 40.0;
            float moving = still || !staged ? 0f : 1f;
            float waiting = still || staged ? 0f : 1f - note.Busy;
            var breath = NoteState.Breath(x, y);
            int beat = (int)Math.Floor(beats);
            int beat4 = ((beat % 4) + 4) % 4;
            double phase = beats - beat;
            int seed = x * 37 + y * 91;
            bool blink = !still && ((stage.Timed ? age : Game1.ticks) + seed) % 197 < 7;

            b.Draw(Game1.shadowTexture, floor + new Vector2(0, -2), Game1.shadowTexture.Bounds, Color.White * alpha, 0f,
                new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), 3f * grow, SpriteEffects.None, layer - 1e-5f);

            if ((stage.Rest > 0 && age >= stage.Rest) || instrument == null)
            {
                // dancing on the beat: arms up and down, spinning round, or jumping for joy (and landing with a squash)
                int style = ((x * 5 + y * 3) % 3 + 3) % 3;
                int frame = 0;
                bool flip = seed % 2 == 1, joy = blink;
                float lift = hop * 1.2f, tilt = 0f, squash = 0f;
                if (stage.Rest > 0 && stage.Dance > 0 && !still)
                {
                    lift = -(float)Math.Sin(Math.PI * phase) * (style == 2 ? 22 : 12);
                    squash = (float)Math.Exp(-phase * 10);
                    frame = style switch { 0 => beat4 % 2 == 0 ? 44 : 0, 1 => new[] { 0, 16, 32, 16 }[beat4], _ => 44 };
                    flip = style == 1 ? beat4 == 3 : style == 0 && beat4 >= 2;
                    tilt = style == 0 ? (beat4 % 2 == 0 ? 0.12f : -0.12f) * (float)Math.Sin(Math.PI * phase) : 0f;
                    joy |= style == 2 && phase < 0.6;
                }
                else if (!staged && !still)                        // its breath
                    lift -= breath.Rise * 2f * waiting;
                var effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Vector2 scale = new Vector2(1 + 0.12f * squash, 1 - 0.15f * squash) * z;
                Junimo(b, sheet, frame, joy, floor + new Vector2(0, lift), scale, tilt, tint * alpha, effects, alpha, layer);
                return;
            }

            var (row, dx, dy, kind, carried) = Instruments[instrument];
            bool striking = note.Index >= 0 && note.Since < 0.16;
            bool hits = kind is Kind.Drum or Kind.Held;
            int column = hits && striking ? 1 + note.Index % 2 : 0;         // mallets strike, cymbals crash; the rest is drawn below
            int bowStroke = note.Stroke;

            // staged, the section sways together over two beats and string players lean into each new bow stroke; not
            // staged, only a touch into each stroke (the bow is what's to be watched), and the least bit with its breath
            float lean = kind switch { Kind.Bowed => 0.10f, Kind.Low => 0.05f, Kind.Plucked => 0.07f, Kind.Keys => 0.06f, _ => 0.04f }
                * (float)Math.Cos(Math.PI * beats) * moving + breath.Sway * NoteState.BreathSway * waiting;
            if (kind is Kind.Bowed or Kind.Low && bowStroke >= 0 && note.Since < StrokeAttack + StrokeLean)
                lean += (bowStroke % 2 == 0 ? 1f : -1f) * (kind == Kind.Bowed ? 0.07f : 0.04f) * (staged ? 1f : 0.35f)
                    * (float)(note.Since < StrokeAttack ? note.Since / StrokeAttack : 1 - (note.Since - StrokeAttack) / StrokeLean);
            // a little squash on every beat (staged), and its breath
            float pulse = (float)Math.Exp(-phase * 8) * moving;
            float bodyY = pulse * 2.5f - breath.Rise * 2f * waiting;
            Vector2 scaleJ = new Vector2(1 + 0.05f * pulse, 1 - 0.07f * pulse) * z;
            bool happy = blink || (!hits && note.Index >= 0 && note.Sounding && note.Since > 0.35 && note.UntilNext > 0.25);
            int jframe = kind == Kind.Held && striking ? 44 : 0;
            bool armless = kind is Kind.Bowed or Kind.Keys;                  // their hands are drawn on the instrument

            // held instruments move and sway with the player (a cello or bass pivots on its endpin); others stay put
            Vector2 cellAt = carried ? floor + Rotate(new Vector2(0, kind == Kind.Low ? 0f : bodyY), lean) : floor;
            var cell = new CellT(cellAt, carried ? lean : 0f, z);
            Vector2 feet = carried
                ? floor + Rotate(new Vector2(dx * z, dy * z + bodyY), lean)
                : floor + new Vector2(dx * z, dy * z + bodyY);

            b.Draw(sheet, cellAt, new Rectangle(column * Cell, row * Cell, Cell, Cell), Color.White * alpha, cell.Rot,
                new Vector2(16, 32), z, SpriteEffects.None, layer - 2e-6f);
            Junimo(b, sheet, jframe, happy, feet, scaleJ, lean, tint * alpha, SpriteEffects.None, alpha, layer, armless);
            b.Draw(sheet, cellAt, new Rectangle((3 + column) * Cell, row * Cell, Cell, Cell), Color.White * alpha, cell.Rot,
                new Vector2(16, 32), z, SpriteEffects.None, layer + 2e-6f);

            int shoulderLift = (int)Math.Round(bodyY / z);
            if (Bows.TryGetValue(instrument, out int bowRow))
                Bowing(b, sheet, cell, bowRow, note, tint, alpha, layer + 3e-6f);
            else if (kind == Kind.Keys)
                PianoHands(b, cell, note, tint, alpha, new Point(8 + dx, 16 + dy + shoulderLift), layer + 3e-6f);
            else if (kind == Kind.Plucked)
                HarpHand(b, cell, note, tint, alpha, new Point(8 + dx, 16 + dy + shoulderLift), layer + 3e-6f);
        }

        /****
        ** Playing
        ****/
        /// <summary>Rows of <see cref="BowedY"/> for each bowed instrument (bow, then the hand on it); violin and viola also have
        /// a row of fingering hands (<see cref="BowedY"/> + 8 and 9 rows).</summary>
        private static readonly Dictionary<string, int> Bows = new() { ["violin"] = 0, ["viola"] = 1, ["cello"] = 2, ["bass"] = 3 };
        private const int BowedY = DressY + 32, BowFrames = 9, FingerFrames = 5;

        /// <summary>A stroke of the bow takes this long at least where the next note allows (seconds).</summary>
        private const double FullBow = 0.35;

        /// <summary>Bow and hands: each note (or chord) is one whole stroke of the bow, down-bow then up-bow, as long as the
        /// note lasts (a short one's stroke runs on to <see cref="FullBow"/>, or to the next note), and the bow rests
        /// where it stopped; the fingering hand moves along the neck with the pitch.</summary>
        private static void Bowing(SpriteBatch b, Texture2D sheet, CellT cell, int bowRow, NoteState note, Color tint, float alpha, float layer)
        {
            double p = 0;
            int pitch = -1;
            if (note.Stroke >= 0)
            {
                // a whole bow on every stroke: as long as its notes and at least FullBow, but done by the next stroke, so a
                // quick run saws away from end to end and the last of them is played right out
                (double length, int top) = note.StrokeNotes;
                double span = Math.Max(0.06, Math.Min(Math.Max(length, FullBow), note.Since + note.UntilNext));
                double q = Math.Clamp(note.Since / span, 0, 1);
                p = note.Stroke % 2 == 0 ? q : 1 - q;
                pitch = top;
            }
            int k = (int)Math.Round(p * (BowFrames - 1));
            if (bowRow < 2)
            {
                int low = bowRow == 0 ? 55 : 48;
                double f = pitch < 0 ? 0.3 : Math.Clamp((pitch - low) / 33.0, 0, 1);
                Layer(b, sheet, cell, (int)Math.Round(f * (FingerFrames - 1)), BowedY + (8 + bowRow) * Cell, tint * alpha, layer);
            }
            Layer(b, sheet, cell, k, BowedY + bowRow * 2 * Cell, Color.White * alpha, layer + 1e-6f);
            Layer(b, sheet, cell, k, BowedY + (bowRow * 2 + 1) * Cell, tint * alpha, layer + 2e-6f);
        }

        /// <summary>Two hands on the keyboard (cell row 23, keys x 5-17): each goes to its latest note (the left hand below
        /// middle C) and presses it, and the keys that are sounding stay down.</summary>
        private static void PianoHands(SpriteBatch b, CellT cell, NoteState note, Color tint, float alpha, Point junimo, float layer)
        {
            static int KeyX(int pitch) => 5 + (int)Math.Round(Math.Clamp((pitch - 36) / 60.0, 0, 1) * 11);
            if (note.Index >= 0)
            {
                for (int j = note.Index; j >= 0 && j > note.Index - 12; j--)
                {
                    if (note.Notes!.Ends[j] > note.Now)
                    {
                        int kx = KeyX(note.Notes.Pitches[j]);
                        Pixel(b, cell, kx, 23, KeyDown * alpha, layer);
                        Pixel(b, cell, kx, 24, KeyDown * alpha, layer);
                    }
                }
            }
            for (int hand = 0; hand < 2; hand++)
            {
                int j = note.Index < 0 ? -1 : note.LatestWhere(hand == 0 ? p => p < 60 : p => p >= 60);
                int hx = j >= 0 ? KeyX(note.Notes!.Pitches[j]) : hand == 0 ? 7 : 14;
                bool press = j >= 0 && note.Now - note.Notes!.Starts[j] < 0.15;
                int hy = 21 + (press ? 1 : 0);
                Hand(b, cell, new Point(junimo.X + (hand == 0 ? 1 : 14), junimo.Y + 10), new Point(hx, hy), tint, alpha, layer + 1e-6f);
            }
        }

        private static readonly int[] HarpX = { 5, 7, 9, 11, 13 }, HarpMid = { 19, 17, 15, 14, 12 };

        /// <summary>A hand plucking the string for the latest note (low notes on the long strings), pulling through it, and
        /// the string shimmering while it rings.</summary>
        private static void HarpHand(SpriteBatch b, CellT cell, NoteState note, Color tint, float alpha, Point junimo, float layer)
        {
            int s = 2;
            if (note.Index >= 0)
                s = (int)Math.Round(Math.Clamp((note.StrokeNotes.Pitch - 40) / 30.0, 0, 1) * 4);
            bool plucking = note.Index >= 0 && note.Since < 0.15;
            if (note.Index >= 0 && note.Sounding && note.Since < 0.6)
            {
                int side = (int)(note.Since * 40) % 2 == 0 ? 1 : -1;
                for (int yy = HarpMid[s] - 3; yy <= HarpMid[s] + 3; yy++)
                    Pixel(b, cell, HarpX[s] + side, yy, StringColor * (alpha * 0.8f), layer);
            }
            Hand(b, cell, new Point(junimo.X, junimo.Y + 9), new Point(HarpX[s] + (plucking ? 1 : 2), HarpMid[s]), tint, alpha, layer + 1e-6f);
        }

        /****
        ** Drawing helpers
        ****/
        private static readonly Color KeyDown = new(180, 170, 150), StringColor = new(239, 227, 196);

        /// <summary>Where an instrument cell is drawn: its bottom centre (16, 32) at <see cref="At"/>, turned and scaled.</summary>
        private readonly record struct CellT(Vector2 At, float Rot, float Z)
        {
            public Vector2 Map(float px, float py) => this.At + Rotate(new Vector2((px - 16) * this.Z, (py - 32) * this.Z), this.Rot);
        }

        private static void Layer(SpriteBatch b, Texture2D sheet, CellT cell, int column, int top, Color color, float layer)
        {
            b.Draw(sheet, cell.At, new Rectangle(column * Cell, top, Cell, Cell), color, cell.Rot, new Vector2(16, 32), cell.Z, SpriteEffects.None, layer);
        }

        private static void Pixel(SpriteBatch b, CellT cell, int px, int py, Color color, float layer)
        {
            b.Draw(Game1.staminaRect, cell.Map(px, py), null, color, cell.Rot, Vector2.Zero, cell.Z, SpriteEffects.None, layer);
        }

        /// <summary>An arm (a line in the Junimo's outline colour) from its shoulder to a little round mitten.</summary>
        private static void Hand(SpriteBatch b, CellT cell, Point shoulder, Point hand, Color tint, float alpha, float layer)
        {
            Color line = Shade(tint, 55, 55, 81) * alpha, body = Shade(tint, 196, 206, 206) * alpha;
            int x0 = shoulder.X, y0 = shoulder.Y, x1 = hand.X, y1 = hand.Y;
            int ddx = Math.Abs(x1 - x0), ddy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = ddx + ddy;
            while (x0 != x1 || y0 != y1)
            {
                Pixel(b, cell, x0, y0, line, layer);
                int e2 = 2 * err;
                if (e2 >= ddy) { err += ddy; x0 += sx; }
                if (e2 <= ddx) { err += ddx; y0 += sy; }
            }
            foreach (var (mx, my) in new[] { (-1, 0), (-1, 1), (2, 0), (2, 1), (0, -1), (1, -1), (0, 2), (1, 2) })
                Pixel(b, cell, hand.X + mx, hand.Y + my, line, layer + 1e-7f);
            for (int mx = 0; mx < 2; mx++)
                for (int my = 0; my < 2; my++)
                    Pixel(b, cell, hand.X + mx, hand.Y + my, body, layer + 2e-7f);
        }

        private static Color Shade(Color tint, int r, int g, int b) => new(tint.R * r / 255, tint.G * g / 255, tint.B * b / 255);

        /// <summary>A tinted Junimo (frames 0, 16, 32 or 44; "happy" = eyes closed; "armless" = its hands are drawn elsewhere) in
        /// concert dress, feet at <paramref name="feet"/>.</summary>
        private static void Junimo(SpriteBatch b, Texture2D sheet, int frame, bool happy, Vector2 feet, Vector2 scale, float rotation,
            Color tint, SpriteEffects effects, float alpha, float layer, bool armless = false)
        {
            if (armless && frame == 0)
                b.Draw(sheet, feet, new Rectangle(happy ? 96 : 80, DressY + 4, 16, 16), tint, rotation, new Vector2(8, 16), scale, effects, layer);
            else if (happy && frame is 0 or 44)
                b.Draw(sheet, feet, new Rectangle(48 + (frame == 44 ? 16 : 0), DressY + 4, 16, 16), tint, rotation, new Vector2(8, 16), scale, effects, layer);
            else
                b.Draw(Game1.content.Load<Texture2D>("Characters\\Junimo"), feet, new Rectangle(frame % 8 * 16, frame / 8 * 16, 16, 16), tint,
                    rotation, new Vector2(8, 16), scale, effects, layer);
            int view = frame switch { 16 => 1, 32 => 2, _ => 0 };
            b.Draw(sheet, feet, new Rectangle(view * 16, DressY, 16, 20), Color.White * alpha, rotation, new Vector2(8, 20), scale, effects, layer + 1e-6f);
        }

        private static Vector2 Rotate(Vector2 v, float angle)
        {
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>A Junimo star (the game's own, spinning) popping up over a Junimo and floating away as it fades.</summary>
        public static void Star(GameLocation location, Vector2 tile, int delayMs)
        {
            location.temporarySprites.Add(new TemporaryAnimatedSprite("Characters\\Junimo", new Rectangle(0, 112, 16, 16), 60f, 8, 2,
                tile * 64f + new Vector2(12, -88), false, false, (tile.Y * 64 + 96) / 10000f, 0.012f, Color.White, 2.5f, 0f, 0f, 0f)
            {
                motion = new Vector2(0, -1.1f),
                acceleration = new Vector2(0, 0.012f),
                delayBeforeAnimationStart = delayMs
            });
        }

        /// <summary>Autumn leaves swirling in to make an instrument (converge) or blowing away from it (scatter).</summary>
        public static void Leaves(GameLocation location, Vector2 tile, bool converge, int delayMs)
        {
            Vector2 centre = tile * 64f + new Vector2(24, -8);
            for (int i = 0; i < 9; i++)
            {
                double angle = i * Math.PI * 2 / 9 + Game1.random.NextDouble() * 0.5;
                Vector2 dir = new((float)Math.Cos(angle), (float)Math.Sin(angle) * 0.6f);
                Vector2 start = converge ? centre + dir * 70f : centre + dir * 8f;
                Vector2 motion = converge ? -dir * 3.2f + new Vector2(-dir.Y, dir.X) * 2.2f : dir * 2.4f + new Vector2(1.6f, -0.8f);
                var leaf = new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(352, 1216, 16, 16), 90f, 4, 3,
                    start, false, Game1.random.NextDouble() < 0.5, (tile.Y * 64 + 90) / 10000f, converge ? 0.02f : 0.012f,
                    Color.White, 3f, 0f, 0f, 0f)
                {
                    motion = motion,
                    acceleration = converge ? -motion * 0.04f : new Vector2(0.03f, 0.05f),
                    delayBeforeAnimationStart = delayMs + i * 12
                };
                location.temporarySprites.Add(leaf);
            }
            location.temporarySprites.Add(new TemporaryAnimatedSprite("Characters\\Junimo", new Rectangle(0, 112, 16, 16), 45f, 8, 0,
                centre + new Vector2(-8, -8), false, false, (tile.Y * 64 + 92) / 10000f, 0.02f, Color.White, 2.5f, 0f, 0f, 0f)
            {
                delayBeforeAnimationStart = delayMs + (converge ? 200 : 0)
            });
        }
    }
}
