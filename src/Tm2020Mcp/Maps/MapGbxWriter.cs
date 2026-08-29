using GBX.NET;
using GBX.NET.Engines.Game;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Writes free blocks into a .Map.Gbx on disk, with no game running.
/// </summary>
/// <remarks>
/// This exists because the Openplanet editor API cannot do it. Every placement method on
/// <c>CGameEditorPluginMapMapType</c> takes an <c>int3</c> coordinate and a cardinal
/// direction, so the bridge can only ever build on the grid - while roughly half the blocks
/// in a Deep Dip are placed off it, at angles the grid cannot express. Editing the file
/// directly is the way past that ceiling.
///
/// The writer edits an existing map rather than creating one from nothing: a real map carries
/// a header, a decoration, a validated block list and an author that this code has no business
/// synthesising. Save an empty map from the editor once and use it as the base.
/// </remarks>
public sealed class MapGbxWriter
{
    /// <summary>
    /// Observed on every free block in Deep Dip 1 and 2. GBX.NET exposes <c>IsFree</c>
    /// separately, but the flag word is what the file carries, so it is set explicitly rather
    /// than left to chance.
    /// </summary>
    public const int FreeBlockFlags = 0x20000000;

    /// <summary>
    /// Adds free blocks to <paramref name="sourcePath"/> and writes the result to
    /// <paramref name="outputPath"/>. The source file is never modified.
    /// </summary>
    /// <returns>What was written, read back from the saved file.</returns>
    public MapGbxWriteResult AddFreeBlocks(
        string sourcePath,
        string outputPath,
        IReadOnlyList<FreeBlockPlacement> placements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (placements.Count == 0)
            throw new ArgumentException("No blocks to write.", nameof(placements));

        foreach (var placement in placements)
        {
            if (string.IsNullOrWhiteSpace(placement.Name))
                throw new ArgumentException("A block placement has no name.", nameof(placements));
        }

        if (Path.GetFullPath(sourcePath) == Path.GetFullPath(outputPath))
            throw new ArgumentException("Refusing to overwrite the source map; write to a different path.", nameof(outputPath));

        MapGbxReader.EnsureLzo();

        var gbx = Gbx.Parse<CGameCtnChallenge>(sourcePath);
        var map = gbx.Node;
        var blocks = map.Blocks?.ToList() ?? [];
        var before = blocks.Count;

        foreach (var placement in placements)
        {
            var rotation = placement.RotationRadians;

            blocks.Add(new CGameCtnBlock
            {
                Name = placement.Name,
                // Collection and author are empty on every TM2020 block ident observed in the
                // corpus, so the model can be named outright - no donor block needed.
                BlockModel = new Ident(placement.Name, new Id(string.Empty), string.Empty),
                Direction = Direction.North,
                Coord = new Int3(-1, 0, -1),
                Flags = FreeBlockFlags,
                IsFree = true,
                AbsolutePositionInMap = new Vec3(placement.X, placement.Y, placement.Z),
                YawPitchRoll = new Vec3(rotation.X, rotation.Y, rotation.Z)
            });
        }

        map.Blocks = blocks;

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        gbx.Save(outputPath);

        // Read the saved file back rather than reporting what we intended to write.
        var written = new MapGbxReader().Read(outputPath);

        return new MapGbxWriteResult(
            outputPath,
            before,
            written.Blocks.Count,
            written.FreeBlockCount,
            written.Blocks
                .Where(b => b.IsFree)
                .TakeLast(placements.Count)
                .ToArray());
    }
}

public sealed record MapGbxWriteResult(
    string Path,
    int BlocksBefore,
    int BlocksAfter,
    int FreeBlocks,
    IReadOnlyList<MapBlock> Written)
{
    public int Added => BlocksAfter - BlocksBefore;
}
