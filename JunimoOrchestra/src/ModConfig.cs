using Newtonsoft.Json;
using StardewModdingAPI.Utilities;

namespace JunimoOrchestra
{
    /// <summary>Per-player preferences (config.json). Shown in the panel's global tab and in GMCM.</summary>
    internal sealed class ModConfig
    {
        /// <summary>Master volume, 0-127 like every other value in the panel.</summary>
        public int MasterVolume { get; set; } = 100;

        /// <summary>Blocks get quieter with distance and are panned by their position.</summary>
        public bool SpatialAudio { get; set; } = true;

        /// <summary>Show the instrument and note name above a hovered block.</summary>
        public bool ShowLabels { get; set; } = true;

        /// <summary>Little music notes float up when a block plays.</summary>
        public bool NoteParticles { get; set; } = true;

        /// <summary>The panel's notes tab picks each note by octave and name, and its length and delay by note value,
        /// instead of the piano roll; off, it's the full roll with velocity and exact ticks.</summary>
        public bool SimpleNotes { get; set; } = true;

        /// <summary>Learn every block recipe automatically.</summary>
        public bool AutoLearnRecipes { get; set; } = true;

        /// <summary>The SoundFont the blocks play: one in a content pack's soundfonts folder ("{pack's UniqueID}/{file}",
        /// see <see cref="Game.CustomPacks"/>), a full path, or empty for the built-in GeneralUser GS.</summary>
        public string SoundFont { get; set; } = "";

        /// <summary>Movement speed keys (<see cref="Game.MoveSpeed"/>): slower and faster through 25–200%, with Shift by 5%.</summary>
        public KeybindList SpeedSlower { get; set; } = KeybindList.Parse("OemOpenBrackets");
        public KeybindList SpeedFaster { get; set; } = KeybindList.Parse("OemCloseBrackets");

        /// <summary>Back to normal speed.</summary>
        public KeybindList SpeedReset { get; set; } = KeybindList.Parse("OemPipe");

        /****
        ** Film tools only: set while the game runs (the debug bridge's "song source", the director's "set jo.Config..."),
        ** never written to config.json, so players see only their own settings.
        ****/
        /// <summary>What plays song blocks: "open" (the mod's SoundFont) or "retro" (MU2000s: a local S-MU2000 build
        /// with your own ROMs, see music/engine/retro; neither ships with the mod).</summary>
        [JsonIgnore]
        public string SongSource { get; set; } = "open";

        /// <summary>The retro source's helper program (music/engine/retro's smu_stream).</summary>
        [JsonIgnore]
        public string RetroHelper { get; set; } = "";

        /// <summary>The folder with your MU2000 ROMs.</summary>
        [JsonIgnore]
        public string RetroRoms { get; set; } = "";
    }
}
