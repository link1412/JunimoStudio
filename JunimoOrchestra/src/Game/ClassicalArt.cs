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
    /// <summary>
    /// The classical look for blocks (assets/textures/junimo_classical.png, made by art/junimo_classical.py): a Junimo in concert
    /// dress playing its family's most representative classical instrument. A block whose look is "classical" is drawn as
    /// its own family's; "classical:&lt;family key&gt;" as another family's (a string ensemble as a violinist, say), and
    /// "classical:&lt;family key&gt;:dx:dy" moves it that many of the art's own pixels off its tile (up to 12), so a
    /// film set can seat an orchestra on curved rows. The sheet has a 32x32 cell per family (column = Family.Index) and
    /// frame (row): idle, blink, A, B, long note (and further rows in a few columns: the conductor's bow, the bowed
    /// strings' bow on its way). A cell is drawn with its bottom centre on the tile's, spilling into the neighbours, and
    /// a row is drawn left to right (the art keeps what a block spills to its left off its neighbour's face). It moves
    /// only as it plays its own notes: A and B in turn on each new note (or chord), held while it sounds, and on a long
    /// note it shuts its eyes and savours it; a violin's or a double bass's bow travels the whole time a note sounds
    /// instead (<see cref="BowFrame"/>). One that holds its instrument (the violin, lute, horn, clarinet, flute, sitar,
    /// cymbals) keeps still while it plays and breathes while it doesn't (<see cref="Motion"/>); one at an instrument on
    /// the floor only plays. The winds
    /// blow with all they have (eyes squeezed, cheeks red, sweat on the brow: the art's), the instrument swung with
    /// their notes and raised to the sky after a phrase (<see cref="Swing"/>). No flash, no notes floating up: the
    /// instrument being played is the effect. The clarinettist and the flautist sit on a stool (row 5 of their columns),
    /// which stays put as they bounce. The conductor (songbird's cell) beats time instead, when staged to
    /// ("conduct:A:B"), and lifts its top hat to us at the end ("bow:T").
    /// A film set can bring a staged one to life on the music's beat ("dance:P:O", see <see cref="Motion"/>).
    /// </summary>
    internal static class ClassicalArt
    {
        private const int Cell = 32;
        private const int Idle = 0, Blink = 1, PlayA = 2, PlayB = 3, LongNote = 4;

        /// <summary>The conductor's further row: its bow.</summary>
        private const int Bow = 5;

        /// <summary>The seated winds' further row: the stool, drawn under them.</summary>
        private const int Stool = 5;

        /// <summary>Whether it holds its instrument (the violin, lute, horn, clarinet, flute, sitar, cymbals), so it can
        /// breathe with it; those at an instrument on the floor (a piano, organ, tubular bells, double bass, harp,
        /// theremin, glass harp, timpani, the singer's music stand, the conductor's podium) would move it too.</summary>
        private static bool Holds(Family family)
            => family.Key is "violin" or "guitar" or "trumpet" or "kalimba" or "sax" or "ocarina" or "drumkit";

        private static bool Seated(Family family) => family.Key is "sax" or "ocarina";

        /// <summary>Where the Junimo stands in each family's cell (the top of its 16x16 frame, art/junimo_classical.py's
        /// Frame.dy).</summary>
        private static readonly int[] JunimoY = { 7, 16, 8, 8, 16, 16, 16, 16, 7, 7, 9, 16, 2, 16, 8, 11, 16 };

        /// <summary>What it turns about when it leans or swings (pixels of its cell): its seat, or its feet.</summary>
        private static Vector2 Pivot(Family family)
            => new(JunimoX[family.Index] + 8, Seated(family) ? JunimoY[family.Index] + 16 : Cell);

        /// <summary>The winds, who swing their instruments as they play (radians, about the seat or the feet; negative
        /// lifts the far end: the flute's end, the clarinet's bell, the horn's bell): how far they rock with each of
        /// their notes, and how high they lift it to the sky through a long note and after a phrase.</summary>
        private static readonly Dictionary<string, (float Rock, float Sky)> Winds = new()
        {
            ["ocarina"] = (0.05f, -0.32f),
            ["sax"] = (0.05f, -0.22f),
            ["trumpet"] = (0.04f, -0.14f),
        };

        /// <summary>A phrase's end raises a wind player's instrument to the sky for this long at least, where the rest
        /// after it allows (seconds): up, held a moment, and down.</summary>
        private const double SkyHold = 0.8;


        /// <summary>A short note's (a drum's) stroke shows at least this long (seconds).</summary>
        private const double Hit = 0.2;

        /// <summary>Notes at least this long are savoured once their attack is over (seconds).</summary>
        private const double Savour = 0.6, Attack = 0.25;

        /// <summary>The bowed strings' further rows (art/junimo_classical.py EXTRA), from row 5: the bow at each position on
        /// its way across the strings, through a down-bow, an up-bow (its other hand's fingers elsewhere) and a long note
        /// (scroll raised, eyes happy) for the violin; through any stroke and a long note for the double bass.</summary>
        private const int BowWay = 5;
        private static readonly Dictionary<string, (int Positions, int Up, int Long)> BowRows = new()
        {
            ["violin"] = (9, 9, 18),
            ["bass"] = (7, 0, 7),
        };

        /// <summary>A whole bow takes this long on a note at least that long; a shorter note goes its share of the way
        /// (seconds), and the quickest notes still a third of it, so a run of them saws away.</summary>
        private const double WholeBow = 1.2, ShortestBow = 1 / 3.0;

        /// <summary>A note longer than this changes bow on the way, a whole bow each time (seconds).</summary>
        private const double LongestBow = 3.0;

        /// <summary>After a rest this long the bow starts afresh, a down-bow from the frog (seconds).</summary>
        private const double Retake = 0.5;

        /// <summary>Where each stroke of a block's notes starts its bow (0 the frog's end of the way, 1 the tip's) and
        /// which way it goes, worked out once per block.</summary>
        private static readonly ConditionalWeakTable<PlayedNotes, Bowing> Bowings = new();

        private sealed record Bowing(double[] From, bool[] Down);

        /// <summary>Where the Junimo stands in each family's cell (the left edge of its 16x16 frame, art/junimo_classical.py's
        /// Frame.dx), so it dances on the same spot once its instrument is put away.</summary>
        private static readonly int[] JunimoX = { 2, 2, 8, 4, 2, 5, 5, 2, 4, 3, 8, 2, 8, 2, 8, 8, 8 };

        private static readonly Dictionary<string, (Family? Family, Vector2 Offset)> Looks = new();

        public static bool IsLook(string? look) => look != null && (look == "classical" || look.StartsWith("classical:", StringComparison.Ordinal));

        /// <summary>The family whose cell a block with this look is drawn as and how far it's moved off its tile (world
        /// pixels), or null if the look isn't classical.</summary>
        public static (Family Family, Vector2 Offset)? Parse(string? look, Family own)
        {
            if (!IsLook(look))
                return null;
            if (!Looks.TryGetValue(look!, out var parsed))
            {
                string[] f = look!.Split(':');
                Family? family = f.Length > 1 ? Families.All.FirstOrDefault(x => x.Key == f[1]) : null;
                Vector2 offset = Vector2.Zero;
                if (f.Length > 3 && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dx)
                    && int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dy))
                    offset = new Vector2(Math.Clamp(dx, -12, 12), Math.Clamp(dy, -12, 12)) * 4;
                Looks[look!] = parsed = (family, offset);
            }
            return (parsed.Family ?? own, parsed.Offset);
        }

        /// <summary>The cell a block with this look is drawn as: a "classical" look's family's own, or the cell nearest to
        /// what it plays where its family's isn't (a double bass for a cello or contrabass, the harp for the orchestral harp,
        /// the timpani for the timpani: the strings family has all four). As <see cref="Parse"/> otherwise.</summary>
        public static (Family Family, Vector2 Offset)? Resolve(string? look, SObject obj, Family own)
        {
            if (look != "classical")
                return Parse(look, own);
            var (bank, program, _) = BlockSettings.SoundOf(obj, own);
            return (CellFamily(own, bank, program), Vector2.Zero);
        }

        /// <summary>The family whose cell is nearest to what a block plays (see <see cref="Resolve"/>).</summary>
        public static Family CellFamily(Family own, int bank, int program)
        {
            string? key = bank >= Families.DrumBank ? null : program switch { 42 or 43 => "bass", 46 => "cloud", 47 => "steeldrum", _ => null };
            return key != null ? Families.All.First(f => f.Key == key) : own;
        }

        /// <summary>The Junimo's colour in a family's cell (the accent lifted towards white, as the art tints it).</summary>
        public static Color Tint(Family family)
        {
            static int Lift(int c) => (int)Math.Round(c + (255 - c) * 0.3);
            return new Color(Lift(family.Accent.R), Lift(family.Accent.G), Lift(family.Accent.B));
        }

        /// <summary>Where its Junimo dances once the instrument is put away (world pixels from the tile's floor): where it
        /// stood, back on the ground.</summary>
        public static Vector2 Seat(Family family) => new((JunimoX[family.Index] - 8) * 4, 0);

        /// <summary>The frame for what its notes are doing right now. Staged ("ready"), a cymbal player lifts its cymbals
        /// the half second before a crash that comes after a rest.</summary>
        public static int FrameOf(NoteState note, bool blink, Family? family = null, bool ready = false)
        {
            if (note.Index >= 0 && (note.Sounding || note.Since < Hit))
            {
                double length = note.StrokeNotes.Length;
                bool savour = length >= Savour && note.Since >= Attack && note.Since < length;
                if (family != null && BowRows.ContainsKey(family.Key))
                    return BowFrame(note, family.Key, length, savour);
                if (savour)
                    return LongNote;
                return note.Stroke % 2 == 0 ? PlayA : PlayB;
            }
            if (ready && family?.Key == "drumkit" && note.Notes is { } notes)
            {
                int next = note.Index + 1;
                double rest = note.Index >= 0 ? notes.Starts[next < notes.Starts.Length ? next : note.Index] - notes.Ends[note.Index] : 1;
                if (next < notes.Starts.Length && notes.Starts[next] - note.Now < 0.5 && rest > 0.6)
                    return notes.Strokes[next] % 2 == 0 ? PlayB : PlayA;     // the other way from the crash to come
            }
            return blink ? Blink : Idle;
        }

        /// <summary>A bowed string's frame: its bow keeps travelling the whole time a note sounds, as a player's does (a
        /// held note is a slow bow, not a held pose), a down-bow and an up-bow in turn.</summary>
        private static int BowFrame(NoteState note, string family, double length, bool savour)
        {
            Bowing bowing = Bowings.GetValue(note.Notes!, BowingOf);
            int stroke = note.Stroke;
            (double at, bool down) = BowAt(bowing.From[stroke], bowing.Down[stroke], length, Math.Min(note.Since, length));
            var rows = BowRows[family];
            return BowWay + (savour ? rows.Long : down ? 0 : rows.Up) + (int)Math.Round(at * (rows.Positions - 1));
        }

        /// <summary>Each stroke's bow, from the last one's end: down and up in turn, a fresh down-bow after a rest.</summary>
        private static Bowing BowingOf(PlayedNotes notes)
        {
            int count = notes.Strokes.Length > 0 ? notes.Strokes[^1] + 1 : 0;
            var bowing = new Bowing(new double[count], new bool[count]);
            double at = 0, heard = double.NegativeInfinity;
            bool down = true;
            for (int i = 0; i < notes.Starts.Length;)
            {
                int stroke = notes.Strokes[i];
                double start = notes.Starts[i], length = 0, end = start;
                for (; i < notes.Starts.Length && notes.Strokes[i] == stroke; i++)
                {
                    length = Math.Max(length, notes.Ends[i] - notes.Starts[i]);
                    end = Math.Max(end, notes.Ends[i]);
                }
                if (start - heard >= Retake)
                    (at, down) = (0, true);
                bowing.From[stroke] = at;
                bowing.Down[stroke] = down;
                (at, down) = BowAt(at, down, length, length);
                down = !down;
                heard = Math.Max(heard, end);
            }
            return bowing;
        }

        /// <summary>Where the bow is (0 the frog's end of its way, 1 the tip's) and which way it's going, `since` seconds
        /// into a stroke that starts at `from` going down (towards the tip) or up, its notes `length` long. A note shorter
        /// than a whole bow goes its share of the way (a third at least); a longer one goes to the end of the way, and one
        /// longer than the longest bow changes bow on the way, a whole bow each time, each bow as long.</summary>
        private static (double At, bool Down) BowAt(double from, bool down, double length, double since)
        {
            if (length < WholeBow)
            {
                double way = Math.Max(length / WholeBow, ShortestBow) * (since / Math.Max(length, 1e-3));
                return (Math.Clamp(down ? from + way : from - way, 0, 1), down);
            }
            int bows = (int)Math.Ceiling(length / LongestBow);
            double each = length / bows;
            int k = Math.Min(bows - 1, (int)(since / each));
            double u = Math.Clamp((since - k * each) / each, 0, 1);
            bool going = k % 2 == 0 ? down : !down;
            double start = k == 0 ? from : going ? 0 : 1, to = going ? 1 : 0;
            return (start + (to - start) * u, going);
        }

        /// <summary>How a staged Junimo moves with the music, on its staging's beat ("dance:P:O"), as players do while they
        /// play and while they count their rests: it breathes on every beat (a little dip), sways with its section over
        /// two beats (each seat's offset makes the wave along the rows), and a string player leans into each stroke; a
        /// cymbal player jumps on a crash after a rest; the harp's cloud floats. Players at the piano or the timpani and
        /// the conductor (a podium) only play: they sit at their instruments. Nothing moves where it holds still; on a
        /// "star" it jumps for joy (a held instrument and all). Staged or not, a wind player swings its instrument
        /// (<see cref="Swing"/>). Not staged, it keeps still while it plays (the playing is what's watched), and one
        /// holding its instrument breathes while it doesn't (<see cref="NoteState.Breath"/>).
        /// Returns the lean (radians, about its seat or feet, <see cref="Pivot"/>), the lift (world pixels, negative =
        /// up) and the squash.</summary>
        public static (float Lean, float Lift, Vector2 Squash) Motion(Family family, Staging stage, long age, NoteState note, int x, int y)
        {
            if (stage.IsStill(age))
                return (0f, 0f, Vector2.One);
            float swing = Swing(family, note);
            if (stage.Dance <= 0)
            {
                // still while it plays; its breath while it doesn't
                if (!Holds(family))
                    return (0f, 0f, Vector2.One);
                var breath = NoteState.Breath(x, y);
                float waiting = 1f - note.Busy;
                float rock = NoteState.BreathSway * breath.Sway * waiting + StrokeLean(family, note);
                return (swing + Math.Clamp(rock, -0.06f, 0.06f), -breath.Rise * 2f * waiting, Vector2.One);
            }
            double beats = (stage.MoveAge(age) - stage.DanceOffset) / stage.Dance;
            float pulse = (float)Math.Exp(-(beats - Math.Floor(beats)) * 8);
            float joy = 0f;
            foreach (int at in stage.Stars)
                if (age >= at && age < at + 24)
                    joy = -(float)Math.Sin(Math.PI * (age - at) / 24.0) * 16f;
            switch (family.Key)
            {
                case "cloud":
                    return (0f, -(float)(1 - Math.Cos(Math.PI * beats / 2)) * 3f + joy, Vector2.One);
                case "piano" or "organ" or "steeldrum" or "bells" or "songbird" or "stardust":
                    return (0f, 0f, Vector2.One);
            }
            float sway = family.Key switch { "violin" => 0.045f, "guitar" or "sax" => 0.04f, "bass" => 0.03f, "choir" => 0.02f, _ => 0.025f };
            float lean = sway * (float)Math.Cos(Math.PI * beats) + StrokeLean(family, note);
            float lift = joy;
            if (family.Key == "drumkit" && note.Index >= 0 && note.Since < 0.3
                && (note.Index == 0 || note.Notes!.Starts[note.Index] - note.Notes.Starts[note.Index - 1] > 0.45))
                lift -= (float)Math.Sin(Math.PI * note.Since / 0.3) * 10f;
            float dip = family.Key == "choir" ? 0.6f : 1f;
            return (swing + Math.Clamp(lean, -0.06f, 0.06f), lift, new Vector2(1 + 0.03f * pulse * dip, 1 - 0.05f * pulse * dip));
        }

        /// <summary>A bowed string player leaning into each new stroke of its bow, one way on a down-bow and the other on
        /// an up-bow.</summary>
        private static float StrokeLean(Family family, NoteState note)
            => family.Key is "violin" or "bass" && note.Stroke >= 0 && note.Since < 0.3
                ? (note.Stroke % 2 == 0 ? 0.04f : -0.04f) * (float)(1 - note.Since / 0.3) : 0f;

        /// <summary>How far a wind player has swung its instrument (radians, see <see cref="Winds"/>): it rocks it down
        /// and up in turn with its notes (<see cref="NoteState.Accents"/>), and lifts it to the sky through a long note and when a phrase ends
        /// (its last note, or one with a rest of a beat or more after it), holding it there a moment and bringing it down
        /// before the next. 0 for the rest.</summary>
        private static float Swing(Family family, NoteState note)
        {
            if (!Winds.TryGetValue(family.Key, out var wind) || note.Index < 0)
                return 0f;
            PlayedNotes notes = note.Notes!;
            static float Ease(double u) => (float)(0.5 - 0.5 * Math.Cos(Math.PI * Math.Clamp(u, 0, 1)));

            // to the sky: through a long note once its attack is over, and when a phrase ends
            float sky = 0f;
            double length = note.StrokeNotes.Length, start = notes.Starts[note.Index];
            if (length >= Savour && note.Now < start + length)
                sky = Ease((note.Since - Attack) / 0.3);
            double end = start + length;
            double next = note.Index + 1 < notes.Starts.Length ? notes.Starts[note.Index + 1] : double.PositiveInfinity;
            double beat = notes.Beat > 0 ? notes.Beat : 0.5;
            if (note.Now >= end && next - end >= beat)
            {
                double over = Math.Min(next - end, Math.Max(1.6 * beat, SkyHold)), after = note.Now - end;
                sky = Math.Max(sky, after < 0.12 ? Ease(after / 0.12) : 1 - Ease((after - 0.4 * over) / (0.6 * over)));
            }

            // with each accent, down and up in turn
            double[] hops = note.Accents;
            int i = Array.BinarySearch(hops, note.Now);
            if (i < 0)
                i = ~i - 1;
            float rock = 0f;
            if (i >= 0 && note.Now - hops[i] < 0.3)
                rock = (i % 2 == 0 ? 1f : -1f) * wind.Rock * (float)Math.Sin(Math.PI * (note.Now - hops[i]) / 0.3);
            return wind.Sky * sky + rock * (1 - sky);
        }

        /// <summary>A conductor's frame (<see cref="Staging.Conduct"/>, on its dance's beat): the baton raised the beat
        /// before a stretch, down on every beat and up between, held high where it holds still and after the last stretch,
        /// and its bow at the end (<see cref="Staging.Bow"/>); null when it isn't conducting.</summary>
        public static int? ConductFrame(Staging stage, long age)
        {
            if (stage.Bow > 0 && age >= stage.Bow)
                return Bow;
            if (stage.Conduct.Count == 0 || stage.Dance <= 0)
                return null;
            foreach ((int from, int to) in stage.Conduct)
            {
                if (age >= from - stage.Dance && age < from)
                    return LongNote;
                if (age >= from && age < to)
                {
                    if (stage.IsStill(age))
                        return LongNote;
                    double beats = (age - stage.DanceOffset) / stage.Dance;
                    return beats - Math.Floor(beats) < 0.5 ? PlayB : PlayA;
                }
            }
            return age >= stage.Conduct.Max(c => c.To) ? LongNote : null;
        }

        /// <param name="offset">How far it's moved off its tile (world pixels).</param>
        /// <param name="grow">Size while it pops in (0-1).</param>
        /// <param name="lean">Its lean (radians, about its seat or feet), lift (world pixels, negative = up) and squash, see <see cref="Motion"/>.</param>
        public static void Draw(SpriteBatch b, Texture2D sheet, Family family, int x, int y, Vector2 offset, float alpha, float layer, int frame, float grow,
            float lean = 0f, float lift = 0f, Vector2? squash = null)
        {
            Vector2 floor = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 64) + offset);
            bool seated = Seated(family);
            float shadow = 3f * grow * (1 + (seated ? 0f : Math.Max(-0.3f, lift / 60f)));
            b.Draw(Game1.shadowTexture, floor + new Vector2(0, -6), Game1.shadowTexture.Bounds, Color.White * alpha, 0f,
                new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), shadow, SpriteEffects.None, layer - 1e-5f);
            if (seated)                                           // its stool, on the floor whatever it does
                b.Draw(sheet, floor, new Rectangle(family.Index * Cell, Stool * Cell, Cell, Cell), Color.White * alpha, 0f,
                    new Vector2(Cell / 2f, Cell), 4f * grow, SpriteEffects.None, layer + x * 1e-7f - 2e-6f);
            // a row left to right: its right neighbour is drawn over it
            Vector2 pivot = Pivot(family);
            b.Draw(sheet, floor + (pivot - new Vector2(Cell / 2f, Cell)) * 4f * grow + new Vector2(0, lift),
                new Rectangle(family.Index * Cell, frame * Cell, Cell, Cell), Color.White * alpha, lean,
                pivot, 4f * grow * (squash ?? Vector2.One), SpriteEffects.None, layer + x * 1e-7f);
        }
    }
}
