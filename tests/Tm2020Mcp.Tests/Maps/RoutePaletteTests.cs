using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class RoutePaletteTests
{
    private readonly BlockConnectionModel _model = BlockConnectionModel.LoadBundled();

    [Fact]
    public void EveryPaletteBlockHasAShapeTheModelCanRead()
    {
        // A palette entry the model cannot read cleanly is dead weight: RouteBuilder skips
        // any (block, direction) pair whose learned shape is ambiguous.
        var usable = RoutePalette.Tricks
            .Where(block => Directions.Any(dir => _model.Connections(block.Name, dir).Count == 2))
            .Select(block => block.Name)
            .ToList();

        Assert.Equal(RoutePalette.Tricks.Select(b => b.Name), usable);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(64)]
    public void TrickRoutesStillVerifyConnected(int seed)
    {
        var plan = new RouteBuilder(_model).Build(
            new RouteRequest(Seed: seed, Length: 24, OriginX: 24, OriginZ: 6, Style: "tricks"));

        Assert.True(plan.HasFinish, string.Join("; ", plan.Notes));

        var result = new TrackVerifier().Verify(
            plan.Blocks.Select(b => b with { Y = 9 }).ToList(),
            _model);

        Assert.True(result.Connected, result.Failure);
    }

    [Fact]
    public void TrickRoutesActuallyUseTrickBlocks()
    {
        // Across seeds, the palette should produce specials rather than tech road only.
        var names = Enumerable.Range(1, 25)
            .SelectMany(seed => new RouteBuilder(_model)
                .Build(new RouteRequest(Seed: seed, Length: 24, OriginX: 24, OriginZ: 6, Style: "tricks"))
                .Blocks)
            .Select(b => b.Name)
            .Distinct()
            .ToList();

        Assert.Contains(names, n => n.Contains("Special", StringComparison.Ordinal));
        Assert.Contains(names, n => n.StartsWith("RoadBump", StringComparison.Ordinal)
            || n.StartsWith("RoadIce", StringComparison.Ordinal)
            || n.StartsWith("RoadWater", StringComparison.Ordinal)
            || n.StartsWith("RoadDirt", StringComparison.Ordinal));
    }

    [Fact]
    public void PlainStyleStaysOnTechRoad()
    {
        var plan = new RouteBuilder(_model).Build(
            new RouteRequest(Seed: 3, Length: 20, Style: "plain"));

        Assert.All(plan.Blocks, block => Assert.StartsWith("RoadTech", block.Name));
    }

    [Fact]
    public void ByName_RejectsAnUnknownStyle()
    {
        Assert.Throws<ArgumentException>(() => RoutePalette.ByName("kacky"));
        Assert.False(RoutePalette.IsKnownStyle("kacky"));
        Assert.True(RoutePalette.IsKnownStyle("tricks"));
    }

    private static readonly string[] Directions = ["North", "East", "South", "West"];
}
