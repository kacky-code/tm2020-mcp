namespace Tm2020Mcp.Maps;

/// <summary>
/// Block sets a route may be built from, with weights so a palette can hold a trick block
/// without paving the whole track in it.
/// </summary>
/// <remarks>
/// Every name here was checked against the learned connection model: each has at least one
/// direction whose shape is unambiguously a straight or a curve. Blocks whose neighbour
/// counts came out noisy are left out rather than guessed at, and <see cref="RouteBuilder"/>
/// skips any (block, direction) pair it cannot read cleanly.
///
/// This is the surface layer only. Loops and flips are multi-cell, multi-height structures -
/// a loop is a five-wide wall of <c>PlatformTechLoopStart</c> over a base row and a
/// <c>StructureStraight</c> support - and nothing here places those. See
/// docs/tm2020-map-geometry.md.
/// </remarks>
public static class RoutePalette
{
    /// <summary>Plain tech road: what a first track should be made of.</summary>
    public static readonly IReadOnlyList<WeightedBlock> Plain =
    [
        new(DummyTrackBuilder.StraightBlock, 6),
        new("RoadTechCurve1", 3)
    ];

    /// <summary>
    /// Surface changes and special road pieces, the Kacky-flavoured ground layer. Tech road
    /// still dominates so the tricks read as events rather than as the whole track.
    /// </summary>
    public static readonly IReadOnlyList<WeightedBlock> Tricks =
    [
        new(DummyTrackBuilder.StraightBlock, 10),
        new("RoadTechCurve1", 5),
        new("RoadBumpStraight", 3),
        new("RoadBumpCurve1", 2),
        new("RoadIceCurve1", 2),
        new("RoadWaterStraight", 2),
        new("RoadWaterCurve1", 1),
        new("RoadDirtStraight", 1),
        new("RoadIceWithWallStraight", 1),
        new("RoadTechSpecialTurbo", 2),
        new("RoadTechSpecialTurbo2", 2),
        new("RoadTechSpecialNoEngine", 1),
        new("RoadTechSpecialReset", 1)
    ];

    public static IReadOnlyList<WeightedBlock> ByName(string style) => style.Trim().ToLowerInvariant() switch
    {
        "tricks" => Tricks,
        "plain" or "" => Plain,
        _ => throw new ArgumentException($"Unknown palette style '{style}'. Use 'plain' or 'tricks'.", nameof(style))
    };

    public static bool IsKnownStyle(string style) =>
        style.Trim().ToLowerInvariant() is "tricks" or "plain" or "";
}

/// <param name="Weight">Relative odds of being picked when several blocks fit.</param>
public sealed record WeightedBlock(string Name, int Weight = 1);
