using Tm2020Mcp.EditorBridge;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Builds a connected, turning route out of blocks whose shapes were learned from real maps,
/// rather than out of block names someone assumed would fit together.
/// </summary>
/// <remarks>
/// Deliberately limited: this produces a route that *connects*, on the grid, within bounds.
/// It does not produce a good track, and it cannot produce a Kacky-style one - those are
/// jump gauntlets whose gaps are the design, and nothing here models a car.
/// See docs/tm2020-map-geometry.md.
/// </remarks>
public sealed class RouteBuilder
{
    private readonly BlockConnectionModel _model;

    public RouteBuilder(BlockConnectionModel model)
    {
        _model = model;
    }

    public RoutePlan Build(RouteRequest request)
    {
        var random = new Random(request.Seed);
        var occupied = new HashSet<(int X, int Z)>();
        var notes = new List<string>();

        var start = new MapBlock(request.StartBlock, request.OriginX, request.Y, request.OriginZ, request.Direction);
        var heading = SingleExit(start.Name, start.Direction) ?? Fallback(request.Direction);

        // Each entry remembers the heading the route leaves that block with, so the tail can
        // be unwound cleanly when the finish will not fit.
        var route = new List<(MapBlock Block, GridOffset Heading)> { (start, heading) };
        occupied.Add((start.X, start.Z));

        for (var placed = 0; placed < request.Length; placed++)
        {
            var (previous, currentHeading) = route[^1];
            var x = previous.X + currentHeading.X;
            var z = previous.Z + currentHeading.Z;

            if (!InBounds(x, z, request) || occupied.Contains((x, z)))
            {
                notes.Add($"Route ran into the map edge or itself after {placed} middle block(s).");
                break;
            }

            var entry = currentHeading.Reversed;

            // Only (block, direction) pairs whose learned shape is unambiguous are usable.
            // A noisy entry - a block the corpus saw wedged between crossing routes - is
            // skipped rather than guessed at.
            var candidates = request.Palette
                .SelectMany(block => Directions.Select(dir => (block.Name, Dir: dir, block.Weight)))
                .Where(c => _model.Connections(c.Name, c.Dir) is { Count: 2 } conn && conn.Contains(entry))
                .ToList();

            if (candidates.Count == 0)
            {
                notes.Add($"No block in the palette connects back towards {entry}; stopped after {placed} middle block(s).");
                break;
            }

            var straightish = candidates
                .Where(c => _model.Connections(c.Name, c.Dir).Contains(currentHeading))
                .ToList();

            var turning = candidates.Except(straightish).ToList();
            var pool = turning.Count > 0 && random.NextDouble() < request.TurnChance
                ? turning
                : straightish.Count > 0 ? straightish : candidates;

            var chosen = PickWeighted(pool, random);
            var block = new MapBlock(chosen.Name, x, request.Y, z, chosen.Dir);
            var exit = _model.Connections(chosen.Name, chosen.Dir).First(c => c != entry);

            route.Add((block, exit));
            occupied.Add((x, z));
        }

        return Finish(route, occupied, request, notes);
    }

    /// <summary>
    /// Caps the route with a finish, unwinding the tail when there is no room. A route that
    /// paints itself into a corner is better shortened than shipped without an end.
    /// </summary>
    private RoutePlan Finish(
        List<(MapBlock Block, GridOffset Heading)> route,
        HashSet<(int X, int Z)> occupied,
        RouteRequest request,
        List<string> notes)
    {
        var trimmed = 0;

        while (route.Count > 1)
        {
            var (last, heading) = route[^1];
            var x = last.X + heading.X;
            var z = last.Z + heading.Z;

            // A finish is entered from behind, so the learned connection - its one open side -
            // must point back the way the route arrives, not along it.
            var finishDirection = Directions.FirstOrDefault(
                d => SingleExit(request.FinishBlock, d) == heading.Reversed);

            if (finishDirection is not null && InBounds(x, z, request) && !occupied.Contains((x, z)))
            {
                if (trimmed > 0)
                    notes.Add($"Trimmed {trimmed} block(s) off the tail to make room for the finish.");

                var blocks = route.Select(r => r.Block).ToList();
                blocks.Add(new MapBlock(request.FinishBlock, x, request.Y, z, finishDirection));
                return new RoutePlan(blocks, notes);
            }

            if (finishDirection is null)
            {
                notes.Add($"No learned direction makes {request.FinishBlock} sit at the end of a route heading {heading}.");
                break;
            }

            occupied.Remove((last.X, last.Z));
            route.RemoveAt(route.Count - 1);
            trimmed++;
        }

        notes.Add("Could not place a finish; returning the route without one.");
        return new RoutePlan(route.Select(r => r.Block).ToList(), notes);
    }

    private static (string Name, string Dir, int Weight) PickWeighted(
        List<(string Name, string Dir, int Weight)> pool,
        Random random)
    {
        var total = pool.Sum(c => Math.Max(1, c.Weight));
        var roll = random.Next(total);

        foreach (var candidate in pool)
        {
            roll -= Math.Max(1, candidate.Weight);
            if (roll < 0)
                return candidate;
        }

        return pool[^1];
    }

    private GridOffset? SingleExit(string name, string direction)
    {
        var connections = _model.Connections(name, direction);
        return connections.Count == 1 ? connections[0] : null;
    }

    private static GridOffset Fallback(string direction)
    {
        var (x, z) = DummyTrackBuilder.Forward(direction);
        return new GridOffset(x, z);
    }

    private static bool InBounds(int x, int z, RouteRequest request) =>
        x >= request.Margin && z >= request.Margin
        && x < request.MapSize - request.Margin && z < request.MapSize - request.Margin;

    private static readonly string[] Directions = ["North", "East", "South", "West"];
}

public sealed record RouteRequest(
    int Seed = 1,
    int Length = 12,
    double TurnChance = 0.35,
    int OriginX = 24,
    int OriginZ = 12,
    int Y = -1,
    string Direction = "North",
    string StartBlock = DummyTrackBuilder.StartBlock,
    string FinishBlock = DummyTrackBuilder.FinishBlock,
    string Style = "plain",
    IReadOnlyList<WeightedBlock>? PaletteOverride = null,
    int MapSize = 48,
    int Margin = 2)
{
    /// <summary>Road pieces the corpus shows connecting, weighted. See <see cref="RoutePalette"/>.</summary>
    public IReadOnlyList<WeightedBlock> Palette => PaletteOverride ?? RoutePalette.ByName(Style);
}

public sealed record RoutePlan(IReadOnlyList<MapBlock> Blocks, IReadOnlyList<string> Notes)
{
    public bool HasFinish => Blocks.Count > 0 && Blocks[^1].IsFinish;

    /// <summary>Placements for the bridge. A negative Y keeps the ground scan in charge.</summary>
    public IReadOnlyList<MapBlockPlacement> ToPlacements() => Blocks
        .Select(b => new MapBlockPlacement(b.Name, b.X, b.Z, b.Y, b.Direction))
        .ToArray();
}
