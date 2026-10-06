using System;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace JunimoOrchestra.UI
{
    /// <summary>Typing an exact number into a value box (ticks, 0–127 values...). Enter confirms, Esc cancels.</summary>
    internal sealed class NumberEntry : IKeyboardSubscriber
    {
        public readonly string Id;
        public readonly int Min;
        public readonly int Max;
        private readonly Action<int> commit;
        public string Text;

        public bool Selected { get; set; }
        public bool Done { get; private set; }

        public NumberEntry(string id, int current, int min, int max, Action<int> commit)
        {
            this.Id = id;
            this.Min = min;
            this.Max = max;
            this.commit = commit;
            this.Text = "";
            _ = current;
        }

        public void Begin()
        {
            this.Selected = true;
            Game1.keyboardDispatcher.Subscriber = this;
        }

        /// <summary>Apply what was typed (if anything) and stop editing.</summary>
        public void Confirm()
        {
            if (this.Done)
                return;
            if (int.TryParse(this.Text, out int value))
                this.commit(Math.Clamp(value, this.Min, this.Max));
            this.End();
        }

        public void Cancel() => this.End();

        private void End()
        {
            this.Done = true;
            this.Selected = false;
            if (Game1.keyboardDispatcher.Subscriber == this)
                Game1.keyboardDispatcher.Subscriber = null;
        }

        public void RecieveTextInput(char c)
        {
            if (this.Done || this.Text.Length >= 6)
                return;
            if (char.IsDigit(c) || (c == '-' && this.Min < 0 && this.Text.Length == 0))
                this.Text += c;
        }

        public void RecieveTextInput(string text)
        {
            foreach (char c in text)
                this.RecieveTextInput(c);
        }

        public void RecieveCommandInput(char command)
        {
            if (this.Done)
                return;
            if (command == '\b' && this.Text.Length > 0)
                this.Text = this.Text[..^1];
            else if (command == '\r')
                this.Confirm();
        }

        public void RecieveSpecialInput(Keys key)
        {
            if (key == Keys.Escape)
                this.Cancel();
        }
    }
}
