using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Junimo.Engine.Mixing;
using Junimo.Engine.Performance;
using StardewValley;

namespace JunimoOrchestra.Songs
{
    /// <summary>A song laid out as blocks (music/tools/songblocks.py), with the mix it's played through.</summary>
    internal sealed class Song
    {
        public Song(string id, SongBlocks blocks, MixSettings mix, int reach)
        {
            this.Id = id;
            this.Blocks = blocks;
            this.Mix = mix;
            this.Reach = reach;
            this.ById = blocks.Blocks.ToDictionary(b => b.Id);
            this.HeardUntil = LiveStage.EndOf(mix);
        }

        public string Id { get; }
        public SongBlocks Blocks { get; }
        public MixSettings Mix { get; }
        public IReadOnlyDictionary<int, Block> ById { get; }

        /// <summary>The performance's sample where the master has come out when the song is played as planned: a
        /// block whose sound starts later is never heard (the song's last reset, after the mix's end), so it starts no
        /// run.</summary>
        public long HeardUntil { get; }

        /// <summary>How far from her path its blocks may lie, in tiles: a block plays when she comes within this many
        /// tiles of it straight across (in its column or its row), so a song can lie on the ground like a piano roll
        /// with her running down the middle.</summary>
        public int Reach { get; }
    }

    /// <summary>
    /// The songs this session knows, and which placed blocks play them. A song block is an ordinary block item whose
    /// data names a song and one of its blocks; everything it sends is in the song. Songs are loaded from files for
    /// now (music/out/blocks); making them in the game comes with the UI.
    /// </summary>
    internal sealed class SongLibrary
    {
        /// <summary>modData of a placed block that plays a song's block: "&lt;song id&gt; &lt;block id&gt;".</summary>
        public const string ModDataKey = "link1412.JunimoOrchestra/song";

        private readonly Dictionary<string, Song> songs = new();

        /// <summary>The longest reach of any song (0 when there are none).</summary>
        public int MaxReach { get; private set; }

        public IEnumerable<Song> All => this.songs.Values;

        public Song? Get(string id) => this.songs.TryGetValue(id, out Song? song) ? song : null;

        public int ReachOf(string id) => this.Get(id)?.Reach ?? 0;

        /// <summary>Load a song from its blocks (songblocks.py) and its mix (export_mix.py), replacing one of the same
        /// id.</summary>
        /// <param name="reach">Tiles from her path its blocks may lie; 0 for the straight line's: its rows split evenly
        /// to both sides.</param>
        public Song Load(string id, string blocksPath, string mixPath, int reach = 0)
        {
            SongBlocks blocks = SongBlocks.Load(blocksPath);
            MixSettings mix = MixSettings.Load(mixPath);
            if (reach <= 0)
                reach = (blocks.Blocks.Select(b => (b.Row, b.Spare)).Distinct().Count() + 1) / 2;
            var song = new Song(id, blocks, mix, reach);
            this.songs[id] = song;
            this.MaxReach = this.songs.Values.Max(s => s.Reach);
            return song;
        }

        /// <summary>The song and block a placed (or held) block plays, if it's a song block.</summary>
        public static (string Song, int Block)? RefOf(Item item)
        {
            if (!item.modData.TryGetValue(ModDataKey, out string? raw))
                return null;
            int space = raw.IndexOf(' ');
            if (space <= 0 || !int.TryParse(raw.AsSpan(space + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int block))
                return null;
            return (raw[..space], block);
        }

        public static bool IsSongBlock(Item item) => item.modData.ContainsKey(ModDataKey);
    }
}
