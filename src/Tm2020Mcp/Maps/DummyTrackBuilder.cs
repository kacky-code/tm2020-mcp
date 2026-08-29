using Tm2020Mcp.EditorBridge;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Lays out the smallest track worth calling a map: a start block, some straights, and a
/// finish block, walking forward from an origin.
/// </summary>
/// <remarks>
/// Block names and the direction-to-axis mapping are both taken from real maps rather than
/// assumed; see <see cref="Forward"/>. Note that the bridge cannot check this for us:
/// <c>PlaceBlock</c> reports whether a block fits, never whether the road connects.
/// </remarks>
public static class DummyTrackBuilder
{
    public const string StartBlock = "RoadTechStart";
    public const string StraightBlock = "RoadTechStraight";
    public const string FinishBlock = "RoadTechFinish";

    /// <summary>Middle-ish of a 48x48 map, so a short track cannot run off the edge.</summary>
    public const int DefaultOriginX = 24;
    public const int DefaultOriginZ = 24;

    public static IReadOnlyList<MapBlockPlacement> Build(
        int originX = DefaultOriginX,
        int originZ = DefaultOriginZ,
        int straightCount = 1,
        string direction = "North")
    {
        if (straightCount < 0)
            throw new ArgumentOutOfRangeException(nameof(straightCount), "Straight count cannot be negative.");

        var (stepX, stepZ) = Forward(direction);
        var blocks = new List<MapBlockPlacement>(straightCount + 2);

        for (var i = 0; i < straightCount + 2; i++)
        {
            var name = i switch
            {
                0 => StartBlock,
                _ when i == straightCount + 1 => FinishBlock,
                _ => StraightBlock
            };

            blocks.Add(new MapBlockPlacement(
                name,
                originX + (stepX * i),
                originZ + (stepZ * i),
                Dir: direction));
        }

        return blocks;
    }

    public static bool IsKnownDirection(string direction) =>
        direction.Trim().ToLowerInvariant() is "north" or "east" or "south" or "west";

    /// <remarks>
    /// Measured, not guessed. Across 450 Kacky maps parsed with GBX.NET, the road neighbouring
    /// a <c>RoadTechStart</c> sits at +Z for North (15 cases, none at -Z), -Z for South (17),
    /// -X for East (37) and +X for West (19), and every <c>RoadTechFinish</c> agrees. East and
    /// West are mirrored from the intuitive reading, which is what made the first attempt place
    /// a start block facing away from its own track.
    /// </remarks>
    /// <summary>The world-space step one block forward for a cardinal direction.</summary>
    public static (int X, int Z) Forward(string direction) => direction.Trim().ToLowerInvariant() switch
    {
        "north" => (0, 1),
        "south" => (0, -1),
        "east" => (-1, 0),
        "west" => (1, 0),
        _ => throw new ArgumentException($"Unknown direction '{direction}'. Use North, East, South, or West.", nameof(direction))
    };
}
