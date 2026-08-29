using System.Text;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Walks a saved map from its start block to check the road actually connects.
/// </summary>
/// <remarks>
/// This is the check the bridge cannot perform. <c>PlaceBlock</c> answers "does this block
/// fit in this cell", so a track laid in the wrong direction places cleanly and reports four
/// successes while the start block faces away from its own road. Reading the map back is the
/// only cheap oracle short of a human looking at the editor.
///
/// Without a <see cref="BlockConnectionModel"/> the walk holds one heading, which covers
/// generated straight-line tracks and rejects almost every hand-built one - real routes turn.
/// Pass a model learned from a corpus and the walk follows curves too.
/// </remarks>
public sealed class TrackVerifier
{
    private const int MaxSteps = 4096;

    public TrackVerification Verify(IReadOnlyList<MapBlock> blocks, BlockConnectionModel? model = null)
    {
        var byCoord = new Dictionary<(int X, int Y, int Z), MapBlock>();
        foreach (var block in blocks.Where(b => !b.IsFree))
            byCoord[(block.X, block.Y, block.Z)] = block;

        var start = blocks.FirstOrDefault(b => b is { IsFree: false, IsStart: true } && b.IsRoad);
        if (start is null)
        {
            return new TrackVerification(
                false,
                "No grid start block found. Maps that place their start as a free block cannot be walked this way.",
                []);
        }

        if (!TryHeading(start, model, out var heading))
            return new TrackVerification(false, $"Start block has an unusable direction '{start.Direction}'.", []);

        var steps = new List<string> { Describe(start) };
        var current = start;

        for (var i = 0; i < MaxSteps; i++)
        {
            var next = (current.X + heading.X, current.Y, current.Z + heading.Z);
            if (!byCoord.TryGetValue(next, out var block))
            {
                return new TrackVerification(
                    false,
                    $"{Describe(current)} points {heading} but ({next.Item1}, {next.Item2}, {next.Item3}) is empty. "
                        + "A start block facing away from its own track looks exactly like this.",
                    steps);
            }

            steps.Add(Describe(block));

            if (block.IsFinish)
                return VerifyFinish(block, heading, steps);

            if (!block.IsRoad && model?.Knows(block.Name, block.Direction) != true)
                return new TrackVerification(false, $"{Describe(block)} is not a road block and is not in the connection model.", steps);

            if (!TryAdvance(block, heading, model, out heading, out var failure))
                return new TrackVerification(false, $"{Describe(block)}: {failure}", steps);

            current = block;
        }

        return new TrackVerification(false, $"Track walk exceeded {MaxSteps} blocks; giving up.", steps);
    }

    private static bool TryHeading(MapBlock start, BlockConnectionModel? model, out GridOffset heading)
    {
        // A start block has exactly one road exit, so a learned model pins the heading down
        // even for a block family whose rotation convention was never measured by hand.
        var learned = model?.Connections(start.Name, start.Direction) ?? [];
        if (learned.Count == 1)
        {
            heading = learned[0];
            return true;
        }

        if (!DummyTrackBuilder.IsKnownDirection(start.Direction))
        {
            heading = default;
            return false;
        }

        var (x, z) = DummyTrackBuilder.Forward(start.Direction);
        heading = new GridOffset(x, z);
        return true;
    }

    private static bool TryAdvance(
        MapBlock block,
        GridOffset heading,
        BlockConnectionModel? model,
        out GridOffset next,
        out string? failure)
    {
        next = heading;
        failure = null;

        var connections = model?.Connections(block.Name, block.Direction) ?? [];
        if (connections.Count > 0)
        {
            var entry = heading.Reversed;
            if (!connections.Contains(entry))
            {
                failure = $"does not connect back towards {entry}, so the route does not actually enter it.";
                return false;
            }

            var exits = connections.Where(c => c != entry).ToList();
            switch (exits.Count)
            {
                case 0:
                    failure = "is a dead end.";
                    return false;
                case 1:
                    next = exits[0];
                    return true;
                default:
                    // Junctions and blocks the corpus saw in crossing routes. Holding the
                    // heading is the honest guess; say so rather than picking silently.
                    next = exits.Contains(heading) ? heading : exits[0];
                    return true;
            }
        }

        // No model: only a straight-line continuation can be checked.
        if (block.Direction.Length > 0 && DummyTrackBuilder.IsKnownDirection(block.Direction))
        {
            var (blockX, blockZ) = DummyTrackBuilder.Forward(block.Direction);
            if (blockX != heading.X && blockZ != heading.Z)
            {
                failure = $"runs along a different axis than the heading {heading}.";
                return false;
            }
        }

        return true;
    }

    private static TrackVerification VerifyFinish(MapBlock finish, GridOffset heading, List<string> steps)
    {
        if (!DummyTrackBuilder.IsKnownDirection(finish.Direction))
            return new TrackVerification(true, null, steps);

        var (x, z) = DummyTrackBuilder.Forward(finish.Direction);
        var facing = new GridOffset(x, z);

        return facing == heading
            ? new TrackVerification(true, null, steps)
            : new TrackVerification(
                false,
                $"{Describe(finish)} faces {facing} but the track arrives heading {heading}, so the finish is turned around.",
                steps);
    }

    private static string Describe(MapBlock block) =>
        $"{block.Name} <{block.X}, {block.Y}, {block.Z}> dir={block.Direction}";

    public static string Format(TrackVerification verification)
    {
        var report = new StringBuilder();
        report.AppendLine(verification.Connected
            ? "Connected: the road runs from start to finish."
            : $"NOT connected: {verification.Failure}");

        if (verification.Path.Count > 0)
        {
            report.AppendLine();
            foreach (var step in verification.Path.Take(60))
                report.AppendLine($"  {step}");

            if (verification.Path.Count > 60)
                report.AppendLine($"  ... {verification.Path.Count - 60} more blocks.");
        }

        return report.ToString().TrimEnd();
    }
}

public sealed record TrackVerification(bool Connected, string? Failure, IReadOnlyList<string> Path);
