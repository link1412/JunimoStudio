using System;
using JunimoOrchestra.UI;
using JunimoOrchestra.UI.Pix;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace JunimoOrchestra.Game
{
    /// <summary>
    /// The player's own movement speed, for walking a row of blocks at the tempo it was laid out for: one tile is one
    /// step of the music, so the speed is the tempo. The slower and faster keys go through 25–200%, with Shift by 5%;
    /// the reset key goes back to 100%. Scales <see cref="Farmer.getMovementSpeed"/> for the local player only (see
    /// <see cref="Patches"/>), outside cutscenes. After a change a tag over her head shows the speed and what a tile
    /// takes at it. Back to 100% on returning to the title.
    /// </summary>
    internal sealed class MoveSpeed
    {
        private static readonly int[] Steps = { 25, 50, 75, 100, 125, 150, 200 };
        private const int Min = 10, Max = 300, Fine = 5;
        private const int Scale = 3;
        private const double ShowSeconds = 2.0, FadeSeconds = 0.4;

        private readonly Func<ModConfig> config;
        private readonly SoundNames names;
        private readonly IInputHelper input;
        private double shownAt = double.NegativeInfinity;

        public MoveSpeed(Func<ModConfig> config, SoundNames names, IInputHelper input)
        {
            this.config = config;
            this.names = names;
            this.input = input;
        }

        /// <summary>The speed in percent (read by the Harmony postfix).</summary>
        public static int Percent { get; private set; } = 100;

        private static double Now => Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0;

        public void Reset() => Percent = 100;

        /// <summary>Set the speed directly (the debug bridge), with the tag.</summary>
        public void Set(int percent)
        {
            Percent = Math.Clamp(percent, Min, Max);
            this.shownAt = Now;
        }

        /// <summary>Handle the speed keys; true if one was pressed.</summary>
        public bool OnButtonsChanged()
        {
            if (!Context.IsPlayerFree)
                return false;
            ModConfig cfg = this.config();
            bool fine = this.input.IsDown(SButton.LeftShift) || this.input.IsDown(SButton.RightShift);
            int before = Percent;
            if (cfg.SpeedFaster.JustPressed())
                Percent = fine ? Math.Min(Max, Percent + Fine) : Array.Find(Steps, s => s > Percent) is var up && up > 0 ? up : Percent;
            else if (cfg.SpeedSlower.JustPressed())
                Percent = fine ? Math.Max(Min, Percent - Fine) : Array.FindLast(Steps, s => s < Percent) is var down && down > 0 ? down : Percent;
            else if (cfg.SpeedReset.JustPressed())
                Percent = 100;
            else
                return false;
            this.shownAt = Now;
            Game1.playSound(Percent == before ? "cancel" : Percent > before ? "drumkit6" : "drumkit1");
            return true;
        }

        /// <summary>What crossing one tile takes her now, in seconds; 0 while the game is paused.</summary>
        public static double SecondsPerTile(Farmer who)
        {
            // the speed is per frame, scaled by the frame's length; per millisecond it doesn't depend on the frame rate
            double ms = Game1.currentGameTime?.ElapsedGameTime.TotalMilliseconds ?? 0;
            double perMs = who.getMovementSpeed() / ms;
            if (who.movementDirections.Count > 1)
                perMs /= 0.707;   // diagonal steps are slowed to the same speed along the path
            return ms > 0 && perMs > 0 ? Game1.tileSize / perMs / 1000 : 0;
        }

        /// <summary>The tag over her head, for a moment after a change.</summary>
        public void Draw(SpriteBatch b)
        {
            double age = Now - this.shownAt;
            if (age > ShowSeconds || Game1.eventUp || Game1.activeClickableMenu != null)
                return;
            float alpha = (float)Math.Clamp((ShowSeconds - age) / FadeSeconds, 0, 1);

            Farmer who = Game1.player;
            bool running = who.running || who.isRidingHorse();
            double perTile = SecondsPerTile(who);
            if (perTile <= 0)
                return;
            int bpm = (int)Math.Round(60 / (perTile * 4));           // a tile a sixteenth
            string line1 = this.names.T("hud.speed", new { percent = Percent });
            string line2 = this.names.T(running ? "hud.speed.run" : "hud.speed.walk",
                new { seconds = perTile.ToString("0.00"), bpm });

            SpriteFont font = Game1.smallFont;
            Vector2 s1 = font.MeasureString(line1), s2 = font.MeasureString(line2);
            int w = (int)Math.Ceiling(Math.Max(s1.X, s2.X) / Scale) + 10;
            int h = (int)Math.Ceiling((s1.Y + s2.Y + 8) / Scale);
            Ramp acc = Ramp.Of(Percent == 100 ? "#4fcb86" : Percent > 100 ? "#ff82ad" : "#5b8def");
            Texture2D box = SpriteCache.Get($"speed:{w}:{h}:{acc.Hex}", () => Px.Box(w, h, 3, acc.O, Px.C("milk")));

            Vector2 head = Game1.GlobalToLocal(Game1.viewport, who.Position + new Vector2(32, -150));
            head = Utility.ModifyCoordinatesForUIScale(head);
            var pos = new Vector2((int)(head.X - w * Scale / 2f), (int)(head.Y - h * Scale));
            Color tint = Color.White * alpha;
            b.Draw(box, pos, null, tint, 0, Vector2.Zero, Scale, SpriteEffects.None, 1);
            b.DrawString(font, line1, pos + new Vector2((w * Scale - s1.X) / 2, 5), acc.D * alpha);
            b.DrawString(font, line2, pos + new Vector2((w * Scale - s2.X) / 2, 5 + s1.Y - 2), Px.C("ink") * alpha);
        }
    }
}
