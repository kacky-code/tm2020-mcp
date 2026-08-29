using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class RouteBuilderTests
{
    private readonly BlockConnectionModel _model = BlockConnectionModel.LoadBundled();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(23)]
    [InlineData(99)]
    public void Build_ProducesRoutesTheVerifierAccepts(int seed)
    {
        var plan = new RouteBuilder(_model).Build(new RouteRequest(Seed: seed, Length: 14, OriginX: 24, OriginZ: 8));

        Assert.True(plan.HasFinish, string.Join("; ", plan.Notes));

        // The bridge resolves Y itself; pin it here so the verifier can walk the plan.
        var result = new TrackVerifier().Verify(plan.Blocks.Select(b => b with { Y = 9 }).ToList(), _model);

        Assert.True(result.Connected, result.Failure);
    }

    [Fact]
    public void Build_StaysInsideTheMapAndNeverReusesACell()
    {
        var plan = new RouteBuilder(_model).Build(new RouteRequest(Seed: 5, Length: 40, MapSize: 48, Margin: 2));

        Assert.All(plan.Blocks, block =>
        {
            Assert.InRange(block.X, 2, 45);
            Assert.InRange(block.Z, 2, 45);
        });

        var cells = plan.Blocks.Select(b => (b.X, b.Z)).ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    [Fact]
    public void Build_IsDeterministicForASeed()
    {
        var builder = new RouteBuilder(_model);
        var request = new RouteRequest(Seed: 42, Length: 16);

        Assert.Equal(builder.Build(request).Blocks, builder.Build(request).Blocks);
    }

    [Fact]
    public void Build_TurnChanceZeroKeepsTheRouteStraight()
    {
        var plan = new RouteBuilder(_model).Build(
            new RouteRequest(Seed: 3, Length: 6, TurnChance: 0, OriginX: 24, OriginZ: 8));

        Assert.All(plan.Blocks, block => Assert.Equal(24, block.X));
    }

    [Fact]
    public void Build_LeavesHeightToTheBridge()
    {
        var plan = new RouteBuilder(_model).Build(new RouteRequest(Seed: 1, Length: 8));

        Assert.All(plan.ToPlacements(), placement => Assert.Equal(-1, placement.Y));
    }

    [Fact]
    public void Build_ExplainsItselfWhenTheRouteRunsOutOfRoom()
    {
        // A long route in a tiny box has to stop early and trim back to fit a finish.
        var plan = new RouteBuilder(_model).Build(
            new RouteRequest(Seed: 4, Length: 60, OriginX: 6, OriginZ: 6, MapSize: 12, Margin: 2));

        Assert.NotEmpty(plan.Notes);
    }
}
