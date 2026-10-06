using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace JunimoOrchestra.Game
{
    /// <summary>What players change for themselves, in content packs for this mod: folders of their own in Mods whose
    /// manifest's ContentPackFor is this mod (the release zip comes with an empty one, [JO] Custom, from custom-pack/).
    /// An update replaces the mod's own folder, so nothing in it would last. A pack's textures/ holds pictures that
    /// replace ours, by file name and at our size; its soundfonts/ holds SoundFonts to pick from beside the built-in one.
    /// </summary>
    internal sealed class CustomPacks
    {
        public const string TexturesFolder = "textures";
        public const string SoundFontsFolder = "soundfonts";

        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly List<IContentPack> packs;
        private readonly HashSet<string> reported = new();

        public CustomPacks(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.packs = helper.ContentPacks.GetOwned().ToList();
            foreach (IContentPack pack in this.packs)
                monitor.Log($"Content pack {pack.Manifest.Name} ({pack.Manifest.UniqueID}) in {pack.DirectoryPath}", LogLevel.Trace);
        }

        /// <summary>One of our pictures (assets/textures/{file}), or a pack's in its place: the last pack's that has it
        /// at our size. A pack's at another size would draw the wrong part of itself, so it's passed over (the log says).
        /// </summary>
        public Texture2D Texture(string file)
        {
            Texture2D ours = this.helper.ModContent.Load<Texture2D>($"assets/{TexturesFolder}/{file}");
            string path = $"{TexturesFolder}/{file}";
            List<IContentPack> having = this.packs.Where(p => p.HasFile(path)).ToList();
            for (int i = having.Count - 1; i >= 0; i--)
            {
                IContentPack pack = having[i];
                Texture2D theirs;
                try
                {
                    theirs = pack.ModContent.Load<Texture2D>(path);
                }
                catch (Exception ex)
                {
                    this.Report($"{pack.Manifest.Name}: {path} can't be read, so it isn't used ({ex.Message})", LogLevel.Warn);
                    continue;
                }
                if (theirs.Width != ours.Width || theirs.Height != ours.Height)
                {
                    this.Report($"{pack.Manifest.Name}: {path} is {theirs.Width}x{theirs.Height}, but it has to be "
                        + $"{ours.Width}x{ours.Height} like ours, so it isn't used", LogLevel.Warn);
                    continue;
                }
                string others = string.Join(", ", having.Take(i).Select(p => p.Manifest.Name));
                this.Report($"{path}: {pack.Manifest.Name}'s" + (others.Length > 0 ? $" (not {others}'s)" : ""), LogLevel.Info);
                return theirs;
            }
            return ours;
        }

        /// <summary>The SoundFonts in the packs, as the setting names them: "{pack's UniqueID}/{file}".</summary>
        public IEnumerable<string> SoundFonts()
            => this.packs.SelectMany(p => Files(Path.Combine(p.DirectoryPath, SoundFontsFolder)).Select(f => $"{p.Manifest.UniqueID}/{f}"));

        /// <summary>The file a "{pack's UniqueID}/{file}" setting means; null if it isn't one, or its pack is gone.</summary>
        public string? SoundFontPath(string choice)
        {
            int slash = choice.IndexOf('/');
            if (slash <= 0)
                return null;
            string id = choice[..slash];
            return this.packs.FirstOrDefault(p => p.Manifest.UniqueID.Equals(id, StringComparison.OrdinalIgnoreCase)) is IContentPack pack
                ? Path.Combine(pack.DirectoryPath, SoundFontsFolder, choice[(slash + 1)..])
                : null;
        }

        /// <summary>The SoundFont files in a folder, by name (SF3 too, so picking one says why it can't play); none if
        /// there's no folder.</summary>
        private static IEnumerable<string> Files(string dir)
        {
            try
            {
                return Directory.EnumerateFiles(dir)
                    .Where(f => f.EndsWith(".sf2", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".sf3", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName).OfType<string>()
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
        }

        private void Report(string message, LogLevel level)
        {
            if (this.reported.Add(message))
                this.monitor.Log(message, level);
        }
    }
}
