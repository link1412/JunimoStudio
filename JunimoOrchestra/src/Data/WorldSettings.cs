using System;

namespace JunimoOrchestra.Data
{
    /// <summary>Reverb "space" presets shared by every block in the save.</summary>
    internal enum Room
    {
        Dry,
        Cabin,
        Room,
        Hall,
        Church,
        Cave
    }

    /// <summary>Save-wide settings (stored in the save file). Single-player for now.</summary>
    internal sealed class WorldSettings
    {
        public const string SaveKey = "world";

        /// <summary>Tempo used by every block that doesn't set its own.</summary>
        public int DefaultTempo { get; set; } = 120;

        public Room Room { get; set; } = Room.Hall;

        /// <summary>Reach (tiles straight across) of every block that doesn't set its own.</summary>
        public int DefaultReach { get; set; } = 1;

        /// <summary>Global transpose in semitones.</summary>
        public int Transpose { get; set; }

        public void Normalize()
        {
            this.DefaultTempo = Math.Clamp(this.DefaultTempo, BlockSettings.MinTempo, BlockSettings.MaxTempo);
            this.Transpose = Math.Clamp(this.Transpose, -12, 12);
            this.DefaultReach = Math.Clamp(this.DefaultReach, 1, BlockSettings.MaxReach);
            if (!Enum.IsDefined(typeof(Room), this.Room))
                this.Room = Room.Hall;
        }
    }
}
