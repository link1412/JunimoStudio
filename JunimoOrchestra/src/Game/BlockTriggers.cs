using System;
using System.Collections.Generic;
using JunimoOrchestra.Data;
using JunimoOrchestra.Songs;
using Microsoft.Xna.Framework;
using StardewValley;
using SObject = StardewValley.Object;

namespace JunimoOrchestra.Game
{
    /// <summary>
    /// Plays a block when someone steps next to it (orthogonally), or within its reach straight across (its row or
    /// column, <see cref="BlockSettings.Reach"/>; other blocks in between don't block it, so rows can lie side by side).
    /// Edge-triggered: standing still next to a block doesn't repeat it, stepping away and back does. A song block plays
    /// when a player passes it: when she comes within its song's reach of it straight across (in its column or its row),
    /// so a song can lie on the ground like a piano roll, rows of blocks to both sides, and she runs down the middle.
    /// </summary>
    internal sealed class BlockTriggers
    {
        private static readonly Point[] Neighbours = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };

        /// <summary>Blocks each character was next to on the previous tick.</summary>
        private readonly Dictionary<Character, List<Vector2>> adjacent = new(ReferenceEqualityComparer.Instance);

        /// <summary>Song blocks each player was within reach of on the previous tick.</summary>
        private readonly Dictionary<Character, List<Vector2>> inReach = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<Character> seen = new(ReferenceEqualityComparer.Instance);
        private readonly List<Character> stale = new();
        private readonly Action<GameLocation, Vector2, SObject, Family, bool> fire;
        private readonly Action<GameLocation, Vector2, SObject, (string Song, int Block)> fireSong;
        private readonly SongLibrary songs;
        private readonly Func<int> worldReach;
        private GameLocation? location;

        /// <param name="fire">Called with (location, tile, block, family, byPlayer) when a block should play.</param>
        /// <param name="fireSong">Called with (location, tile, block, which song block) when a song block should play.</param>
        /// <param name="worldReach">The save's reach, for blocks that don't set their own.</param>
        public BlockTriggers(Action<GameLocation, Vector2, SObject, Family, bool> fire,
            Action<GameLocation, Vector2, SObject, (string Song, int Block)> fireSong, SongLibrary songs, Func<int> worldReach)
        {
            this.fire = fire;
            this.fireSong = fireSong;
            this.songs = songs;
            this.worldReach = worldReach;
        }

        public void Reset()
        {
            this.adjacent.Clear();
            this.inReach.Clear();
            this.location = null;
        }

        public void Update()
        {
            GameLocation? loc = Game1.currentLocation;
            if (loc == null)
            {
                this.Reset();
                return;
            }

            // arriving somewhere shouldn't set off the blocks you happen to spawn next to
            bool seedOnly = loc != this.location;
            if (seedOnly)
            {
                this.adjacent.Clear();
                this.inReach.Clear();
                this.location = loc;
            }

            this.seen.Clear();
            foreach (Farmer farmer in loc.farmers)
            {
                this.Visit(loc, farmer, true, seedOnly);
                this.Pass(loc, farmer, seedOnly);
            }
            foreach (NPC npc in loc.characters)
                this.Visit(loc, npc, false, seedOnly);
            foreach (FarmAnimal animal in loc.animals.Values)
                this.Visit(loc, animal, false, seedOnly);

            if (this.adjacent.Count > 0)
            {
                this.stale.Clear();
                foreach (Character c in this.adjacent.Keys)
                    if (!this.seen.Contains(c))
                        this.stale.Add(c);
                foreach (Character c in this.stale)
                    this.adjacent.Remove(c);
            }
            if (this.inReach.Count > 0)
            {
                this.stale.Clear();
                foreach (Character c in this.inReach.Keys)
                    if (!this.seen.Contains(c))
                        this.stale.Add(c);
                foreach (Character c in this.stale)
                    this.inReach.Remove(c);
            }
        }

        private void Visit(GameLocation loc, Character who, bool isPlayer, bool seedOnly)
        {
            this.seen.Add(who);
            Point p = who.TilePoint;
            this.adjacent.TryGetValue(who, out List<Vector2>? before);
            List<Vector2>? now = null;

            foreach (Point d in Neighbours)
            {
                for (int k = 1; k <= BlockSettings.MaxReach; k++)
                {
                    var tile = new Vector2(p.X + d.X * k, p.Y + d.Y * k);
                    if (!loc.objects.TryGetValue(tile, out SObject? obj) || obj.bigCraftable.Value || SongLibrary.IsSongBlock(obj))
                        continue;
                    Family? family = Families.FromItemId(obj.ItemId);
                    if (family == null || (k > 1 && BlockSettings.ReachOf(obj, this.worldReach()) < k))
                        continue;
                    (now ??= new List<Vector2>(2)).Add(tile);
                    if (!seedOnly && (before == null || !before.Contains(tile)))
                        this.fire(loc, tile, obj, family, isPlayer);
                }
            }

            if (now != null)
                this.adjacent[who] = now;
            else if (before != null)
                this.adjacent.Remove(who);
        }

        /// <summary>The song blocks a player has come within reach of, straight across.</summary>
        private void Pass(GameLocation loc, Farmer who, bool seedOnly)
        {
            int reach = this.songs.MaxReach;
            this.inReach.TryGetValue(who, out List<Vector2>? before);
            List<Vector2>? now = null;
            if (reach > 0)
            {
                Point p = who.TilePoint;
                foreach (Point d in Neighbours)
                {
                    for (int k = 1; k <= reach; k++)
                    {
                        var tile = new Vector2(p.X + d.X * k, p.Y + d.Y * k);
                        if (!loc.objects.TryGetValue(tile, out SObject? obj) || SongLibrary.RefOf(obj) is not { } at
                            || k > this.songs.ReachOf(at.Song))
                            continue;
                        (now ??= new List<Vector2>()).Add(tile);
                        if (!seedOnly && (before == null || !before.Contains(tile)))
                            this.fireSong(loc, tile, obj, at);
                    }
                }
            }
            if (now != null)
                this.inReach[who] = now;
            else if (before != null)
                this.inReach.Remove(who);
        }
    }
}
