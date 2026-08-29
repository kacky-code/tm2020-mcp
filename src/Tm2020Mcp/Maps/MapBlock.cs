namespace Tm2020Mcp.Maps;

/// <summary>
/// One block read out of a .Map.Gbx, flattened away from the GBX.NET types so the analysis
/// below can be exercised without map files.
/// </summary>
/// <param name="Direction">North, East, South or West. Free blocks carry a rotation instead
/// and report an empty direction.</param>
/// <param name="Position">
/// World position, for free blocks only. A free block's grid <c>Coord</c> is meaningless -
/// the engine reports it as &lt;-1, 0, -1&gt; - so anything geometric about modern maps has to
/// come from here. One grid cell is 32 world units across and 8 tall.
/// </param>
/// <param name="Rotation">Yaw, pitch and roll in radians, for free blocks only.</param>
public sealed record MapBlock(
    string Name,
    int X,
    int Y,
    int Z,
    string Direction,
    bool IsFree = false,
    int Variant = 0,
    Vector3? Position = null,
    Vector3? Rotation = null)
{
    public const float CellWidth = 32f;
    public const float CellHeight = 8f;

    /// <summary>The cell a free block sits in, derived from its world position.</summary>
    public (int X, int Y, int Z)? GridCell => Position is { } p
        ? ((int)MathF.Floor(p.X / CellWidth),
           (int)MathF.Floor(p.Y / CellHeight),
           (int)MathF.Floor(p.Z / CellWidth))
        : null;

    /// <summary>True when the block is tilted out of the flat grid orientation.</summary>
    public bool IsTilted => Rotation is { } r
        && (MathF.Abs(r.Y) > 0.01f || MathF.Abs(r.Z) > 0.01f);

    public bool IsRoad => Name.StartsWith("Road", StringComparison.OrdinalIgnoreCase);

    public bool IsStart => Name.EndsWith("Start", StringComparison.Ordinal);

    public bool IsFinish => Name.EndsWith("Finish", StringComparison.Ordinal);
}

/// <summary>Three floats. Meaning depends on use: world units for a position, radians for a rotation.</summary>
public readonly record struct Vector3(float X, float Y, float Z)
{
    public override string ToString() => $"<{X:0.##}, {Y:0.##}, {Z:0.##}>";
}

/// <summary>A parsed map: header facts plus its grid blocks.</summary>
public sealed record MapGbxFile(
    string FileName,
    string MapName,
    string? Decoration,
    string? Size,
    IReadOnlyList<MapBlock> Blocks)
{
    public int FreeBlockCount => Blocks.Count(b => b.IsFree);

    public int TiltedBlockCount => Blocks.Count(b => b.IsTilted);
}
