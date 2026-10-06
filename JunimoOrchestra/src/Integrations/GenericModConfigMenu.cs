using System;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace JunimoOrchestra.Integrations
{
    /// <summary>The subset of Generic Mod Config Menu's API we use (https://github.com/spacechase0/StardewValleyMods).</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null,
            int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null,
            string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
        void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name,
            Func<string>? tooltip = null, string? fieldId = null);
    }

    internal static class GenericModConfigMenu
    {
        public static void Register(ModEntry mod)
        {
            var api = mod.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api == null)
                return;
            ITranslationHelper tr = mod.Helper.Translation;
            api.Register(mod.ModManifest, reset: () => mod.Config = new ModConfig(), save: () =>
            {
                mod.SaveConfig();
                mod.ApplySoundFont();
            });
            api.AddNumberOption(mod.ModManifest, () => mod.Config.MasterVolume, v => mod.Config.MasterVolume = v,
                () => tr.Get("config.master-volume"), min: 0, max: 127);
            api.AddBoolOption(mod.ModManifest, () => mod.Config.SpatialAudio, v => mod.Config.SpatialAudio = v, () => tr.Get("config.spatial"));
            api.AddBoolOption(mod.ModManifest, () => mod.Config.ShowLabels, v => mod.Config.ShowLabels = v, () => tr.Get("config.labels"));
            api.AddBoolOption(mod.ModManifest, () => mod.Config.NoteParticles, v => mod.Config.NoteParticles = v, () => tr.Get("config.particles"));
            api.AddBoolOption(mod.ModManifest, () => mod.Config.SimpleNotes, v => mod.Config.SimpleNotes = v, () => tr.Get("config.simple-notes"),
                () => tr.Get("config.simple-notes.tip"));
            api.AddBoolOption(mod.ModManifest, () => mod.Config.AutoLearnRecipes, v => mod.Config.AutoLearnRecipes = v, () => tr.Get("config.auto-learn"));
            // the files there when the game started (a new one shows up after a restart, or in the panel's 全局 tab)
            api.AddTextOption(mod.ModManifest, () => mod.Config.SoundFont, v => mod.Config.SoundFont = v, () => tr.Get("config.soundfont"),
                () => tr.Get("config.soundfont.tip"), mod.SoundFontChoices().ToArray(),
                v => v.Length == 0 ? tr.Get("config.soundfont.builtin") : System.IO.Path.GetFileName(v));
            api.AddKeybindList(mod.ModManifest, () => mod.Config.SpeedSlower, v => mod.Config.SpeedSlower = v,
                () => tr.Get("config.speed-slower"), () => tr.Get("config.speed.tip"));
            api.AddKeybindList(mod.ModManifest, () => mod.Config.SpeedFaster, v => mod.Config.SpeedFaster = v,
                () => tr.Get("config.speed-faster"), () => tr.Get("config.speed.tip"));
            api.AddKeybindList(mod.ModManifest, () => mod.Config.SpeedReset, v => mod.Config.SpeedReset = v,
                () => tr.Get("config.speed-reset"));
        }
    }
}
