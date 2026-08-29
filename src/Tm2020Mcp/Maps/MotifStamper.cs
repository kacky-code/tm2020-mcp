using System.Text;
using Tm2020Mcp.EditorBridge;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Places a learned motif into a map: rotates it, checks what it would land on, and reports
/// collisions instead of quietly overwriting them.
/// </summary>
/// <remarks>
/// The bridge cannot help here either. <c>PlaceBlock</c> refuses a cell it cannot use but
/// says nothing about a structure being half-placed, and a loop missing two of its five wall
/// pieces is worse than no loop. So the footprint is checked as a unit first.
/// </remarks>
public sealed class MotifStamper
{
    /// <param name="existing">Blocks already in the map, used for collision checks.</param>
    /// <param name="minimumSupport">
    /// Drop motif blocks weaker than this. A low-support block is something that happened to
    /// be near the anchor in some maps, not part of the structure.
    /// </param>
    public MotifStamp Stamp(
        BlockMotif motif,
        int x,
        int y,
        int z,
        string direction,
        IEnumerable<MapBlock>? existing = null,
        double minimumSupport = 0.5,
        int mapSize = 48,
        int groundY = 9,
        bool liftToGround = true)
    {
        if (motif.Samples == 0 || motif.Blocks.Count == 0)
            return new MotifStamp([], [], ["The motif is empty; nothing to stamp."]);

        var filtered = motif with
        {
            Blocks = motif.Blocks.Where(b => b.Support >= minimumSupport).ToArray()
        };

        var blocks = filtered.At(x, y, z, direction);
        var notes = new List<string>();

        // Motifs carry supports below their anchor. Learned from loops that sit elevated,
        // stamping one at ground level puts those supports underground, where the engine
        // silently refuses them and leaves the structure half-built.
        var lowest = blocks.Min(b => b.Y);
        if (liftToGround && lowest < groundY)
        {
            var lift = groundY - lowest;
            blocks = blocks.Select(b => b with { Y = b.Y + lift }).ToArray();
            notes.Add($"Lifted the motif {lift} level(s) so its lowest block sits on ground level {groundY}; the anchor is now at y={y + lift}.");
        }

        var occupied = new HashSet<(int, int, int)>();

        foreach (var block in existing ?? [])
        {
            if (!block.IsFree)
                occupied.Add((block.X, block.Y, block.Z));
        }

        var problems = new List<string>(notes);
        var collisions = blocks.Where(b => occupied.Contains((b.X, b.Y, b.Z))).ToArray();
        var outside = blocks
            .Where(b => b.X < 0 || b.Z < 0 || b.X >= mapSize || b.Z >= mapSize || b.Y < groundY)
            .ToArray();

        foreach (var block in collisions)
            problems.Add($"{block.Name} at <{block.X}, {block.Y}, {block.Z}> lands on an existing block.");

        foreach (var block in outside)
            problems.Add($"{block.Name} at <{block.X}, {block.Y}, {block.Z}> falls outside the map or below ground level {groundY}.");

        return new MotifStamp(blocks, [.. collisions, .. outside], problems);
    }

    public static string Format(MotifStamp stamp)
    {
        var report = new StringBuilder();
        report.AppendLine(stamp.CanPlace
            ? $"{stamp.Blocks.Count} block(s), clear to place."
            : $"{stamp.Blocks.Count} block(s), {stamp.Blocked.Count} blocked.");

        foreach (var problem in stamp.Problems.Take(12))
            report.AppendLine($"  {problem}");

        if (stamp.Problems.Count > 12)
            report.AppendLine($"  ... {stamp.Problems.Count - 12} more.");

        return report.ToString().TrimEnd();
    }
}

public sealed record MotifStamp(
    IReadOnlyList<MapBlock> Blocks,
    IReadOnlyList<MapBlock> Blocked,
    IReadOnlyList<string> Problems)
{
    public bool CanPlace => Blocks.Count > 0 && Blocked.Count == 0;

    /// <summary>
    /// Placements for the bridge. Motifs carry real heights, including supports below the
    /// anchor, so Y is explicit here rather than left to the ground scan.
    /// </summary>
    public IReadOnlyList<MapBlockPlacement> ToPlacements() => Blocks
        .Select(b => new MapBlockPlacement(b.Name, b.X, b.Z, b.Y, b.Direction))
        .ToArray();
}
