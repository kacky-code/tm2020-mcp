using System.Text;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Derives what a block's <c>direction</c> means in world coordinates by looking at maps
/// people actually built.
/// </summary>
/// <remarks>
/// The editor API cannot answer this: <c>PlaceBlock</c> reports whether a block fits, never
/// whether the road connects, so a reversed track reports nothing but successes. Counting
/// neighbours across a corpus does answer it. Findings live in
/// <c>docs/tm2020-map-geometry.md</c>.
/// </remarks>
public sealed class BlockDirectionAnalyzer
{
    public static readonly (string Label, int X, int Z)[] Offsets =
    [
        ("+X", 1, 0),
        ("-X", -1, 0),
        ("+Z", 0, 1),
        ("-Z", 0, -1)
    ];

    /// <summary>
    /// Counts, per (block name, direction), which neighbouring cell holds a related block.
    /// A start or finish has one road exit, so its neighbour gives the forward vector with
    /// its sign; a straight is symmetric and gives only the axis.
    /// </summary>
    public IReadOnlyList<DirectionObservation> Analyze(
        IEnumerable<IReadOnlyList<MapBlock>> maps,
        string? nameFilter = null,
        int minimumSamples = 5)
    {
        var neighbours = new Dictionary<(string Name, string Dir, string Offset), int>();
        var totals = new Dictionary<(string Name, string Dir), int>();

        foreach (var blocks in maps)
        {
            var byCoord = new Dictionary<(int X, int Y, int Z), MapBlock>();
            foreach (var block in blocks.Where(b => !b.IsFree))
                byCoord[(block.X, block.Y, block.Z)] = block;

            foreach (var block in byCoord.Values)
            {
                if (nameFilter is not null && !block.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (block.Direction.Length == 0)
                    continue;

                var key = (block.Name, block.Direction);
                totals[key] = totals.GetValueOrDefault(key) + 1;

                foreach (var (label, dx, dz) in Offsets)
                {
                    if (!byCoord.TryGetValue((block.X + dx, block.Y, block.Z + dz), out var neighbour))
                        continue;

                    if (!IsRelated(block, neighbour))
                        continue;

                    var neighbourKey = (block.Name, block.Direction, label);
                    neighbours[neighbourKey] = neighbours.GetValueOrDefault(neighbourKey) + 1;
                }
            }
        }

        return totals
            .Where(kv => kv.Value >= minimumSamples)
            .Select(kv => new DirectionObservation(
                kv.Key.Name,
                kv.Key.Dir,
                kv.Value,
                Offsets.ToDictionary(
                    o => o.Label,
                    o => neighbours.GetValueOrDefault((kv.Key.Name, kv.Key.Dir, o.Label)))))
            .OrderByDescending(o => o.Samples)
            .ToArray();
    }

    /// <summary>
    /// A straight only chains with its own kind; an end block connects to any road piece.
    /// Matching ends against the whole road family is what makes their sample size usable.
    /// </summary>
    private static bool IsRelated(MapBlock block, MapBlock neighbour)
    {
        if (block.IsStart || block.IsFinish)
            return neighbour.IsRoad;

        return neighbour.Name == block.Name;
    }

    public static string Format(IReadOnlyList<DirectionObservation> observations, int limit = 40)
    {
        if (observations.Count == 0)
            return "No blocks matched. Grid blocks only: free blocks carry a rotation instead of a direction and are skipped.";

        var report = new StringBuilder();
        report.AppendLine($"{observations.Count} (block, direction) pairs above the sample threshold. Dominant offset is the forward axis.");
        report.AppendLine();

        foreach (var observation in observations.Take(limit))
        {
            var detail = string.Join("  ", observation.Neighbours.Select(n => $"{n.Key}={n.Value,-5}"));
            var verdict = observation.Forward is null ? "" : $"  => forward {observation.Forward}";
            report.AppendLine($"{observation.Name,-26} dir={observation.Direction,-6} n={observation.Samples,-6} {detail}{verdict}");
        }

        if (observations.Count > limit)
            report.AppendLine($"... {observations.Count - limit} more not shown.");

        return report.ToString().TrimEnd();
    }
}

/// <param name="Neighbours">Offset label to the number of times a related block sat there.</param>
public sealed record DirectionObservation(
    string Name,
    string Direction,
    int Samples,
    IReadOnlyDictionary<string, int> Neighbours)
{
    /// <summary>
    /// The single offset that dominates, or null when the block is symmetric (a straight
    /// chains both ways) or the evidence is too even to call.
    /// </summary>
    public string? Forward
    {
        get
        {
            var ranked = Neighbours.OrderByDescending(n => n.Value).ToList();
            if (ranked.Count < 2 || ranked[0].Value == 0)
                return null;

            return ranked[1].Value * 3 <= ranked[0].Value ? ranked[0].Key : null;
        }
    }
}
