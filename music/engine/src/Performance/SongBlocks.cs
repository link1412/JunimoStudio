using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Junimo.Engine.Performance
{
    /// <summary>A note in a block: dt ticks after the block's home tick, with its place in the song's order for the
    /// note-on and the note-off.</summary>
    public readonly record struct BlockNote(long Dt, long Length, int Key, int Velocity, long OnSeq, long OffSeq);

    /// <summary>A setting's new value dt ticks after the block's home tick.</summary>
    public readonly record struct LanePoint(long Dt, int Value, long Seq);

    /// <summary>A world event (a system reset, an effect type) dt ticks after the block's home tick. Values are
    /// "GM" / "XG" for a reset, else data bytes as decimal numbers ("1 0").</summary>
    public readonly record struct WorldPoint(long Dt, long Seq, string Name, string Value);

    /// <summary>One part's share of a block: its state when the block starts, its notes and its setting changes.</summary>
    public sealed class BlockPart
    {
        public Dictionary<string, int> State { get; init; } = new();
        public List<BlockNote> Notes { get; init; } = new();
        public Dictionary<string, List<LanePoint>> Lanes { get; init; } = new();
    }

    /// <summary>
    /// A block: what one block in the world sends when it's triggered (music/tools/songblocks.py writes them). Times
    /// are ticks after its home tick (its first event), at its own stretch of the tempo map. It starts
    /// <see cref="Lead"/> samples after its trigger frame, plus the fraction of a sample its home tick lies past a
    /// whole sample (<see cref="PhaseNum"/> / <see cref="PhaseDen"/>).
    /// </summary>
    public sealed class Block
    {
        public int Id { get; init; }
        public string Kind { get; init; } = "";      // "instrument" | "conductor"
        public string Row { get; init; } = "";
        public int Spare { get; init; }
        public long Tile { get; init; }
        public long Frame { get; init; }             // the game frame the plan has it triggered at
        public long HomeTick { get; init; }
        public long HomeSeq { get; init; }
        public long PhaseNum { get; init; }
        public long PhaseDen { get; init; }
        public long Lead { get; init; }
        public List<(long Dt, long Micros)> Tempo { get; init; } = new();
        public Dictionary<string, string> WorldState { get; init; } = new();
        public List<WorldPoint> WorldPoints { get; init; } = new();
        public SortedDictionary<int, BlockPart> Parts { get; init; } = new();
    }

    /// <summary>A song laid out as blocks, as a save would hold it.</summary>
    public sealed class SongBlocks
    {
        public int Ppq { get; init; }
        public long End { get; init; }                // the song's end (ticks): renders run 5 s past it
        public List<(long Tick, long Micros)> Tempo { get; init; } = new();
        public List<Block> Blocks { get; init; } = new();

        public static SongBlocks Load(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            return Parse(doc.RootElement);
        }

        public static SongBlocks Parse(JsonElement root)
        {
            Model.Check(root.GetProperty("params"), root.GetProperty("world_params"));
            var song = root.GetProperty("song");
            return new SongBlocks
            {
                Ppq = song.GetProperty("ppq").GetInt32(),
                End = song.GetProperty("end").GetInt64(),
                Tempo = Pairs(song.GetProperty("tempo")),
                Blocks = root.GetProperty("blocks").EnumerateArray().Select(ParseBlock).ToList(),
            };
        }

        private static Block ParseBlock(JsonElement b)
        {
            var home = b.GetProperty("home");
            var phase = b.GetProperty("phase");
            var world = b.GetProperty("world");
            return new Block
            {
                Id = b.GetProperty("id").GetInt32(),
                Kind = b.GetProperty("kind").GetString()!,
                Row = b.GetProperty("row").GetString()!,
                Spare = b.GetProperty("spare").GetInt32(),
                Tile = b.GetProperty("tile").GetInt64(),
                Frame = b.GetProperty("frame").GetInt64(),
                HomeTick = home[0].GetInt64(),
                HomeSeq = home[1].GetInt64(),
                PhaseNum = phase[0].GetInt64(),
                PhaseDen = phase[1].GetInt64(),
                Lead = b.GetProperty("lead").GetInt64(),
                Tempo = Pairs(b.GetProperty("tempo")),
                WorldState = world.GetProperty("state").EnumerateObject().ToDictionary(p => p.Name, p => WorldValue(p.Value)),
                WorldPoints = world.GetProperty("points").EnumerateArray()
                    .Select(p => new WorldPoint(p[0].GetInt64(), p[1].GetInt64(), p[2].GetString()!, WorldValue(p[3]))).ToList(),
                Parts = new SortedDictionary<int, BlockPart>(b.GetProperty("parts").EnumerateObject().ToDictionary(
                    p => int.Parse(p.Name),
                    p => new BlockPart
                    {
                        State = p.Value.GetProperty("state").EnumerateObject().ToDictionary(s => s.Name, s => s.Value.GetInt32()),
                        Notes = p.Value.GetProperty("notes").EnumerateArray().Select(n => new BlockNote(
                            n[0].GetInt64(), n[1].GetInt64(), n[2].GetInt32(), n[3].GetInt32(), n[4].GetInt64(), n[5].GetInt64())).ToList(),
                        Lanes = p.Value.GetProperty("lanes").EnumerateObject().ToDictionary(l => l.Name, l => l.Value.EnumerateArray()
                            .Select(x => new LanePoint(x[0].GetInt64(), x[1].GetInt32(), x[2].GetInt64())).ToList()),
                    })),
            };
        }

        private static List<(long, long)> Pairs(JsonElement list) =>
            list.EnumerateArray().Select(x => (x[0].GetInt64(), x[1].GetInt64())).ToList();

        private static string WorldValue(JsonElement v) => v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : string.Join(" ", v.EnumerateArray().Select(x => x.GetInt32()));
    }
}
