using System.Text.Json;

namespace Tm2020Mcp.Maps;

/// <summary>
/// A multi-block structure copied out of real maps: an anchor block plus the blocks that
/// reliably sit around it, in three dimensions, relative to the anchor.
/// </summary>
/// <remarks>
/// Some things in Trackmania are not a block but a shape. A loop is a five-wide wall of
/// <c>PlatformTechLoopStart</c> over a base row with a <c>StructureStraight</c> support
/// underneath; a reset is a run of two or three <c>GateSpecialReset</c>. Naming one block and
/// hoping is how you get a broken loop, so a motif is measured from the corpus and stamped
/// whole. Evidence: docs/tm2020-map-geometry.md.
/// </remarks>
/// <param name="Support">
/// Share of the anchor sightings that had this block at this offset. 1.0 means every single
/// one; the learner's threshold decides what is structural rather than coincidence.
/// </param>
public sealed record MotifBlock(
    int DX,
    int DY,
    int DZ,
    string Name,
    string Direction,
    double Support);

public sealed record BlockMotif(
    string Anchor,
    string CanonicalDirection,
    int Samples,
    IReadOnlyList<MotifBlock> Blocks)
{
    /// <summary>Direction order matching the measured forward vectors: +Z, -X, -Z, +X.</summary>
    public static readonly string[] DirectionCycle = ["North", "East", "South", "West"];

    /// <summary>
    /// Rotates the motif so its anchor faces <paramref name="direction"/>.
    /// </summary>
    /// <remarks>
    /// North is +Z and East is -X, so one step clockwise through the cycle maps (x, z) to
    /// (-z, x). Rotating the offsets without also stepping each block's own direction would
    /// leave a correctly placed structure made of wrongly turned pieces.
    /// </remarks>
    public BlockMotif RotatedTo(string direction)
    {
        var from = IndexOf(CanonicalDirection);
        var to = IndexOf(direction);
        var steps = ((to - from) % 4 + 4) % 4;

        if (steps == 0)
            return this;

        var rotated = Blocks.Select(block =>
        {
            var (dx, dz) = (block.DX, block.DZ);
            var blockDirection = block.Direction;

            for (var i = 0; i < steps; i++)
            {
                (dx, dz) = (-dz, dx);
                blockDirection = StepDirection(blockDirection);
            }

            return block with { DX = dx, DZ = dz, Direction = blockDirection };
        }).ToArray();

        return this with { CanonicalDirection = direction, Blocks = rotated };
    }

    /// <summary>Blocks of this motif placed at a world coordinate.</summary>
    public IReadOnlyList<MapBlock> At(int x, int y, int z, string direction) =>
        RotatedTo(direction).Blocks
            .Select(b => new MapBlock(b.Name, x + b.DX, y + b.DY, z + b.DZ, b.Direction))
            .ToArray();

    /// <summary>Cells the motif occupies, for collision checks before anything is placed.</summary>
    public IReadOnlyList<(int X, int Y, int Z)> Footprint(int x, int y, int z, string direction) =>
        At(x, y, z, direction).Select(b => (b.X, b.Y, b.Z)).ToArray();

    private static string StepDirection(string direction)
    {
        var index = IndexOf(direction);
        return index < 0 ? direction : DirectionCycle[(index + 1) % 4];
    }

    private static int IndexOf(string direction) =>
        Array.FindIndex(DirectionCycle, d => string.Equals(d, direction, StringComparison.OrdinalIgnoreCase));

    public string ToJson() => JsonSerializer.Serialize(this, MotifJson.Options);

    public static BlockMotif FromJson(string json) =>
        JsonSerializer.Deserialize<BlockMotif>(json, MotifJson.Options)
            ?? throw new ArgumentException("Motif JSON was empty.", nameof(json));
}

internal static class MotifJson
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}
